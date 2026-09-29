using NUnit.Framework;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace MxTests
{
  // RH-99019: Intersection.MeshMeshPredicate reports each intersecting mesh pair as one IntersectingMeshPair.
  [TestFixture]
  public class MeshMeshPredicateSdkTests
  {
    const double Tolerance = 1e-7;

    static Mesh Box(double x0, double y0, double z0, double x1, double y1, double z1)
    {
      return Mesh.CreateFromBox(new BoundingBox(new Point3d(x0, y0, z0), new Point3d(x1, y1, z1)), 1, 1, 1);
    }

    [Test]
    public void EachIntersectingPairNamesBothMeshesAndOneFaceOfEach()
    {
      SetupFixture.Prerequisites();
      using (Mesh a = Box(0, 0, 0, 1, 1, 1))
      using (Mesh far = Box(10, 10, 10, 11, 11, 11))
      using (Mesh b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5))
      {
        bool rc = Intersection.MeshMeshPredicate(new[] { a }, new[] { far, b }, Tolerance, true, out IntersectingMeshPair[] intersectingPairs, null, System.Threading.CancellationToken.None);

        Assert.That(rc, Is.True);
        Assert.That(intersectingPairs.Length, Is.EqualTo(1));
        var (meshIndexA, meshIndexB, faceIndexA, faceIndexB) = intersectingPairs[0];
        Assert.That(meshIndexA, Is.EqualTo(0), "index in the first set");
        Assert.That(meshIndexB, Is.EqualTo(1), "index in the second set");
        Assert.That(faceIndexA, Is.InRange(0, a.Faces.Count - 1));
        Assert.That(faceIndexB, Is.InRange(0, b.Faces.Count - 1));
      }
    }

    [Test]
    public void NullEntriesKeepTheirIndex()
    {
      SetupFixture.Prerequisites();
      using (Mesh a = Box(0, 0, 0, 1, 1, 1))
      using (Mesh far = Box(10, 10, 10, 11, 11, 11))
      using (Mesh b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5))
      {
        bool rcOneSet = Intersection.MeshMeshPredicate(new[] { a, null, b }, null, Tolerance, true, out IntersectingMeshPair[] oneSetPairs, null, System.Threading.CancellationToken.None);
        Assert.That(rcOneSet, Is.True);
        Assert.That(oneSetPairs.Length, Is.EqualTo(1));
        Assert.That(oneSetPairs[0].MeshIndexA, Is.EqualTo(0));
        Assert.That(oneSetPairs[0].MeshIndexB, Is.EqualTo(2), "the null at index 1 keeps its place");

        bool rcTwoSets = Intersection.MeshMeshPredicate(new[] { null, a }, new[] { null, far, b }, Tolerance, true, out IntersectingMeshPair[] twoSetsPairs, null, System.Threading.CancellationToken.None);
        Assert.That(rcTwoSets, Is.True);
        Assert.That(twoSetsPairs.Length, Is.EqualTo(1));
        Assert.That(twoSetsPairs[0].MeshIndexA, Is.EqualTo(1), "index in the first set, null included");
        Assert.That(twoSetsPairs[0].MeshIndexB, Is.EqualTo(2), "index in the second set, null included");
      }
    }

    [Test]
    public void TheOverloadWithoutPairsGivesTheSameAnswer([Values(true, false)] bool fast)
    {
      SetupFixture.Prerequisites();
      using (Mesh a = Box(0, 0, 0, 1, 1, 1))
      using (Mesh b = Box(0.5, 0.5, 0.5, 1.5, 1.5, 1.5))
      using (Mesh far = Box(10, 10, 10, 11, 11, 11))
      {
        Assert.That(Intersection.MeshMeshPredicate(new[] { far, a, b }, null, Tolerance, fast, null, System.Threading.CancellationToken.None), Is.True, "one set");
        Assert.That(Intersection.MeshMeshPredicate(new[] { a }, new[] { far, b }, Tolerance, fast, null, System.Threading.CancellationToken.None), Is.True, "two sets");
        Assert.That(Intersection.MeshMeshPredicate(new[] { a, far }, null, Tolerance, fast, null, System.Threading.CancellationToken.None), Is.False, "disjoint");
      }
    }
  }
}
