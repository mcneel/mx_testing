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
  /// Reads and checks the sidecar oracle that accompanies each DXF model. The measured quantities,
  /// file format and comparison are <see cref="StepOracle"/>'s, reused the way the IGES suite
  /// reuses them, on the MX_DXF_* environment prefix.
  /// </summary>
  internal static class DxfOracle
  {
    internal const string Incipit = "DXF IMPORT";
    internal const string Suffix = ".expected.txt";
    internal const string EnvPrefix = "MX_DXF";

    internal static string PathFor(string modelPath) => modelPath + Suffix;

    internal static StepOracleFile Read(string oraclePath)
      => StepOracle.Read(oraclePath, Incipit, StepOracle.AllKeys);

    internal static void Check(string filename, StepOracleFile oracle, StepMetrics actual)
      => StepOracle.Check(filename, oracle.Entries, actual, EnvPrefix);
  }

  /// <summary>
  /// Imports one DXF file into a headless document and measures it.
  /// </summary>
  /// <remarks>
  /// The import goes through <see cref="FileDwg.Read"/> with an explicit default
  /// <see cref="FileDwgReadOptions"/>, never bare <c>RhinoDoc.Import</c>: the full dictionary pins
  /// all twelve read options in the baseline's meaning, and the plugin ignores dictionaries with
  /// fewer than two entries, so partial hand-built dictionaries are a trap. DXF has no RhinoCommon
  /// class of its own - the ACAD plugin registers .dwg and .dxf and dispatches on the extension.
  ///
  /// Two importer behaviours the baselines record: paper space is skipped in import mode (only
  /// model-space entities count), and the ModelUnits/LayoutUnits read options do NOT rescale
  /// geometry on a headless import (the unit scale is only computed in the options UI, which
  /// headless skips) - coordinates arrive as the raw numbers in the file, whatever $INSUNITS says.
  /// Deterministic, but a genuine plumbing gap; see the guide.
  /// </remarks>
  internal static class DxfImporter
  {
    internal static RhinoDoc CreateDoc() => StepImporter.CreateDoc();

    internal static bool Read(string filepath, RhinoDoc doc, out double seconds)
    {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      bool rc = FileDwg.Read(filepath, doc, new FileDwgReadOptions());
      watch.Stop();

      seconds = watch.Elapsed.TotalSeconds;
      return rc;
    }
  }

  /// <summary>
  /// Drives one model: import, measure, compare - plus the opt-in baseline regeneration.
  /// The counterpart of <see cref="IgesImportRunner"/> on the MX_DXF_* variables.
  /// </summary>
  internal static class DxfImportRunner
  {
    internal static void Run(string filepath, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = DxfOracle.PathFor(filepath);

      if (!File.Exists(oraclePath))
        Assert.Fail(
          $"'{filename}' has no baseline. Expected '{Path.GetFileName(oraclePath)}' beside it. " +
          $"Create it by running the fixture's explicit Regenerate test with MX_DXF_REGEN=\"{filename}\" " +
          "(or MX_DXF_REGEN=* for the whole folder), then review the file before committing it.");

      StepOracleFile oracle = DxfOracle.Read(oraclePath);
      var wanted = new HashSet<string>(oracle.Entries.Select(e => e.Key));

      RhinoDoc doc = DxfImporter.CreateDoc();
      try
      {
        bool rc = DxfImporter.Read(filepath, doc, out double seconds);

        Assert.IsTrue(rc, $"'{filename}': FileDwg.Read() returned false, the file did not import.");
        Assert.IsTrue(doc.Objects.Count > 0, $"'{filename}': imported without error but produced no objects.");

        StepMetrics measured = StepImporter.Measure(doc, wanted);
        measured.ReadSeconds = seconds;
        Emit(filename, measured);

        try
        {
          DxfOracle.Check(filename, oracle, measured);
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

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXDXF] could not save '{debugPath}': {e.Message}"); }
    }

    static void Emit(string filename, StepMetrics m)
    {
      string line =
        $"[MXDXF]\t{filename}\tobjects={m.Objects}\tbreps={m.Breps}\tcurves={m.Curves}\tinstances={m.Instances}" +
        $"\tinvalid={m.Invalid}\tseconds={m.ReadSeconds.ToString("F2", CultureInfo.InvariantCulture)}";

      foreach (string report in m.InvalidReports)
        line += $"{System.Environment.NewLine}[MXDXF]\t{filename}\tinvalid: {report}";

      if (m.Invalid > m.InvalidReports.Count)
        line += $"{System.Environment.NewLine}[MXDXF]\t{filename}\tinvalid: " +
                $"and {m.Invalid - m.InvalidReports.Count} more, not listed.";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_DXF_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_dxf.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_DXF_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_DXF_REGEN_DRYRUN") == "1";
      string oraclePath = DxfOracle.PathFor(filepath);

      StepOracleFile old = File.Exists(oraclePath) ? DxfOracle.Read(oraclePath) : null;
      string[] keys = ChooseKeys(old, defaultKeys);

      RhinoDoc doc = DxfImporter.CreateDoc();
      try
      {
        bool rc = DxfImporter.Read(filepath, doc, out double seconds);
        if (!rc || doc.Objects.Count == 0)
        {
          failure = $"[regen] '{filename}': import failed (returned {rc}, {doc.Objects.Count} objects); cannot regenerate.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] =====\n{failure}\n\n");
          return StepImportRunner.RegenOutcome.Failed;
        }

        StepMetrics measured = StepImporter.Measure(doc, keys);
        measured.ReadSeconds = seconds;

        string newText = StepOracle.Write(DxfOracle.Incipit, old, keys.Select(k => StepOracle.Format(k, measured)));

        string report =
          $"===== REGEN {filename}  [{(dryRun ? "DRY-RUN, " : "")}{seconds.ToString("F2", CultureInfo.InvariantCulture)}s] =====\n" +
          "----- OLD -----\n" + (old == null ? "(none)" : File.ReadAllText(oraclePath).TrimEnd()) + "\n" +
          "----- NEW -----\n" + newText.TrimEnd() + "\n\n";

        AppendRegenReport(report);

        if (!dryRun) File.WriteAllText(oraclePath, newText);
        return StepImportRunner.RegenOutcome.Written;
      }
      finally
      {
        doc.Dispose();
      }
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_DXF_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_dxf_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_DXF_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return StepOracle.AllKeys;
        case "COUNTS": return StepOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>
  /// Folder-scanning base for DXF import fixtures, honouring the same file name conventions as
  /// every other suite here.
  /// </summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyDxfFixture<T> where T : AnyDxfFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".dxf" };

    static AnyDxfFixture()
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
    internal static void Execute(string filename, string filepath, bool writeDebugModel)
    {
      string full = Path.Combine(filepath, filename);

      if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
        Assert.Throws<AssertionException>(
          delegate { DxfImportRunner.Run(full, false); },
          "Expected failure, but test succeeded.");
      else
        DxfImportRunner.Run(full, writeDebugModel);
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
          DxfImportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be imported:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_DXF_REGEN='{System.Environment.GetEnvironmentVariable("MX_DXF_REGEN")}'.");
    }
  }
}
