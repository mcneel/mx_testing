using NUnit.Framework;
using Rhino;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FileIO
{
  /// <summary>
  /// Reads and checks the sidecar oracle that accompanies each IGES model.
  /// </summary>
  /// <remarks>
  /// The measured quantities, the file format and the comparison are <see cref="StepOracle"/>'s,
  /// reused wholesale the way the STEP export suite already reuses them: the parser takes its
  /// incipit and key vocabulary as parameters, and the comparison takes an environment prefix so
  /// that MX_IGES_RELTOL / MX_IGES_ABSTOL override the slack here without touching STEP's knobs.
  /// All this class pins down is the IGES spelling of each. (StepOracle and StepMetrics are
  /// format-neutral in behaviour; renaming them is a follow-up once more formats prove the shape.)
  /// </remarks>
  internal static class IgesOracle
  {
    internal const string Incipit = "IGES IMPORT";
    internal const string Suffix = ".expected.txt";
    internal const string EnvPrefix = "MX_IGES";

    internal static string PathFor(string modelPath) => modelPath + Suffix;

    internal static StepOracleFile Read(string oraclePath)
      => StepOracle.Read(oraclePath, Incipit, StepOracle.AllKeys);

    internal static void Check(string filename, StepOracleFile oracle, StepMetrics actual)
      => StepOracle.Check(filename, oracle.Entries, actual, EnvPrefix);
  }

  /// <summary>
  /// Imports one IGES file into a headless document and measures it.
  /// </summary>
  /// <remarks>
  /// Units and tolerance are pinned to the same millimetre / 0.001 document
  /// <see cref="StepImporter"/> uses, for the same reason: the importer converts the file's units
  /// into the document's, and a baseline is only portable if every machine imports into the same
  /// unit system. There is no FileIgs.Read - RhinoCommon exposes the IGES writer but not the
  /// reader - so the import goes through <see cref="RhinoDoc.Import(string)"/> and runs with the
  /// reader's default options (surfaces joined). That default is part of what the baselines pin.
  /// </remarks>
  internal static class IgesImporter
  {
    internal static RhinoDoc CreateDoc() => StepImporter.CreateDoc();

    internal static bool Read(string filepath, RhinoDoc doc, out double seconds)
    {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      bool rc = doc.Import(filepath);
      watch.Stop();

      seconds = watch.Elapsed.TotalSeconds;
      return rc;
    }
  }

  /// <summary>
  /// Drives one model: import, measure, compare - plus the opt-in baseline regeneration that
  /// produces the oracles in the first place. The counterpart of <see cref="StepImportRunner"/>,
  /// on its own MX_IGES_* environment variables.
  /// </summary>
  internal static class IgesImportRunner
  {
    internal static void Run(string filepath, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = IgesOracle.PathFor(filepath);

      if (!File.Exists(oraclePath))
        Assert.Fail(
          $"'{filename}' has no baseline. Expected '{Path.GetFileName(oraclePath)}' beside it. " +
          $"Create it by running the fixture's explicit Regenerate test with MX_IGES_REGEN=\"{filename}\" " +
          "(or MX_IGES_REGEN=* for the whole folder), then review the file before committing it.");

      StepOracleFile oracle = IgesOracle.Read(oraclePath);
      var wanted = new HashSet<string>(oracle.Entries.Select(e => e.Key));

      RhinoDoc doc = IgesImporter.CreateDoc();
      try
      {
        bool rc = IgesImporter.Read(filepath, doc, out double seconds);

        Assert.IsTrue(rc, $"'{filename}': RhinoDoc.Import() returned false, the file did not import.");
        Assert.IsTrue(doc.Objects.Count > 0, $"'{filename}': imported without error but produced no objects.");

        StepMetrics measured = StepImporter.Measure(doc, wanted);
        measured.ReadSeconds = seconds;
        Emit(filename, measured);

        try
        {
          IgesOracle.Check(filename, oracle, measured);
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
      try { doc.WriteFile(debugPath, new Rhino.FileIO.FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXIGES] could not save '{debugPath}': {e.Message}"); }
    }

    static void Emit(string filename, StepMetrics m)
    {
      string line =
        $"[MXIGES]\t{filename}\tobjects={m.Objects}\tbreps={m.Breps}\tsolids={m.Solids}\tinvalid={m.Invalid}" +
        $"\tseconds={m.ReadSeconds.ToString("F2", CultureInfo.InvariantCulture)}";

      foreach (string report in m.InvalidReports)
        line += $"{System.Environment.NewLine}[MXIGES]\t{filename}\tinvalid: {report}";

      if (m.Invalid > m.InvalidReports.Count)
        line += $"{System.Environment.NewLine}[MXIGES]\t{filename}\tinvalid: " +
                $"and {m.Invalid - m.InvalidReports.Count} more, not listed.";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_IGES_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_iges.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    /// <summary>True if <paramref name="filename"/> matches the comma-separated MX_IGES_REGEN list. "*" matches all.</summary>
    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_IGES_REGEN");
      if (string.IsNullOrWhiteSpace(selection)) return false;

      return selection.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0)
                      .Any(s => s == "*" || filename.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
    }

    /// <summary>
    /// Re-imports one model and rewrites its sidecar oracle from what was actually measured.
    /// An existing oracle keeps exactly the keys it already declares; a new one gets
    /// <paramref name="defaultKeys"/>. MX_IGES_REGEN_FIELDS=ALL or =COUNTS overrides both.
    /// MX_IGES_REGEN_DRYRUN=1 reports without writing.
    /// </summary>
    internal static StepImportRunner.RegenOutcome RegenerateOracle(string filepath, string[] defaultKeys, out string failure)
    {
      failure = null;

      string filename = Path.GetFileName(filepath);
      if (!RegenSelected(filename)) return StepImportRunner.RegenOutcome.Skipped;

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_IGES_REGEN_DRYRUN") == "1";
      string oraclePath = IgesOracle.PathFor(filepath);

      StepOracleFile old = File.Exists(oraclePath) ? IgesOracle.Read(oraclePath) : null;
      string[] keys = ChooseKeys(old, defaultKeys);

      RhinoDoc doc = IgesImporter.CreateDoc();
      try
      {
        bool rc = IgesImporter.Read(filepath, doc, out double seconds);
        if (!rc || doc.Objects.Count == 0)
        {
          failure = $"[regen] '{filename}': import failed (returned {rc}, {doc.Objects.Count} objects); cannot regenerate.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] =====\n{failure}\n\n");
          return StepImportRunner.RegenOutcome.Failed;
        }

        StepMetrics measured = StepImporter.Measure(doc, keys);
        measured.ReadSeconds = seconds;

        string newText = StepOracle.Write(IgesOracle.Incipit, old, keys.Select(k => StepOracle.Format(k, measured)));

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

      string logPath = System.Environment.GetEnvironmentVariable("MX_IGES_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_iges_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_IGES_REGEN_FIELDS")?.Trim().ToUpperInvariant())
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
  /// Folder-scanning base for IGES import fixtures, honouring the same file name conventions as
  /// every other suite here: a name beginning with '#' is skipped, one beginning with '!' is
  /// expected to fail.
  /// </summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyIgesFixture<T> where T : AnyIgesFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".igs", ".iges" };

    static AnyIgesFixture()
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
      // An [Explicit] fixture is allowed to find nothing - the -future folder is empty whenever
      // there is no outstanding bug, and the -large models are deliberately not committed. A
      // fixture that runs by default is not: an empty corpus there means the ModelDirectory
      // entries are wrong, which is worth failing on.
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
          delegate { IgesImportRunner.Run(full, false); },
          "Expected failure, but test succeeded.");
      else
        IgesImportRunner.Run(full, writeDebugModel);
    }

    /// <summary>Shared body of the fixtures' [Explicit] Regenerate tests.</summary>
    internal static void ExecuteRegenerate(string[] defaultKeys)
    {
      int n = 0;
      List<string> failures = new List<string>();

      foreach (string path in g_test_models)
      {
        StepImportRunner.RegenOutcome outcome =
          IgesImportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be imported:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_IGES_REGEN='{System.Environment.GetEnvironmentVariable("MX_IGES_REGEN")}'.");
    }
  }
}
