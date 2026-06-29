using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System.Collections.Generic;

namespace MxTests
{
  /// <summary>
  /// Exercises <see cref="Intersection.MeshMeshPredicate"/> (the .NET wrapper of the native
  /// MX_MeshMeshIntersectPredicate / ON_Mesh::IntersectArrayPredicate fast predicate).
  /// Verifies both the boolean answer and the reported intersecting mesh couples.
  /// </summary>
  [TestFixture]
  public class MeshIntersectPredicate
  {
    const double Tolerance = 1e-7;

    static Mesh Box(double x0, double y0, double z0, double x1, double y1, double z1)
    {
      var bbox = new BoundingBox(new Point3d(x0, y0, z0), new Point3d(x1, y1, z1));
      return Mesh.CreateFromBox(bbox, 1, 1, 1);
    }

    // The native predicate returns couples of mesh indices flattened into the int[].
    // Normalize each couple to "min-max" so the assertions are order/orientation independent.
    static List<string> Couples(int[] pairs)
    {
      var list = new List<string>();
      if (pairs == null) return list;
      Assert.That(pairs.Length % 2, Is.EqualTo(0), "the pairs array must contain index couples (even length)");
      for (int i = 0; i < pairs.Length; i += 2)
      {
        int a = pairs[i], b = pairs[i + 1];
        list.Add(a <= b ? a + "-" + b : b + "-" + a);
      }
      return list;
    }

    [Test]
    public void TwoOverlappingMeshes_IntersectAndReportTheCouple()
    {
      SetupFixture.Prerequisites();

      using (var a = Box(0, 0, 0, 1, 1, 1))
      using (var b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5)) // volumetrically penetrates a
      using (var log = new TextLog())
      {
        bool rc = Intersection.MeshMeshPredicate(new[] { a, b }, Tolerance, out int[] pairs, log);

        Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
        Assert.That(rc, Is.True, "overlapping boxes must be reported as intersecting");
        Assert.That(Couples(pairs), Is.EquivalentTo(new[] { "0-1" }), "the single intersecting couple (0,1) must be reported");
      }
    }

    [Test]
    public void TwoDisjointMeshes_DoNotIntersect()
    {
      SetupFixture.Prerequisites();

      using (var a = Box(0, 0, 0, 1, 1, 1))
      using (var b = Box(5, 5, 5, 6, 6, 6)) // far away
      using (var log = new TextLog())
      {
        bool rc = Intersection.MeshMeshPredicate(new[] { a, b }, Tolerance, out int[] pairs, log);

        Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
        Assert.That(rc, Is.False, "disjoint boxes must not be reported as intersecting");
        Assert.That(Couples(pairs), Is.Empty, "no couple may be reported for disjoint meshes");
      }
    }

    [Test]
    public void NestedMeshes_HaveNoSurfaceIntersection()
    {
      SetupFixture.Prerequisites();

      using (var outer = Box(0, 0, 0, 3, 3, 3))
      using (var inner = Box(1, 1, 1, 2, 2, 2)) // wholly inside outer, surfaces never cross
      using (var log = new TextLog())
      {
        bool rc = Intersection.MeshMeshPredicate(new[] { outer, inner }, Tolerance, out int[] pairs, log);

        Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
        Assert.That(rc, Is.False, "containment is not a surface intersection");
        Assert.That(Couples(pairs), Is.Empty);
      }
    }

    [Test]
    public void ThreeMeshes_OnlyTheIntersectingCoupleIsReported()
    {
      SetupFixture.Prerequisites();

      using (var a = Box(0, 0, 0, 1, 1, 1))
      using (var b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5)) // overlaps a
      using (var c = Box(10, 10, 10, 11, 11, 11))       // disjoint from a and b
      using (var log = new TextLog())
      {
        bool rc = Intersection.MeshMeshPredicate(new[] { a, b, c }, Tolerance, out int[] pairs, log);

        Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
        Assert.That(rc, Is.True);
        Assert.That(Couples(pairs), Is.EquivalentTo(new[] { "0-1" }), "only the (0,1) couple intersects; mesh 2 is disjoint");
      }
    }
  }
}
