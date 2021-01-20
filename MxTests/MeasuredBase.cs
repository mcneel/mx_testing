using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MxTests
{
  /// <summary>
  /// Provides a basic implementation for File3dm-based tests that run from a folder.
  /// </summary>
  internal abstract class MeasuredBase<T> where T : GeometryBase
  {

    internal void ParseAndExecuteNotes(string filepath, string notesIncipit, bool twoGroups)
    {
      string filename = Path.GetFileName(filepath);
      if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
      {
        Assert.Throws<AssertionException>(
            delegate { RunParseExecuteNotes(filepath, notesIncipit, twoGroups, true); },
            "Expected failure, but test succeeded.");
      }
      else
        RunParseExecuteNotes(filepath, notesIncipit, twoGroups, false);
    }

    private void RunParseExecuteNotes(string filepath, string notesIncipit, bool twoGroups, bool shouldThrow)
    {
      using (var file = File3dm.Read(filepath))
      {
        var notes = file.Notes.Notes;

        if (string.IsNullOrWhiteSpace(notes))
          throw new NotSupportedException("Expected notes with information on processing.");

        var otherlines = new List<string>();
        string incipit = null;
        using (var tr = new StringReader(notes))
        {
          incipit = tr.ReadLine();
          string new_line;
          while ((new_line = tr.ReadLine()) != null)
          {
            if (string.IsNullOrWhiteSpace(new_line) ||
                new_line.Trim().StartsWith("#", false, CultureInfo.InvariantCulture))
              continue;
            otherlines.Add(new_line);
          }
        }

        if (incipit.Trim().StartsWith(notesIncipit, StringComparison.InvariantCultureIgnoreCase))
        {
          MeasuredTest(otherlines, file, filepath, twoGroups, shouldThrow);
        }
        else
          throw new NotSupportedException($"Unexpected type of test found in notes: {incipit}");
      }
    }

    internal void MeasuredTest(List<string> otherlines, File3dm file, string filepath, bool twoGroups, bool shouldThrow)
    {
      var expected = ExtractExpectedValues(otherlines);
      ExtractInputsFromFile(file, twoGroups, out double final_tolerance, out IEnumerable<T> inputMeshes, out IEnumerable<T> secondMeshesGroup);

      bool rv = OperateCommandOnGeometry(
        inputMeshes, secondMeshesGroup, final_tolerance,
        out List<ResultMetrics> result_ordered, out string log_text);

      try
      {
        CheckAssertions(file, expected, result_ordered, rv, log_text);
      }
      catch (AssertionException a) when (!shouldThrow)
      {
        string new_name = Path.Combine(Path.GetDirectoryName(filepath), "#" + Path.GetFileName(filepath));

        try
        {
          File.Copy(filepath, new_name, true);
          using (File3dm new_file = File3dm.Read(new_name))
          {
            Layer debug_layer = new Layer { Name = "DEBUG", Color = Color.Red };
            new_file.AllLayers.Add(debug_layer);
            debug_layer = new_file.AllLayers.FindName(debug_layer.Name, Guid.Empty);
            if (result_ordered != null)
              foreach (var result in result_ordered)
              {
                if (result == null) continue;

                if (result.Polyline != null)
                  new_file.Objects.AddCurve(result.Polyline.ToPolylineCurve(),
                      new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });
                else if (result.Curve != null)
                  new_file.Objects.AddCurve(result.Curve,
                      new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });

                if (result.Mesh != null)
                  new_file.Objects.AddMesh(result.Mesh,
                      new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });

                if (result.Point != null)
                  new_file.Objects.AddPoint(result.Point.Value,
                      new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });
              }
            new_file.Write(new_name, new File3dmWriteOptions());
          }
        }
        catch (Exception b)
        {
          throw new AggregateException(
              $"A {a.GetType().FullName} exception was thrown, then saving the file failed.",
              new Exception[] { a, b });
        }

        throw;
      }
    }

    internal abstract bool OperateCommandOnGeometry
      (IEnumerable<T> inputMeshes, IEnumerable<T> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog);
    internal abstract void CheckAssertions(File3dm file, List<ResultMetrics> expected, List<ResultMetrics> returned, bool rv, string textLog);

    internal virtual double ToleranceCoefficient { get { return 1.0; } }

    internal virtual void ExtractInputsFromFile<T>(File3dm file, bool usesSecondGroup, out double final_tolerance, out IEnumerable<T> surfaces, out IEnumerable<T> secondSurfacesGroup)
    {
      secondSurfacesGroup = null;

      var items = file.Objects.Select(item => new { item.Id, item.Geometry, item.Attributes }).ToList(); //buffer

      var curves = items.Where(item => item.Geometry.ObjectType == ObjectType.Curve).ToList();

      double doc_tolerance = file.Settings.ModelAbsoluteTolerance;
      double coefficient = ToleranceCoefficient;

      final_tolerance = doc_tolerance * coefficient;

      ObjectType filter = typeof(T) == typeof(Mesh) ? ObjectType.Mesh : ObjectType.Surface | ObjectType.Brep;

      var specific_items = items.Where(a => ((a.Geometry.ObjectType & filter) != 0)).Select(i => new { i.Id, Geometry = (T)(object)i.Geometry, i.Attributes });

      if (curves.Count > 0)
      {
        BoundingBox box = BoundingBox.Empty;
        foreach (var orig in items.Select(a => a.Geometry))
          box.Union(orig.GetBoundingBox(true));

        var test_cplane = file.AllNamedConstructionPlanes.FindName("Test");
        if (test_cplane == null) throw new NotSupportedException("Curves exist in the document, but there's no CPlane named 'Test' to extrude them.");

        var meshed_curves = curves.Select(
            c => new
            {
              c.Id,
              Geometry = (T)(
                    typeof(T) == typeof(Mesh) ?
                    (object)Mesh.CreateFromCurveExtrusion(
                    (Curve)c.Geometry,
                    test_cplane.Plane.ZAxis,
                    MeshingParameters.Default,
                    box
                    ):
                    (object)Surface.CreateExtrusion(
                    (Curve)c.Geometry,
                    test_cplane.Plane.ZAxis
                    )
                    ),
              c.Attributes
            });

        specific_items = specific_items.Concat(meshed_curves);
      }

      if (usesSecondGroup)
      {
        var layers = file.AllLayers.OrderBy(l => l.Name).ToList();
        if (layers.Count < 2) throw new InvalidOperationException("At least two layers are required for operations that take 2 inputs.");

        surfaces = specific_items.Where(i => i.Attributes.LayerIndex == layers[0].Index).Select(i => i.Geometry);
        secondSurfacesGroup = specific_items.Where(i => i.Attributes.LayerIndex == layers[1].Index).Select(i => i.Geometry);
      }
      else surfaces = specific_items.Select(i => i.Geometry);
    }


    readonly static string[] separators = new string[] { " ", "\t", "- " };
    internal static List<ResultMetrics> ExtractExpectedValues(List<string> otherlines)
    {
      var expected = otherlines
          .Select(
              line =>
              {
                var split = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                var rc = new ResultMetrics
                {
                  Measurement = double.Parse(split[0], CultureInfo.InvariantCulture),
                };

                if (split.Length > 1)
                  rc.Closed = split[1].Equals("CLOSED", StringComparison.InvariantCultureIgnoreCase);

                if (split.Length > 2)
                  rc.Overlap = split[2].StartsWith("OVERLAP", StringComparison.InvariantCultureIgnoreCase);

                return rc;
              }
          )
          .OrderBy(expectedresult => expectedresult.Measurement)
          .ToList();

      return expected;
    }
  }

  internal abstract class MeasuredIntersectionsBase<T>
    : MeasuredBase<T> where T : GeometryBase
  {
    const string incipitString = "MEASURED INTERSECTION";
    public static string IncipitString => incipitString;

    internal override void CheckAssertions(File3dm file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
    {
      Assert.IsTrue(rv, "Return value of intersection function was false.");
      Assert.IsEmpty(log_text, "Textlog of function must be empty");

      NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} curves but expected {expected.Count}.");

      for (int i = 0; i < expected.Count; i++)
      {
        Assert.AreEqual(expected[i].Measurement, result_ordered[i].Measurement, Math.Max(expected[i].Measurement * 10e-8, file.Settings.ModelAbsoluteTolerance));
        if (expected[i].Closed.HasValue) Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value,
            $"Curve of length {expected[i].Measurement} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
        if (expected[i].Overlap.HasValue) Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value,
            $"Curve of length {expected[i].Measurement} was not {(expected[i].Overlap.Value ? "ovelapping" : "perforating")} as expected.");
        if (expected[i].Point.HasValue) Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value,
            $"Curve of length {expected[i].Measurement} was not {(expected[i].Overlap.Value ? "ovelapping" : "perforating")} as expected.");
      }
    }
  }
}
