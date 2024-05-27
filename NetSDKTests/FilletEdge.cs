using NUnit.Framework;
using NUnit.Framework.Constraints;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NetSDKTests
{
  [TestFixture]
  public class FilletEdge
  {

    Brep[] Fillet(Brep box, IEnumerable<int> edges, IEnumerable<double> r0, IEnumerable<double> r1, BlendType bt, RailType rt, IResolveConstraint a)
    {
      Brep[] result = Brep.CreateFilletEdges(box, edges, r0, r1, bt, rt, 1e-3);
      Assert.That(result, a);
      return result;
    }

    [Test]
    public void TestFilletEdge()
    {
      {
        var bts = new[] { BlendType.Fillet, BlendType.Chamfer, BlendType.Blend };
        var rts = new[] { RailType.RollingBall, RailType.DistanceBetweenRails, RailType.DistanceFromEdge };

        Brep box = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 10, 10));
        int[] edges = Enumerable.Range(0, 12).ToArray();
        double[] r0 = Enumerable.Repeat(1.0, edges.Length).ToArray();
        double[] r1 = Enumerable.Repeat(1.5, edges.Length).ToArray();

        foreach (var bt in bts)
        {
          foreach (var rt in rts)
          {
            Brep[] filleted = Fillet(box, edges, r0, r1, bt, rt, Is.Not.Null);
            Assert.That(filleted.Length, Is.EqualTo(1));
            Assert.That(filleted[0].IsValid, Is.True);
            if (bt == BlendType.Blend)
            {
              // Blend uses setback corners with 6 surfaces per corner
              Assert.That(filleted[0].Faces.Count, Is.EqualTo(66)); // 6 faces, 12 edges and 6*8 corners 
            }
            else
            {
              Assert.That(filleted[0].Faces.Count, Is.EqualTo(26)); // 6 faces, 12 edges and 8 corners
            }
            Console.WriteLine("Pass with {0}/{1}", bt, rt);
          }
        }
      }
    }

    [Test]
    public void TestFilletEdgeNoEdges()
    {
      Brep box = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 10, 10));
      int[] edges = Array.Empty<int>();
      double[] r0 = Enumerable.Repeat(1.0, edges.Length).ToArray();
      double[] r1 = Enumerable.Repeat(1.5, edges.Length).ToArray();
      var bt = BlendType.Fillet;
      var rt = RailType.RollingBall;

      Brep[] filleted = Fillet(box, edges, r0, r1, bt, rt, Is.Not.Null);
      Assert.That(filleted, Is.Empty);
    }

    [Test]
    public void TestFilletEdgeInvalidEdges()
    {
      Brep box = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 10, 10));
      int[] edges = Enumerable.Range(1, 12).ToArray();
      double[] r0 = Enumerable.Repeat(1.0, edges.Length).ToArray();
      double[] r1 = Enumerable.Repeat(1.5, edges.Length).ToArray();
      var bt = BlendType.Fillet;
      var rt = RailType.RollingBall;

      Brep[] filleted = Fillet(box, edges, r0, r1, bt, rt, Is.Not.Null);
      Assert.That(filleted, Is.Empty);
    }



    [Test]
    public void TestFilletEdgeNegativeStartRadii()
    {
      Brep box = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 10, 10));
      int[] edges = Enumerable.Range(0, 12).ToArray();
      double[] r0 = Enumerable.Repeat(-1.0, edges.Length).ToArray();
      double[] r1 = Enumerable.Repeat(1.5, edges.Length).ToArray();
      var bt = BlendType.Fillet;
      var rt = RailType.RollingBall;

      Brep[] filleted = Fillet(box, edges, r0, r1, bt, rt, Is.Not.Null);
      Assert.That(filleted, Is.Empty);
    }


    [Test]
    public void TestFilletEdgeNegativeEndRadii()
    {
      Brep box = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 10, 10));
      int[] edges = Enumerable.Range(0, 12).ToArray();
      double[] r0 = Enumerable.Repeat(1.0, edges.Length).ToArray();
      double[] r1 = Enumerable.Repeat(-1.5, edges.Length).ToArray();
      var bt = BlendType.Fillet;
      var rt = RailType.RollingBall;

      Brep[] filleted = Fillet(box, edges, r0, r1, bt, rt, Is.Not.Null);
      Assert.That(filleted, Is.Empty);
    }
  }
}
