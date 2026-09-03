using NUnit.Framework;
using Rhino;
using Rhino.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FileIO
{
  /// <summary>
  /// The write options one export test runs with, plus the parsing and formatting that puts them
  /// in the sidecar.
  /// </summary>
  /// <remarks>
  /// <see cref="FileStpWriteOptions"/> defaults to AP203 with <c>ExportBlack</c> on. Nothing here
  /// inherits those: every option is pinned, written into every baseline, and therefore visible in
  /// the diff if a default ever moves. The suite's own default schema is AP214, because that is
  /// what the model corpus is and what most callers ask Rhino for.
  /// </remarks>
  internal sealed class StepExportOptions
  {
    internal FileStpWriteOptions.StepSchema Schema = FileStpWriteOptions.StepSchema.SF_214;
    internal bool Export2dCurves;
    internal bool ExportBlack = true;
    internal bool SplitClosedSurfaces;

    internal FileStpWriteOptions ToRhino()
    {
      return new FileStpWriteOptions
      {
        Schema = Schema,
        Export2dCurves = Export2dCurves,
        ExportBlack = ExportBlack,
        SplitClosedSurfaces = SplitClosedSurfaces,
      };
    }

    /// <summary>Applies whichever option lines the sidecar carries; the rest keep the defaults above.</summary>
    internal static StepExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new StepExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        switch (entry.Key)
        {
          case "schema": rc.Schema = ParseSchema(entry.Value, where); break;
          case "export2dcurves": rc.Export2dCurves = ParseBool(entry.Value, where, entry.Key); break;
          case "exportblack": rc.ExportBlack = ParseBool(entry.Value, where, entry.Key); break;
          case "splitclosedsurfaces": rc.SplitClosedSurfaces = ParseBool(entry.Value, where, entry.Key); break;
        }
      }

      return rc;
    }

    /// <summary>Every spelling of a schema the sidecar accepts, mapped to the enum.</summary>
    static readonly Dictionary<string, FileStpWriteOptions.StepSchema> s_schemas =
      new Dictionary<string, FileStpWriteOptions.StepSchema>(StringComparer.InvariantCultureIgnoreCase)
      {
        { "AP203", FileStpWriteOptions.StepSchema.SF_203 },
        { "SF_203", FileStpWriteOptions.StepSchema.SF_203 },
        { "AP214", FileStpWriteOptions.StepSchema.SF_214 },
        { "SF_214", FileStpWriteOptions.StepSchema.SF_214 },
        { "AP214CC2", FileStpWriteOptions.StepSchema.SF_214_CC2 },
        { "AP214_CC2", FileStpWriteOptions.StepSchema.SF_214_CC2 },
        { "SF_214_CC2", FileStpWriteOptions.StepSchema.SF_214_CC2 },
        { "AP242", FileStpWriteOptions.StepSchema.SF_242 },
        { "SF_242", FileStpWriteOptions.StepSchema.SF_242 },
      };

    static FileStpWriteOptions.StepSchema ParseSchema(string value, string where)
    {
      if (s_schemas.TryGetValue(value.Trim(), out FileStpWriteOptions.StepSchema rc)) return rc;

      throw new NotSupportedException(
        $"{where}: '{value}' is not a STEP schema. Known: AP203, AP214, AP214_CC2, AP242.");
    }

    /// <summary>The spelling a regenerated sidecar uses, which is the friendly one.</summary>
    internal static string NameOf(FileStpWriteOptions.StepSchema schema)
    {
      switch (schema)
      {
        case FileStpWriteOptions.StepSchema.SF_203: return "AP203";
        case FileStpWriteOptions.StepSchema.SF_214: return "AP214";
        case FileStpWriteOptions.StepSchema.SF_214_CC2: return "AP214_CC2";
        case FileStpWriteOptions.StepSchema.SF_242: return "AP242";
        default: return schema.ToString();
      }
    }

    static bool ParseBool(string value, string where, string key)
    {
      string v = value.Trim();
      if (v.Equals("true", StringComparison.InvariantCultureIgnoreCase) || v == "1") return true;
      if (v.Equals("false", StringComparison.InvariantCultureIgnoreCase) || v == "0") return false;

      throw new NotSupportedException($"{where}: '{key}' wants true or false, got '{value}'.");
    }

    /// <summary>The option lines a regenerated sidecar opens with, in a fixed order.</summary>
    internal string Format(string key)
    {
      switch (key)
      {
        case "schema": return "schema " + NameOf(Schema);
        case "export2dcurves": return "export2dcurves " + (Export2dCurves ? "true" : "false");
        case "exportblack": return "exportblack " + (ExportBlack ? "true" : "false");
        case "splitclosedsurfaces": return "splitclosedsurfaces " + (SplitClosedSurfaces ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not an export option.");
      }
    }
  }

  /// <summary>
  /// The vocabulary of the export sidecar, and the split of one parsed file into its three parts.
  /// </summary>
  /// <remarks>
  /// The file reuses <see cref="StepOracle"/>'s parser, format and comparison wholesale; all this
  /// adds is a second namespace of keys. A key with the <c>src</c> prefix - <c>srcbreps</c>,
  /// <c>srcvolume</c> - is asserted against the model as it went in, the same key without the
  /// prefix against the model as it came back out of the round trip. Reading the two columns
  /// beside each other is the point: the difference between them *is* what the export did.
  /// </remarks>
  internal static class StepExportOracle
  {
    internal const string Incipit = "STEP EXPORT";
    internal const string Suffix = ".exported.txt";

    /// <summary>Prefix marking a key as a measurement of the source rather than of the round trip.</summary>
    internal const string SourcePrefix = "src";

    /// <summary>The write options, in the order a regenerated file writes them.</summary>
    internal static readonly string[] OptionKeys = new string[]
    {
      "schema", "export2dcurves", "exportblack", "splitclosedsurfaces"
    };

    /// <summary>
    /// The FILE_SCHEMA string the written file must carry, compared by containment so that a
    /// baseline may pin just "AUTOMOTIVE_DESIGN" instead of the whole bracketed version stamp.
    /// </summary>
    internal const string FileSchemaKey = "fileschema";

    internal static readonly string[] SourceKeys =
      StepOracle.AllKeys.Select(k => SourcePrefix + k).ToArray();

    internal static readonly string[] AllKeys =
      OptionKeys
        .Concat(new string[] { FileSchemaKey })
        .Concat(SourceKeys)
        .Concat(StepOracle.AllKeys)
        .ToArray();

    /// <summary>
    /// What a new baseline records: the write options, then the same eighteen keys on both sides.
    /// The symmetry is the point - the two columns line up so that the difference between them can
    /// be read straight off the file. It is also the strictest possible setting; a model that only
    /// wants its counts guarded can have the mass properties deleted by hand, and regeneration will
    /// then keep it that way.
    /// </summary>
    internal static readonly string[] DefaultKeys = AllKeys;

    /// <summary>The same, minus everything that costs a mass property computation.</summary>
    internal static readonly string[] CountKeys =
      DefaultKeys.Where(k => k != "area" && k != "volume" && k != "srcarea" && k != "srcvolume").ToArray();

    /// <summary>The option lines, which are inputs rather than measurements.</summary>
    internal static bool IsOption(string key) =>
      OptionKeys.Contains(key) || key == FileSchemaKey;

    /// <summary>Entries measuring the source, with the <c>src</c> prefix stripped.</summary>
    internal static IEnumerable<KeyValuePair<string, string>> SourceEntries(StepOracleFile oracle)
    {
      return oracle.Entries
                   .Where(e => !IsOption(e.Key) && e.Key.StartsWith(SourcePrefix, StringComparison.InvariantCulture))
                   .Select(e => new KeyValuePair<string, string>(e.Key.Substring(SourcePrefix.Length), e.Value));
    }

    /// <summary>Entries measuring what came back from the round trip.</summary>
    internal static IEnumerable<KeyValuePair<string, string>> ResultEntries(StepOracleFile oracle)
    {
      return oracle.Entries
                   .Where(e => !IsOption(e.Key) && !e.Key.StartsWith(SourcePrefix, StringComparison.InvariantCulture));
    }

    /// <summary>The measurement keys a given key set asks of the source.</summary>
    internal static string[] SourceWanted(IEnumerable<string> keys)
    {
      return keys.Where(k => !IsOption(k) && k.StartsWith(SourcePrefix, StringComparison.InvariantCulture))
                 .Select(k => k.Substring(SourcePrefix.Length))
                 .ToArray();
    }

    /// <summary>The measurement keys a given key set asks of the round trip.</summary>
    internal static string[] ResultWanted(IEnumerable<string> keys)
    {
      return keys.Where(k => !IsOption(k) && !k.StartsWith(SourcePrefix, StringComparison.InvariantCulture))
                 .ToArray();
    }
  }

  /// <summary>What one round trip produced, beyond the two measurements.</summary>
  internal sealed class StepExportResult
  {
    internal StepMetrics Source;
    internal StepMetrics Result;

    /// <summary>The contents of the written file's FILE_SCHEMA, whitespace collapsed.</summary>
    internal string FileSchema = string.Empty;

    internal long Bytes;
    internal double WriteSeconds;
    internal double ReadBackSeconds;

    /// <summary>Where the written file ended up, for the failure message.</summary>
    internal string OutputPath = string.Empty;
  }

  /// <summary>
  /// Loads a source model, writes it to STEP and reads it back, all headless.
  /// </summary>
  internal static class StepExporter
  {
    /// <summary>Header lines scanned for FILE_SCHEMA before giving up; real headers are a dozen.</summary>
    const int MaxHeaderLines = 400;

    /// <summary>
    /// Opens the model a round trip starts from. A <c>.3dm</c> keeps the units it was authored in -
    /// the exporter writes in document units, so that is part of what is under test - while a STEP
    /// source is imported into the same millimetre document <see cref="StepImporter"/> pins, so
    /// that both suites measure the same numbers for the same file.
    /// </summary>
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

      RhinoDoc doc = StepImporter.CreateDoc();
      try
      {
        if (!StepImporter.Read(filepath, doc, out seconds))
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

    internal static bool Write(string filepath, RhinoDoc doc, StepExportOptions options, out double seconds)
    {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      bool rc = FileStp.Write(filepath, doc, options.ToRhino());
      watch.Stop();

      seconds = watch.Elapsed.TotalSeconds;
      return rc;
    }

    /// <summary>
    /// The text inside the written file's <c>FILE_SCHEMA(( ... ))</c>, whitespace collapsed and
    /// quotes removed, or an empty string when the header has none.
    /// </summary>
    /// <remarks>
    /// Part 21 allows the header to wrap anywhere, so this joins the lines before matching rather
    /// than looking for FILE_SCHEMA at the start of one.
    /// </remarks>
    internal static string ReadFileSchema(string filepath)
    {
      var header = new StringBuilder();

      using (var reader = new StreamReader(filepath))
      {
        for (int i = 0; i < MaxHeaderLines; i++)
        {
          string line = reader.ReadLine();
          if (line == null) break;

          header.Append(line).Append(' ');
          if (line.IndexOf("DATA;", StringComparison.InvariantCultureIgnoreCase) >= 0) break;
        }
      }

      string text = header.ToString();
      int start = text.IndexOf("FILE_SCHEMA", StringComparison.InvariantCultureIgnoreCase);
      if (start < 0) return string.Empty;

      int open = text.IndexOf('(', start);
      if (open < 0) return string.Empty;

      int close = text.IndexOf(')', open);
      if (close < 0) return string.Empty;

      // Closing the inner list is enough: the schema names live in a single parenthesised list.
      string inner = text.Substring(open + 1, close - open - 1).Replace("(", " ").Replace("'", " ");
      return string.Join(" ", inner.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The first line of the file, for the "is this even a Part 21 file" assertion.</summary>
    internal static string FirstLine(string filepath)
    {
      using (var reader = new StreamReader(filepath))
        return (reader.ReadLine() ?? string.Empty).Trim();
    }

    /// <summary>
    /// The last non-empty line, which a complete Part 21 file ends with. A truncated write - the
    /// exporter throwing half way, or running out of disk - still leaves a file that opens and
    /// still reports a plausible header, so the tail is what catches it.
    /// </summary>
    internal static string LastLine(string filepath)
    {
      string last = string.Empty;

      using (var reader = new StreamReader(filepath))
      {
        string line;
        while ((line = reader.ReadLine()) != null)
        {
          line = line.Trim();
          if (line.Length > 0) last = line;
        }
      }

      return last;
    }
  }

  /// <summary>
  /// Drives one export test: open the source, write STEP, read it back, compare both ends against
  /// the sidecar - plus the opt-in baseline regeneration that writes the sidecar in the first place.
  /// </summary>
  internal static class StepExportRunner
  {
    /// <summary>
    /// Runs one model through the round trip and asserts it.
    /// </summary>
    /// <param name="filepath">Full path of the source model, a <c>.3dm</c> or a STEP file.</param>
    /// <param name="defaultKeys">The key set to measure when the model has no sidecar yet.</param>
    /// <param name="writeDebugModel">
    /// On failure, keep the written STEP file next to the model as "#name.exported.stp" and save
    /// what it read back as "#name.exported.3dm". Off for the large assemblies, where both writes
    /// cost minutes.
    /// </param>
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, StepExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_STEPEXPORT_REQUIRE_BASELINE=1. " +
          $"Expected '{Path.GetFileName(oraclePath)}' beside it. Create it by running the fixture's " +
          $"explicit Regenerate test with MX_STEPEXPORT_REGEN=\"{filename}\" (or =* for the whole folder).");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, StepExportOracle.Incipit, StepExportOracle.AllKeys)
        : null;

      // Without a sidecar the model still gets the mechanical checks - it wrote, the file is a
      // Part 21 file, it read back - which is worth having the moment a model is dropped in the
      // folder. Nothing about the geometry is asserted until someone generates a baseline.
      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : StepExportOracle.CountKeys;

      var where = $"{filename} [export]";
      StepExportOptions options = StepExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".stp");

      var result = new StepExportResult { OutputPath = outputPath };
      bool keep = System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_KEEP") == "1";
      bool failed = true;

      try
      {
        RhinoDoc source = StepExporter.OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, StepExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          Assert.IsTrue(StepExporter.Write(outputPath, source, options, out double writeSeconds),
            $"{where}: FileStp.Write() returned false with schema {StepExportOptions.NameOf(options.Schema)}.");
          result.WriteSeconds = writeSeconds;
        }
        finally
        {
          source.Dispose();
        }

        CheckPart21(where, outputPath, result);

        if (oracle != null)
        {
          var pinned = oracle.Entries.FirstOrDefault(e => e.Key == StepExportOracle.FileSchemaKey);
          if (pinned.Key != null)
            Assert.IsTrue(
              result.FileSchema.IndexOf(pinned.Value.Trim(), StringComparison.InvariantCultureIgnoreCase) >= 0,
              $"{where}: '{StepExportOracle.FileSchemaKey}' expected to contain '{pinned.Value.Trim()}' " +
              $"but the file declares '{result.FileSchema}'.");
        }

        RhinoDoc readBack = StepImporter.CreateDoc();
        try
        {
          Assert.IsTrue(StepImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. FileStp.Read() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, StepExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", StepExportOracle.SourceEntries(oracle), result.Source);
            StepOracle.Check(filename + " [round trip]", StepExportOracle.ResultEntries(oracle), result.Result);
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

    /// <summary>
    /// The checks that need no baseline: the file exists, is not empty, opens with the Part 21
    /// incipit, declares a schema and is terminated. These are what a model with no sidecar gets.
    /// </summary>
    static void CheckPart21(string where, string outputPath, StepExportResult result)
    {
      Assert.IsTrue(File.Exists(outputPath),
        $"{where}: FileStp.Write() reported success but wrote no file at '{outputPath}'.");

      result.Bytes = new FileInfo(outputPath).Length;
      Assert.Greater(result.Bytes, 0L, $"{where}: the exported file is empty.");

      string first = StepExporter.FirstLine(outputPath);
      Assert.IsTrue(first.StartsWith("ISO-10303-21", StringComparison.InvariantCultureIgnoreCase),
        $"{where}: the exported file starts with '{first}', not the Part 21 'ISO-10303-21;'.");

      string last = StepExporter.LastLine(outputPath);
      Assert.IsTrue(last.StartsWith("END-ISO-10303-21", StringComparison.InvariantCultureIgnoreCase),
        $"{where}: the exported file ends with '{last}', not 'END-ISO-10303-21;'. It looks truncated.");

      result.FileSchema = StepExporter.ReadFileSchema(outputPath);
      Assert.IsNotEmpty(result.FileSchema, $"{where}: the exported file's header declares no FILE_SCHEMA.");
    }

    /// <summary>A private directory per run, so two fixtures never collide over one model name.</summary>
    static string TempDir()
    {
      string rc = Path.Combine(Path.GetTempPath(), "mx_step_export", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(rc);
      return rc;
    }

    /// <summary>
    /// Copies the written file next to the model as "#name.exported.stp". The '#' is the folder
    /// scanner's skip marker, so the copy never becomes a test case of its own.
    /// </summary>
    static void KeepOutput(string outputPath, string modelPath)
    {
      if (!File.Exists(outputPath)) return;

      string kept = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.stp");

      // Best effort: a failed comparison is the news, a failed copy is not.
      try
      {
        File.Copy(outputPath, kept, true);
        TestContext.Progress.WriteLine($"[MXSTEPEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSTEPEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXSTEPEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    /// <summary>
    /// One line per model, with the source and round-trip counts side by side: that comparison is
    /// the whole point of the suite and it should be readable without opening the sidecar.
    /// </summary>
    static void Emit(string filename, StepExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXSTEPEX]\t{filename}\tschema={StepExportOptions.NameOf(options.Schema)}" +
        $"\tobjects={s.Objects}->{r.Objects}\tbreps={s.Breps}->{r.Breps}" +
        $"\tsurfaces={s.Surfaces}->{r.Surfaces}\tsolids={s.Solids}->{r.Solids}" +
        $"\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\tfileschema={result.FileSchema}";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXSTEPEX]\t{filename}\tno baseline: Part 21 invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXSTEPEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_step_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====
    // The same shape as StepImportRunner's, on its own environment variables so that regenerating
    // one suite never quietly rewrites the other.

    /// <summary>True if <paramref name="filename"/> matches the comma-separated MX_STEPEXPORT_REGEN list. "*" matches all.</summary>
    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_REGEN");
      if (string.IsNullOrWhiteSpace(selection)) return false;

      return selection.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0)
                      .Any(s => s == "*" || filename.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
    }

    /// <summary>
    /// Re-runs one round trip and rewrites its sidecar from what was actually measured. An existing
    /// sidecar keeps exactly the keys it already declares - including the write options, so a model
    /// pinned to AP242 stays on AP242 - while a new one gets <paramref name="defaultKeys"/>.
    /// MX_STEPEXPORT_REGEN_FIELDS=ALL or =COUNTS overrides both. MX_STEPEXPORT_REGEN_DRYRUN=1
    /// reports without writing.
    /// </summary>
    /// <param name="failure">Set to the reason when <see cref="StepImportRunner.RegenOutcome.Failed"/> is returned.</param>
    internal static StepImportRunner.RegenOutcome RegenerateOracle(string filepath, string[] defaultKeys, out string failure)
    {
      failure = null;

      string filename = Path.GetFileName(filepath);
      if (!RegenSelected(filename)) return StepImportRunner.RegenOutcome.Skipped;

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, StepExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, StepExportOracle.Incipit, StepExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      StepExportOptions options = StepExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".stp");

      try
      {
        var result = new StepExportResult { OutputPath = outputPath };

        RhinoDoc source;
        try { source = StepExporter.OpenSource(filepath, out _); }
        catch (Exception e)
        {
          // Not an assert: the caller walks a whole folder, and one unreadable model must not hide
          // the baselines of every model after it.
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

          result.Source = StepImporter.Measure(source, StepExportOracle.SourceWanted(keys));

          if (!StepExporter.Write(outputPath, source, options, out double writeSeconds))
          {
            failure = $"[regen] '{filename}': FileStp.Write() returned false with schema " +
                      $"{StepExportOptions.NameOf(options.Schema)}; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.WriteSeconds = writeSeconds;
        }
        finally
        {
          source.Dispose();
        }

        if (!File.Exists(outputPath))
        {
          failure = $"[regen] '{filename}': FileStp.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;
        result.FileSchema = StepExporter.ReadFileSchema(outputPath);

        RhinoDoc readBack = StepImporter.CreateDoc();
        try
        {
          if (!StepImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, StepExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(StepExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    /// <summary>One sidecar line, choosing between the options, the source and the round trip by key.</summary>
    static string Format(string key, StepExportOptions options, StepExportResult result)
    {
      if (key == StepExportOracle.FileSchemaKey)
        return StepExportOracle.FileSchemaKey + " " + result.FileSchema;

      if (StepExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(StepExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return StepExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(StepExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_step_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return StepExportOracle.AllKeys;
        case "COUNTS": return StepExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          // Preserve the old key order, minus any duplicates.
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>
  /// Folder-scanning base for STEP export fixtures. The counterpart of <see cref="AnyStepFixture{T}"/>,
  /// differing in one way: a source model may be a <c>.3dm</c> as well as a STEP file, because the
  /// geometry an exporter has to cope with is Rhino's, not a STEP reader's. It honours the same file
  /// name conventions - a name beginning with '#' is skipped, one beginning with '!' is expected to fail.
  /// </summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyStepExportFixture<T> where T : AnyStepExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".stp", ".step", ".p21" };

    static AnyStepExportFixture()
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
    internal static void Execute(string filename, string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string full = Path.Combine(filepath, filename);

      if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
        Assert.Throws<AssertionException>(
          delegate { StepExportRunner.Run(full, defaultKeys, false); },
          "Expected failure, but test succeeded.");
      else
        StepExportRunner.Run(full, defaultKeys, writeDebugModel);
    }

    /// <summary>Shared body of the fixtures' [Explicit] Regenerate tests.</summary>
    internal static void ExecuteRegenerate(string[] defaultKeys)
    {
      int n = 0;
      List<string> failures = new List<string>();

      foreach (string path in g_test_models)
      {
        StepImportRunner.RegenOutcome outcome =
          StepExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_STEPEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_REGEN")}'.");
    }
  }
}
