using NUnit.Framework;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace MxTests
{
  [TestFixture]
  public class MeshSdkTests
  {
    [TestCase(0.0, false,   0.5, 0.5, -1.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]
    [TestCase(0.0, true,   0.5, 0.5, -1.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]

    [TestCase(-100.0, false,  -99.5, -99.5, -101.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]
    [TestCase(-100.0, true,   -99.5, -99.5, -101.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]

    [TestCase(0.0, false,   0.5, 0.5, -2.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]
    [TestCase(0.0, true,   0.5, 0.5, -2.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]

    [TestCase(-100.0, false,  -99.5, -99.5, -102.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]
    [TestCase(-100.0, true,   -99.5, -99.5, -102.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]

    [TestCase(0.0, false, 0.5, 0.5, -1.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(0.0, true, 0.5, 0.5, -1.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(-100.0, false, -99.5, -99.5, -101.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(-100.0, true, -99.5, -99.5, -101.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(0.0, false, 0.5, 0.5, -2.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(0.0, true, 0.5, 0.5, -2.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(-100.0, false, -99.5, -99.5, -102.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(-100.0, true, -99.5, -99.5, -102.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(0.0, false, 1.0, 0.50001, 1.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(0.0, false, 1.0, 0.5, 1.0, 0.0, 0.0, -1.0, ExpectedResult = 1.0)]
    [TestCase(0.0, false, 0.9999, 0.49999, 1.0, 0.0, 0.0, -1.0, ExpectedResult = 1.0)]
    public double IntersectionMeshRay(double meshOffset, bool meshFlip,   double x, double y, double z,   double vx, double vy, double vz)
    {
      return MinorImplmentations.IntersectionMeshRay(meshOffset, meshFlip, x, y, z, vx, vy, vz);
    }

    [TestCase(1.0, 10, 10, 0.0,0.0,0.0, ExpectedResult = true)]
    [TestCase(100.0, 10, 10, 99,99,99, ExpectedResult = false)]

    [TestCase(100.0, 100, 100, 70,70,0, ExpectedResult = true)]
    [TestCase(99.0, 100, 100, 70, 70, 0, ExpectedResult = false)]
    public bool MeshIsPointInside(double radius, int u, int v, double x, double y, double z)
    {
      return MinorImplmentations.MeshIsPointInside(radius, u, v, x, y, z);
    }

    internal static class MinorImplmentations
    {
      public static double IntersectionMeshRay(
        double meshOffset, bool meshFlip,
        double x, double y, double z,
        double vx, double vy, double vz)
      {
        using (var m = new Mesh())
        {
          m.Vertices.Add(meshOffset, meshOffset, meshOffset);
          m.Vertices.Add(meshOffset + 1.0, meshOffset + 0.5, meshOffset);
          m.Vertices.Add(meshOffset + 0.5, meshOffset + 1.0, meshOffset);

          if (meshFlip)
            m.Faces.AddFace(new MeshFace(0, 1, 2));
          else
            m.Faces.AddFace(new MeshFace(0, 2, 1));

          Ray3d ray = new Ray3d(new Point3d(x, y, z), new Vector3d(vx, vy, vz));

          return Intersection.MeshRay(m, ray);
        }
      }

      public static bool MeshIsPointInside(double radius, int u, int v, double x, double y, double z)
      {
        using (var sphere = Mesh.CreateFromSphere(new Sphere(new Point3d(), radius), u, v))
        {
          return sphere.IsPointInside(new Point3d(x, y, z), 0.0, true);
        }
      }
    }
  }
}
