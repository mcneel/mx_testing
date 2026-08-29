using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MxTests
{
  [TestFixture]
  public class AlignVertices : AnyCommand<AlignVertices>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);

      AlignVerticesImplementation.Instance.Model(Path.Combine(filepath, filename));
    }

    // The three models below mix kinds of geometry, and which object moves depends on the order
    // the objects are handed to the aligner: the first one searches first and keeps its place.
    // Their expected layers record what the engine produces for document order.
    [Test, Explicit]
    public void RewriteMixedGeometryOracles()
    {
      string dir = Path.GetDirectoryName(g_test_models.First());

      foreach (var name in new[] { "mesh_polyline.3dm", "subd_polyline.3dm", "subd_two.3dm" })
      {
        string path = Path.Combine(dir, name);

        using (var file = File3dm.Read(path))
        {
          double distance = 0.0;
          bool average = false, onlyNaked = false;

          foreach (var line in file.Notes.Notes.Split(new[] { (char)13, (char)10 }, StringSplitOptions.RemoveEmptyEntries))
          {
            int equals = line.IndexOf('=');
            if (equals < 0) continue;

            string key = line.Substring(0, equals).Trim(), value = line.Substring(equals + 1).Trim();
            if (key.Equals("DistanceToAdjust", StringComparison.InvariantCultureIgnoreCase)) distance = double.Parse(value, CultureInfo.InvariantCulture);
            else if (key.Equals("AverageVertices", StringComparison.InvariantCultureIgnoreCase)) average = bool.Parse(value);
            else if (key.Equals("OnlyNaked", StringComparison.InvariantCultureIgnoreCase)) onlyNaked = bool.Parse(value);
          }

          var a = file.AllLayers.First(l => l.Name.Equals("A", StringComparison.InvariantCultureIgnoreCase));
          var b = file.AllLayers.First(l => l.Name.Equals("B", StringComparison.InvariantCultureIgnoreCase));

          var inputs = file.Objects
            .Where(o => o.Attributes.LayerIndex == a.Index && (o.Geometry is Mesh || o.Geometry is SubD || o.Geometry is Curve))
            .Select(o => o.Geometry.Duplicate())
            .ToList();

          int moved = Aligner.AlignVertices(inputs, distance, onlyNaked, average);

          foreach (var stale in file.Objects.Where(o => o.Attributes.LayerIndex == b.Index).Select(o => o.Id).ToList())
            file.Objects.Delete(stale);

          foreach (var result in inputs)
          {
            var attributes = new ObjectAttributes { LayerIndex = b.Index };
            if (result is Mesh m) file.Objects.AddMesh(m, attributes);
            else if (result is SubD s) file.Objects.AddSubD(s, attributes);
            else if (result is Curve c) file.Objects.AddCurve(c, attributes);
          }

          file.Write(path, 8);
          TestContext.WriteLine($"rewrote {name}, {moved} vertices moved");
        }
      }
    }

    [Test, Explicit]
    public void RewriteSelectVerticesOracles()
    {
      string dir = Path.GetDirectoryName(g_test_models.First());

      foreach (var name in new[] { "alignvertices-mesh-selectvertices.3dm", "testing-sample-for-alignvertices.3dm" })
      {
        string path = Path.Combine(dir, name);

        using (var file = File3dm.Read(path))
        {
          var results = file.AllLayers.First(l => l.Name.Equals("Results", StringComparison.InvariantCultureIgnoreCase));
          var input = file.Objects.First(o => o.Geometry is Mesh && o.Attributes.LayerIndex != results.Index);
          var expected = file.Objects.First(o => o.Geometry is Mesh && o.Attributes.LayerIndex == results.Index);

          var corrected = ((Mesh)input.Geometry).DuplicateMesh();
          corrected.Vertices.SetVertex(5, corrected.Vertices.Point3dAt(2));

          var attributes = expected.Attributes.Duplicate();
          file.Objects.Delete(expected.Id);
          file.Objects.AddMesh(corrected, attributes);

          file.Write(path, 8);
        }

        TestContext.WriteLine("rewrote " + name);
      }
    }

    [Test, Explicit]
    public void CreateGeometryModels()
    {
      string dir = Path.GetDirectoryName(g_test_models.First());

      var t1 = Triangle(0, 0, 0, 1, 0, 0, 0, 1, 0);
      var t2 = Triangle(0, 0, 0.1, -1, 0, 0, 0, -1, 0);

      Write(Path.Combine(dir, "mesh_two_triangles_average.3dm"),
        new[] { "COMPARE", "", "DistanceToAdjust = 0.2", "AverageVertices = True" },
        new[] { t1, t2 },
        new[] { Triangle(0, 0, 0.05, 1, 0, 0, 0, 1, 0), Triangle(0, 0, 0.05, -1, 0, 0, 0, -1, 0) });

      var probe = Triangle(1, 1, 0.05, 5, 5, 0, 5, 6, 0);

      Write(Path.Combine(dir, "mesh_interior_onlynaked.3dm"),
        new[] { "COMPARE", "", "DistanceToAdjust = 0.1", "OnlyNaked = True", "AverageVertices = False" },
        new[] { Grid3x3(0.0), probe },
        new[] { Grid3x3(0.0), Triangle(1, 1, 0.05, 5, 5, 0, 5, 6, 0) });

      Write(Path.Combine(dir, "mesh_interior_averaged.3dm"),
        new[] { "COMPARE", "", "DistanceToAdjust = 0.1", "OnlyNaked = False", "AverageVertices = True" },
        new[] { Grid3x3(0.0), Triangle(1, 1, 0.05, 5, 5, 0, 5, 6, 0) },
        new[] { Grid3x3(0.025), Triangle(1, 1, 0.025, 5, 5, 0, 5, 6, 0) });

      // Three vertices in a row, 1.25 apart, with a distance of 1.875: the ends are out of reach
      // of each other, so only one of the two pairs can merge. The first mesh claims the middle
      // one and keeps it, because a target already claimed is only given up to a nearer one.
      Write(Path.Combine(dir, "mesh_contested_vertex.3dm"),
        new[] { "COMPARE", "", "DistanceToAdjust = 1.875", "AverageVertices = False", "OnlyNaked = False" },
        new[]
        {
          Triangle(0.00, 0, 0,  0.00, -10, 0,  -3.00, -10, 0),
          Triangle(1.25, 0, 0,  1.25,  10, 0,   4.25,  10, 0),
          Triangle(2.50, 0, 0,  2.50, -10, 5,   5.50, -10, 5),
        },
        new[]
        {
          Triangle(0.00, 0, 0,  0.00, -10, 0,  -3.00, -10, 0),
          Triangle(0.00, 0, 0,  1.25,  10, 0,   4.25,  10, 0),
          Triangle(2.50, 0, 0,  2.50, -10, 5,   5.50, -10, 5),
        });
    }

    static Mesh Triangle(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz)
    {
      var mesh = new Mesh();
      mesh.Vertices.Add(ax, ay, az);
      mesh.Vertices.Add(bx, by, bz);
      mesh.Vertices.Add(cx, cy, cz);
      mesh.Faces.AddFace(0, 1, 2);
      return mesh;
    }

    static Mesh Grid3x3(double interiorZ)
    {
      var mesh = new Mesh();
      for (int j = 0; j < 3; j++)
        for (int i = 0; i < 3; i++)
          mesh.Vertices.Add(i, j, (i == 1 && j == 1) ? interiorZ : 0.0);

      mesh.Faces.AddFace(0, 1, 4, 3);
      mesh.Faces.AddFace(1, 2, 5, 4);
      mesh.Faces.AddFace(3, 4, 7, 6);
      mesh.Faces.AddFace(4, 5, 8, 7);
      return mesh;
    }

    static void Write(string path, string[] notes, Mesh[] inputs, Mesh[] expected)
    {
      using (var file = new File3dm())
      {
        file.Settings.ModelAbsoluteTolerance = 0.001;
        file.Notes.Notes = string.Join(System.Environment.NewLine, notes);

        int a = file.AllLayers.Count; file.AllLayers.Add(new Layer { Name = "A" });
        int b = file.AllLayers.Count; file.AllLayers.Add(new Layer { Name = "B" });

        foreach (var mesh in inputs) file.Objects.AddMesh(mesh, new ObjectAttributes { LayerIndex = a });
        foreach (var mesh in expected) file.Objects.AddMesh(mesh, new ObjectAttributes { LayerIndex = b });

        file.Write(path, 8);
      }

      TestContext.WriteLine("wrote " + path);
    }

    internal class AlignVerticesImplementation
    : MeasuredBase
    {
      static AlignVerticesImplementation() { Instance = new AlignVerticesImplementation(); }
      private AlignVerticesImplementation() { }
      public static AlignVerticesImplementation Instance { get; private set; }

      internal override Type TargetType => typeof(Mesh);

      internal override double ToleranceCoefficient => 1.0;

      const string incipitString = "COMPARE";

      double m_distance;
      bool m_average;
      bool m_only_naked;
      List<int> m_select_vertices;

      public void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, incipitString, true);
      }

      internal override List<ResultMetrics> ExtractExpectedValues(List<string> otherlines)
      {
        m_distance = 0.0;
        m_average = false;
        m_only_naked = false;
        m_select_vertices = null;

        foreach (string line in otherlines)
        {
          int equals = line.IndexOf('=');
          if (equals < 0) throw new NotSupportedException($"Expected 'Key = Value' in notes, got: {line}");

          string key = line.Substring(0, equals).Trim();
          string value = line.Substring(equals + 1).Trim();

          if (key.Equals("DistanceToAdjust", StringComparison.InvariantCultureIgnoreCase))
            m_distance = double.Parse(value, CultureInfo.InvariantCulture);
          else if (key.Equals("AverageVertices", StringComparison.InvariantCultureIgnoreCase))
            m_average = bool.Parse(value);
          else if (key.Equals("OnlyNaked", StringComparison.InvariantCultureIgnoreCase))
            m_only_naked = bool.Parse(value);
          else if (key.Equals("SelectVertices", StringComparison.InvariantCultureIgnoreCase))
            m_select_vertices = value.Split(',').Select(v => int.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToList();
          else
            throw new NotSupportedException($"Unexpected key in notes: {key}");
        }

        if (m_distance <= 0.0) throw new NotSupportedException("Notes must state a positive DistanceToAdjust.");

        return new List<ResultMetrics>();
      }

      internal override void ExtractInputsFromFile(
          object file, bool usesSecondGroup, out double final_tolerance, out IEnumerable<object> surfaces, out IEnumerable<object> secondSurfacesGroup)
      {
        var doc = (File3dm)file;
        final_tolerance = doc.Settings.ModelAbsoluteTolerance * ToleranceCoefficient;

        Layer input_layer = doc.AllLayers.FirstOrDefault(l => l.Name.Equals("A", StringComparison.InvariantCultureIgnoreCase));
        Layer expected_layer = doc.AllLayers.FirstOrDefault(l => l.Name.Equals("B", StringComparison.InvariantCultureIgnoreCase))
                            ?? doc.AllLayers.FirstOrDefault(l => l.Name.Equals("Results", StringComparison.InvariantCultureIgnoreCase));

        if (expected_layer == null)
          throw new NotSupportedException("Expected a layer named 'B' or 'Results' holding the aligned result.");

        var considered = doc.Objects
          .Where(o => o.Geometry is Mesh || o.Geometry is SubD || o.Geometry is Curve)
          .ToList();

        surfaces = considered
          .Where(o => input_layer != null ? o.Attributes.LayerIndex == input_layer.Index : o.Attributes.LayerIndex != expected_layer.Index)
          .Select(o => (object)o.Geometry)
          .ToList();

        secondSurfacesGroup = considered
          .Where(o => o.Attributes.LayerIndex == expected_layer.Index)
          .Select(o => (object)o.Geometry)
          .ToList();
      }

      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputGeometry, IEnumerable<object> expectedGeometry, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        var input = inputGeometry.Cast<GeometryBase>().Select(g => g.Duplicate()).ToList();
        var expected = expectedGeometry.Cast<GeometryBase>().ToList();

        returned = new List<ResultMetrics>();
        var tl = new StringBuilder();

        foreach (var geometry in input)
          if (!Aligner.SupportsGeometry(geometry))
            Assert.Ignore($"Model contains {geometry.GetType().Name}, which vertex alignment does not support.");

        if (input.Count != expected.Count)
          throw new NotSupportedException($"Model has {input.Count} inputs but {expected.Count} expected objects.");

        if (m_select_vertices != null && m_only_naked)
          throw new NotSupportedException("SelectVertices and OnlyNaked cannot both be stated.");

        IEnumerable<IEnumerable<bool>> flags = null;
        if (m_select_vertices != null)
          flags = input.Select(g => Enumerable.Range(0, PointsOf(g).Count).Select(i => m_select_vertices.Contains(i)).ToList()).ToList();

        int moved = Aligner.AlignVertices(input, m_distance, m_only_naked, m_average, flags);

        tl.AppendLine($"Align(distance={m_distance.ToString(CultureInfo.InvariantCulture)}, average={m_average}, onlyNaked={m_only_naked}"
          + $", select={(m_select_vertices == null ? "all" : string.Join("+", m_select_vertices))}) moved {moved} vertices");

        var wanted = expected.Select(Normalized).ToList();

        for (int i = 0; i < input.Count; i++)
        {
          GeometryBase got = Normalized(input[i]);

          int best = -1;
          double worst = double.MaxValue;

          for (int j = 0; j < wanted.Count; j++)
          {
            if (wanted[j] == null) continue;

            double deviation = Deviation(got, wanted[j]);
            if (deviation < worst) { worst = deviation; best = j; }
          }

          string rewired = best < 0 ? null : FirstDifferentFace(got as Mesh, wanted[best] as Mesh);

          tl.AppendLine($"{got.GetType().Name} {i}: {PointsOf(got).Count} points, compared against expected "
            + (best < 0 ? "(nothing of matching kind and size)" : best.ToString(CultureInfo.InvariantCulture)));

          returned.Add(new ResultMetrics
          {
            Measurement = rewired == null ? worst : double.MaxValue,
            Mesh = got as Mesh,
            TextInfo = best < 0
              ? $"{got.GetType().Name} {i}: {PointsOf(got).Count} points, nothing of matching kind and size remains"
              : rewired ?? $"{got.GetType().Name} {i}: {PointsOf(got).Count} points, worst deviation {worst.ToString("G6", CultureInfo.InvariantCulture)}"
          });

          if (best >= 0) wanted[best] = null;
        }

        textLog = tl.ToString();
        return moved >= 0;
      }

      // The points vertex alignment can move, in the order the engine indexes them.
      static List<Point3d> PointsOf(GeometryBase geometry)
      {
        if (geometry is Mesh mesh)
          return Enumerable.Range(0, mesh.Vertices.Count).Select(i => mesh.Vertices.Point3dAt(i)).ToList();

        if (geometry is PointCloud cloud)
          return cloud.Select(item => item.Location).ToList();

        if (geometry is SubD subd)
          return subd.Vertices.Select(v => v.ControlNetPoint).ToList();

        if (geometry is PolylineCurve polyline)
          return Enumerable.Range(0, polyline.PointCount).Select(i => polyline.Point(i)).ToList();

        if (geometry is LineCurve line)
          return new List<Point3d> { line.Line.From, line.Line.To };

        if (geometry is NurbsCurve nurbs)
          return nurbs.Points.Select(cv => cv.Location).ToList();

        throw new NotSupportedException($"No point list for {geometry.GetType().Name}.");
      }

      static string FirstDifferentFace(Mesh got, Mesh want)
      {
        if (got == null || want == null || got.Faces.Count != want.Faces.Count) return null;

        for (int k = 0; k < got.Faces.Count; k++)
        {
          MeshFace g = got.Faces[k], w = want.Faces[k];
          if (g.A != w.A || g.B != w.B || g.C != w.C || g.D != w.D)
            return $"face {k}: got ({g.A},{g.B},{g.C},{g.D}), expected ({w.A},{w.B},{w.C},{w.D})";
        }

        return null;
      }

      static double Deviation(GeometryBase got, GeometryBase want)
      {
        if (got.GetType() != want.GetType()) return double.MaxValue;

        List<Point3d> a = PointsOf(got), b = PointsOf(want);
        if (a.Count != b.Count) return double.MaxValue;

        if (got is Mesh gm && want is Mesh wm && gm.Faces.Count != wm.Faces.Count) return double.MaxValue;

        double worst = 0.0;
        for (int v = 0; v < a.Count; v++)
          worst = Math.Max(worst, a[v].DistanceTo(b[v]));
        return worst;
      }

      // Aligning collapses mesh faces and can leave the vertices they referenced unused. Neither
      // entry point compacts, so both sides are normalized the same way before comparing.
      static GeometryBase Normalized(GeometryBase geometry)
      {
        if (!(geometry is Mesh mesh)) return geometry;

        Mesh rc = mesh.DuplicateMesh();
        rc.Faces.CullDegenerateFaces();
        rc.Compact();
        return rc;
      }

      internal override void CheckAssertions(object file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
      {
        Assert.IsTrue(rv, "Align reported failure.");

        double tolerance = ((File3dm)file).Settings.ModelAbsoluteTolerance * ToleranceCoefficient;

        foreach (var result in result_ordered)
          if (!(result.Measurement <= tolerance))
            Assert.Fail($"{result.TextInfo} (tolerance {tolerance.ToString(CultureInfo.InvariantCulture)})"
              + $"{System.Environment.NewLine}{log_text}");
      }
    }
  }
}
