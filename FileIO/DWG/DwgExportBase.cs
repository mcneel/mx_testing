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
  /// The vocabulary of the DWG export sidecar. The write options and their parsing are
  /// <see cref="DxfExportOptions"/>'s, reused wholesale - DWG and DXF share
  /// <see cref="FileDwgWriteOptions"/> and the same three known-broken keys - so all this adds is
  /// the DWG incipit.
  /// </summary>
  internal static class DwgExportOracle
  {
    internal const string Incipit = "DWG EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = DxfExportOracle.OptionKeys;

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
  /// The structural checks a written DWG file gets with no baseline at all. DWG is binary, so the
  /// checks are byte-level on the fixed file header (the R2004+ layout is shared by
  /// AC1018-AC1032, and the R2000 layout agrees on everything checked here): the six-byte version
  /// magic (which doubles as the live probe that the Version option plumbs), the null pad behind
  /// it, preview-pointer sanity, the CP_ANSI_1252 codepage the writer sets explicitly, a minimum
  /// plausible size, and a cheap not-actually-a-DXF negative. Deliberately NOT attempted: section
  /// map decode and header CRCs - compressed/obfuscated, far beyond a ten-line check.
  /// </summary>
  internal static class DwgStructure
  {
    static readonly byte[] s_previewSentinel = new byte[]
    {
      0x1F, 0x25, 0x6D, 0x07, 0xD4, 0x36, 0x28, 0x28, 0x9D, 0x57, 0xCA, 0x3F, 0x9D, 0x44, 0x10, 0x2B
    };

    internal static void Check(string where, string filepath, DxfExportOptions options)
    {
      byte[] bytes = File.ReadAllBytes(filepath);
      Assert.Greater(bytes.Length, 0x400,
        $"{where}: the file is {bytes.Length} bytes; even an empty DWG database is larger than 1 KB. It looks truncated.");

      // 1. Version magic - and the live probe that the Version option reached the ODA writer.
      string magic = System.Text.Encoding.ASCII.GetString(bytes, 0, 6);
      Assert.AreEqual(options.ExpectedAcadVer(), magic,
        $"{where}: the file begins '{magic}' but the pinned acadversion {(int)options.Version} maps to '{options.ExpectedAcadVer()}'.");

      // 2. Not a DXF in disguise (extension-based plugin dispatch misrouting would land here).
      string head = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(16, bytes.Length));
      Assert.IsFalse(head.TrimStart().StartsWith("0"),
        $"{where}: the file starts like an ASCII DXF, not a binary DWG.");

      // 3. Null pad: five zero bytes after the version tag (R13+ header layout).
      for (int i = 0x06; i <= 0x0A; i++)
        Assert.AreEqual(0, bytes[i], $"{where}: header byte 0x{i:X2} is 0x{bytes[i]:X2}, expected 0x00.");

      // 4. Preview pointer sanity: zero, or an in-file offset carrying the preview sentinel.
      uint preview = BitConverter.ToUInt32(bytes, 0x0D);
      if (preview != 0)
      {
        Assert.IsTrue(preview > 0x20 && preview < bytes.Length - s_previewSentinel.Length,
          $"{where}: preview pointer 0x{preview:X} is outside the file.");
        for (int i = 0; i < s_previewSentinel.Length; i++)
          Assert.AreEqual(s_previewSentinel[i], bytes[preview + i],
            $"{where}: no preview sentinel at the preview pointer 0x{preview:X}.");
      }

      // 5. Codepage: the writer sets CP_ANSI_1252 explicitly for R13+.
      ushort codepage = BitConverter.ToUInt16(bytes, 0x13);
      Assert.AreEqual(30, codepage, $"{where}: header codepage is {codepage}, expected 30 (CP_ANSI_1252).");
    }
  }

  /// <summary>
  /// Drives one DWG export test: open the source, write via FileDwg.Write, check the binary
  /// header, read it back via FileDwg.Read, compare both ends against the sidecar - plus the
  /// opt-in baseline regeneration on the MX_DWGEXPORT_* variables.
  /// </summary>
  internal static class DwgExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, DwgExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_DWGEXPORT_REQUIRE_BASELINE=1. " +
          $"Expected '{Path.GetFileName(oraclePath)}' beside it. Create it with MX_DWGEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, DwgExportOracle.Incipit, DwgExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : DwgExportOracle.CountKeys;

      var where = $"{filename} [export]";
      DxfExportOptions options = DxfExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".dwg");

      bool keep = System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, DwgExportOracle.SourceWanted(keys));
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

        DwgStructure.Check(where, outputPath, options);

        RhinoDoc readBack = DwgImporter.CreateDoc();
        try
        {
          Assert.IsTrue(DwgImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileDwg.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, DwgExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", DwgExportOracle.SourceEntries(oracle), result.Source, DwgOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", DwgExportOracle.ResultEntries(oracle), result.Result, DwgOracle.EnvPrefix);
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

      RhinoDoc doc = DwgImporter.CreateDoc();
      try
      {
        if (!DwgImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_dwg_export", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(rc);
      return rc;
    }

    static void KeepOutput(string outputPath, string modelPath)
    {
      if (!File.Exists(outputPath)) return;

      string kept = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.dwg");

      try
      {
        File.Copy(outputPath, kept, true);
        TestContext.Progress.WriteLine($"[MXDWGEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXDWGEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXDWGEX] could not save '{debugPath}': {e.Message}"); }
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
        $"[MXDWGEX]\t{filename}\tacadversion={(int)options.Version}" +
        $"\tobjects={s.Objects}->{r.Objects}\tbreps={s.Breps}->{r.Breps}" +
        $"\tcurves={s.Curves}->{r.Curves}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tinstances={s.Instances}->{r.Instances}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXDWGEX]\t{filename}\tno baseline: DWG header invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXDWGEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_dwg_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, DwgExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, DwgExportOracle.Incipit, DwgExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      DxfExportOptions options = DxfExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".dwg");

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

          result.Source = StepImporter.Measure(source, DwgExportOracle.SourceWanted(keys));

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

        RhinoDoc readBack = DwgImporter.CreateDoc();
        try
        {
          if (!DwgImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, DwgExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(DwgExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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
      if (DwgExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(DwgExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return DwgExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(DwgExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_dwg_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return DwgExportOracle.AllKeys;
        case "COUNTS": return DwgExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>Folder-scanning base for DWG export fixtures; sources may be .3dm or .dwg.</summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyDwgExportFixture<T> where T : AnyDwgExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".dwg" };

    static AnyDwgExportFixture()
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
        try { DwgExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        DwgExportRunner.Run(full, defaultKeys, writeDebugModel);
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
          DwgExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_DWGEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_DWGEXPORT_REGEN")}'.");
    }
  }
}
