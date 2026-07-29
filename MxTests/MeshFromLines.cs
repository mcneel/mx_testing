using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class MeshFromLines : AnyCommand<MeshFromLines>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      MeshFromLinesImplementation.Instance.Model(Path.Combine(filepath, filename));
    }

    [Test, Explicit]
    public void Regenerate()
    {
      int n = 0;
      foreach (var path in g_test_models)
        if (MeshFromLinesImplementation.Instance.RegenerateOracle(path, MeshFromLinesImplementation.incipitString, false)) n++;
      if (n == 0) Assert.Ignore($"No models matched MX_REGEN='{Environment.GetEnvironmentVariable("MX_REGEN")}'.");
    }

    /// <summary>One measured piece, and the maximum face valence it was asked for.</summary>
    internal sealed class ValenceResult : ResultMetrics
    {
      public int Valence { get; set; }

      /// <summary>True on the piece that carries its valence's header in the oracle.</summary>
      public bool FirstOfValence { get; set; }
    }

    internal class MeshFromLinesImplementation
    : MeasuredBase
    {
      static MeshFromLinesImplementation() { Instance = new MeshFromLinesImplementation(); }
      private MeshFromLinesImplementation() { }
      public static MeshFromLinesImplementation Instance { get; private set; }

      internal override Type TargetType => typeof(Curve);

      // The MeshFromLines command hands Mesh.CreateFromLines the document tolerance as it
      // stands, since that tolerance is what tells two line ends apart.
      internal override double ToleranceCoefficient => 1.0;

      internal const string incipitString = "MESH FROM LINES";

      // How many sides a face may have decides which faces are found at all, so every model
      // is measured across the whole range the command offers.
      static readonly int[] g_valences = { 3, 4, 5, 6, 7 };

      const string valenceWord = "VALENCE";
      const string noneWord = "NONE";

      public void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, incipitString, false);
      }

      // The input here is the lines themselves, so neither the base's extrusion of curves into
      // meshes nor the 'Test' CPlane it needs to do it applies.
      internal override void ExtractInputsFromFile(object file, bool usesSecondGroup,
          out double final_tolerance, out IEnumerable<object> curves, out IEnumerable<object> secondGroup)
      {
        var doc = (File3dm)file;

        final_tolerance = doc.Settings.ModelAbsoluteTolerance * ToleranceCoefficient;

        // Document order is kept: which face a shared edge is walked into can depend on it.
        var lines = new List<object>();
        foreach (File3dmObject item in doc.Objects)
          if (item.Geometry.ObjectType == Rhino.DocObjects.ObjectType.Curve) lines.Add(item.Geometry);

        curves = lines;
        secondGroup = null;
      }

      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputCurves,
          IEnumerable<object> secondGroup, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        textLog = null;
        returned = new List<ResultMetrics>();

        Curve[] lines = inputCurves.Cast<Curve>().ToArray();

        foreach (int valence in g_valences)
        {
          var group = new List<ResultMetrics>();

          using (Mesh mesh = Mesh.CreateFromLines(lines, valence, tolerance))
          {
            if (mesh != null)
              foreach (Mesh piece in mesh.SplitDisjointPieces())
                group.Add(new ValenceResult
                {
                  Valence = valence,
                  Measurement = AreaMassProperties.Compute(piece).Area,
                  Mesh = piece,
                  Closed = piece.IsClosed,
                  TextInfo = ObtainVividDescription(piece),
                });
          }

          // Two pieces of one valence can have the same area to the last bit, so the
          // description settles the order rather than leaving it to an unstable sort.
          group = group
              .OrderBy(a => a.Measurement)
              .ThenBy(a => a.TextInfo, StringComparer.Ordinal)
              .ToList();

          // A valence that makes nothing is recorded as such rather than left out, so the
          // oracle says what happened at every valence that was tried.
          if (group.Count == 0) group.Add(new ValenceResult { Valence = valence });

          ((ValenceResult)group[0]).FirstOfValence = true;
          returned.AddRange(group);
        }

        return returned.Any(r => r.Mesh != null);
      }

      internal override void CheckAssertions(object file, List<ResultMetrics> expected,
          List<ResultMetrics> result_ordered, bool rv, string log_text)
      {
        Assert.IsTrue(rv, "Mesh.CreateFromLines() made no mesh at any valence.");

        Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} meshes but expected {expected.Count}.");

        double doc_tolerance = ((File3dm)file).Settings.ModelAbsoluteTolerance;

        for (int i = 0; i < expected.Count; i++)
        {
          var e = (ValenceResult)expected[i];
          var r = (ValenceResult)result_ordered[i];

          Assert.AreEqual(e.Valence, r.Valence, $"Expected a result for valence {e.Valence}, but got one for valence {r.Valence}.");

          string where = $"valence {e.Valence}, mesh of area {e.Measurement}";

          Assert.AreEqual(e.Measurement, r.Measurement, Math.Max(e.Measurement * 10e-8, doc_tolerance), where);

          if (e.Closed.HasValue) Assert.AreEqual(e.Closed.Value, r.Closed.Value,
              $"Mesh at {where} was not {(e.Closed.Value ? "closed" : "open")} as expected.");

          if (e.TextInfo != null) Assert.AreEqual(e.TextInfo, r.TextInfo,
              $"Expected different geometry description at {where}:");
        }
      }

      // 'VALENCE n' opens a group and the lines under it are that group's pieces, smallest
      // first, which is the order OperateCommandOnGeometry returns them in.
      internal override List<ResultMetrics> ExtractExpectedValues(List<string> otherlines)
      {
        var expected = new List<ResultMetrics>();
        int valence = 0;

        foreach (string line in otherlines)
        {
          var split = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
          if (split.Length == 0) continue;

          if (split[0].Equals(valenceWord, StringComparison.InvariantCultureIgnoreCase))
          {
            valence = int.Parse(split[1], CultureInfo.InvariantCulture);
            continue;
          }

          if (split[0].Equals(noneWord, StringComparison.InvariantCultureIgnoreCase))
          {
            expected.Add(new ValenceResult { Valence = valence });
            continue;
          }

          var rc = new ValenceResult
          {
            Valence = valence,
            Measurement = double.Parse(split[0], CultureInfo.InvariantCulture),
          };

          if (split.Length > 1)
            rc.Closed = split[1].Equals("CLOSED", StringComparison.InvariantCultureIgnoreCase);

          int open_bracket_index = Array.FindIndex(split, s => s.StartsWith("[", StringComparison.InvariantCulture));
          if (open_bracket_index != -1)
            rc.TextInfo = SimplifyDescription(string.Join(" ", split.Skip(open_bracket_index)));

          expected.Add(rc);
        }

        return expected;
      }

      internal override string FormatOracleLine(ResultMetrics m, bool wantClosed, bool wantOverlap, bool wantText)
      {
        var piece = (ValenceResult)m;

        string header = piece.FirstOfValence
            ? $"{valenceWord} {piece.Valence.ToString(CultureInfo.InvariantCulture)}\n"
            : string.Empty;

        if (piece.Mesh == null) return header + noneWord;

        return header + base.FormatOracleLine(m, wantClosed, false, wantText);
      }
    }
  }
}
