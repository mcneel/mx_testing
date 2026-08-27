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

    static Mesh Grid3x3(double centreZ)
    {
      var mesh = new Mesh();
      for (int j = 0; j < 3; j++)
        for (int i = 0; i < 3; i++)
          mesh.Vertices.Add(i, j, (i == 1 && j == 1) ? centreZ : 0.0);

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
        var input = inputGeometry.ToList();
        var expected = expectedGeometry.ToList();

        returned = new List<ResultMetrics>();
        var tl = new StringBuilder();

        if (input.Any(g => g is SubD))
          Assert.Ignore("Model aligns SubDs. MX_AlignVertices accepts them, but no SubD overload is exposed to RhinoCommon: "
            + "RHC_MeshesVerticesAlign takes ON_Mesh only.");

        if (input.Any(g => g is Curve))
          Assert.Ignore("Model aligns curve control points. MX_AlignVertices accepts meshes, SubDs and point clouds only; "
            + "there is no MX_3dPointSparseEnumerator for curves.");

        var meshes = input.Cast<Mesh>().Select(m => m.DuplicateMesh()).ToList();
        var expected_meshes = expected.Cast<Mesh>().ToList();

        if (meshes.Count != expected_meshes.Count)
          throw new NotSupportedException($"Model has {meshes.Count} input meshes but {expected_meshes.Count} expected meshes.");

        IEnumerable<IEnumerable<bool>> flags = null;
        if (m_select_vertices != null)
          flags = meshes.Select(m => Enumerable.Range(0, m.Vertices.Count).Select(i => m_select_vertices.Contains(i)).ToList()).ToList();

        if (m_select_vertices != null && m_only_naked)
          throw new NotSupportedException("No overload takes both SelectVertices and OnlyNaked.");

        int moved = m_select_vertices != null
          ? MeshVertexList.Align(meshes, m_distance, m_average, flags)
          : MeshVertexList.Align(meshes, m_distance, m_only_naked, m_average);

        tl.AppendLine($"Align(distance={m_distance.ToString(CultureInfo.InvariantCulture)}, average={m_average}, onlyNaked={m_only_naked}"
          + $", select={(m_select_vertices == null ? "all" : string.Join("+", m_select_vertices))}) moved {moved} vertices");

        var wanted = expected_meshes.Select(Normalized).ToList();

        for (int i = 0; i < meshes.Count; i++)
        {
          Mesh got = Normalized(meshes[i]);

          int best = -1;
          double worst = double.MaxValue;

          for (int j = 0; j < wanted.Count; j++)
          {
            if (wanted[j] == null) continue;

            double deviation = Deviation(got, wanted[j]);
            if (deviation < worst) { worst = deviation; best = j; }
          }

          tl.AppendLine($"mesh {i}: aligned V={meshes[i].Vertices.Count} F={meshes[i].Faces.Count}"
            + $", compared V={got.Vertices.Count} F={got.Faces.Count} against expected mesh "
            + (best < 0 ? "(none of matching size)" : best.ToString(CultureInfo.InvariantCulture)));


          string rewired = best < 0 ? null : FirstDifferentFace(got, wanted[best]);

          returned.Add(new ResultMetrics
          {
            Measurement = rewired == null ? worst : double.MaxValue,
            Mesh = got,
            TextInfo = best < 0
              ? $"mesh {i}: got V={got.Vertices.Count} F={got.Faces.Count}, no expected mesh of that size remains"
              : rewired ?? $"mesh {i}: V={got.Vertices.Count} F={got.Faces.Count}, worst vertex deviation {worst.ToString("G6", CultureInfo.InvariantCulture)}"
          });

          if (best >= 0) wanted[best] = null;
        }

        textLog = tl.ToString();
        return moved >= 0;
      }


      static string FirstDifferentFace(Mesh got, Mesh want)
      {
        if (got.Faces.Count != want.Faces.Count) return null;

        for (int k = 0; k < got.Faces.Count; k++)
        {
          MeshFace g = got.Faces[k], w = want.Faces[k];
          if (g.A != w.A || g.B != w.B || g.C != w.C || g.D != w.D)
            return $"face {k}: got ({g.A},{g.B},{g.C},{g.D}), expected ({w.A},{w.B},{w.C},{w.D})";
        }

        return null;
      }
      static double Deviation(Mesh got, Mesh want)
      {
        if (got.Vertices.Count != want.Vertices.Count || got.Faces.Count != want.Faces.Count) return double.MaxValue;

        double worst = 0.0;
        for (int v = 0; v < got.Vertices.Count; v++)
          worst = Math.Max(worst, got.Vertices.Point3dAt(v).DistanceTo(want.Vertices.Point3dAt(v)));
        return worst;
      }

      static Mesh Normalized(Mesh mesh)
      {
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
