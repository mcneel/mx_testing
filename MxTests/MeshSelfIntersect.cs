using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class MeshSelfIntersect : AnyCommand<MeshSelfIntersect>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);

      MeshSelfIntersectImplementation.Instance.Model(Path.Combine(filepath, filename));
    }

    private class MeshSelfIntersectImplementation
    : MeshIntersect.MeshIntersectImplementation
    {
      static MeshSelfIntersectImplementation() { Instance = new MeshSelfIntersectImplementation(); }
      private MeshSelfIntersectImplementation() { }
      public static new MeshSelfIntersectImplementation Instance { get; private set; }

      const string incipitString = "MEASURED SELFINTERSECTION";

      public override void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, incipitString, false);
      }

      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputMeshes, IEnumerable<object> secondMeshes, 
        double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        Polyline[] intersections;
        Polyline[] overlaps;
        bool rc;

        var mesh = (Mesh)inputMeshes.FirstOrDefault();
        if (mesh == null) Assert.Fail("Expected one and only one object to test.");

        using (var log = new TextLog())
        {
          rc = mesh.GetSelfIntersections(tolerance,
              out intersections, true, out overlaps, false, out _, log,
              System.Threading.CancellationToken.None, null);
          textLog = log.ToString();
        }

        returned = null;

        var results = intersections != null ? 
          intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = false, Polyline = a }) : 
          Array.Empty<ResultMetrics>();

        if (overlaps != null) results = results.Concat(
          overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = true, Polyline = a }));
        returned = results.OrderBy(a => a.Measurement).ToList();

        return rc;
      }
    }
  }
}
