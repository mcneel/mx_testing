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
    public void FastAndPrecise_AgreeOnPenetratingBoxes_AndReportWitnessFaces()
    {
      SetupFixture.Prerequisites();
      using (var a = Box(0, 0, 0, 1, 1, 1))
      using (var b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5))
      using (var log = new TextLog())
      {
        foreach (bool fast in new[] { true, false })
        {
          bool rc = Intersection.MeshMeshPredicate(new[] { a, b }, null, Tolerance, fast, out int[] pairs, out int[] faces, log, System.Threading.CancellationToken.None);
          Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
          Assert.That(rc, Is.True, "fast=" + fast);
          Assert.That(Couples(pairs), Is.EquivalentTo(new[] { "0-1" }), "fast=" + fast);
          Assert.That(faces.Length, Is.EqualTo(2), "one face couple per mesh couple, fast=" + fast);
          Assert.That(faces[0], Is.InRange(0, a.Faces.Count - 1), "fast=" + fast);
          Assert.That(faces[1], Is.InRange(0, b.Faces.Count - 1), "fast=" + fast);
        }
      }
    }

    [Test]
    public void CoplanarPatches_OnlyThePreciseFormReportsTheOverlap()
    {
      SetupFixture.Prerequisites();
      using (var a = Mesh.CreateFromPlane(Plane.WorldXY, new Interval(0, 10), new Interval(0, 10), 4, 4))
      using (var b = Mesh.CreateFromPlane(Plane.WorldXY, new Interval(5, 15), new Interval(5, 15), 4, 4))
      using (var log = new TextLog())
      {
        bool fast = Intersection.MeshMeshPredicate(new[] { a, b }, null, Tolerance, true, out int[] fastPairs, out _, log, System.Threading.CancellationToken.None);
        Assert.That(fast, Is.False, "no face crosses another: the fast form reports nothing");
        Assert.That(fastPairs, Is.Empty);

        bool precise = Intersection.MeshMeshPredicate(new[] { a, b }, null, Tolerance, false, out int[] precisePairs, out int[] faces, log, System.Threading.CancellationToken.None);
        Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
        Assert.That(precise, Is.True, "coplanar overlap is an intersection for the precise form");
        Assert.That(Couples(precisePairs), Is.EquivalentTo(new[] { "0-1" }));
        Assert.That(faces.Length, Is.EqualTo(2));
      }
    }

    [Test]
    public void TwoSets_IndicesAreIntoEachSet()
    {
      SetupFixture.Prerequisites();
      using (var a = Box(0, 0, 0, 1, 1, 1))
      using (var far = Box(10, 10, 10, 11, 11, 11))
      using (var b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5))
      using (var log = new TextLog())
      {
        bool rc = Intersection.MeshMeshPredicate(new[] { a }, new[] { far, b }, Tolerance, true, out int[] pairs, out int[] faces, log, System.Threading.CancellationToken.None);
        Assert.That(log.ToString(), Does.Not.StartWith("Error:"), log.ToString());
        Assert.That(rc, Is.True);
        Assert.That(pairs, Is.EqualTo(new[] { 0, 1 }), "(index in first set, index in second set)");
        Assert.That(faces.Length, Is.EqualTo(2));
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
