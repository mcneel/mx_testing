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
  /// The write options one glTF export test runs with. The dictionary reaches the exporter, so
  /// these are real effects.
  ///
  /// <para><b>ExportMaterials defaults to false here, unlike RhinoCommon.</b> A glTF carrying
  /// materials cannot be imported back into a headless document - RH-81973 - because creating the
  /// PBR material returns null unless the Commands plug-in is loaded. Exporting with materials off
  /// is therefore what lets this suite round trip like every other format. A materials-on export is
  /// still covered: the writer's output is checked structurally, and the unreadable result is kept
  /// in models\GLTFfile-future\ as the standing witness for RH-81973.</para>
  ///
  /// <para>Draco compression is pinned but left off in the committed corpus: with it on the mesh
  /// payload becomes an opaque compressed blob, which is a fine thing to test but a poor thing to
  /// pin geometry counts against.</para>
  /// </summary>
  internal sealed class GltfExportOptions
  {
    internal bool MapZToY;
    internal bool ExportMaterials;   // default OFF - see the remarks on the class
    internal bool ExportTextureCoordinates = true;
    internal bool ExportVertexNormals = true;
    internal bool ExportOpenMeshes = true;
    internal bool ExportVertexColors;
    internal bool ExportLayers;
    internal bool UseDracoCompression;

    internal FileGltfWriteOptions ToRhino()
    {
      return new FileGltfWriteOptions
      {
        MapZToY = MapZToY,
        ExportMaterials = ExportMaterials,
        ExportTextureCoordinates = ExportTextureCoordinates,
        ExportVertexNormals = ExportVertexNormals,
        ExportOpenMeshes = ExportOpenMeshes,
        ExportVertexColors = ExportVertexColors,
        ExportLayers = ExportLayers,
        UseDracoCompression = UseDracoCompression,
      };
    }

    internal static GltfExportOptions From(StepOracleFile oracle, string where)
    {
      var rc = new GltfExportOptions();
      if (oracle == null) return rc;

      foreach (var entry in oracle.Entries)
      {
        string v = entry.Value.Trim();
        switch (entry.Key)
        {
          case "mapztoy": rc.MapZToY = B(v, where, entry.Key); break;
          case "exportmaterials": rc.ExportMaterials = B(v, where, entry.Key); break;
          case "exporttexturecoordinates": rc.ExportTextureCoordinates = B(v, where, entry.Key); break;
          case "exportvertexnormals": rc.ExportVertexNormals = B(v, where, entry.Key); break;
          case "exportopenmeshes": rc.ExportOpenMeshes = B(v, where, entry.Key); break;
          case "exportvertexcolors": rc.ExportVertexColors = B(v, where, entry.Key); break;
          case "exportlayers": rc.ExportLayers = B(v, where, entry.Key); break;
          case "usedracocompression": rc.UseDracoCompression = B(v, where, entry.Key); break;
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
        case "mapztoy": return "mapztoy " + (MapZToY ? "true" : "false");
        case "exportmaterials": return "exportmaterials " + (ExportMaterials ? "true" : "false");
        case "exporttexturecoordinates": return "exporttexturecoordinates " + (ExportTextureCoordinates ? "true" : "false");
        case "exportvertexnormals": return "exportvertexnormals " + (ExportVertexNormals ? "true" : "false");
        case "exportopenmeshes": return "exportopenmeshes " + (ExportOpenMeshes ? "true" : "false");
        case "exportvertexcolors": return "exportvertexcolors " + (ExportVertexColors ? "true" : "false");
        case "exportlayers": return "exportlayers " + (ExportLayers ? "true" : "false");
        case "usedracocompression": return "usedracocompression " + (UseDracoCompression ? "true" : "false");
        default: throw new NotSupportedException($"'{key}' is not a glTF export option.");
      }
    }
  }

  /// <summary>The vocabulary of the glTF export sidecar.</summary>
  internal static class GltfExportOracle
  {
    internal const string Incipit = "GLTF EXPORT";
    internal const string Suffix = ".exported.txt";
    internal const string SourcePrefix = "src";

    internal static readonly string[] OptionKeys = new string[]
    {
      "mapztoy", "exportmaterials", "exporttexturecoordinates", "exportvertexnormals",
      "exportopenmeshes", "exportvertexcolors", "exportlayers", "usedracocompression"
    };

    internal static readonly string[] SourceKeys =
      StepOracle.AllKeys.Select(k => SourcePrefix + k).ToArray();

    internal static readonly string[] AllKeys =
      OptionKeys.Concat(SourceKeys).Concat(StepOracle.AllKeys).ToArray();

    internal static readonly string[] DefaultKeys = AllKeys;

    /// <summary>
    /// The keys a regenerated sidecar can actually fill for a materials-on export: there is no
    /// read-back to measure (RH-81973), so the round-trip column is dropped rather than written
    /// as zeroes.
    /// </summary>
    internal static readonly string[] SourceOnlyKeys = OptionKeys.Concat(SourceKeys).ToArray();

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
  /// The structural checks a written glTF gets with no baseline. glTF has two on-disk forms and
  /// the output extension picks which grammar applies. <c>.glb</c> is a binary container whose
  /// header declares its own length and whose chunks must tile the file exactly, so truncation is
  /// caught arithmetically. <c>.gltf</c> is bare JSON, checked for balanced structure outside
  /// string literals, the required asset version, and self-containment - every uri must be a
  /// data: URI, because a .gltf referencing a missing .bin is useless to whoever receives it.
  /// </summary>
  internal static class GltfStructure
  {
    const uint GlbMagic = 0x46546C67;   // 'glTF'
    const uint JsonChunk = 0x4E4F534A;  // 'JSON'

    internal static void Check(string where, string filepath, GltfExportOptions options)
    {
      if (Path.GetExtension(filepath).Equals(".glb", StringComparison.InvariantCultureIgnoreCase))
        CheckGlb(where, filepath, options);
      else
        CheckJsonFile(where, filepath, options);
    }

    static void CheckGlb(string where, string filepath, GltfExportOptions options)
    {
      byte[] b = File.ReadAllBytes(filepath);
      Assert.GreaterOrEqual(b.Length, 20, $"{where}: {b.Length} bytes is shorter than a GLB header.");

      Assert.AreEqual(GlbMagic, BitConverter.ToUInt32(b, 0),
        $"{where}: the file does not start with the glTF magic.");
      Assert.AreEqual(2u, BitConverter.ToUInt32(b, 4), $"{where}: GLB container version is not 2.");
      Assert.AreEqual((uint)b.Length, BitConverter.ToUInt32(b, 8),
        $"{where}: the header declares {BitConverter.ToUInt32(b, 8)} bytes but the file holds {b.Length}. It looks truncated.");

      // The chunks must tile the rest of the file exactly: (uint length, uint type, payload).
      int offset = 12, chunks = 0;
      string json = null;

      while (offset < b.Length)
      {
        Assert.LessOrEqual(offset + 8, b.Length, $"{where}: a chunk header runs off the end of the file.");

        uint len = BitConverter.ToUInt32(b, offset);
        uint type = BitConverter.ToUInt32(b, offset + 4);

        Assert.LessOrEqual((long)offset + 8 + len, (long)b.Length,
          $"{where}: the chunk at {offset} declares {len} bytes, which runs past the end of the file.");
        Assert.AreEqual(0u, len % 4,
          $"{where}: the chunk at {offset} has length {len}, which is not 4-byte aligned.");

        if (chunks == 0)
        {
          Assert.AreEqual(JsonChunk, type, $"{where}: the first chunk is not the JSON chunk.");
          json = System.Text.Encoding.UTF8.GetString(b, offset + 8, (int)len).TrimEnd(' ', '\0');
        }

        offset += 8 + (int)len;
        chunks++;
      }

      Assert.AreEqual(b.Length, offset, $"{where}: the chunks do not tile the file exactly.");
      Assert.GreaterOrEqual(chunks, 1, $"{where}: the GLB carries no chunks.");

      CheckJsonText(where, json, options, requireSelfContained: false);
    }

    static void CheckJsonFile(string where, string filepath, GltfExportOptions options)
    {
      string text = File.ReadAllText(filepath);
      Assert.IsNotEmpty(text, $"{where}: the exported file is empty.");
      Assert.AreEqual('{', text.TrimStart()[0], $"{where}: a .gltf must be a bare JSON object.");
      CheckJsonText(where, text, options, requireSelfContained: true);
    }

    static void CheckJsonText(string where, string json, GltfExportOptions options, bool requireSelfContained)
    {
      Assert.IsFalse(string.IsNullOrWhiteSpace(json), $"{where}: the JSON payload is empty.");

      // Balance braces and brackets, ignoring anything inside string literals.
      int braces = 0, brackets = 0;
      bool inString = false, escaped = false;

      foreach (char c in json)
      {
        if (inString)
        {
          if (escaped) escaped = false;
          else if (c == '\\') escaped = true;
          else if (c == '"') inString = false;
          continue;
        }

        if (c == '"') inString = true;
        else if (c == '{') braces++;
        else if (c == '}') braces--;
        else if (c == '[') brackets++;
        else if (c == ']') brackets--;

        Assert.GreaterOrEqual(braces, 0, $"{where}: a closing brace appears before its opening one.");
        Assert.GreaterOrEqual(brackets, 0, $"{where}: a closing bracket appears before its opening one.");
      }

      Assert.AreEqual(0, braces, $"{where}: braces do not balance; the JSON is truncated.");
      Assert.AreEqual(0, brackets, $"{where}: brackets do not balance; the JSON is truncated.");
      Assert.IsFalse(inString, $"{where}: the JSON ends inside a string literal.");

      Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(json, "\"version\"\\s*:\\s*\"2\\.0\""),
        $"{where}: the asset block does not declare glTF version 2.0.");

      if (requireSelfContained)
      {
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(json, "\"uri\"\\s*:\\s*\"([^\"]+)\""))
        {
          string uri = m.Groups[1].Value;
          Assert.IsTrue(uri.StartsWith("data:", StringComparison.InvariantCultureIgnoreCase),
            $"{where}: uri '{uri}' is not a data: URI, so the .gltf is not self-contained.");
        }
      }

      // The option is pinned, so the file has to agree with it in both directions.
      bool draco = json.IndexOf("KHR_draco_mesh_compression", StringComparison.Ordinal) >= 0;
      Assert.AreEqual(options.UseDracoCompression, draco,
        $"{where}: usedracocompression is {options.UseDracoCompression} but the file " +
        (draco ? "declares" : "does not declare") + " the Draco extension.");
    }
  }

  /// <summary>
  /// Drives one STL export test on the MX_GLTFEXPORT_* variables. The writer meshes every
  /// meshable object into ONE STL body (curves, points and annotations are dropped); the import
  /// side's SplitDisjointMeshes default then splits disjoint pieces back apart, and the baselines
  /// record that round-trip shape.
  /// </summary>
  internal static class GltfExportRunner
  {
    /// <summary>
    /// Whether a file written with these options can be read back, and so whether this run round
    /// trips or stops at the structural checks.
    /// </summary>
    /// <remarks>
    /// Headless import of a glTF that carries materials fails - RH-81973 - so a materials-on
    /// export is checked structurally and no further. With materials off the round trip works
    /// normally, which is how the committed corpus is pinned.
    ///
    /// Isolated 2026-09-15 on 9.0.26258: Rhino's own glTF output imports once its materials array
    /// and the primitives' material references are removed by hand, while the same file untouched
    /// does not. That also rules out the KHR extensions the exporter declares - stripping
    /// extensionsUsed alone changes nothing. The probes are in models\GLTFfile-future\.
    /// </remarks>
    static bool CanRoundTrip(GltfExportOptions options)
    {
      return !options.ExportMaterials;
    }

    internal static void Run(string filepath, string[] defaultKeys, bool writeDebugModel)
    {
      string filename = Path.GetFileName(filepath);
      string oraclePath = StepOracle.PathFor(filepath, GltfExportOracle.Suffix);
      bool hasOracle = File.Exists(oraclePath);

      if (!hasOracle && System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_REQUIRE_BASELINE") == "1")
        Assert.Fail(
          $"'{filename}' has no export baseline and MX_GLTFEXPORT_REQUIRE_BASELINE=1. " +
          $"Create '{Path.GetFileName(oraclePath)}' with MX_GLTFEXPORT_REGEN=\"{filename}\".");

      StepOracleFile oracle = hasOracle
        ? StepOracle.Read(oraclePath, GltfExportOracle.Incipit, GltfExportOracle.AllKeys)
        : null;

      string[] keys = oracle != null
        ? oracle.Entries.Select(e => e.Key).Distinct().ToArray()
        : GltfExportOracle.CountKeys;

      var where = $"{filename} [export]";
      GltfExportOptions options = GltfExportOptions.From(oracle, where);

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".gltf");

      bool keep = System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_KEEP") == "1";
      bool failed = true;
      var result = new StepExportResult { OutputPath = outputPath };

      try
      {
        RhinoDoc source = OpenSource(filepath, out double openSeconds);
        try
        {
          Assert.IsTrue(source.Objects.Count > 0,
            $"{where}: the source opened but has no objects, so the export would have nothing to write.");

          result.Source = StepImporter.Measure(source, GltfExportOracle.SourceWanted(keys));
          result.Source.ReadSeconds = openSeconds;

          var watch = System.Diagnostics.Stopwatch.StartNew();
          bool wrote = FileGltf.Write(outputPath, source, options.ToRhino());
          watch.Stop();

          Assert.IsTrue(wrote, $"{where}: FileGltf.Write() returned false.");
          result.WriteSeconds = watch.Elapsed.TotalSeconds;
        }
        finally
        {
          source.Dispose();
        }

        Assert.IsTrue(File.Exists(outputPath),
          $"{where}: FileGltf.Write() reported success but wrote no file at '{outputPath}'.");
        result.Bytes = new FileInfo(outputPath).Length;

        GltfStructure.Check(where, outputPath, options);

        // With materials on there is nothing to read back - RH-81973 - so the run stops at the
        // source measurement and the structural checks. With materials off it round trips like
        // every other format.
        if (!CanRoundTrip(options))
        {
          Emit(filename, options, result, hasOracle);

          if (oracle != null)
            StepOracle.Check(filename + " [source]", GltfExportOracle.SourceEntries(oracle), result.Source, GltfOracle.EnvPrefix);

          failed = false;
        }
        else
        {
          RhinoDoc readBack = GltfImporter.CreateDoc();
          try
          {
            Assert.IsTrue(GltfImporter.Read(outputPath, readBack, out double readSeconds),
              $"{where}: the exported file did not import again. RhinoDoc.Import() returned false on '{outputPath}'.");
            result.ReadBackSeconds = readSeconds;

            Assert.IsTrue(readBack.Objects.Count > 0,
              $"{where}: the exported file read back without error but produced no objects.");

            result.Result = StepImporter.Measure(readBack, GltfExportOracle.ResultWanted(keys));
            result.Result.ReadSeconds = readSeconds;

            Emit(filename, options, result, hasOracle);

            if (oracle != null)
            {
              StepOracle.Check(filename + " [source]", GltfExportOracle.SourceEntries(oracle), result.Source, GltfOracle.EnvPrefix);
              StepOracle.Check(filename + " [round trip]", GltfExportOracle.ResultEntries(oracle), result.Result, GltfOracle.EnvPrefix);
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

      RhinoDoc doc = GltfImporter.CreateDoc();
      try
      {
        if (!GltfImporter.Read(filepath, doc, out seconds))
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
      string rc = Path.Combine(Path.GetTempPath(), "mx_gltf_export", Guid.NewGuid().ToString("N"));
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
        TestContext.Progress.WriteLine($"[MXGLTFEX] kept the exported file at '{kept}'.");
      }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXGLTFEX] could not keep '{kept}': {e.Message}"); }
    }

    static void SaveDebugModel(RhinoDoc doc, string modelPath)
    {
      string debugPath = Path.Combine(
        Path.GetDirectoryName(modelPath),
        "#" + Path.GetFileNameWithoutExtension(modelPath) + ".exported.3dm");

      try { doc.WriteFile(debugPath, new FileWriteOptions()); }
      catch (Exception e) { TestContext.Progress.WriteLine($"[MXGLTFEX] could not save '{debugPath}': {e.Message}"); }
    }

    static void TryDelete(string dir)
    {
      try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp cleanup is best-effort */ }
    }

    static void Emit(string filename, GltfExportOptions options, StepExportResult result, bool hasOracle)
    {
      StepMetrics s = result.Source;
      StepMetrics r = result.Result ?? new StepMetrics();

      string line =
        $"[MXGLTFEX]\t{filename}\tdraco={(options.UseDracoCompression ? "true" : "false")}" +
        $"\tobjects={s.Objects}->{r.Objects}\tmeshes={s.Meshes}->{r.Meshes}" +
        $"\tsolids={s.Solids}->{r.Solids}\tinvalid={s.Invalid}->{r.Invalid}\tbytes={result.Bytes}" +
        $"\twrite={result.WriteSeconds.ToString("F2", CultureInfo.InvariantCulture)}s" +
        $"\treadback={result.ReadBackSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

      if (!hasOracle)
        line += $"{System.Environment.NewLine}[MXGLTFEX]\t{filename}\tno baseline: glTF structure invariants only, " +
                "nothing about the geometry was asserted.";

      foreach (string report in r.InvalidReports)
        line += $"{System.Environment.NewLine}[MXGLTFEX]\t{filename}\tround trip invalid: {report}";

      try { TestContext.Progress.WriteLine(line); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_gltf_export.txt");
      try { File.AppendAllText(logPath, line + System.Environment.NewLine); } catch { /* log file is best-effort */ }
    }

    // ===== Baseline regeneration =====

    internal static bool RegenSelected(string filename)
    {
      string selection = System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_REGEN");
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

      bool dryRun = System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_REGEN_DRYRUN") == "1";
      string oraclePath = StepOracle.PathFor(filepath, GltfExportOracle.Suffix);

      StepOracleFile old = File.Exists(oraclePath)
        ? StepOracle.Read(oraclePath, GltfExportOracle.Incipit, GltfExportOracle.AllKeys)
        : null;

      string[] keys = ChooseKeys(old, defaultKeys);
      var where = $"{filename} [export regen]";
      GltfExportOptions options = GltfExportOptions.From(old, where);

      // A materials-on export has no readable result, so keep only what can be measured.
      if (!CanRoundTrip(options))
        keys = keys.Where(k => GltfExportOracle.SourceOnlyKeys.Contains(k)).ToArray();

      string outputDir = TempDir();
      string outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filepath) + ".gltf");

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

          result.Source = StepImporter.Measure(source, GltfExportOracle.SourceWanted(keys));

          if (!FileGltf.Write(outputPath, source, options.ToRhino()))
          {
            failure = $"[regen] '{filename}': FileGltf.Write() returned false; cannot regenerate.";
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
          failure = $"[regen] '{filename}': FileGltf.Write() reported success but wrote no file.";
          AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
          return StepImportRunner.RegenOutcome.Failed;
        }

        result.Bytes = new FileInfo(outputPath).Length;

        if (CanRoundTrip(options))
        {
          RhinoDoc readBack = GltfImporter.CreateDoc();
          try
          {
            if (!GltfImporter.Read(outputPath, readBack, out double readSeconds) || readBack.Objects.Count == 0)
            {
              failure = $"[regen] '{filename}': the exported file did not import again; cannot regenerate.";
              AppendRegenReport($"===== REGEN {filename}  [FAILED] ====={System.Environment.NewLine}{failure}{System.Environment.NewLine}{System.Environment.NewLine}");
              return StepImportRunner.RegenOutcome.Failed;
            }

            result.ReadBackSeconds = readSeconds;
            result.Result = StepImporter.Measure(readBack, GltfExportOracle.ResultWanted(keys));
          }
          finally
          {
            readBack.Dispose();
          }
        }

        string newText = StepOracle.Write(GltfExportOracle.Incipit, old, keys.Select(k => Format(k, options, result)));

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

    static string Format(string key, GltfExportOptions options, StepExportResult result)
    {
      if (GltfExportOracle.OptionKeys.Contains(key)) return options.Format(key);

      if (key.StartsWith(GltfExportOracle.SourcePrefix, StringComparison.InvariantCulture))
        return GltfExportOracle.SourcePrefix +
               StepOracle.Format(key.Substring(GltfExportOracle.SourcePrefix.Length), result.Source);

      return StepOracle.Format(key, result.Result);
    }

    static void AppendRegenReport(string report)
    {
      try { TestContext.Progress.WriteLine(report); } catch { /* progress stream is best-effort */ }

      string logPath = System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_REGEN_LOG");
      if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Path.GetTempPath(), "mx_gltf_export_regen_report.txt");
      try { File.AppendAllText(logPath, report); } catch { /* report file is best-effort */ }
    }

    static string[] ChooseKeys(StepOracleFile old, string[] defaultKeys)
    {
      switch (System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_REGEN_FIELDS")?.Trim().ToUpperInvariant())
      {
        case "ALL": return GltfExportOracle.AllKeys;
        case "COUNTS": return GltfExportOracle.CountKeys;
        default:
          if (old == null) return defaultKeys;
          return old.Entries.Select(e => e.Key).Distinct().ToArray();
      }
    }
  }

  /// <summary>Folder-scanning base for STL export fixtures; sources may be .3dm or .stl.</summary>
  /// <typeparam name="T">The fixture itself; its class name selects the ModelDirectory entries.</typeparam>
  public abstract class AnyGltfExportFixture<T> where T : AnyGltfExportFixture<T>
  {
    internal static readonly List<string> g_test_models = new List<string>();

    static readonly string[] g_extensions = new string[] { ".3dm", ".gltf" };

    static AnyGltfExportFixture()
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
        try { GltfExportRunner.Run(full, defaultKeys, false); }
        catch (AssertionException) { failedAsExpected = true; }
        catch (InvalidOperationException) { failedAsExpected = true; }
        Assert.IsTrue(failedAsExpected, "Expected failure, but test succeeded.");
      }
      else
        GltfExportRunner.Run(full, defaultKeys, writeDebugModel);
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
          GltfExportRunner.RegenerateOracle(path, defaultKeys, out string failure);

        if (outcome == StepImportRunner.RegenOutcome.Written) n++;
        else if (outcome == StepImportRunner.RegenOutcome.Failed) failures.Add(failure);
      }

      if (failures.Count > 0)
        Assert.Fail($"Regenerated {n} baseline(s). {failures.Count} model(s) could not be round tripped:"
                    + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, failures));

      if (n == 0)
        Assert.Ignore($"No models matched MX_GLTFEXPORT_REGEN='{System.Environment.GetEnvironmentVariable("MX_GLTFEXPORT_REGEN")}'.");
    }
  }
}
