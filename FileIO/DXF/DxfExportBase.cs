using NUnit.Framework;
using Rhino;
using Rhino.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FileIO
{
  /// <summary>
  /// The write options one DXF export test runs with, plus the parsing and formatting that puts
  /// them in the sidecar.
  /// </summary>
  /// <remarks>
  /// Unlike IGES, the DXF Export dictionary genuinely drives the native writer, so most keys here
  /// are real effects and option-varied corpus models are meaningful. Two known exceptions are
  /// pinned as requests-not-effects until the plumbing is fixed: <c>simplifytolerance</c> (managed
  /// writes a Double, native reads it with TryGetBool) and <c>curveusemaxangle</c> (Bool written,
  /// Double read) - the strictly-typed dictionary makes both silently dead. A third defect,
  /// ExportMeshesAs=ThreeDFace serializing "ThreeDFace" where the native side matches only
  /// "ThreeDFaces" (falling through to lines), is why the committed corpus never pins 3dface.
  /// There is no Units/Tolerance option: the writer always takes units from the source document.
  /// </remarks>
  internal sealed class DxfExportOptions
  {
    // Properties over ints for the reason given on StepMetrics.Bbox.
    internal FileDwgWriteOptions.AutocadVersion Version
    {
      get { return (FileDwgWriteOptions.AutocadVersion)m_version; }
      set { m_version = (int)value; }
    }
    int m_version = (int)FileDwgWriteOptions.AutocadVersion.Acad2018;

    internal FileDwgWriteOptions.ExportSurfaceMode SurfacesAs
    {
      get { return (FileDwgWriteOptions.ExportSurfaceMode)m_surfaces; }
      set { m_surfaces = (int)value; }
    }
    int m_surfaces = (int)FileDwgWriteOptions.ExportSurfaceMode.Curves;

    internal FileDwgWriteOptions.ExportMeshMode MeshesAs
    {
      get { return (FileDwgWriteOptions.ExportMeshMode)m_meshes; }
      set { m_meshes = (int)value; }
    }
    int m_meshes = (int)FileDwgWriteOptions.ExportMeshMode.Meshes;

    internal FileDwgWriteOptions.ExportLineMode LinesAs
    {
      get { return (FileDwgWriteOptions.ExportLineMode)m_lines; }
      set { m_lines = (int)value; }
    }
    int m_lines = (int)FileDwgWriteOptions.ExportLineMode.Lines;

    internal FileDwgWriteOptions.ExportArcMode ArcsAs
    {
      get { return (FileDwgWriteOptions.ExportArcMode)m_arcs; }
      set { m_arcs = (int)value; }
    }
    int m_arcs = (int)FileDwgWriteOptions.ExportArcMode.Arcs;

    internal FileDwgWriteOptions.ExportSplineMode SplinesAs
    {
      get { return (FileDwgWriteOptions.ExportSplineMode)m_splines; }
      set { m_splines = (int)value; }
    }
    int m_splines = (int)FileDwgWriteOptions.ExportSplineMode.Splines;

    internal FileDwgWriteOptions.ExportPolylineMode PolylinesAs
    {
      get { return (FileDwgWriteOptions.ExportPolylineMode)m_polylines; }
      set { m_polylines = (int)value; }
    }
    int m_polylines = (int)FileDwgWriteOptions.ExportPolylineMode.Polylines;

    internal FileDwgWriteOptions.ExportPolycurveMode PolycurvesAs
    {
      get { return (FileDwgWriteOptions.ExportPolycurveMode)m_polycurves; }
      set { m_polycurves = (int)value; }
    }
    int m_polycurves = (int)FileDwgWriteOptions.ExportPolycurveMode.Splines;

    internal FileDwgWriteOptions.FlattenMode Flatten
    {
      get { return (FileDwgWriteOptions.FlattenMode)m_flatten; }
      set { m_flatten = (int)value; }
    }
    int m_flatten = (int)FileDwgWriteOptions.FlattenMode.None;

    internal FileDwgWriteOptions.ColorMethodType ColorMethod
    {
      get { return (FileDwgWriteOptions.ColorMethodType)m_color; }
      set { m_color = (int)value; }
    }
    int m_color = (int)FileDwgWriteOptions.ColorMethodType.RGB;

    internal bool SplitPolycurves = true;
    internal bool SplitSplines;
    internal bool Simplify;
    internal bool FullLayerPath = true;
    internal bool UseLWPolylines;
    internal bool WriteThickCurves;
    internal bool PreserveArcNormals = true;

    // Requests, not effects (see remarks): pinned so a plumbing fix diffs loudly.
    internal double SimplifyTolerance = 0.05;
    internal bool CurveUseMaxAngle = true;

    internal FileDwgWriteOptions ToRhino()
    {
      return new FileDwgWriteOptions
      {
        Version = Version,
        ExportSurfacesAs = SurfacesAs,
        ExportMeshesAs = MeshesAs,
        ExportLinesAs = LinesAs,
        ExportArcsAs = ArcsAs,
        ExportSplinesAs = SplinesAs,
        ExportPolylinesAs = PolylinesAs,
        ExportPolycurvesAs = PolycurvesAs,
        Flatten = Flatten,
        ColorMethod = ColorMethod,
        SplitPolycurves = SplitPolycurves,
        SplitSplines = SplitSplines,
        Simplify = Simplify,
        FullLayerPath = FullLayerPath,
        UseLWPolylines = UseLWPolylines,
        WriteThickCurves = WriteThickCurves,
        PreserveArcNormals = PreserveArcNormals,
        SimplifyTolerance = SimplifyTolerance,
        CurveUseMaxAngle = CurveUseMaxAngle,
      };
    }

    /// <summary>The version string the file's $ACADVER header variable must carry.</summary>
    internal string ExpectedAcadVer()
    {
      switch ((int)Version)
      {
        case 12: return "AC1009";
        case 13: return "AC1012";
        case 14: return "AC1014";
        case 2000: return "AC1015";
        case 2004: return "AC1018";
        case 2007: return "AC1021";
        case 2010: return "AC1024";
        case 2013: return "AC1027";
        case 2018: return "AC1032";
        default: throw new NotSupportedException($"no $ACADVER mapping for version {(int)Version}.");
      }
    }

    internal static DxfExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new DxfExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        string v = entry.Value.Trim();
        switch (entry.Key)
        {
          case "acadversion": rc.Version = ParseVersion(v, where); break;
          case "surfacetype": rc.SurfacesAs = ParseEnum<FileDwgWriteOptions.ExportSurfaceMode>(v, where); break;
          case "meshtype": rc.MeshesAs = ParseMesh(v, where); break;
          case "linetype": rc.LinesAs = ParseEnum<FileDwgWriteOptions.ExportLineMode>(v, where); break;
          case "arctype": rc.ArcsAs = ParseEnum<FileDwgWriteOptions.ExportArcMode>(v, where); break;
          case "splinetype": rc.SplinesAs = ParseEnum<FileDwgWriteOptions.ExportSplineMode>(v, where); break;
          case "polylinetype": rc.PolylinesAs = ParseEnum<FileDwgWriteOptions.ExportPolylineMode>(v, where); break;
          case "polycurvetype": rc.PolycurvesAs = ParseEnum<FileDwgWriteOptions.ExportPolycurveMode>(v, where); break;
          case "flatten": rc.Flatten = ParseEnum<FileDwgWriteOptions.FlattenMode>(v, where); break;
          case "colormethod": rc.ColorMethod = ParseEnum<FileDwgWriteOptions.ColorMethodType>(v, where); break;
          case "splitpolycurves": rc.SplitPolycurves = ParseBool(v, where, entry.Key); break;
          case "splitsplines": rc.SplitSplines = ParseBool(v, where, entry.Key); break;
          case "simplify": rc.Simplify = ParseBool(v, where, entry.Key); break;
          case "fulllayerpath": rc.FullLayerPath = ParseBool(v, where, entry.Key); break;
          case "uselwpolylines": rc.UseLWPolylines = ParseBool(v, where, entry.Key); break;
          case "writethickcurves": rc.WriteThickCurves = ParseBool(v, where, entry.Key); break;
          case "preservearcnormals": rc.PreserveArcNormals = ParseBool(v, where, entry.Key); break;
          case "simplifytolerance": rc.SimplifyTolerance = ParseDouble(v, where, entry.Key); break;
          case "curveusemaxangle": rc.CurveUseMaxAngle = ParseBool(v, where, entry.Key); break;
        }
      }

      return rc;
    }

    static FileDwgWriteOptions.AutocadVersion ParseVersion(string value, string where)
    {
      if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) &&
          Enum.IsDefined(typeof(FileDwgWriteOptions.AutocadVersion), n))
        return (FileDwgWriteOptions.AutocadVersion)n;

      throw new NotSupportedException(
        $"{where}: '{value}' is not an AutoCAD version. Known: 12, 13, 14, 2000, 2004, 2007, 2010, 2013, 2018.");
    }

    static FileDwgWriteOptions.ExportMeshMode ParseMesh(string value, string where)
    {
      if (value.Equals("meshes", StringComparison.InvariantCultureIgnoreCase))
        return FileDwgWriteOptions.ExportMeshMode.Meshes;
      if (value.Equals("3dface", StringComparison.InvariantCultureIgnoreCase) ||
          value.Equals("ThreeDFace", StringComparison.InvariantCultureIgnoreCase))
        return FileDwgWriteOptions.ExportMeshMode.ThreeDFace;

      throw new NotSupportedException($"{where}: '{value}' is not a mesh mode. Known: meshes, 3dface.");
    }

    static TEnum ParseEnum<TEnum>(string value, string where) where TEnum : struct
    {
      if (Enum.TryParse(value, true, out TEnum rc) && Enum.IsDefined(typeof(TEnum), rc)) return rc;
      throw new NotSupportedException(
        $"{where}: '{value}' is not one of: {string.Join(", ", Enum.GetNames(typeof(TEnum)))}.");
    }

    static bool ParseBool(string value, string where, string key)
    {
      if (value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || value == "1") return true;
      if (value.Equals("false", StringComparison.InvariantCultureIgnoreCase) || value == "0") return false;
      throw new NotSupportedException($"{where}: '{key}' wants true or false, got '{value}'.");
    }

    static double ParseDouble(string value, string where, string key)
    {
      if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rc)) return rc;
      throw new NotSupportedException($"{where}: '{key}' wants a number, got '{value}'.");
    }

    internal string Format(string key)
    {
      switch (key)
      {
        case "acadversion": return "acadversion " + ((int)Version).ToString(CultureInfo.InvariantCulture);
        case "surfacetype": return "surfacetype " + SurfacesAs.ToString().ToLowerInvariant();
        case "meshtype":
          return "meshtype " + (MeshesAs == FileDwgWriteOptions.ExportMeshMode.Meshes ? "meshes" : "3dface");
        case "linetype": return "linetype " + LinesAs.ToString().ToLowerInvariant();
        case "arctype": return "arctype " + ArcsAs.ToString().ToLowerInvariant();
        case "splinetype": return "splinetype " + SplinesAs.ToString().ToLowerInvariant();
        case "polylinetype": return "polylinetype " + PolylinesAs.ToString().ToLowerInvariant();
        case "polycurvetype": return "polycurvetype " + PolycurvesAs.ToString().ToLowerInvariant();
        case "flatten": return "flatten " + Flatten.ToString().ToLowerInvariant();
        case "colormethod": return "colormethod " + ColorMethod.ToString().ToLowerInvariant();
        case "splitpolycurves": return "splitpolycurves " + (SplitPolycurves ? "true" : "false");
        case "splitsplines": return "splitsplines " + (SplitSplines ? "true" : "false");
        case "simplify": return "simplify " + (Simplify ? "true" : "false");
        case "fulllayerpath": return "fulllayerpath " + (FullLayerPath ? "true" : "false");
        case "uselwpolylines": return "uselwpolylines " + (UseLWPolylines ? "true" : "false");
        case "writethickcurves": return "writethickcurves " + (WriteThickCurves ? "true" : "false");
        case "preservearcnormals": return "preservearcnormals " + (PreserveArcNormals ? "true" : "false");
        case "simplifytolerance": return "simplifytolerance " + SimplifyTolerance.ToString("R", CultureInfo.InvariantCulture);
        case "curveusemaxangle": return "curveusemaxangle " + (CurveUseMaxAngle ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not a DXF export option.");
      }
    }
  }

  /// <summary>
  /// The vocabulary of the DXF export sidecar: the write options, then the measurement keys on
  /// both sides of the round trip, 'src'-prefixed for the source.
  /// </summary>
  internal static class DxfExportOracle
  {
    internal const string Incipit = "DXF EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[]
    {
      "acadversion", "surfacetype", "meshtype",
      "linetype", "arctype", "splinetype", "polylinetype", "polycurvetype",
      "flatten", "colormethod",
      "splitpolycurves", "splitsplines", "simplify", "fulllayerpath",
      "uselwpolylines", "writethickcurves", "preservearcnormals",
      "simplifytolerance", "curveusemaxangle"
    };

    internal static readonly string[] SourceKeys =
      StepOracle.AllKeys.Select(k => SourcePrefix + k).ToArray();

    internal static readonly string[] AllKeys =
      OptionKeys.Concat(SourceKeys).Concat(StepOracle.AllKeys).ToArray();

    internal static readonly string[] DefaultKeys = AllKeys;

    internal static readonly string[] CountKeys =
      DefaultKeys.Where(k => k != "area" && k != "volume" && k != "srcarea" && k != "srcvolume").ToArray();

    internal static bool IsOption(string key) => OptionKeys.Contains(key);

    internal static IEnumerable<KeyValuePair<string, string>> SourceEntries(StepOracleFile oracle)
    {
      return oracle.Entries
                   .Where(e => !IsOption(e.Key) && e.Key.StartsWith(SourcePrefix, StringComparison.InvariantCulture))
                   .Select(e => new KeyValuePair<string, string>(e.Key.Substring(SourcePrefix.Length), e.Value));
    }

    internal static IEnumerable<KeyValuePair<string, string>> ResultEntries(StepOracleFile oracle)
    {
      return oracle.Entries
                   .Where(e => !IsOption(e.Key) && !e.Key.StartsWith(SourcePrefix, StringComparison.InvariantCulture));
    }

    internal static string[] SourceWanted(IEnumerable<string> keys)
    {
      return keys.Where(k => !IsOption(k) && k.StartsWith(SourcePrefix, StringComparison.InvariantCulture))
                 .Select(k => k.Substring(SourcePrefix.Length))
                 .ToArray();
    }

    internal static string[] ResultWanted(IEnumerable<string> keys)
    {
      return keys.Where(k => !IsOption(k) && !k.StartsWith(SourcePrefix, StringComparison.InvariantCulture))
                 .ToArray();
    }
  }

  /// <summary>
  /// The structural checks a written DXF file gets with no baseline at all, from the ASCII DXF
  /// grammar: an even sequence of group-code/value line pairs, organized as
  /// 0/SECTION ... 0/ENDSEC blocks and terminated by a single 0/EOF pair. A truncated,
  /// interleaved or garbage write fails here before any geometry is compared.
  /// </summary>
  internal static class DxfStructure
  {
    internal static void Check(string where, string filepath, DxfExportOptions options, Rhino.UnitSystem sourceUnits)
    {
      string[] raw = File.ReadAllLines(filepath);
      // Values may legitimately be empty strings; the pair grammar counts every line.
      int n = raw.Length;
      Assert.Greater(n, 0, $"{where}: the exported file is empty.");
      Assert.AreEqual(0, n % 2, $"{where}: {n} lines - a DXF is code/value pairs, the count must be even. It looks truncated.");

      // 1. Every even-index line is an integer group code.
      for (int i = 0; i < n; i += 2)
        Assert.IsTrue(int.TryParse(raw[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
          $"{where}: line {i + 1} ('{raw[i]}') should be an integer group code.");

      // 2. Single 0/EOF terminator, at the end.
      int eofCount = 0;
      for (int i = 0; i < n; i += 2)
        if (raw[i].Trim() == "0" && raw[i + 1].Trim() == "EOF") eofCount++;
      Assert.AreEqual(1, eofCount, $"{where}: expected exactly one 0/EOF pair, found {eofCount}.");
      Assert.IsTrue(raw[n - 2].Trim() == "0" && raw[n - 1].Trim() == "EOF",
        $"{where}: the file does not end with the 0/EOF pair. It looks truncated.");

      // 3. SECTION/ENDSEC balance and shape; collect section names in order.
      var sections = new List<string>();
      bool inSection = false;
      for (int i = 0; i < n - 2; i += 2)
      {
        if (raw[i].Trim() != "0") continue;
        string v = raw[i + 1].Trim();
        if (v == "SECTION")
        {
          Assert.IsFalse(inSection, $"{where}: nested SECTION at line {i + 1}.");
          inSection = true;
          Assert.IsTrue(i + 3 < n && raw[i + 2].Trim() == "2",
            $"{where}: SECTION at line {i + 1} is not followed by a 2/<name> pair.");
          sections.Add(raw[i + 3].Trim());
        }
        else if (v == "ENDSEC")
        {
          Assert.IsTrue(inSection, $"{where}: ENDSEC without SECTION at line {i + 1}.");
          inSection = false;
        }
      }
      Assert.IsFalse(inSection, $"{where}: a SECTION is never closed. It looks truncated.");

      // 4. Required sections, in order.
      string[] required = new string[] { "HEADER", "TABLES", "BLOCKS", "ENTITIES" };
      int prev = -1;
      foreach (string s in required)
      {
        int idx = sections.IndexOf(s);
        Assert.IsTrue(idx >= 0, $"{where}: the file has no {s} section. Sections found: {string.Join(", ", sections)}.");
        Assert.Greater(idx, prev, $"{where}: section {s} is out of order. Sections found: {string.Join(", ", sections)}.");
        prev = idx;
      }

      // 5. $ACADVER matches the pinned version option.
      string acadVer = HeaderVariable(raw, "$ACADVER", "1");
      Assert.AreEqual(options.ExpectedAcadVer(), acadVer,
        $"{where}: $ACADVER is '{acadVer}' but the pinned acadversion {(int)options.Version} maps to '{options.ExpectedAcadVer()}'.");

      // 6. $INSUNITS matches the source document (2000+ files; only asserted for a mm source,
      //    the corpus standard - mapping verified against Write_ACAD.cpp).
      if ((int)options.Version >= 2000 && sourceUnits == Rhino.UnitSystem.Millimeters)
      {
        string insunits = HeaderVariable(raw, "$INSUNITS", "70");
        Assert.IsNotNull(insunits, $"{where}: the HEADER declares no $INSUNITS.");
        Assert.AreEqual("4", insunits.Trim(), $"{where}: $INSUNITS is '{insunits}', expected 4 (millimeters).");
      }

      // 7. ENTITIES is non-empty.
      int entityCount = 0;
      bool inEntities = false;
      for (int i = 0; i < n - 2; i += 2)
      {
        if (raw[i].Trim() != "0") continue;
        string v = raw[i + 1].Trim();
        if (v == "SECTION" && i + 3 < n && raw[i + 3].Trim() == "ENTITIES") { inEntities = true; continue; }
        if (v == "ENDSEC") { if (inEntities) break; continue; }
        if (inEntities) entityCount++;
      }
      Assert.Greater(entityCount, 0, $"{where}: the ENTITIES section is empty.");
    }

    /// <summary>The value of one HEADER variable: the pair with <paramref name="valueCode"/> after 9/<paramref name="name"/>.</summary>
    static string HeaderVariable(string[] raw, string name, string valueCode)
    {
      for (int i = 0; i < raw.Length - 3; i += 2)
      {
        if (raw[i].Trim() == "9" && raw[i + 1].Trim().Equals(name, StringComparison.InvariantCultureIgnoreCase))
        {
          if (raw[i + 2].Trim() == valueCode) return raw[i + 3].Trim();
          return null;
        }
      }
      return null;
    }
  }

  /// <summary>
  /// Drives one DXF export test: open the source, write DXF via FileDwg.Write, check the pair
  /// grammar, read it back via FileDwg.Read, compare both ends against the sidecar - plus the
  /// opt-in baseline regeneration on the MX_DXFEXPORT_* variables.
  /// </summary>
  internal static class DxfExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, DxfExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_DXFEXPORT_REQUIRE_BASELINE=1. " +
          $"Expected '{Path.GetFileName(oraclePath)}' beside it. Create it with MX_DXFEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, DxfExportOracle.Incipit, DxfExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : DxfExportOracle.CountKeys;

      var where = $"{filename} [export]";
      DxfExportOptions options = DxfExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".dxf");

      bool keep = System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };
      Rhino.UnitSystem sourceUnits;

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          sourceUnits = source.ModelUnitSystem;
          result.Source = StepImporter.Measure(source, DxfExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          bool wrote = FileDwg.Write(outputPath, source, options.ToRhino());
          watch.Stop();

          Assert.IsTrue(wrote, $"{where}: FileDwg.Write() returned false.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: FileDwg.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;
        Assert.Greater(result.Bytes, 0L, $"{where}: the exported file is empty.");

        DxfStructure.Check(where, outputPath, options, sourceUnits);

        RhinoDoc readBack = DxfImporter.CreateDoc();
        try
        {
          Assert.IsTrue(DxfImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileDwg.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, DxfExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", DxfExportOracle.SourceEntries(oracle), result.Source, DxfOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", DxfExportOracle.ResultEntries(oracle), result.Result, DxfOracle.EnvPrefix);
          }
        }
        catch (AssertionException)
        {
          if (writeDebugModel) SaveDebugModel(readBack, filepath);
          throw;
        }
        finally
        {
          readBack.Dispose();
        }

        failed = false;
      }
      finally
      {
        if (keep || (failed && writeDebugModel)) KeepOutput(outputPath, filepath);
        TryDelete(outputDir);
      }
    }

    internal static RhinoDoc OpenSource(string filepath, out double seconds)
    {
      if (Path.GetExtension(filepath).Equals(".3dm", StringComparison.InvariantCultureIgnoreCase))
      {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        RhinoDoc rc = RhinoDoc.OpenHeadless(filepath);
        watch.Stop();
        seconds = watch.Elapsed.TotalSeconds;

        if (rc == null)
          throw new InvalidOperationException($"'{Path.GetFileName(filepath)}': RhinoDoc.OpenHeadless returned null.");

        return rc;
      }

      RhinoDoc doc = DxfImporter.CreateDoc();
      try
      {
        if (!DxfImporter.Read(filepath, doc, out seconds))
          throw new InvalidOperationException(
            $"'{Path.GetFileName(filepath)}': the source did not import, so there is nothing to export. " +
            "A model that cannot be read has no business in the export suite - fix the import first.");
      }
      catch
      {
        doc.Dispose();
        throw;
      }

      return doc;
    }

    static string TempDir()
    {
      string rc = Path.Combine(Path.GetTempPath(), "mx_dxf_export", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(rc);
      return rc;
    }

    static void KeepOutput(string outputPath, string modelPath)
    {
      if (!File.Exists(outputPath)) return;

      string kept = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.dxf");

      try
      {
        File.Copy(outputPath, kept, true);
        TestContext.Progress.WriteLine($"[MXDXFEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXDXFEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXDXFEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, DxfExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXDXFEX]\t{filename}\tacadversion={(int)options.Version}" +
        $"\tobjects={s.Objects}->{r.Objects}\tbreps={s.Breps}->{r.Breps}" +
        $"\tcurves={s.Curves}->{r.Curves}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tinstances={s.Instances}->{r.Instances}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXDXFEX]\t{filename}\tno baseline: DXF structure invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXDXFEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_dxf_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_REGEN");
      if (string.IsNullOrWhiteSpace(selection)) return false;

      return selection.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0)
                      .Any(s => s == "*" || filename.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
    }

    internal static StepImportRunner.RegenOutcome RegenerateOracle(string filepath, string[] defaultKeys, out string failure)
    {
      failure = null;

      string filename = Path.GetFileName(filepath);
      if (!RegenSelected(filename)) return StepImportRunner.RegenOutcome.Skipped;

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, DxfExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, DxfExportOracle.Incipit, DxfExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      DxfExportOptions options = DxfExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".dxf");

      try
      {
        var result = new StepExportResult { OutputPath = outputPath };

        RhinoDoc source;
        try { source = OpenSource(filepath, out _); }
        catch (Exception e)
        {
          failure = $"[regen] '{filename}': the source could not be opened: {e.Message}";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        try
        {
          if (source.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the source opened with no objects; there is nothing to export.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.Source = StepImporter.Measure(source, DxfExportOracle.SourceWanted(keys));

          if (!FileDwg.Write(outputPath, source, options.ToRhino()))
          {
            failure = $"[regen] '{filename}': FileDwg.Write() returned false; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }
        }
        finally
        {
          source.Dispose();
        }

        if (!File.Exists(outputPath))
        {
          failure = $"[regen] '{filename}': FileDwg.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        RhinoDoc readBack = DxfImporter.CreateDoc();
        try
        {
          if (!DxfImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, DxfExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(DxfExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

        string report =
          $"===== REGEN {filename}  [{(dryRun ? "DRY-RUN, " : "")}" +
          $"{(result.WriteSeconds + result.ReadBackSeconds).ToString("F2", CultureInfo.InvariantCulture)}s] ====={System.Environment.NewLine}" +
          $"----- OLD -----{System.Environment.NewLine}" +
          (old == null ? "(none)" : File.ReadAllText(oraclePath).TrimEnd()) + System.Environment.NewLine +
          $"----- NEW -----{System.Environment.NewLine}" + newText.TrimEnd() + System.Environment.NewLine + System.Environment.NewLine;

        AppendRegenReport(report);

        if (!dryRun) File.WriteAllText(oraclePath, newText);
        return StepImportRunner.RegenOutcome.Written;
      }
      finally
      {
        TryDelete(outputDir);
      }
    }

    static string Format(string key, DxfExportOptions options, StepExportResult result)
    {
      if (DxfExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(DxfExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return DxfExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(DxfExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_dxf_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return DxfExportOracle.AllKeys;
        case "COUNTS": return DxfExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>
  /// Folder-scanning base for DXF export fixtures. A source model may be a <c>.3dm</c> as well as
  /// a DXF file. Same file name conventions as everywhere else.
  /// </summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyDxfExportFixture<T> where T : AnyDxfExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".dxf" };

    static AnyDxfExportFixture()
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
    internal static void Execute(string filename, string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string full = Path.Combine(filepath, filename);

      if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
      {
        // Expected to fail. A '!' source may fail as an assertion (a comparison) or by being
        // unreadable in the first place (OpenSource throws InvalidOperationException) - both are
        // the failure the '!' announces.
        bool failedAsExpected = false;
        try { DxfExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        DxfExportRunner.Run(full, defaultKeys, writeDebugModel);
    }

    /// <summary>Shared body of the fixtures' [Explicit] Regenerate tests.</summary>
    internal static void ExecuteRegenerate(string[] defaultKeys)
    {
      int n = 0;
      List<string> failures = new List<string>();

      foreach (string path in g_test_models)
      {
        // '!' models are expected to fail; never regenerate their baselines.
        if (Path.GetFileName(path).StartsWith("!", StringComparison.InvariantCultureIgnoreCase)) continue;

        StepImportRunner.RegenOutcome outcome =
          DxfExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_DXFEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_DXFEXPORT_REGEN")}'.");
    }
  }
}
