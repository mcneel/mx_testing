using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino;

namespace NetSDKTests
{
  [TestFixture]
  public class LineTests
  {
    [Test]
    public void ColinearMinimumDistanceTo()
    {
      // co-linear lines touching head-to-tail
      Line l0 = new Line(0, 0, 0, 10, 0, 0);
      Line l1 = new Line(10, 0, 0, 20, 0, 0);
      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);

      // co-linear lines touching tail-to-tail
      l1 = new Line(20, 0, 0, 10, 0, 0);
      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);

      // co-linear lines touching tail-to-head
      l0 = new Line(10, 0, 0, 0, 0, 0);
      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);

      // co-linear lines touching head-to-head
      l1 = new Line(10, 0, 0, 20, 0, 0);
      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);
    }

    [Test]
    public void PointOnLineMinimumDistanceTo()
    {
      Line l0 = new Line(0, 0, 0, 10, 0, 0);
      Line l1 = new Line(5, 0, 0, 5, 10, 0);

      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);
      l1 = new Line(5, 10, 0, 5, 0, 0);
      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);
    }

    [Test]
    public void CrossingLinesMinimumDistanceTo()
    {
      Line l0 = new Line(0, 0, 0, 10, 0, 0);
      Line l1 = new Line(5, -5, 0, 5, 5, 0);
      Assert.AreEqual(0.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);
    }

    [Test]
    public void DisjointLinesMinimumDistanceTo()
    {
      Line l0 = new Line(0, 0, 0, 10, 0, 0);
      Line l1 = new Line(11, 1, 0, 11, 10, 0);

      Assert.AreEqual(Math.Sqrt(2.0), l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);

      l1 = new Line(5, 1, 0, 5, 10, 0);
      Assert.AreEqual(1.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);
    }

    [Test]
    public void DisjointColinearLinesMinimumDistanceTo()
    {
      Line l0 = new Line(0, 0, 0, 10, 0, 0);
      Line l1 = new Line(11, 0, 0, 20, 0, 0);

      Assert.AreEqual(1.0, l0.MinimumDistanceTo(l1), RhinoMath.ZeroTolerance);
    }

    [Test]
    public void NearlyParallel()
    {
      Line A = new Line(5.4301839655138417, -9.5, 0, -0.6, -9.5, 0);
      Line B = new Line(5.2373595635311068, 10.5, 0, 5.6603292194932395, 10.5, 0);

      bool intersect = Rhino.Geometry.Intersect.Intersection.LineLine(A, B, out double a, out double b);
      Assert.That(intersect, Is.False);
    }

    [Test]
    public void RH89164_MinimumDistance()
    {
      Line A = new Line(-502.12241078825264, 463.17458426757145, -2.6515717396328807e-12, - 569.15428634054422, 1336.2761199492861, 0);
      Line B = new Line(-474.85259053896368, 439.79346146347723, -2.7284841053187847e-12, -543.82881481395043, 1338.2204686719679, 0);

      bool intersect = Rhino.Geometry.Intersect.Intersection.LineLine(A, B, out double a, out double b);
      Assert.That(intersect, Is.False);

      Assert.AreEqual(25.4, A.MinimumDistanceTo(B), RhinoMath.SqrtEpsilon);
    }
  }
}
