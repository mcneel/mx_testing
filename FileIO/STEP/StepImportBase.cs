using NUnit.Framework;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FileIO
{
  /// <summary>
  /// The quantities a STEP import is judged on. Counts are exact; areas, volumes and the bounding
  /// box are compared within a tolerance. Type counts, solids, invalids and the mass properties are
  /// gathered over *leaf* geometry, that is, after block instances have been expanded and their
  /// transforms applied - so an assembly that arrives as nested blocks measures the same as the
  /// same assembly arriving flat.
  /// </summary>
  internal sealed class StepMetrics
  {
    internal string Units;

    // document-level
    internal int Objects;     // top level objects, blocks counted as one
    internal int Instances;   // top level block instances
    internal int BlockDefs;   // block definitions in the document
    internal int Layers;

    // leaf geometry
    internal int Breps;
    internal int Extrusions;
    internal int Surfaces;
    internal int Meshes;
    internal int SubDs;
    internal int Curves;
    internal int Points;
    internal int Other;
    internal int Solids;
    internal int Invalid;

    /// <summary>
    /// Why each invalid leaf failed, at most <see cref="MaxInvalidReports"/> of them. 'invalid 1'
    /// on its own says a large assembly has one bad object somewhere among a few thousand and
    /// leaves you no way to find it; this names the type and the validity log.
    /// </summary>
    internal readonly List<string> InvalidReports = new List<string>();

    /// <summary>Enough to characterise the problem without flooding the log of a broken import.</summary>
    internal const int MaxInvalidReports = 10;

    internal double Area;
    internal double Volume;

    /// <summary>The bounding box of everything <see cref="StepImporter.Measure"/> walked.</summary>
    /// <remarks>
    /// Held as plain doubles rather than as a <see cref="BoundingBox"/> field on purpose. A field
    /// of a RhinoCommon value type is part of this class's layout, so the runtime must load
    /// RhinoCommon to load the class - and NUnit scans every type in the test assembly before
    /// Rhino.Testing has booted Rhino and installed its assembly resolvers. Standalone on
    /// net10.0-windows there is nothing else to resolve RhinoCommon at that point, so one such
    /// field makes the whole assembly undiscoverable: zero tests, "NUnit failed to load". net48
    /// does not care, because the &lt;codeBase&gt; bindings written by Directory.Build.targets
    /// bind RhinoCommon before anything is scanned. A property is a method, and method signatures
    /// resolve lazily, so exposing the box this way costs nothing.
    /// </remarks>
    internal BoundingBox Bbox
    {
      get
      {
        return m_has_bbox
          ? new BoundingBox(m_bbox_min_x, m_bbox_min_y, m_bbox_min_z,
                            m_bbox_max_x, m_bbox_max_y, m_bbox_max_z)
          : BoundingBox.Empty;
      }
    }

    /// <summary>Grows <see cref="Bbox"/> to include <paramref name="box"/>.</summary>
    internal void UnionBbox(BoundingBox box)
    {
      BoundingBox rc = Bbox;
      rc.Union(box);

      m_has_bbox = rc.IsValid;
      if (!m_has_bbox) return;

      m_bbox_min_x = rc.Min.X; m_bbox_min_y = rc.Min.Y; m_bbox_min_z = rc.Min.Z;
      m_bbox_max_x = rc.Max.X; m_bbox_max_y = rc.Max.Y; m_bbox_max_z = rc.Max.Z;
    }

    bool m_has_bbox;
    double m_bbox_min_x, m_bbox_min_y, m_bbox_min_z;
    double m_bbox_max_x, m_bbox_max_y, m_bbox_max_z;

    internal double ReadSeconds;
  }

  /// <summary>One parsed <c>.expected.txt</c> oracle: its incipit, its comments and its entries, in file order.</summary>
  internal sealed class StepOracleFile
  {
    internal string Incipit = StepOracle.Incipit;
    internal readonly List<string> Comments = new List<string>();
    internal readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();
  }

  /// <summary>
  /// Reads, writes and checks the sidecar oracle that accompanies each STEP model.
  /// </summary>
  /// <remarks>
  /// A STEP file has nowhere to keep the expected values the way a .3dm keeps them in its Notes, so
  /// the oracle lives next to the model as "&lt;model&gt;.stp.expected.txt". Only the keys actually
  /// present in that file are asserted, which lets a model be pinned loosely (counts only) or
  /// tightly (down to the volume) without any code change.
  /// </remarks>
  internal static class StepOracle
  {
    internal const string Incipit = "STEP IMPORT";
    internal const string Suffix = ".expected.txt";

    /// <summary>Every assertable key, in the order a regenerated file writes them.</summary>
    internal static readonly string[] AllKeys = new string[]
    {
      "units", "objects", "instances", "blockdefs", "layers",
      "breps", "extrusions", "surfaces", "meshes", "subds", "curves", "points", "other",
      "solids", "invalid", "area", "volume", "bbox"
    };

    /// <summary>The keys that cost nothing to measure: everything but the mass properties.</summary>
    internal static readonly string[] CountKeys =
      AllKeys.Where(k => k != "area" && k != "volume").ToArray();

    static readonly string[] s_separators = new string[] { " ", "\t", ",", "to" };

    // Relative and absolute slack on area, volume and every bounding box coordinate. The relative
    // term carries large models, the absolute one keeps a coordinate that lands on zero from
    // needing an exact hit.
    internal static double RelativeTolerance => EnvDouble("MX_STEP_RELTOL", 1e-8);
    internal static double AbsoluteTolerance => EnvDouble("MX_STEP_ABSTOL", 1e-6);

    static double EnvDouble(string name, double fallback)
    {
      string v = System.Environment.GetEnvironmentVariable(name);
      if (!string.IsNullOrWhiteSpace(v) &&
          double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
      return fallback;
    }

    static double Delta(double expected)
    {
      return Math.Max(Math.Abs(expected) * RelativeTolerance, AbsoluteTolerance);
    }

    internal static string PathFor(string modelPath) => modelPath + Suffix;

    /// <summary>Sidecar path for a model under a suffix other than the import one.</summary>
    internal static string PathFor(string modelPath, string suffix) => modelPath + suffix;

    internal static StepOracleFile Read(string oraclePath) => Read(oraclePath, Incipit, AllKeys);

    /// <summary>
    /// Parses one sidecar oracle. <paramref name="incipit"/> is the line it must open with and
    /// <paramref name="knownKeys"/> the keys it may use, so that the export suite can reuse this
    /// parser with its own vocabulary.
    /// </summary>
    internal static StepOracleFile Read(string oraclePath, string incipit, string[] knownKeys)
    {
      var rc = new StepOracleFile { Incipit = incipit };
      bool incipitSeen = false;

      foreach (string raw in File.ReadAllLines(oraclePath))
      {
        string line = raw.Trim();
        if (line.Length == 0) continue;

        if (line.StartsWith("#", StringComparison.InvariantCulture)) { rc.Comments.Add(raw.TrimEnd()); continue; }

        if (!incipitSeen)
        {
          if (!line.StartsWith(incipit, StringComparison.InvariantCultureIgnoreCase))
            throw new NotSupportedException(
              $"'{Path.GetFileName(oraclePath)}' must start with '{incipit}', but starts with '{line}'.");
          rc.Incipit = line;
          incipitSeen = true;
          continue;
        }

        int split = line.IndexOfAny(new char[] { ' ', '\t' });
        if (split < 0)
          throw new NotSupportedException(
            $"'{Path.GetFileName(oraclePath)}': line '{line}' has a key but no value.");

        string key = line.Substring(0, split).Trim().ToLowerInvariant();
        string value = line.Substring(split + 1).Trim();

        if (!knownKeys.Contains(key))
          throw new NotSupportedException(
            $"'{Path.GetFileName(oraclePath)}': unknown key '{key}'. Known keys: {string.Join(", ", knownKeys)}.");

        rc.Entries.Add(new KeyValuePair<string, string>(key, value));
      }

      if (!incipitSeen)
        throw new NotSupportedException($"'{Path.GetFileName(oraclePath)}' is empty or has no '{incipit}' line.");

      return rc;
    }

    /// <summary>Asserts the measured values against every key the oracle actually declares.</summary>
    internal static void Check(string filename, StepOracleFile oracle, StepMetrics actual)
    {
      Check(filename, oracle.Entries, actual);
    }

    /// <summary>
    /// Asserts <paramref name="entries"/> against <paramref name="actual"/>. Taking the entries
    /// rather than the whole file lets one caller assert one subset of a sidecar against one
    /// measurement and another subset against another - which is what the export suite does with
    /// its 'src'-prefixed keys.
    /// </summary>
    internal static void Check(string filename, IEnumerable<KeyValuePair<string, string>> entries, StepMetrics actual)
    {
      foreach (var entry in entries)
      {
        string key = entry.Key;
        string value = entry.Value;
        string where = $"{filename}: '{key}'";

        switch (key)
        {
          case "units":
            Assert.IsTrue(string.Equals(value.Trim(), actual.Units, StringComparison.InvariantCultureIgnoreCase),
              $"{where}: expected '{value.Trim()}' but the model was imported into '{actual.Units}'.");
            break;

          case "bbox":
            {
              double[] n = ParseNumbers(value, where);
              if (n.Length != 6)
                throw new NotSupportedException($"{where}: expected 6 numbers for a bounding box, got {n.Length}.");
              Assert.IsTrue(actual.Bbox.IsValid, $"{where}: no geometry was measured, so there is no bounding box.");
              var corners = new double[]
              {
                actual.Bbox.Min.X, actual.Bbox.Min.Y, actual.Bbox.Min.Z,
                actual.Bbox.Max.X, actual.Bbox.Max.Y, actual.Bbox.Max.Z
              };
              string[] names = new string[] { "min.x", "min.y", "min.z", "max.x", "max.y", "max.z" };
              for (int i = 0; i < 6; i++)
                Assert.AreEqual(n[i], corners[i], Delta(n[i]), $"{where}: {names[i]} differs.");
            }
            break;

          case "area":
            {
              double expected = ParseDouble(value, where);
              Assert.AreEqual(expected, actual.Area, Delta(expected), where);
            }
            break;

          case "volume":
            {
              double expected = ParseDouble(value, where);
              Assert.AreEqual(expected, actual.Volume, Delta(expected), where);
            }
            break;

          case "invalid":
            {
              // The one count worth explaining when it differs: 'expected 0 but was 1' over a
              // 2000-brep assembly is useless on its own.
              string detail = actual.InvalidReports.Count == 0
                ? string.Empty
                : System.Environment.NewLine + string.Join(System.Environment.NewLine, actual.InvalidReports);
              Assert.AreEqual(ParseInt(value, where), actual.Invalid, where + detail);
            }
            break;

          default:
            Assert.AreEqual(ParseInt(value, where), CountOf(key, actual, where), where);
            break;
        }
      }
    }

    internal static int CountOf(string key, StepMetrics m, string where)
    {
      switch (key)
      {
        case "objects": return m.Objects;
        case "instances": return m.Instances;
        case "blockdefs": return m.BlockDefs;
        case "layers": return m.Layers;
        case "breps": return m.Breps;
        case "extrusions": return m.Extrusions;
        case "surfaces": return m.Surfaces;
        case "meshes": return m.Meshes;
        case "subds": return m.SubDs;
        case "curves": return m.Curves;
        case "points": return m.Points;
        case "other": return m.Other;
        case "solids": return m.Solids;
        case "invalid": return m.Invalid;
        default: throw new NotSupportedException($"{where}: '{key}' is not a count.");
      }
    }

    static double[] ParseNumbers(string value, string where)
    {
      return value.Split(s_separators, StringSplitOptions.RemoveEmptyEntries)
                  .Select(t => ParseDouble(t, where))
                  .ToArray();
    }

    static double ParseDouble(string value, string where)
    {
      if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double rc))
        throw new NotSupportedException($"{where}: '{value}' is not a number.");
      return rc;
    }

    static int ParseInt(string value, string where)
    {
      if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rc))
        throw new NotSupportedException($"{where}: '{value}' is not a whole number.");
      return rc;
    }

    /// <summary>Inverse of <see cref="Read"/>: the text of one oracle line for a measured value.</summary>
    internal static string Format(string key, StepMetrics m)
    {
      switch (key)
      {
        case "units": return "units " + m.Units;
        case "area": return "area " + m.Area.ToString("R", CultureInfo.InvariantCulture);
        case "volume": return "volume " + m.Volume.ToString("R", CultureInfo.InvariantCulture);
        case "bbox":
          if (!m.Bbox.IsValid) return "bbox 0,0,0 to 0,0,0";
          return "bbox " +
            Point(m.Bbox.Min) + " to " + Point(m.Bbox.Max);
        default: return key + " " + CountOf(key, m, key).ToString(CultureInfo.InvariantCulture);
      }
    }

    static string Point(Point3d p)
    {
      return p.X.ToString("R", CultureInfo.InvariantCulture) + "," +
             p.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
             p.Z.ToString("R", CultureInfo.InvariantCulture);
    }

    internal static string Write(StepOracleFile old, IEnumerable<string> keys, StepMetrics m)
    {
      return Write(Incipit, old, keys.Select(k => Format(k, m)));
    }

    /// <summary>Assembles a sidecar from an incipit, the old file's comments and ready-made lines.</summary>
    internal static string Write(string incipit, StepOracleFile old, IEnumerable<string> lines)
    {
      var sb = new StringBuilder();
      sb.Append(incipit).Append('\n');
      if (old != null) foreach (string c in old.Comments) sb.Append(c).Append('\n');
      foreach (string line in lines) sb.Append(line).Append('\n');
      return sb.ToString();
    }
  }

  /// <summary>
  /// Imports one STEP file into a headless document and measures it.
  /// </summary>
  /// <remarks>
  /// Units and tolerance are pinned here rather than inherited from whatever template the machine
  /// happens to default to: the importer converts the file's own units into the document's, so a
  /// baseline recorded on one machine is only meaningful if every other machine imports into the
  /// same unit system.
  /// </remarks>
  internal static class StepImporter
  {
    internal const UnitSystem ModelUnits = UnitSystem.Millimeters;
    internal const double ModelTolerance = 0.001;

    /// <summary>Guards against a block definition graph that somehow refers back into itself.</summary>
    const int MaxInstanceDepth = 64;

    internal static RhinoDoc CreateDoc()
    {
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      doc.ModelUnitSystem = ModelUnits;
      doc.ModelAbsoluteTolerance = ModelTolerance;
      return doc;
    }

    internal static bool Read(string filepath, RhinoDoc doc, out double seconds)
    {
      // LimitFaces off: the point of the large-assembly suite is that nothing gets dropped, and a
      // face cap would silently truncate exactly the models it matters most for.
      var options = new FileStpReadOptions { JoinSurfaces = true, LimitFaces = false };

      var watch = System.Diagnostics.Stopwatch.StartNew();
      bool rc = FileStp.Read(filepath, doc, options);
      watch.Stop();

      seconds = watch.Elapsed.TotalSeconds;
      return rc;
    }

    /// <summary>
    /// Measures <paramref name="doc"/>, computing only what <paramref name="wanted"/> asks for.
    /// Mass properties over a large assembly cost far more than the counts do, so an oracle that
    /// does not mention area or volume does not pay for them.
    /// </summary>
    internal static StepMetrics Measure(RhinoDoc doc, ICollection<string> wanted)
    {
      bool wantArea = wanted.Contains("area");
      bool wantVolume = wanted.Contains("volume");
      bool wantBbox = wanted.Contains("bbox");

      var m = new StepMetrics
      {
        Units = doc.ModelUnitSystem.ToString(),
        BlockDefs = doc.InstanceDefinitions.Count,
        Layers = doc.Layers.Count,
      };

      // Deliberately not doc.Objects.Count: that also counts the objects living inside block
      // definitions, so a nested assembly reports 19 where Rhino shows one block instance. Counting
      // what the enumerator yields keeps 'objects' equal to what a person sees in the model, which
      // is what someone hand-writing a baseline for models\STEPfile-future\ will write down.
      foreach (RhinoObject obj in doc.Objects)
      {
        m.Objects++;
        if (obj is InstanceObject) m.Instances++;
      }

      Walk(doc.Objects.Cast<RhinoObject>(), Transform.Identity, 0, m, wantArea, wantVolume, wantBbox);

      return m;
    }

    static void Walk(IEnumerable<RhinoObject> objects, Transform xform, int depth,
                     StepMetrics m, bool wantArea, bool wantVolume, bool wantBbox)
    {
      if (depth > MaxInstanceDepth)
        throw new InvalidOperationException(
          $"Block instances are nested more than {MaxInstanceDepth} deep; refusing to recurse further.");

      foreach (RhinoObject obj in objects)
      {
        if (obj is InstanceObject instance)
        {
          InstanceDefinition definition = instance.InstanceDefinition;
          if (definition == null) continue;

          // The parent transform applies after the instance's own, hence this order.
          Walk(definition.GetObjects(), xform * instance.InstanceXform, depth + 1, m, wantArea, wantVolume, wantBbox);
          continue;
        }

        GeometryBase geometry = obj.Geometry;
        if (geometry == null) continue;

        Leaf(geometry, xform, m, wantArea, wantVolume, wantBbox);
      }
    }

    static void Leaf(GeometryBase geometry, Transform xform, StepMetrics m,
                     bool wantArea, bool wantVolume, bool wantBbox)
    {
      if (!geometry.IsValid)
      {
        m.Invalid++;
        if (m.InvalidReports.Count < StepMetrics.MaxInvalidReports)
          m.InvalidReports.Add(DescribeInvalid(geometry));
      }

      bool solid = false;
      switch (geometry)
      {
        // Extrusion derives from Surface, so it has to be matched first.
        case Brep brep: m.Breps++; solid = brep.IsSolid; break;
        case Extrusion extrusion: m.Extrusions++; solid = extrusion.IsSolid; break;
        case Surface _: m.Surfaces++; break;
        case Mesh mesh: m.Meshes++; solid = mesh.IsClosed; break;
        case SubD subd: m.SubDs++; solid = subd.IsSolid; break;
        case Curve _: m.Curves++; break;
        case Point _: m.Points++; break;
        default: m.Other++; break;
      }
      if (solid) m.Solids++;

      if (wantBbox) m.UnionBbox(geometry.GetBoundingBox(xform));

      if (!wantArea && !wantVolume) return;

      // Area and volume are invariant under the rigid transforms a STEP assembly normally carries,
      // but nothing guarantees the transform is rigid, so measure the placed copy when there is one.
      GeometryBase placed = null;
      try
      {
        GeometryBase target = geometry;
        if (!xform.IsIdentity)
        {
          placed = geometry.Duplicate();
          if (placed != null && placed.Transform(xform)) target = placed;
        }

        if (wantArea) m.Area += AreaOf(target);
        if (wantVolume && solid) m.Volume += VolumeOf(target);
      }
      finally
      {
        if (placed != null) placed.Dispose();
      }
    }

    /// <summary>
    /// One line naming an invalid leaf and, where the type offers a validity log, saying why.
    /// The bounding box is included because it is usually the only way to find the thing again in
    /// a 2000-brep assembly.
    /// </summary>
    static string DescribeInvalid(GeometryBase geometry)
    {
      string reason = null;
      switch (geometry)
      {
        case Brep brep: brep.IsValidWithLog(out reason); break;
        case Mesh mesh: mesh.IsValidWithLog(out reason); break;
        case Curve curve: curve.IsValidWithLog(out reason); break;
        case SubD subd: subd.IsValidWithLog(out reason); break;
      }

      if (string.IsNullOrWhiteSpace(reason)) reason = "no validity log available for this type";
      reason = reason.Replace("\r", " ").Replace("\n", " ").Trim();

      // Rounded, not round-tripped: this is a signpost for a human opening the model, not an
      // oracle value, and full double precision would drown the message.
      BoundingBox box = geometry.GetBoundingBox(true);
      string where = box.IsValid
        ? $"({box.Center.X:F3}, {box.Center.Y:F3}, {box.Center.Z:F3})"
        : "an invalid bounding box";

      return $"{geometry.GetType().Name} centred at {where}: {reason}";
    }

    static double AreaOf(GeometryBase geometry)
    {
      AreaMassProperties amp = null;
      switch (geometry)
      {
        case Brep brep: amp = AreaMassProperties.Compute(brep); break;
        case Surface surface: amp = AreaMassProperties.Compute(surface); break;
        case Mesh mesh: amp = AreaMassProperties.Compute(mesh); break;
        default: return 0.0;
      }

      if (amp == null) return 0.0;
      double rc = amp.Area;
      amp.Dispose();
      return rc;
    }

    static double VolumeOf(GeometryBase geometry)
    {
      VolumeMassProperties vmp = null;
      switch (geometry)
      {
        case Brep brep: vmp = VolumeMassProperties.Compute(brep); break;
        case Surface surface: vmp = VolumeMassProperties.Compute(surface); break;
        case Mesh mesh: vmp = VolumeMassProperties.Compute(mesh); break;
        default: return 0.0;
      }

      if (vmp == null) return 0.0;
      double rc = vmp.Volume;
      vmp.Dispose();
      return rc;
    }
  }

  /// <summary>
  /// Drives one model: import, measure, compare - plus the opt-in baseline regeneration that
  /// produces the oracles in the first place.
  /// </summary>
  internal static class StepImportRunner
  {
    /// <summary>
    /// Imports <paramref name="filepath"/> and asserts it against its sidecar oracle.
    /// </summary>
    /// <param name="filepath">Full path of the STEP model.</param>
    /// <param name="writeDebugModel">
    /// When the comparison fails, save what was imported next to the model as a "#name.3dm" so it
    /// can be opened and looked at. Off for the large assemblies, where the write would itself
    /// take minutes and hundreds of megabytes.
    /// </param>
    internal static void Run(string filepath, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath);

      if (!File.Exists(oraclePath))
        Assert.Fail(
          $"'{filename}' has no baseline. Expected '{Path.GetFileName(oraclePath)}' beside it. " +
          $"Create it by running the fixture's explicit Regenerate test with MX_STEP_REGEN=\"{filename}\" " +
          "(or MX_STEP_REGEN=* for the whole folder), then review the file before committing it.");

      StepOracleFile oracle = StepOracle.Read(oraclePath);
      var wanted = new HashSet<string>(oracle.Entries.Select(e => e.Key));

      RhinoDoc doc = StepImporter.CreateDoc();
      try
      {
        bool rc = StepImporter.Read(filepath, doc, out double seconds);

        Assert.IsTrue(rc, $"'{filename}': FileStp.Read() returned false, the file did not import.");
        Assert.IsTrue(doc.Objects.Count > 0, $"'{filename}': imported without error but produced no objects.");

        StepMetrics measured = StepImporter.Measure(doc, wanted);
        measured.ReadSeconds = seconds;
        Emit(filename, measured);

        try
        {
          StepOracle.Check(filename, oracle, measured);
        }
        catch (AssertionException)
        {
          if (writeDebugModel) SaveDebugModel(doc, filepath);
          throw;
        }
      }
      finally
      {
        doc.Dispose();
      }
    }

    static void SaveDebugModel(RhinoDoc doc, string filepath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(filepath), "#" + Path.GetFileNameWithoutExtension(filepath) + ".3dm");

      // Best effort: a failed comparison is the news, a failed debug write is not.
      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSTEP] could not save '{debugPath}': {e.Message}"); }
    }

    static void Emit(string filename, StepMetrics m)
    {
      string line =
        $"[MXSTEP]\t{filename}\tobjects={m.Objects}\tbreps={m.Breps}\tsolids={m.Solids}\tinvalid={m.Invalid}" +
        $"\tseconds={m.ReadSeconds.ToString("F2", CultureInfo.InvariantCulture)}";

      foreach (string report in m.InvalidReports)
        line += $"{System.Environment.NewLine}[MXSTEP]\t{filename}\tinvalid: {report}";

      if (m.Invalid > m.InvalidReports.Count)
        line += $"{System.Environment.NewLine}[MXSTEP]\t{filename}\tinvalid: " +
                $"and {m.Invalid - m.InvalidReports.Count} more, not listed.";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_STEP_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_step.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====
    // Opt-in tooling, mirroring the oracle regeneration of the mesh suites in MxTests: never
    // runs during a normal assert, only from the [Explicit] Regenerate() tests and only for
    // files named by MX_STEP_REGEN.

    /// <summary>True if <paramref name="filename"/> matches the comma-separated MX_STEP_REGEN list. "*" matches all.</summary>
    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_STEP_REGEN");
      if (string.IsNullOrWhiteSpace(selection)) return false;

      return selection.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0)
                      .Any(s => s == "*" || filename.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
    }

    /// <summary>Outcome of one <see cref="RegenerateOracle"/> call.</summary>
    internal enum RegenOutcome
    {
      /// <summary>The file was not named by MX_STEP_REGEN.</summary>
      Skipped,

      /// <summary>The oracle was rewritten, or would have been under MX_STEP_REGEN_DRYRUN.</summary>
      Written,

      /// <summary>The model could not be imported, so there was nothing to record.</summary>
      Failed,
    }

    /// <summary>
    /// Re-imports one model and rewrites its sidecar oracle from what was actually measured.
    /// An existing oracle keeps exactly the keys it already declares; a new one gets
    /// <paramref name="defaultKeys"/>. MX_STEP_REGEN_FIELDS=ALL or =COUNTS overrides both.
    /// MX_STEP_REGEN_DRYRUN=1 reports without writing.
    /// </summary>
    /// <param name="failure">Set to the reason when <see cref="RegenOutcome.Failed"/> is returned.</param>
    internal static RegenOutcome RegenerateOracle(string filepath, string[] defaultKeys, out string failure)
    {
      failure = null;

      string filename = Path.GetFileName(filepath);
      if (!RegenSelected(filename)) return RegenOutcome.Skipped;

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_STEP_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath);

      StepOracleFile old = File.Exists(oraclePath) ? StepOracle.Read(oraclePath) : null;
      string[] keys = ChooseKeys(old, defaultKeys);

      RhinoDoc doc = StepImporter.CreateDoc();
      try
      {
        bool rc = StepImporter.Read(filepath, doc, out double seconds);
        if (!rc || doc.Objects.Count == 0)
        {
          // Not an assert: the caller walks a whole folder, and one un-importable model must not
          // hide the baselines of every model after it.
          failure = $"[regen] '{filename}': import failed (returned {rc}, {doc.Objects.Count} objects); cannot regenerate.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] =====\n{failure}\n\n");
          return RegenOutcome.Failed;
        }

        StepMetrics measured = StepImporter.Measure(doc, keys);
        measured.ReadSeconds = seconds;

        string newText = StepOracle.Write(old, keys, measured);

        string report =
          $"===== REGEN {filename}  [{(dryRun ? "DRY-RUN, " : "")}{seconds.ToString("F2", CultureInfo.InvariantCulture)}s] =====\n" +
          "----- OLD -----\n" + (old == null ? "(none)" : File.ReadAllText(oraclePath).TrimEnd()) + "\n" +
          "----- NEW -----\n" + newText.TrimEnd() + "\n\n";

        AppendRegenReport(report);

        if (!dryRun) File.WriteAllText(oraclePath, newText);
        return RegenOutcome.Written;
      }
      finally
      {
        doc.Dispose();
      }
    }

    /// <summary>Echoes one regeneration report to the progress stream and to MX_STEP_REGEN_LOG.</summary>
    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_STEP_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_step_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_STEP_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return StepOracle.AllKeys;
        case "COUNTS": return StepOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          // Preserve the old key order, minus any duplicates.
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>
  /// Folder-scanning base for STEP fixtures. It is the counterpart of <c>MxTests.AnyCommand&lt;T&gt;</c>
  /// for tests whose input is not a .3dm, and it honours the same file name conventions: a name
  /// beginning with '#' is skipped, a name beginning with '!' is expected to fail.
  /// </summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyStepFixture<T> where T : AnyStepFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".stp", ".step", ".p21" };

    static AnyStepFixture()
    {
      SetupFixture.ScanFolders(typeof(T).Name, g_test_models, g_extensions);
    }

    public static IEnumerable<string[]> GetTestModels()
    {
      return g_test_models.Select(p => new string[] { Path.GetFileName(p), Path.GetDirectoryName(p) });
    }

    [Test]
    public void ThereAreDataDrivenModels()
    {
      // An [Explicit] fixture is allowed to find nothing: the -future folders are empty whenever
      // there is no outstanding bug, and the -large models are deliberately not committed, so on
      // most machines that folder does not exist at all. A fixture that runs by default is not -
      // an empty corpus there means the ModelDirectory entries are wrong, which is worth failing on.
      if (g_test_models.Count == 0 &&
          typeof(T).GetCustomAttributes(typeof(ExplicitAttribute), false).Length > 0)
        Assert.Ignore(
          $"'{typeof(T).Name}' is [Explicit] and its folders hold no models. Nothing to do.");

      Assert.IsNotEmpty(g_test_models, $"There are no data driven models for '{GetType().Name}'.");
    }

    public virtual void Run(string filename, string filepath)
    {
      Console.WriteLine($"SettingsFile: {SetupFixture.Settings.SettingsFile}");
      Console.WriteLine($"RhinoSystemDir: {SetupFixture.Settings.RhinoSystemDir}");
      Console.WriteLine($"RhinoCommon: {typeof(Rhino.Geometry.Mesh).Assembly.Location}");
      Console.WriteLine($"Test filename: {filename}");
      Console.WriteLine($"Path: {filepath}");
    }

    /// <summary>Runs one model, honouring the leading '!' expected-failure convention.</summary>
    internal static void Execute(string filename, string filepath, bool writeDebugModel)
    {
      string full = Path.Combine(filepath, filename);

      if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
        Assert.Throws<AssertionException>(
          delegate { StepImportRunner.Run(full, false); },
          "Expected failure, but test succeeded.");
      else
        StepImportRunner.Run(full, writeDebugModel);
    }

    /// <summary>Shared body of the fixtures' [Explicit] Regenerate tests.</summary>
    internal static void ExecuteRegenerate(string[] defaultKeys)
    {
      int n = 0;
      List<string> failures = new List<string>();

      foreach (string path in g_test_models)
      {
        StepImportRunner.RegenOutcome outcome =
          StepImportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be imported:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_STEP_REGEN='{System.Environment.GetEnvironmentVariable("MX_STEP_REGEN")}'.");
    }
  }
}
