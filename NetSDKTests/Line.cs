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
  }
}
