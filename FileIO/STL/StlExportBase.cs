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
  /// The write options one STL export test runs with. The STL export dictionary genuinely plumbs
  /// (BinaryFile, ExportOpenObjects and MeshingParameters are all read by the native writer with
  /// matching key spellings), so both sidecar keys are real effects. ExportOpenObjects stays true
  /// as a hard rule: false plus open geometry raises an ungated modal error dialog, a headless
  /// hazard. MeshingParameters stay at the RhinoCommon default and are not a sidecar key.
  /// </summary>
  internal sealed class StlExportOptions
  {
    internal bool BinaryFile = true;

    internal FileStlWriteOptions ToRhino()
    {
      return new FileStlWriteOptions
      {
        BinaryFile = BinaryFile,
        ExportOpenObjects = true,
        MeshingParameters = Rhino.Geometry.MeshingParameters.Default,
      };
    }

    internal static StlExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new StlExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        string v = entry.Value.Trim();
        switch (entry.Key)
        {
          case "binaryfile":
            if (v.Equals("true", StringComparison.InvariantCultureIgnoreCase) || v == "1") rc.BinaryFile = true;
            else if (v.Equals("false", StringComparison.InvariantCultureIgnoreCase) || v == "0") rc.BinaryFile = false;
            else throw new NotSupportedException($"{where}: 'binaryfile' wants true or false, got '{v}'.");
            break;
        }
      }

      return rc;
    }

    internal string Format(string key)
    {
      switch (key)
      {
        case "binaryfile": return "binaryfile " + (BinaryFile ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not an STL export option.");
      }
    }
  }

  /// <summary>The vocabulary of the STL export sidecar.</summary>
  internal static class StlExportOracle
  {
    internal const string Incipit = "STL EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[] { "binaryfile" };

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
  /// The structural checks a written STL gets with no baseline. STL has two on-disk variants and
  /// the pinned <c>binaryfile</c> option selects which grammar applies. Binary: an 80-byte header,
  /// a uint32 triangle count, and exactly count x 50 bytes of triangle records - the size formula
  /// is the whole file format, so it catches any truncation to the byte. ASCII: the
  /// solid/endsolid envelope with balanced facet/loop blocks of exactly three numeric vertices.
  /// </summary>
  internal static class StlStructure
  {
    internal static void Check(string where, string filepath, StlExportOptions options)
    {
      if (options.BinaryFile) CheckBinary(where, filepath);
      else CheckAscii(where, filepath);
    }

    static void CheckBinary(string where, string filepath)
    {
      byte[] bytes = File.ReadAllBytes(filepath);
      Assert.GreaterOrEqual(bytes.Length, 84, $"{where}: {bytes.Length} bytes; a binary STL is at least 84.");

      uint triangles = BitConverter.ToUInt32(bytes, 80);
      Assert.Greater(triangles, 0u, $"{where}: the binary STL declares zero triangles.");
      Assert.AreEqual(84L + 50L * triangles, (long)bytes.Length,
        $"{where}: {bytes.Length} bytes but the header declares {triangles} triangles " +
        $"(expected exactly {84L + 50L * triangles}). It looks truncated.");
    }

    static void CheckAscii(string where, string filepath)
    {
      string[] lines = File.ReadAllLines(filepath);
      Assert.IsNotEmpty(lines, $"{where}: the exported file is empty.");
      Assert.IsTrue(lines[0].TrimStart().StartsWith("solid", StringComparison.InvariantCultureIgnoreCase),
        $"{where}: an ASCII STL must open with 'solid', got '{lines[0]}'.");

      string lastNonEmpty = lines.LastOrDefault(l => l.Trim().Length > 0) ?? string.Empty;
      Assert.IsTrue(lastNonEmpty.TrimStart().StartsWith("endsolid", StringComparison.InvariantCultureIgnoreCase),
        $"{where}: an ASCII STL must end with 'endsolid', got '{lastNonEmpty}'. It looks truncated.");

      int facets = 0, endfacets = 0, loops = 0, endloops = 0, vertices = 0;
      foreach (string raw in lines)
      {
        string line = raw.Trim();
        if (line.StartsWith("facet normal", StringComparison.InvariantCultureIgnoreCase)) facets++;
        else if (line.StartsWith("endfacet", StringComparison.InvariantCultureIgnoreCase)) endfacets++;
        else if (line.StartsWith("outer loop", StringComparison.InvariantCultureIgnoreCase)) loops++;
        else if (line.StartsWith("endloop", StringComparison.InvariantCultureIgnoreCase)) endloops++;
        else if (line.StartsWith("vertex", StringComparison.InvariantCultureIgnoreCase))
        {
          vertices++;
          string[] tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
          Assert.AreEqual(4, tokens.Length, $"{where}: a vertex line needs exactly 3 coordinates: '{line}'.");
          for (int i = 1; i < 4; i++)
            Assert.IsTrue(double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
              $"{where}: vertex coordinate '{tokens[i]}' is not a number.");
        }
      }

      Assert.Greater(facets, 0, $"{where}: the ASCII STL has no facets.");
      Assert.AreEqual(facets, endfacets, $"{where}: {facets} 'facet' but {endfacets} 'endfacet'.");
      Assert.AreEqual(facets, loops, $"{where}: {facets} facets but {loops} 'outer loop'.");
      Assert.AreEqual(loops, endloops, $"{where}: {loops} 'outer loop' but {endloops} 'endloop'.");
      Assert.AreEqual(facets * 3, vertices, $"{where}: {facets} facets but {vertices} vertices; each loop has exactly 3.");
    }
  }

  /// <summary>
  /// Drives one STL export test on the MX_STLEXPORT_* variables. The writer meshes every
  /// meshable object into ONE STL body (curves, points and annotations are dropped); the import
  /// side's SplitDisjointMeshes default then splits disjoint pieces back apart, and the baselines
  /// record that round-trip shape.
  /// </summary>
  internal static class StlExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, StlExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_STLEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_STLEXPORT_REQUIRE_BASELINE=1. " +
          $"Create '{Path.GetFileName(oraclePath)}' with MX_STLEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, StlExportOracle.Incipit, StlExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : StlExportOracle.CountKeys;

      var where = $"{filename} [export]";
      StlExportOptions options = StlExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".stl");

      bool keep = System.Environment.GetEnvironmentVariable("MX_STLEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, StlExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          bool wrote = FileStl.Write(outputPath, source, options.ToRhino());
          watch.Stop();

          Assert.IsTrue(wrote, $"{where}: FileStl.Write() returned false.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: FileStl.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;

        StlStructure.Check(where, outputPath, options);

        RhinoDoc readBack = StlImporter.CreateDoc();
        try
        {
          Assert.IsTrue(StlImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileStl.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, StlExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", StlExportOracle.SourceEntries(oracle), result.Source, StlOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", StlExportOracle.ResultEntries(oracle), result.Result, StlOracle.EnvPrefix);
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

      RhinoDoc doc = StlImporter.CreateDoc();
      try
      {
        if (!StlImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_stl_export", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(rc);
      return rc;
    }

    static void KeepOutput(string outputPath, string modelPath)
    {
      if (!File.Exists(outputPath)) return;

      string kept = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.stl");

      try
      {
        File.Copy(outputPath, kept, true);
        TestContext.Progress.WriteLine($"[MXSTLEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSTLEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSTLEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, StlExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXSTLEX]\t{filename}\tbinary={(options.BinaryFile ? "true" : "false")}" +
        $"\tobjects={s.Objects}->{r.Objects}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tsolids={s.Solids}->{r.Solids}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXSTLEX]\t{filename}\tno baseline: STL structure invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXSTLEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_STLEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_stl_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_STLEXPORT_REGEN");
      if (string.IsNullOrWhiteSpace(selection)) return false;

      return selection.Split(',')
                      .Select(x => x.Trim())
                      .Where(x => x.Length > 0)
                      .Any(x => x == "*" || filename.IndexOf(x, StringComparison.InvariantCultureIgnoreCase) >= 0);
    }

    internal static StepImportRunner.RegenOutcome RegenerateOracle(string filepath, string[] defaultKeys, out string failure)
    {
      failure = null;

      string filename = Path.GetFileName(filepath);
      if (!RegenSelected(filename)) return StepImportRunner.RegenOutcome.Skipped;

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_STLEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, StlExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, StlExportOracle.Incipit, StlExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      StlExportOptions options = StlExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".stl");

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

          result.Source = StepImporter.Measure(source, StlExportOracle.SourceWanted(keys));

          if (!FileStl.Write(outputPath, source, options.ToRhino()))
          {
            failure = $"[regen] '{filename}': FileStl.Write() returned false; cannot regenerate.";
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
          failure = $"[regen] '{filename}': FileStl.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        RhinoDoc readBack = StlImporter.CreateDoc();
        try
        {
          if (!StlImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, StlExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(StlExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    static string Format(string key, StlExportOptions options, StepExportResult result)
    {
      if (StlExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(StlExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return StlExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(StlExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_STLEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_stl_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_STLEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return StlExportOracle.AllKeys;
        case "COUNTS": return StlExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>Folder-scanning base for STL export fixtures; sources may be .3dm or .stl.</summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyStlExportFixture<T> where T : AnyStlExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".stl" };

    static AnyStlExportFixture()
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
        try { StlExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        StlExportRunner.Run(full, defaultKeys, writeDebugModel);
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
          StlExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_STLEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_STLEXPORT_REGEN")}'.");
    }
  }
}
