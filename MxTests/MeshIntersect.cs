using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class MeshIntersect : AnyCommand<MeshIntersect>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);

      MeshIntersectImplementation.Instance.Model(Path.Combine(filepath, filename));
    }

    internal class MeshIntersectImplementation
    : MeasuredMeshIntersectionsBase
    {
      static MeshIntersectImplementation() { Instance = new MeshIntersectImplementation(); }

       internal override Type TargetType => typeof(Mesh);

       protected MeshIntersectImplementation() { }
      public static MeshIntersectImplementation Instance { get; private set; }

      internal override double ToleranceCoefficient => Intersection.MeshIntersectionsTolerancesCoefficient;

      public virtual void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, IncipitString, false);
      }

      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputMeshes, IEnumerable<object> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        Polyline[] intersections;
        Polyline[] overlaps;
        bool rc;

        using (var log = new TextLog())
        {
          rc = Intersection.MeshMesh(inputMeshes.Cast<Mesh>(), tolerance,
              out intersections, true, out overlaps, false, out _, log,
              System.Threading.CancellationToken.None, null);
          textLog = log.ToString();
        }

        returned = null;
        var results = intersections != null ? intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = false, Polyline = a }) : Array.Empty<ResultMetrics>();
        if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = true, Polyline = a }));
        returned = results.OrderBy(a => a.Measurement).ToList();

        return rc;
      }
    }
  }
}
