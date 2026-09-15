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
  /// The write options one IGES export test runs with, plus the parsing and formatting that puts
  /// them in the sidecar.
  /// </summary>
  /// <remarks>
  /// <see cref="FileIgsWriteOptions"/> has over forty knobs; the sidecar pins the handful that
  /// change what geometry ends up in the file. Everything pinned here is written into every
  /// baseline, so a moved Rhino default shows up as a diff. The rest keep RhinoCommon's defaults.
  /// Two options are deliberately not sidecar keys but follow the source document instead:
  /// <c>Units</c> and <c>Tolerance</c> are set from the document being exported, because writing
  /// in document units is part of what is under test - the same reason a .3dm source keeps its
  /// authored units.
  /// </remarks>
  internal sealed class IgesExportOptions
  {
    // Properties over ints for the reason given on StepMetrics.Bbox: a field of a RhinoCommon
    // value type would force RhinoCommon to load while NUnit is scanning the assembly.
    internal FileIgsWriteOptions.IgeswVersionMode IgesVersion
    {
      get { return (FileIgsWriteOptions.IgeswVersionMode)m_version; }
      set { m_version = (int)value; }
    }
    int m_version = (int)FileIgsWriteOptions.IgeswVersionMode.Igv52;

    internal FileIgsWriteOptions.SurfacesMode SurfaceType
    {
      get { return (FileIgsWriteOptions.SurfacesMode)m_surface; }
      set { m_surface = (int)value; }
    }
    int m_surface = (int)FileIgsWriteOptions.SurfacesMode.Srf143;

    internal FileIgsWriteOptions.SolidsMode SolidType
    {
      get { return (FileIgsWriteOptions.SolidsMode)m_solid; }
      set { m_solid = (int)value; }
    }
    int m_solid = (int)FileIgsWriteOptions.SolidsMode.SldSeparate;

    internal FileIgsWriteOptions.MeshesMode MeshType
    {
      get { return (FileIgsWriteOptions.MeshesMode)m_mesh; }
      set { m_mesh = (int)value; }
    }
    int m_mesh = (int)FileIgsWriteOptions.MeshesMode.MeshNone;

    internal bool SplitClosedSurfaces;
    internal bool SimplifyCurves;

    internal FileIgsWriteOptions ToRhino(RhinoDoc doc)
    {
      return new FileIgsWriteOptions
      {
        IgesVersion = IgesVersion,
        SurfaceType = SurfaceType,
        SolidType = SolidType,
        MeshType = MeshType,
        SplitClosedSurfaces = SplitClosedSurfaces,
        SimplifyCurves = SimplifyCurves,
        Units = doc.ModelUnitSystem,
        Tolerance = doc.ModelAbsoluteTolerance,
      };
    }

    /// <summary>Applies whichever option lines the sidecar carries; the rest keep the defaults above.</summary>
    internal static IgesExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new IgesExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        switch (entry.Key)
        {
          case "igesversion": rc.IgesVersion = ParseVersion(entry.Value, where); break;
          case "surfacetype": rc.SurfaceType = ParseEnum<FileIgsWriteOptions.SurfacesMode>(s_surfaces, entry.Value, where, "143, 144, 128"); break;
          case "solidtype": rc.SolidType = ParseEnum<FileIgsWriteOptions.SolidsMode>(s_solids, entry.Value, where, "separate, 184, 186, 402"); break;
          case "meshtype": rc.MeshType = ParseEnum<FileIgsWriteOptions.MeshesMode>(s_meshes, entry.Value, where, "none, 10612, 10613"); break;
          case "splitclosedsurfaces": rc.SplitClosedSurfaces = ParseBool(entry.Value, where, entry.Key); break;
          case "simplifycurves": rc.SimplifyCurves = ParseBool(entry.Value, where, entry.Key); break;
        }
      }

      return rc;
    }

    static readonly Dictionary<string, FileIgsWriteOptions.SurfacesMode> s_surfaces =
      new Dictionary<string, FileIgsWriteOptions.SurfacesMode>(StringComparer.InvariantCultureIgnoreCase)
      {
        { "143", FileIgsWriteOptions.SurfacesMode.Srf143 },
        { "Srf143", FileIgsWriteOptions.SurfacesMode.Srf143 },
        { "144", FileIgsWriteOptions.SurfacesMode.Srf144 },
        { "Srf144", FileIgsWriteOptions.SurfacesMode.Srf144 },
        { "128", FileIgsWriteOptions.SurfacesMode.Srf128 },
        { "Srf128", FileIgsWriteOptions.SurfacesMode.Srf128 },
      };

    static readonly Dictionary<string, FileIgsWriteOptions.SolidsMode> s_solids =
      new Dictionary<string, FileIgsWriteOptions.SolidsMode>(StringComparer.InvariantCultureIgnoreCase)
      {
        { "separate", FileIgsWriteOptions.SolidsMode.SldSeparate },
        { "SldSeparate", FileIgsWriteOptions.SolidsMode.SldSeparate },
        { "184", FileIgsWriteOptions.SolidsMode.Sld184 },
        { "186", FileIgsWriteOptions.SolidsMode.SldManifoldBRep },
        { "manifoldbrep", FileIgsWriteOptions.SolidsMode.SldManifoldBRep },
        { "402", FileIgsWriteOptions.SolidsMode.SldUnorderedGroup },
      };

    static readonly Dictionary<string, FileIgsWriteOptions.MeshesMode> s_meshes =
      new Dictionary<string, FileIgsWriteOptions.MeshesMode>(StringComparer.InvariantCultureIgnoreCase)
      {
        { "none", FileIgsWriteOptions.MeshesMode.MeshNone },
        { "MeshNone", FileIgsWriteOptions.MeshesMode.MeshNone },
        { "10612", FileIgsWriteOptions.MeshesMode.Mesh10612 },
        { "10613", FileIgsWriteOptions.MeshesMode.Mesh10613 },
      };

    static FileIgsWriteOptions.IgeswVersionMode ParseVersion(string value, string where)
    {
      string v = value.Trim();
      if (v == "5.2" || v.Equals("Igv52", StringComparison.InvariantCultureIgnoreCase))
        return FileIgsWriteOptions.IgeswVersionMode.Igv52;
      if (v == "5.3" || v.Equals("Igv53", StringComparison.InvariantCultureIgnoreCase))
        return FileIgsWriteOptions.IgeswVersionMode.Igv53;

      throw new NotSupportedException($"{where}: '{value}' is not an IGES version. Known: 5.2, 5.3.");
    }

    static TEnum ParseEnum<TEnum>(Dictionary<string, TEnum> known, string value, string where, string help)
    {
      if (known.TryGetValue(value.Trim(), out TEnum rc)) return rc;
      throw new NotSupportedException($"{where}: '{value}' is not recognised. Known: {help}.");
    }

    static bool ParseBool(string value, string where, string key)
    {
      string v = value.Trim();
      if (v.Equals("true", StringComparison.InvariantCultureIgnoreCase) || v == "1") return true;
      if (v.Equals("false", StringComparison.InvariantCultureIgnoreCase) || v == "0") return false;

      throw new NotSupportedException($"{where}: '{key}' wants true or false, got '{value}'.");
    }

    /// <summary>The option lines a regenerated sidecar uses, in a fixed order.</summary>
    internal string Format(string key)
    {
      switch (key)
      {
        case "igesversion":
          return "igesversion " + (IgesVersion == FileIgsWriteOptions.IgeswVersionMode.Igv53 ? "5.3" : "5.2");
        case "surfacetype": return "surfacetype " + ((int)SurfaceType).ToString(CultureInfo.InvariantCulture);
        case "solidtype":
          return "solidtype " + (SolidType == FileIgsWriteOptions.SolidsMode.SldSeparate
            ? "separate" : ((int)SolidType).ToString(CultureInfo.InvariantCulture));
        case "meshtype":
          return "meshtype " + (MeshType == FileIgsWriteOptions.MeshesMode.MeshNone
            ? "none" : ((int)MeshType).ToString(CultureInfo.InvariantCulture));
        case "splitclosedsurfaces": return "splitclosedsurfaces " + (SplitClosedSurfaces ? "true" : "false");
        case "simplifycurves": return "simplifycurves " + (SimplifyCurves ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not an IGES export option.");
      }
    }
  }

  /// <summary>
  /// The vocabulary of the IGES export sidecar: the write options, then the same eighteen
  /// measurement keys on both sides of the round trip, 'src'-prefixed for the source.
  /// </summary>
  internal static class IgesExportOracle
  {
    internal const string Incipit = "IGES EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[]
    {
      "igesversion", "surfacetype", "solidtype", "meshtype", "splitclosedsurfaces", "simplifycurves"
    };

    internal static readonly string[] SourceKeys =
      StepOracle.AllKeys.Select(k => SourcePrefix + k).ToArray();

    internal static readonly string[] AllKeys =
      OptionKeys.Concat(SourceKeys).Concat(StepOracle.AllKeys).ToArray();

    internal static readonly string[] DefaultKeys = AllKeys;

    internal static readonly string[] CountKeys =
      DefaultKeys.Where(k => k != "area" && k != "volume" && k != "srcarea" && k != "srcvolume").ToArray();

    internal static bool IsOption(string key) => OptionKeys.Contains(key);

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
  /// The structural checks a written IGES file gets with no baseline at all - the counterpart of
  /// the STEP suite's Part 21 checks, derived from the fixed-format layout every ASCII IGES file
  /// must have: 80-column records, a section letter in column 73 running S, G, D, P, T in order,
  /// per-section sequence numbers counting up from 1, directory entries in pairs, and a Terminate
  /// record whose four counts match the sections actually written. A truncated or interleaved
  /// write fails here before any geometry is compared.
  /// </summary>
  internal static class IgesStructure
  {
    static readonly string SectionOrder = "SGDPT";

    internal static void Check(string where, string filepath)
    {
      string[] lines = File.ReadAllLines(filepath);
      Assert.IsNotEmpty(lines, $"{where}: the exported file is empty.");

      var counts = new Dictionary<char, int> { { 'S', 0 }, { 'G', 0 }, { 'D', 0 }, { 'P', 0 }, { 'T', 0 } };
      int sectionRank = 0;
      string terminate = null;

      for (int i = 0; i < lines.Length; i++)
      {
        string line = lines[i];
        string at = $"{where}: record {i + 1}";

        Assert.AreEqual(80, line.Length, $"{at} is {line.Length} characters long; every ASCII IGES record is exactly 80.");

        char section = line[72];
        int rank = SectionOrder.IndexOf(section);
        Assert.IsTrue(rank >= 0, $"{at} has '{section}' in column 73; expected one of S, G, D, P, T.");
        Assert.IsTrue(rank >= sectionRank,
          $"{at} is a '{section}' record after a '{SectionOrder[sectionRank]}' record; sections must run S, G, D, P, T.");
        sectionRank = rank;

        string seqText = line.Substring(73).Trim();
        Assert.IsTrue(int.TryParse(seqText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seq),
          $"{at}: columns 74-80 hold '{seqText}', not a sequence number.");
        counts[section]++;
        Assert.AreEqual(counts[section], seq,
          $"{at}: the '{section}' section's sequence numbers must count up from 1 without gaps.");

        if (section == 'T') terminate = line;
      }

      Assert.Greater(counts['G'], 0, $"{where}: the file has no Global section.");
      Assert.Greater(counts['D'], 0, $"{where}: the file has no Directory Entry section.");
      Assert.Greater(counts['P'], 0, $"{where}: the file has no Parameter Data section.");
      Assert.AreEqual(1, counts['T'], $"{where}: expected exactly one Terminate record, found {counts['T']}. It looks truncated.");
      Assert.AreEqual(0, counts['D'] % 2, $"{where}: {counts['D']} Directory Entry records; they come in pairs.");

      // The Terminate record repeats the section totals as S G D P, seven columns each. A file cut
      // short between sections still ends plausibly, so this cross-check is what catches it.
      string[] names = new string[] { "S", "G", "D", "P" };
      for (int i = 0; i < 4; i++)
      {
        string field = terminate.Substring(i * 8, 8).Trim();
        Assert.AreEqual(names[i], field.Substring(0, 1),
          $"{where}: Terminate record field {i + 1} is '{field}', expected it to start with '{names[i]}'.");
        Assert.IsTrue(int.TryParse(field.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int declared),
          $"{where}: Terminate record field '{field}' does not end in a count.");
        Assert.AreEqual(counts[names[i][0]], declared,
          $"{where}: the Terminate record declares {declared} '{names[i]}' records but the file holds {counts[names[i][0]]}.");
      }
    }
  }

  /// <summary>
  /// Drives one IGES export test: open the source, write IGES, check the structure, read it back,
  /// compare both ends against the sidecar - plus the opt-in baseline regeneration. The
  /// counterpart of <see cref="StepExportRunner"/> on its own MX_IGESEXPORT_* variables.
  /// </summary>
  internal static class IgesExportRunner
  {
    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, IgesExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_IGESEXPORT_REQUIRE_BASELINE=1. " +
          $"Expected '{Path.GetFileName(oraclePath)}' beside it. Create it by running the fixture's " +
          $"explicit Regenerate test with MX_IGESEXPORT_REGEN=\"{filename}\" (or =* for the whole folder).");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, IgesExportOracle.Incipit, IgesExportOracle.AllKeys)
        : null;

      // Without a sidecar the model still gets the mechanical checks - it wrote, the file is
      // structurally an IGES file, it read back - which is worth having the moment a model is
      // dropped in the folder. Nothing about the geometry is asserted until someone generates a
      // baseline.
      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : IgesExportOracle.CountKeys;

      var where = $"{filename} [export]";
      IgesExportOptions options = IgesExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".igs");

      bool keep = System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, IgesExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          bool wrote = FileIgs.Write(outputPath, source, options.ToRhino(source));
          watch.Stop();

          Assert.IsTrue(wrote, $"{where}: FileIgs.Write() returned false.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: FileIgs.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;
        Assert.Greater(result.Bytes, 0L, $"{where}: the exported file is empty.");

        IgesStructure.Check(where, outputPath);

        RhinoDoc readBack = IgesImporter.CreateDoc();
        try
        {
          Assert.IsTrue(IgesImporter.Read(outputPath, readBack, out double readSeconds),
            $"{where}: the exported file did not import again. RhinoDoc.Import() returned false on '{outputPath}'.");
          result.ReadBackSeconds = readSeconds;

          Assert.IsTrue(readBack.Objects.Count > 0,
            $"{where}: the exported file read back without error but produced no objects.");

          result.Result = StepImporter.Measure(readBack, IgesExportOracle.ResultWanted(keys));
          result.Result.ReadSeconds = readSeconds;

          Emit(filename, options, result, hasOracle);

          if (oracle != null)
          {
            StepOracle.Check(filename + " [source]", IgesExportOracle.SourceEntries(oracle), result.Source, IgesOracle.EnvPrefix);
            StepOracle.Check(filename + " [round trip]", IgesExportOracle.ResultEntries(oracle), result.Result, IgesOracle.EnvPrefix);
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
    /// Opens the model a round trip starts from. A <c>.3dm</c> keeps the units it was authored in;
    /// an IGES source is imported into the same pinned millimetre document the import suite uses,
    /// so both suites measure the same numbers for the same file.
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

      RhinoDoc doc = IgesImporter.CreateDoc();
      try
      {
        if (!IgesImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_iges_export", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(rc);
      return rc;
    }

    static void KeepOutput(string outputPath, string modelPath)
    {
      if (!File.Exists(outputPath)) return;

      string kept = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.igs");

      try
      {
        File.Copy(outputPath, kept, true);
        TestContext.Progress.WriteLine($"[MXIGESEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXIGESEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXIGESEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, IgesExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result;

      string line =
        $"[MXIGESEX]\t{filename}\t{options.Format("igesversion").Replace(" ", "=")}" +
        $"\tobjects={s.Objects}->{r.Objects}\tbreps={s.Breps}->{r.Breps}" +
        $"\tsurfaces={s.Surfaces}->{r.Surfaces}\tsolids={s.Solids}->{r.Solids}" +
        $"\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXIGESEX]\t{filename}\tno baseline: IGES structure invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXIGESEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_iges_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, IgesExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, IgesExportOracle.Incipit, IgesExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      IgesExportOptions options = IgesExportOptions.From(old, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".igs");

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

          result.Source = StepImporter.Measure(source, IgesExportOracle.SourceWanted(keys));

          if (!FileIgs.Write(outputPath, source, options.ToRhino(source)))
          {
            failure = $"[regen] '{filename}': FileIgs.Write() returned false; cannot regenerate.";
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
          failure = $"[regen] '{filename}': FileIgs.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        RhinoDoc readBack = IgesImporter.CreateDoc();
        try
        {
          if (!IgesImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
          {
            failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
            AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
            return StepImportRunner.RegenOutcome.Failed;
          }

          result.ReadBackSeconds = readSeconds;
          result.Result = StepImporter.Measure(readBack, IgesExportOracle.ResultWanted(keys));
        }
        finally
        {
          readBack.Dispose();
        }

        string newText = StepOracle.Write(IgesExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    static string Format(string key, IgesExportOptions options, StepExportResult result)
    {
      if (IgesExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(IgesExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return IgesExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(IgesExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_iges_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return IgesExportOracle.AllKeys;
        case "COUNTS": return IgesExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          // Preserve the old key order, minus any duplicates.
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>
  /// Folder-scanning base for IGES export fixtures. A source model may be a <c>.3dm</c> as well as
  /// an IGES file, because the geometry an exporter has to cope with is Rhino's, not an IGES
  /// reader's. Same file name conventions as everywhere else.
  /// </summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyIgesExportFixture<T> where T : AnyIgesExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".igs", ".iges" };

    static AnyIgesExportFixture()
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
        try { IgesExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        IgesExportRunner.Run(full, defaultKeys, writeDebugModel);
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
          IgesExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_IGESEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_IGESEXPORT_REGEN")}'.");
    }
  }
}
