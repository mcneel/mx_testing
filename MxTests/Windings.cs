using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class Windings : AnyCommand<Windings>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);

      WindingsImplementation.Instance.Model(Path.Combine(filepath, filename));
    } 

    internal class WindingsImplementation
    : MeasuredMeshIntersectionsBase
    {
      static WindingsImplementation() { Instance = new WindingsImplementation(); }

       internal override Type TargetType => typeof(Mesh);

       protected WindingsImplementation() { }
      public static WindingsImplementation Instance { get; private set; }

      internal override double ToleranceCoefficient => Intersection.MeshIntersectionsTolerancesCoefficient;

      public override string IncipitString => "WINDING DIRECTION";

      public virtual void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, IncipitString, false);
      }

      //  http://www.opengl.org/wiki/Calculating_a_Surface_Normal
      private static Vector3d ComputeFaceNormal(Polyline pl)
      {
        var rc = default(Vector3d);

        for (int j = 1; j < pl.Count; j++)
        {
          var next = pl[j];
          var current = pl[j - 1];

          rc.X += (current.Y - next.Y) * (current.Z + next.Z);
          rc.Y += (current.Z - next.Z) * (current.X + next.X);
          rc.Z += (current.X - next.X) * (current.Y + next.Y);
        }

        rc.Unitize();
        return rc;
      }

      private static Vector3d Winding(Polyline pl)
      {
        Vector3d normal = ComputeFaceNormal(pl);

        double max = normal.MaximumCoordinate;

        if (max == Math.Abs(normal.X)) normal = new Vector3d(Math.Sign(normal.X), 0, 0);
        else if (max == Math.Abs(normal.Y)) normal = new Vector3d(0, Math.Sign(normal.Y), 0);
        else normal = new Vector3d(0, 0, Math.Sign(normal.Z));

        //normal.Unitize();

        return normal;
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
        var results = intersections != null ? intersections.Select(a => new ResultMetrics { TextInfo = Winding(a).ToString(), Polyline = a }) : Array.Empty<ResultMetrics>();
        if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { TextInfo = Winding(a).ToString(), Polyline = a }));
        returned = results.OrderBy(a => a.Measurement).ToList();

        return rc;
      }

      internal override void CheckAssertions(object file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
      {
        //base.CheckAssertions(file, expected, result_ordered, rv, log_text);

        Assert.IsTrue(rv, "Return value of intersection function was false.");
        Assert.IsEmpty(log_text, "Textlog of function must be empty");

        NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} curves but expected {expected.Count}.");

        for (int i = 0; i < expected.Count; i++)
        {
          Assert.AreEqual(expected[i].TextInfo, result_ordered[i].TextInfo);
        }
      }

      internal override List<ResultMetrics> ExtractExpectedValues(List<string> otherlines)
      {
        var expected = otherlines
            .Select(
                line =>
                {
                  var split = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                  var rc = new ResultMetrics
                  {
                    TextInfo = split[0],
                  };

                  //ignore all rest

                  Console.WriteLine(rc.TextInfo);

                  return rc;
                }
            )
            .ToList();

        return expected;
      }
    }
  }
}
