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
  public class SurfaceIntersect : AnyCommand<SurfaceIntersect>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);

      SurfaceIntersectImplementation.Instance.Model(Path.Combine(filepath, filename));
    }

    internal class SurfaceIntersectImplementation
    : MeasuredSurfaceIntersectionsBase
    {
      static SurfaceIntersectImplementation() { Instance = new SurfaceIntersectImplementation(); }
      protected SurfaceIntersectImplementation() { }
      public static SurfaceIntersectImplementation Instance { get; private set; }

      public virtual void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, IncipitString, false);
      }

      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputs, IEnumerable<object> _, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        bool rc;

        var inputslist = inputs.ToList();

        if (inputslist.Count != 2) throw new InvalidOperationException($"Expected two surfaces, but found {inputslist.Count}.");

        rc = Intersection.SurfaceSurface(inputslist[0].Surfaces[0], inputslist[1].Surfaces[0], tolerance, out Curve[] intersections, out Point3d[] points);

        returned = null;
        var results = intersections != null ? intersections.Select(
          a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.GetLength(), Overlap = false, Curve = a }) 
          : Array.Empty<ResultMetrics>();
        if (points != null) results = results.Concat(points.Select(a => new ResultMetrics { Point = a }));
        returned = results.OrderBy(a => a.Measurement).ToList();

        textLog = string.Empty;

        return rc;
      }

      internal override void CheckAssertions(File3dm file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
      {
        base.CheckAssertions(file, expected, result_ordered, rv, log_text);
      }
    }
  }
}
