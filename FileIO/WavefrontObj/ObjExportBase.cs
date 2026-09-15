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
  /// The write options one OBJ export test runs with. The OBJ writer is fully managed C# inside
  /// RhinoCommon and consumes every option directly, so every sidecar key here is a real effect -
  /// the inverse of the IGES situation. Two hard rules baked into <see cref="ToRhino"/>:
  /// <c>WriteOptions.SuppressAllInput = true</c> (the writer consults it before showing UI) and
  /// <c>ExportOpenMeshes</c> stays true (false plus open geometry raises a message box).
  /// </summary>
  internal sealed class ObjExportOptions
  {
    internal FileObjWriteOptions.GeometryType ObjectType
    {
      get { return (FileObjWriteOptions.GeometryType)m_objecttype; }
      set { m_objecttype = (int)value; }
    }
    int m_objecttype = (int)FileObjWriteOptions.GeometryType.Mesh;

    internal FileObjWriteOptions.VertexWelding MeshType
    {
      get { return (FileObjWriteOptions.VertexWelding)m_meshtype; }
      set { m_meshtype = (int)value; }
    }
    int m_meshtype = (int)FileObjWriteOptions.VertexWelding.Normal;

    internal FileObjWriteOptions.PolylineExportType PolylineType
    {
      get { return (FileObjWriteOptions.PolylineExportType)m_polylinetype; }
      set { m_polylinetype = (int)value; }
    }
    int m_polylinetype = (int)FileObjWriteOptions.PolylineExportType.Multiple;

    internal FileObjWriteOptions.NGons NgonMode
    {
      get { return (FileObjWriteOptions.NGons)m_ngons; }
      set { m_ngons = (int)value; }
    }
    int m_ngons = (int)FileObjWriteOptions.NGons.None;

    internal bool ExportTcs = true;
    internal bool ExportNormals = true;
    internal bool ExportVcs;
    internal int VcsFormat;
    internal bool ExportAsTriangles;
    internal bool MapZtoY;
    internal bool ExportMaterialDefinitions = true;

    internal FileObjWriteOptions ToRhino()
    {
      var writeOptions = new FileWriteOptions { SuppressAllInput = true, SuppressDialogBoxes = true };
      return new FileObjWriteOptions(writeOptions)
      {
        ObjectType = ObjectType,
        MeshType = MeshType,
        PolylineType = PolylineType,
        NgonMode = NgonMode,
        ExportTcs = ExportTcs,
        ExportNormals = ExportNormals,
        ExportVcs = ExportVcs,
        VcsFormat = VcsFormat,
        ExportAsTriangles = ExportAsTriangles,
        MapZtoY = MapZtoY,
        ExportMaterialDefinitions = ExportMaterialDefinitions,
        ExportOpenMeshes = true,
        EolType = FileObjWriteOptions.AsciiEol.Crlf,
      };
    }

    internal static ObjExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new ObjExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        string v = entry.Value.Trim();
        switch (entry.Key)
        {
          case "objecttype": rc.ObjectType = ParseEnum<FileObjWriteOptions.GeometryType>(v, where); break;
          case "meshtype": rc.MeshType = ParseEnum<FileObjWriteOptions.VertexWelding>(v, where); break;
          case "polylinetype": rc.PolylineType = ParseEnum<FileObjWriteOptions.PolylineExportType>(v, where); break;
          case "ngonmode": rc.NgonMode = ParseEnum<FileObjWriteOptions.NGons>(v, where); break;
          case "exporttcs": rc.ExportTcs = ParseBool(v, where, entry.Key); break;
          case "exportnormals": rc.ExportNormals = ParseBool(v, where, entry.Key); break;
          case "exportvcs": rc.ExportVcs = ParseBool(v, where, entry.Key); break;
          case "vcsformat": rc.VcsFormat = ParseInt(v, where, entry.Key); break;
          case "exportastriangles": rc.ExportAsTriangles = ParseBool(v, where, entry.Key); break;
          case "mapztoy": rc.MapZtoY = ParseBool(v, where, entry.Key); break;
          case "exportmaterialdefinitions": rc.ExportMaterialDefinitions = ParseBool(v, where, entry.Key); break;
        }
      }

      return rc;
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

    static int ParseInt(string value, string where, string key)
    {
      if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rc)) return rc;
      throw new NotSupportedException($"{where}: '{key}' wants a whole number, got '{value}'.");
    }

    internal string Format(string key)
    {
      switch (key)
      {
        case "objecttype": return "objecttype " + ObjectType.ToString().ToLowerInvariant();
        case "meshtype": return "meshtype " + MeshType.ToString().ToLowerInvariant();
        case "polylinetype": return "polylinetype " + PolylineType.ToString().ToLowerInvariant();
        case "ngonmode": return "ngonmode " + NgonMode.ToString().ToLowerInvariant();
        case "exporttcs": return "exporttcs " + (ExportTcs ? "true" : "false");
        case "exportnormals": return "exportnormals " + (ExportNormals ? "true" : "false");
        case "exportvcs": return "exportvcs " + (ExportVcs ? "true" : "false");
        case "vcsformat": return "vcsformat " + VcsFormat.ToString(CultureInfo.InvariantCulture);
        case "exportastriangles": return "exportastriangles " + (ExportAsTriangles ? "true" : "false");
        case "mapztoy": return "mapztoy " + (MapZtoY ? "true" : "false");
        case "exportmaterialdefinitions": return "exportmaterialdefinitions " + (ExportMaterialDefinitions ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not an OBJ export option.");
      }
    }
  }

  /// <summary>The vocabulary of the OBJ export sidecar.</summary>
  internal static class ObjExportOracle
  {
    internal const string Incipit = "OBJ EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[]
    {
      "objecttype", "meshtype", "polylinetype", "ngonmode",
      "exporttcs", "exportnormals", "exportvcs", "vcsformat",
      "exportastriangles", "mapztoy", "exportmaterialdefinitions"
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
  /// The structural checks a written OBJ gets with no baseline, from the writer's own emission
  /// grammar: the '# Rhino' header, CRLF discipline, ASCII-only bytes, the emitted keyword
  /// vocabulary, v/vn/vt field arity, face index validity and per-line ref-format consistency,
  /// and .mtl sidecar coherence. A truncated, mis-encoded or index-corrupt write fails here
  /// before any geometry is compared.
  /// </summary>
  internal static class ObjStructure
  {
    static readonly HashSet<string> s_keywords = new HashSet<string>(StringComparer.Ordinal)
    {
      "#", "mtllib", "usemtl", "g", "o", "v", "vp", "vt", "vn", "f", "l", "p",
      "cstype", "deg", "curv", "curv2", "surf", "parm", "trim", "hole", "end"
    };

    internal static void Check(string where, string filepath, ObjExportOptions options)
    {
      byte[] bytes = File.ReadAllBytes(filepath);
      Assert.Greater(bytes.Length, 0, $"{where}: the exported file is empty.");

      // ASCII-only, and CRLF discipline (the suite pins EolType=Crlf).
      for (int i = 0; i < bytes.Length; i++)
      {
        Assert.Less((int)bytes[i], 128, $"{where}: non-ASCII byte 0x{bytes[i]:X2} at offset {i}.");
        if (bytes[i] == (byte)'\n')
          Assert.IsTrue(i > 0 && bytes[i - 1] == (byte)'\r', $"{where}: bare LF at offset {i}; the suite pins CRLF.");
        if (bytes[i] == (byte)'\r')
          Assert.IsTrue(i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n', $"{where}: bare CR at offset {i}.");
      }

      string[] lines = File.ReadAllLines(filepath);
      Assert.IsTrue(lines.Length > 0 && lines[0].TrimEnd() == "# Rhino",
        $"{where}: the first line is '{(lines.Length > 0 ? lines[0] : "")}', expected '# Rhino'.");

      int vCount = 0, vtCount = 0, vnCount = 0;
      var mtllibs = new List<string>();
      var usemtls = new List<string>();

      for (int i = 0; i < lines.Length; i++)
      {
        string line = lines[i].Trim();
        if (line.Length == 0) continue;

        string[] tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        string keyword = tokens[0];
        string at = $"{where}: line {i + 1}";

        Assert.IsTrue(s_keywords.Contains(keyword), $"{at}: unknown keyword '{keyword}'.");

        switch (keyword)
        {
          case "v":
            vCount++;
            Assert.IsTrue(tokens.Length == 4 || tokens.Length == 7,
              $"{at}: 'v' has {tokens.Length - 1} fields; expected 3, or 6 with vertex colors.");
            AssertNumeric(tokens, at);
            break;
          case "vn":
            vnCount++;
            Assert.AreEqual(4, tokens.Length, $"{at}: 'vn' must have exactly 3 fields.");
            AssertNumeric(tokens, at);
            break;
          case "vt":
            vtCount++;
            Assert.IsTrue(tokens.Length >= 3, $"{at}: 'vt' must have at least 2 fields.");
            AssertNumeric(tokens, at);
            break;
          case "mtllib": mtllibs.Add(line.Substring(6).Trim()); break;
          case "usemtl": usemtls.Add(line.Substring(6).Trim()); break;
        }
      }

      // Faces: validated in a second pass so the v/vt/vn totals are complete.
      for (int i = 0; i < lines.Length; i++)
      {
        string line = lines[i].Trim();
        if (!line.StartsWith("f ", StringComparison.Ordinal)) continue;
        string at = $"{where}: line {i + 1}";

        string[] refs = line.Substring(2).Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.GreaterOrEqual(refs.Length, 3, $"{at}: a face needs at least 3 vertex refs.");

        int shape = refs[0].Count(c => c == '/') * 10 + (refs[0].Contains("//") ? 1 : 0);
        foreach (string r in refs)
        {
          Assert.AreEqual(shape, r.Count(c => c == '/') * 10 + (r.Contains("//") ? 1 : 0),
            $"{at}: mixed vertex-ref formats within one face line.");

          string[] parts = r.Split('/');
          AssertIndex(parts[0], vCount, at, "v");
          if (parts.Length > 1 && parts[1].Length > 0) AssertIndex(parts[1], vtCount, at, "vt");
          if (parts.Length > 2 && parts[2].Length > 0) AssertIndex(parts[2], vnCount, at, "vn");
        }
      }

      // .mtl sidecar coherence.
      if (options.ExportMaterialDefinitions && mtllibs.Count > 0)
      {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (string lib in mtllibs)
        {
          string mtlPath = Path.Combine(Path.GetDirectoryName(filepath), lib);
          Assert.IsTrue(File.Exists(mtlPath), $"{where}: mtllib '{lib}' does not exist beside the .obj.");

          string[] mtlLines = File.ReadAllLines(mtlPath);
          Assert.IsTrue(mtlLines.Length > 0 && mtlLines[0].TrimEnd() == "# Rhino",
            $"{where}: '{lib}' does not open with '# Rhino'.");
          foreach (string ml in mtlLines)
          {
            string t = ml.Trim();
            if (t.StartsWith("newmtl ", StringComparison.Ordinal)) declared.Add(t.Substring(7).Trim());
          }
        }

        foreach (string used in usemtls)
          Assert.IsTrue(declared.Contains(used), $"{where}: usemtl '{used}' is not declared in any mtllib.");
      }
      else if (!options.ExportMaterialDefinitions)
      {
        Assert.IsEmpty(mtllibs, $"{where}: exportmaterialdefinitions is false but the file has an mtllib line.");
      }
    }

    static void AssertNumeric(string[] tokens, string at)
    {
      for (int i = 1; i < tokens.Length; i++)
        Assert.IsTrue(double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
          $"{at}: field '{tokens[i]}' is not a number.");
    }

    static void AssertIndex(string token, int total, string at, string what)
    {
      Assert.IsTrue(int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx),
        $"{at}: {what}-index '{token}' is not a whole number.");
      Assert.IsTrue(idx >= 1 && idx <= total,
        $"{at}: {what}-index {idx} is out of range 1..{total}.");
    }
  }

  /// <summary>
  /// Drives one OBJ export test on the MX_OBJEXPORT_* variables. FileObj.Write returns
  /// WriteFileResult, not bool.
  /// </summary>
  internal static class ObjExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, ObjExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_OBJEXPORT_REQUIRE_BASELINE=1. " +
          $"Create '{Path.GetFileName(oraclePath)}' with MX_OBJEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, ObjExportOracle.Incipit, ObjExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : ObjExportOracle.CountKeys;

      var where = $"{filename} [export]";
      ObjExportOptions options = ObjExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".obj");

      bool keep = System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, ObjExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          Rhino.PlugIns.WriteFileResult wrote = FileObj.Write(outputPath, source, options.ToRhino());
          watch.Stop();

          Assert.AreEqual(Rhino.PlugIns.WriteFileResult.Success, wrote,
            $"{where}: FileObj.Write() returned {wrote}.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: FileObj.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;

        ObjStructure.Check(where, outputPath, options);

        RhinoDoc readBack = ObjImporter.CreateDoc();
        try
        {
          Assert.IsTrue(ObjImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileObj.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, ObjExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", ObjExportOracle.SourceEntries(oracle), result.Source, ObjOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", ObjExportOracle.ResultEntries(oracle), result.Result, ObjOracle.EnvPrefix);
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

      RhinoDoc doc = ObjImporter.CreateDoc();
      try
      {
        if (!ObjImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_obj_export", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(rc);
      return rc;
    }

    static void KeepOutput(string outputPath, string modelPath)
    {
      if (!File.Exists(outputPath)) return;

      string kept = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.obj");

      try
      {
        File.Copy(outputPath, kept, true);
        // The .mtl sidecar travels with its .obj when present.
        string mtl = Path.ChangeExtension(outputPath, ".mtl");
        if (File.Exists(mtl))
          File.Copy(mtl, Path.ChangeExtension(kept, ".mtl"), true);
        TestContext.Progress.WriteLine($"[MXOBJEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXOBJEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXOBJEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, ObjExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXOBJEX]\t{filename}\tobjecttype={options.ObjectType.ToString().ToLowerInvariant()}" +
        $"\tobjects={s.Objects}->{r.Objects}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tbreps={s.Breps}->{r.Breps}\tcurves={s.Curves}->{r.Curves}" +
        $"\tinstances={s.Instances}->{r.Instances}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXOBJEX]\t{filename}\tno baseline: OBJ grammar invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXOBJEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_obj_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, ObjExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, ObjExportOracle.Incipit, ObjExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      ObjExportOptions options = ObjExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".obj");

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

          result.Source = StepImporter.Measure(source, ObjExportOracle.SourceWanted(keys));

          if (FileObj.Write(outputPath, source, options.ToRhino()) != Rhino.PlugIns.WriteFileResult.Success)
          {
            failure = $"[regen] '{filename}': FileObj.Write() did not succeed; cannot regenerate.";
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
          failure = $"[regen] '{filename}': FileObj.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        RhinoDoc readBack = ObjImporter.CreateDoc();
        try
        {
          if (!ObjImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, ObjExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(ObjExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    static string Format(string key, ObjExportOptions options, StepExportResult result)
    {
      if (ObjExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(ObjExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return ObjExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(ObjExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_obj_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return ObjExportOracle.AllKeys;
        case "COUNTS": return ObjExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>Folder-scanning base for OBJ export fixtures; sources may be .3dm or .obj.</summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyObjExportFixture<T> where T : AnyObjExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".obj" };

    static AnyObjExportFixture()
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

    internal static void Execute(string filename, string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string full = Path.Combine(filepath, filename);

      if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
      {
        // Expected to fail - as an assertion, or by being unreadable in the first place.
        bool failedAsExpected = false;
        try { ObjExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        ObjExportRunner.Run(full, defaultKeys, writeDebugModel);
    }

    internal static void ExecuteRegenerate(string[] defaultKeys)
    {
      int n = 0;
      List<string> failures = new List<string>();

      foreach (string path in g_test_models)
      {
        // '!' models are expected to fail; never regenerate their baselines.
        if (Path.GetFileName(path).StartsWith("!", StringComparison.InvariantCultureIgnoreCase)) continue;

        StepImportRunner.RegenOutcome outcome =
          ObjExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_OBJEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_OBJEXPORT_REGEN")}'.");
    }
  }
}
