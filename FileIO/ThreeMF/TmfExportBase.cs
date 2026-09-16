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
  /// The write options one 3MF export test runs with. File3mfWriteOptions is mostly document
  /// metadata (title, designer, copyright and so on); the one option that changes geometry is
  /// MoveOutputToPositiveXYZOctant, which 3MF needs because the format requires non-negative
  /// coordinates. That one is pinned; the metadata strings are left at their empty defaults
  /// rather than pinned, because they cannot affect what the round trip measures.
  /// </summary>
  internal sealed class TmfExportOptions
  {
    internal bool MoveToPositiveOctant = true;

    internal File3mfWriteOptions ToRhino()
    {
      return new File3mfWriteOptions { MoveOutputToPositiveXYZOctant = MoveToPositiveOctant };
    }

    internal static TmfExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new TmfExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        string v = entry.Value.Trim();
        if (entry.Key != "movetopositiveoctant") continue;

        if (v.Equals("true", StringComparison.InvariantCultureIgnoreCase) || v == "1") rc.MoveToPositiveOctant = true;
        else if (v.Equals("false", StringComparison.InvariantCultureIgnoreCase) || v == "0") rc.MoveToPositiveOctant = false;
        else throw new NotSupportedException($"{where}: 'movetopositiveoctant' wants true or false, got '{v}'.");
      }

      return rc;
    }

    internal string Format(string key)
    {
      switch (key)
      {
        case "movetopositiveoctant": return "movetopositiveoctant " + (MoveToPositiveOctant ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not a 3MF export option.");
      }
    }
  }

  /// <summary>The vocabulary of the 3MF export sidecar.</summary>
  internal static class TmfExportOracle
  {
    internal const string Incipit = "3MF EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[] { "movetopositiveoctant" };

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
  /// The structural checks a written 3MF gets with no baseline. 3MF is an OPC package - a ZIP
  /// with a prescribed set of parts - so the checks walk the container rather than the geometry:
  /// the ZIP magic and a findable end-of-central-directory, the [Content_Types].xml and
  /// _rels/.rels parts every OPC package must carry, a root relationship that resolves to a part
  /// that is actually in the archive, and that part parsing as XML whose root is a 3MF model
  /// element carrying a legal unit. A truncated or non-package write fails long before any
  /// geometry is compared.
  /// </summary>
  internal static class TmfStructure
  {
    static readonly string[] LegalUnits =
      new string[] { "micron", "millimeter", "centimeter", "inch", "foot", "meter" };

    internal static void Check(string where, string filepath, TmfExportOptions options)
    {
      byte[] bytes = File.ReadAllBytes(filepath);
      Assert.Greater(bytes.Length, 22, $"{where}: {bytes.Length} bytes is smaller than an empty ZIP.");
      Assert.IsTrue(bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04,
        $"{where}: the file does not start with the ZIP local-header magic 'PK\x03\x04'.");

      // End-of-central-directory must exist, or the archive is truncated. It lives in the last
      // 22 bytes plus up to 64 KB of comment.
      int eocd = -1;
      for (int i = bytes.Length - 22; i >= Math.Max(0, bytes.Length - 65557); i--)
      {
        if (bytes[i] == 0x50 && bytes[i + 1] == 0x4B && bytes[i + 2] == 0x05 && bytes[i + 3] == 0x06)
        {
          eocd = i;
          break;
        }
      }
      Assert.GreaterOrEqual(eocd, 0, $"{where}: no ZIP end-of-central-directory record. It looks truncated.");

      using (var zip = System.IO.Compression.ZipFile.OpenRead(filepath))
      {
        Assert.IsNotNull(zip.GetEntry("[Content_Types].xml"),
          $"{where}: the package has no [Content_Types].xml, so it is not a valid OPC package.");

        var rels = zip.GetEntry("_rels/.rels");
        Assert.IsNotNull(rels, $"{where}: the package has no _rels/.rels root relationship part.");

        string relsXml;
        using (var reader = new StreamReader(rels.Open())) relsXml = reader.ReadToEnd();

        var doc = System.Xml.Linq.XDocument.Parse(relsXml);
        string target = doc.Root.Elements()
          .Where(e => e.Name.LocalName == "Relationship")
          .Select(e => (string)e.Attribute("Target"))
          .FirstOrDefault(t => !string.IsNullOrEmpty(t) && t.EndsWith(".model", StringComparison.InvariantCultureIgnoreCase));
        Assert.IsNotNull(target, $"{where}: _rels/.rels declares no relationship to a .model part.");

        string modelPath = target.TrimStart('/');
        var model = zip.GetEntry(modelPath);
        Assert.IsNotNull(model, $"{where}: the root relationship points at '{modelPath}', which is not in the archive.");

        string modelXml;
        using (var reader = new StreamReader(model.Open())) modelXml = reader.ReadToEnd();

        var modelDoc = System.Xml.Linq.XDocument.Parse(modelXml);
        Assert.AreEqual("model", modelDoc.Root.Name.LocalName,
          $"{where}: the model part's root element is '{modelDoc.Root.Name.LocalName}', expected 'model'.");

        string unit = (string)modelDoc.Root.Attribute("unit");
        Assert.IsNotNull(unit, $"{where}: the model element declares no unit attribute.");
        Assert.IsTrue(LegalUnits.Contains(unit),
          $"{where}: the model declares unit '{unit}', which is not one of {string.Join(", ", LegalUnits)}.");
      }
    }
  }

  /// <summary>
  /// Drives one STL export test on the MX_TMFEXPORT_* variables. The writer meshes every
  /// meshable object into ONE STL body (curves, points and annotations are dropped); the import
  /// side's SplitDisjointMeshes default then splits disjoint pieces back apart, and the baselines
  /// record that round-trip shape.
  /// </summary>
  internal static class TmfExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, TmfExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_TMFEXPORT_REQUIRE_BASELINE=1. " +
          $"Create '{Path.GetFileName(oraclePath)}' with MX_TMFEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, TmfExportOracle.Incipit, TmfExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : TmfExportOracle.CountKeys;

      var where = $"{filename} [export]";
      TmfExportOptions options = TmfExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".3mf");

      bool keep = System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, TmfExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          bool wrote = File3mf.Write(outputPath, source, options.ToRhino());
          watch.Stop();

          Assert.IsTrue(wrote, $"{where}: File3mf.Write() returned false.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: File3mf.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;

        TmfStructure.Check(where, outputPath, options);

        RhinoDoc readBack = TmfImporter.CreateDoc();
        try
        {
          Assert.IsTrue(TmfImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileTmf.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, TmfExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", TmfExportOracle.SourceEntries(oracle), result.Source, TmfOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", TmfExportOracle.ResultEntries(oracle), result.Result, TmfOracle.EnvPrefix);
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

      RhinoDoc doc = TmfImporter.CreateDoc();
      try
      {
        if (!TmfImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_tmf_export", Guid.NewGuid().ToString("N"));
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
        TestContext.Progress.WriteLine($"[MXTMFEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXTMFEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXTMFEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, TmfExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXTMFEX]\t{filename}\toctant={(options.MoveToPositiveOctant ? "true" : "false")}" +
        $"\tobjects={s.Objects}->{r.Objects}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tsolids={s.Solids}->{r.Solids}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXTMFEX]\t{filename}\tno baseline: 3MF package invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXTMFEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_tmf_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, TmfExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, TmfExportOracle.Incipit, TmfExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      TmfExportOptions options = TmfExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".3mf");

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

          result.Source = StepImporter.Measure(source, TmfExportOracle.SourceWanted(keys));

          if (!File3mf.Write(outputPath, source, options.ToRhino()))
          {
            failure = $"[regen] '{filename}': File3mf.Write() returned false; cannot regenerate.";
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
          failure = $"[regen] '{filename}': File3mf.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        RhinoDoc readBack = TmfImporter.CreateDoc();
        try
        {
          if (!TmfImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, TmfExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(TmfExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    static string Format(string key, TmfExportOptions options, StepExportResult result)
    {
      if (TmfExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(TmfExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return TmfExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(TmfExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_tmf_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return TmfExportOracle.AllKeys;
        case "COUNTS": return TmfExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>Folder-scanning base for STL export fixtures; sources may be .3dm or .stl.</summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyTmfExportFixture<T> where T : AnyTmfExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".3mf" };

    static AnyTmfExportFixture()
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
        try { TmfExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        TmfExportRunner.Run(full, defaultKeys, writeDebugModel);
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
          TmfExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_TMFEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_TMFEXPORT_REGEN")}'.");
    }
  }
}
