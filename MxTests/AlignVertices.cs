using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
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

    internal class AlignVerticesImplementation
    : ExpectedMeshBase
    {
      static AlignVerticesImplementation() { Instance = new AlignVerticesImplementation(); }

       internal override Type TargetType => typeof(Mesh);

       protected AlignVerticesImplementation() { }
      public static AlignVerticesImplementation Instance { get; private set; }

      internal override double ToleranceCoefficient => Intersection.MeshIntersectionsTolerancesCoefficient;

      public virtual void Model(string filepath)
      {
        ParseNotesAndExecute(filepath, "COMPARE", twoGroups: true);
      }


      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputMeshes, IEnumerable<object> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        Polyline[] intersections;
        Polyline[] overlaps;
        int rc = 0;

        using (var log = new TextLog())
        {
          foreach (var inputMesh in inputMeshes.Cast<Mesh>())
          {
            rc = inputMesh.Vertices.Align(tolerance, null);
            
          }

          textLog = log.ToString();
        }

        returned = null;
        //var results = intersections != null ? intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = false, Polyline = a }) : Array.Empty<ResultMetrics>();
        //if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = true, Polyline = a }));
        //returned = results.OrderBy(a => a.Measurement).ToList();

        return rc > 0;
      }
    }
  }

  internal class ExpectedMeshBase : MeasuredBase
  {
    internal override Type TargetType => typeof(Mesh);

    internal override void ExtractInputGeometryFromFile(object file, bool usesSecondGroup, out double final_tolerance, out IEnumerable<object> surfaces, out IEnumerable<object> secondSurfacesGroup)
    {
      base.ExtractInputGeometryFromFile(file, usesSecondGroup, out final_tolerance, out surfaces, out secondSurfacesGroup);
    }

    internal override bool OperateCommandOnGeometry(IEnumerable<object> inputMeshes, IEnumerable<object> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog)
    {
      var meshesA = inputMeshes.Cast<Mesh>().ToList();
      var meshesB = secondMeshes.Cast<Mesh>().ToList();

      returned = new List<ResultMetrics>();
      StringBuilder tl = new StringBuilder();

      for (int i = 0; i < meshesA.Count; i++)
      {
        bool rc = Rhino.Geometry.GeometryBase.GeometryEquals(meshesA[i], meshesB[i]);

        if (rc)
        {
          returned.Add(new ResultMetrics { Measurement = 1, TextInfo = i.ToString(), AdditionalTextValue = "Succeeded" });
        }
        else
        {
          returned.Add(new ResultMetrics { Measurement = 0, TextInfo = i.ToString(), AdditionalTextValue = $"Meshes {i} are different" });
        }
      }

      textLog = tl.ToString();

      return true;
    }

    internal override void CheckAssertions(object file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
    {
      for (int i =0; i < result_ordered.Count; i++)
      {
        var result = result_ordered[i];
        if (result.Measurement == 0)
        {
          Assert.Fail(result.AdditionalTextValue);
        }
      }

    }
  }
}
