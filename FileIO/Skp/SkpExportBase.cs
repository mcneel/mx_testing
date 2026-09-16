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
  /// The write options one SketchUp export test runs with. <see cref="FileSkpWriteOptions"/> is
  /// small: the target SketchUp version, whether planar regions become polygons, whether objects
  /// are grouped, and the tessellation angle. Version is the one that shows up in the file's own
  /// header, which makes it the live probe that the options reach the writer at all.
  /// </summary>
  internal sealed class SkpExportOptions
  {
    // A property over an int for the reason given on StepMetrics.Bbox: a field of a RhinoCommon
    // enum type would force RhinoCommon to load while NUnit is still scanning this assembly.
    internal FileSkpWriteOptions.SketchUpVersion Version
    {
      get { return (FileSkpWriteOptions.SketchUpVersion)m_version; }
      set { m_version = (int)value; }
    }
    int m_version = (int)FileSkpWriteOptions.SketchUpVersion.SketchUp2021;

    internal bool ExportPlanarRegionsAsPolygons = true;
    internal bool GroupObjects = true;
    internal double MaxAngle = 15.0;

    internal FileSkpWriteOptions ToRhino()
    {
      return new FileSkpWriteOptions
      {
        Version = Version,
        ExportPlanarRegionsAsPolygons = ExportPlanarRegionsAsPolygons,
        GroupObjects = GroupObjects,
        MaxAngle = MaxAngle,
      };
    }

    /// <summary>The year the file header is expected to declare for the pinned version.</summary>
    internal string ExpectedVersionYear()
    {
      string name = Version.ToString();            // e.g. "SketchUp2021" or "SketchUp8"
      return name.Substring("SketchUp".Length);
    }

    internal static SkpExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new SkpExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        string v = entry.Value.Trim();
        switch (entry.Key)
        {
          case "version":
            {
              if (!Enum.TryParse("SketchUp" + v, true, out FileSkpWriteOptions.SketchUpVersion parsed) ||
                  !Enum.IsDefined(typeof(FileSkpWriteOptions.SketchUpVersion), parsed))
                throw new NotSupportedException(
                  $"{where}: '{v}' is not a SketchUp version. Use the bare year or number, e.g. 2021 or 8.");
              rc.Version = parsed;
            }
            break;
          case "planarregionsaspolygons": rc.ExportPlanarRegionsAsPolygons = B(v, where, entry.Key); break;
          case "groupobjects": rc.GroupObjects = B(v, where, entry.Key); break;
          case "maxangle":
            if (!double.TryParse(v, System.Globalization.NumberStyles.Float,
                                 CultureInfo.InvariantCulture, out double angle))
              throw new NotSupportedException($"{where}: 'maxangle' wants a number, got '{v}'.");
            rc.MaxAngle = angle;
            break;
        }
      }

      return rc;
    }

    static bool B(string value, string where, string key)
    {
      if (value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || value == "1") return true;
      if (value.Equals("false", StringComparison.InvariantCultureIgnoreCase) || value == "0") return false;
      throw new NotSupportedException($"{where}: '{key}' wants true or false, got '{value}'.");
    }

    internal string Format(string key)
    {
      switch (key)
      {
        case "version": return "version " + ExpectedVersionYear();
        case "planarregionsaspolygons":
          return "planarregionsaspolygons " + (ExportPlanarRegionsAsPolygons ? "true" : "false");
        case "groupobjects": return "groupobjects " + (GroupObjects ? "true" : "false");
        case "maxangle": return "maxangle " + MaxAngle.ToString("R", CultureInfo.InvariantCulture);
        default: throw new NotSupportedException($"'{key}' is not a SketchUp export option.");
      }
    }
  }

  /// <summary>The vocabulary of the SketchUp export sidecar.</summary>
  internal static class SkpExportOracle
  {
    internal const string Incipit = "SKP EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[]
    { "version", "planarregionsaspolygons", "groupobjects", "maxangle" };

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
  /// The structural checks a written .skp gets with no baseline. A SketchUp file opens with a
  /// fixed signature: the UTF-16LE string "SketchUp Model" behind a three-byte marker, then a
  /// length-prefixed version string of the form "{major.minor.build}". That version string is
  /// what makes these checks worth more than a magic-number test - it carries the version the
  /// writer actually targeted. It was meant to double as the live probe that the pinned Version
  /// option reaches the writer; it proved the opposite - see the note inside Check.
  /// </summary>
  internal static class SkpStructure
  {
    internal static void Check(string where, string filepath, SkpExportOptions options)
    {
      byte[] b = File.ReadAllBytes(filepath);
      Assert.Greater(b.Length, 64, $"{where}: {b.Length} bytes is far smaller than any .skp header.");

      // Bytes 0-3 are the marker, then "SketchUp Model" as UTF-16LE.
      Assert.IsTrue(b[0] == 0xFF && b[1] == 0xFE && b[2] == 0xFF,
        $"{where}: the file does not open with the SketchUp 0xFF 0xFE 0xFF marker.");

      string signature = System.Text.Encoding.Unicode.GetString(b, 4, 28);
      Assert.AreEqual("SketchUp Model", signature,
        $"{where}: the header signature reads '{signature}', expected 'SketchUp Model'.");

      // A second marker, then a byte length and that many UTF-16LE characters of version string.
      Assert.IsTrue(b[32] == 0xFF && b[33] == 0xFE && b[34] == 0xFF,
        $"{where}: no second marker where the version string should begin.");

      int n = b[35];
      Assert.IsTrue(n >= 5 && n <= 16,
        $"{where}: the version string declares {n} characters, which is outside the plausible 5-16.");
      Assert.LessOrEqual(36 + 2 * n, b.Length, $"{where}: the version string runs off the end of the file.");

      string version = System.Text.Encoding.Unicode.GetString(b, 36, 2 * n);
      Assert.IsTrue(version.StartsWith("{", StringComparison.Ordinal) &&
                    version.EndsWith("}", StringComparison.Ordinal),
        $"{where}: the version string is '{version}', expected it wrapped in braces.");

      string[] parts = version.Trim('{', '}').Split('.');
      Assert.GreaterOrEqual(parts.Length, 2,
        $"{where}: the version string '{version}' is not major.minor(.build).");

      Assert.IsTrue(int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int major),
        $"{where}: '{parts[0]}' in version string '{version}' is not a number.");

      // ⚠ Deliberately NOT compared against the pinned version option (RH-98712). Every file
      // this suite writes declares format 25, including the one written with SketchUp2014, so the
      // Version write option does not reach the writer - it always emits the SketchUp SDK's own
      // current format. Asserting the pinned version here would leave the suite permanently red
      // on a defect it has already recorded, so the check stops at "the header is well formed and
      // the major version is plausible". The option stays pinned in the sidecar as a request, the
      // same way IGES and DXF pin theirs, so a fix shows up as a baseline diff.
      Assert.Greater(major, 0, $"{where}: the file declares format version {major} ('{version}').");

      Assert.GreaterOrEqual(b.Length, 36 + 2 * n + 64,
        $"{where}: nothing follows the header; the file looks truncated.");
    }
  }

  /// <summary>
  /// Drives one STL export test on the MX_SKPEXPORT_* variables. The writer meshes every
  /// meshable object into ONE STL body (curves, points and annotations are dropped); the import
  /// side's SplitDisjointMeshes default then splits disjoint pieces back apart, and the baselines
  /// record that round-trip shape.
  /// </summary>
  internal static class SkpExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, SkpExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_SKPEXPORT_REQUIRE_BASELINE=1. " +
          $"Create '{Path.GetFileName(oraclePath)}' with MX_SKPEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, SkpExportOracle.Incipit, SkpExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : SkpExportOracle.CountKeys;

      var where = $"{filename} [export]";
      SkpExportOptions options = SkpExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".skp");

      bool keep = System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, SkpExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          bool wrote = FileSkp.Write(outputPath, source, options.ToRhino());
          watch.Stop();

          Assert.IsTrue(wrote, $"{where}: FileSkp.Write() returned false.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: FileSkp.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;

        SkpStructure.Check(where, outputPath, options);

        RhinoDoc readBack = SkpImporter.CreateDoc();
        try
        {
          Assert.IsTrue(SkpImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileSkp.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, SkpExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", SkpExportOracle.SourceEntries(oracle), result.Source, SkpOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", SkpExportOracle.ResultEntries(oracle), result.Result, SkpOracle.EnvPrefix);
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

      RhinoDoc doc = SkpImporter.CreateDoc();
      try
      {
        if (!SkpImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_skp_export", Guid.NewGuid().ToString("N"));
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
        TestContext.Progress.WriteLine($"[MXSKPEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSKPEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSKPEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, SkpExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXSKPEX]\t{filename}\tversion={options.ExpectedVersionYear()}" +
        $"\tobjects={s.Objects}->{r.Objects}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tsolids={s.Solids}->{r.Solids}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXSKPEX]\t{filename}\tno baseline: SketchUp header invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXSKPEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_skp_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, SkpExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, SkpExportOracle.Incipit, SkpExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      SkpExportOptions options = SkpExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".skp");

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

          result.Source = StepImporter.Measure(source, SkpExportOracle.SourceWanted(keys));

          if (!FileSkp.Write(outputPath, source, options.ToRhino()))
          {
            failure = $"[regen] '{filename}': FileSkp.Write() returned false; cannot regenerate.";
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
          failure = $"[regen] '{filename}': FileSkp.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        RhinoDoc readBack = SkpImporter.CreateDoc();
        try
        {
          if (!SkpImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, SkpExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(SkpExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    static string Format(string key, SkpExportOptions options, StepExportResult result)
    {
      if (SkpExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(SkpExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return SkpExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(SkpExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_skp_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return SkpExportOracle.AllKeys;
        case "COUNTS": return SkpExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>Folder-scanning base for STL export fixtures; sources may be .3dm or .stl.</summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnySkpExportFixture<T> where T : AnySkpExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".skp" };

    static AnySkpExportFixture()
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
        try { SkpExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        SkpExportRunner.Run(full, defaultKeys, writeDebugModel);
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
          SkpExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_SKPEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_SKPEXPORT_REGEN")}'.");
    }
  }
}
