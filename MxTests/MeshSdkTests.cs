using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.IO;

namespace MxTests
{
  [TestFixture]
  public class MeshSdkTests
  {
    [TestCase(0.0, false, 0.5, 0.5, -1.0, 0.0, 0.0, 1.0, ExpectedResult = 1.0)]
    [TestCase(0.0, true, 0.5, 0.5, -1.0, 0.0, 0.0, 1.0, ExpectedResult = 1.0)]

    [TestCase(-100.0, false, -99.5, -99.5, -101.0, 0.0, 0.0, 1.0, ExpectedResult = 1.0)]
    [TestCase(-100.0, true, -99.5, -99.5, -101.0, 0.0, 0.0, 1.0, ExpectedResult = 1.0)]

    [TestCase(0.0, false, 0.5, 0.5, -2.0, 0.0, 0.0, 1.0, ExpectedResult = 2.0)]
    [TestCase(0.0, true, 0.5, 0.5, -2.0, 0.0, 0.0, 1.0, ExpectedResult = 2.0)]

    [TestCase(-100.0, false, -99.5, -99.5, -102.0, 0.0, 0.0, 1.0, ExpectedResult = 2.0)]
    [TestCase(-100.0, true, -99.5, -99.5, -102.0, 0.0, 0.0, 1.0, ExpectedResult = 2.0)]

    [TestCase(0.0, false, 0.5, 0.5, -1.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]
    [TestCase(0.0, true, 0.5, 0.5, -1.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]

    [TestCase(-100.0, false, -99.5, -99.5, -101.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]
    [TestCase(-100.0, true, -99.5, -99.5, -101.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]

    [TestCase(0.0, false, 0.5, 0.5, -2.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]
    [TestCase(0.0, true, 0.5, 0.5, -2.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]

    [TestCase(-100.0, false, -99.5, -99.5, -102.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]
    [TestCase(-100.0, true, -99.5, -99.5, -102.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]

    [TestCase(0.0, false, 1.0, 0.50001, 1.0, 0.0, 0.0, -1.0, ExpectedResult = double.NegativeInfinity)]
    [TestCase(0.0, false, 1.0, 0.5, 1.0, 0.0, 0.0, -1.0, ExpectedResult = 1.0)]
    [TestCase(0.0, false, 0.9999, 0.49999, 1.0, 0.0, 0.0, -1.0, ExpectedResult = 1.0)]
    public double IntersectionMeshRay(double meshOffset, bool meshFlip, double x, double y, double z, double vx, double vy, double vz)
    {
      return MinorImplmentations.IntersectionMeshRay(meshOffset, meshFlip, x, y, z, vx, vy, vz);
    }

    [TestCase(1.0, 10, 10, 0.0, 0.0, 0.0, ExpectedResult = true)]
    [TestCase(100.0, 10, 10, 99, 99, 99, ExpectedResult = false)]

    [TestCase(100.0, 100, 100, 70, 70, 0, ExpectedResult = true)]
    [TestCase(99.0, 100, 100, 70, 70, 0, ExpectedResult = false)]
    public bool MeshIsPointInside(double radius, int u, int v, double x, double y, double z)
    {
      return MinorImplmentations.MeshIsPointInside(radius, u, v, x, y, z);
    }

    [Test]
    public void MeshRay()
    {
      MinorImplmentations.MeshRay();
    }

    [Test]
    public void MeshRayOther()
    {
      MinorImplmentations.MeshLineMiss();
      MinorImplmentations.MeshRay_RH62807();
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

      internal static bool MeshIsPointInside(double radius, int u, int v, double x, double y, double z)
      {
        using (var sphere = Mesh.CreateFromSphere(new Sphere(new Point3d(), radius), u, v))
        {
          return sphere.IsPointInside(new Point3d(x, y, z), 0.0, true);
        }
      }

      internal static void MeshRay()
      {
        Random random = new Random(123);

        for (int i = 0; i < 100; i++)
        {
          Point3d center = new Point3d(random.NextDouble(), random.NextDouble(), random.NextDouble());

          using (var largesphere = Mesh.CreateFromSphere(new Sphere(center, 3), 30, 30))
          {
            using (var smallsphere = Mesh.CreateFromSphere(new Sphere(center, 2.7), 30, 30))
              largesphere.Append(smallsphere);

            for (int j = 0; j < 1000; j++)
            {
              var q = new Quaternion(random.NextDouble(), random.NextDouble(), random.NextDouble(), random.NextDouble());
              q.Unitize();
              q.GetRotation(out _, out Vector3d vector);
              vector.Unitize();
              vector *= 4;
              Line line = new Line(center, center + vector);

              Point3d[] points = Intersection.MeshLine(largesphere, line, out _);

              if (points.Length != 2)
              {
                string message;
                using (File3dm model = new File3dm())
                {
                  model.Objects.AddMesh(largesphere);
                  model.Objects.AddLine(line);
                  for (int k = 0; k < points.Length; k++)
                    model.Objects.AddPoint(points[k]);
                  message = Path.Combine(OpenRhinoSetup.SettingsDir,
                    "MeshRay_" +
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" +
                    j.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ".3dm");
                  model.Write(message, 5);
                }
                Console.WriteLine(message);
              }

              Assert.AreEqual(2, points.Length);
            }
          }
        }
      }

      internal static void MeshLineMiss()
      {
        Point3d center = new Point3d(0,0,0);
        var mesh = Mesh.CreateFromSphere(new Sphere(center, 1), 10, 10);
        Line line = new Line(20,0,0, 30,0,0);

        var points = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, line, out int[] faceIds);

        Assert.IsEmpty(faceIds);
        Assert.IsEmpty(points);
      }

      internal static void MeshRay_RH62807()
      {
        Point3d[] points;
        int[] faceIds;
        var mesh = new Mesh();

        mesh.Vertices.Add(0.0, 0.0, 0.0);
        mesh.Vertices.Add(1.0, 0.0, 0.0);
        mesh.Vertices.Add(1.0, 1.0, 0.0);
        mesh.Faces.AddFace(0, 1, 2);

        Line line = new Line(new Point3d(0.9, 0.1, -1.0), new Point3d(0.9, 0.1, 1.0));

        points = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, line, out faceIds);

        Assert.That(points, Has.Length.EqualTo(1));
        Assert.Contains(new Point3d(0.9, 0.1, 0), points);
        Assert.That(faceIds, Has.Length.EqualTo(1));
        Assert.Contains(0, faceIds);

        mesh.Dispose();

        mesh = new Mesh();
        mesh.Vertices.Add(0, 0, 1.0);
        mesh.Vertices.Add(0.0, 0.0, 0.0);
        mesh.Vertices.Add(1.0, 0.0, 0.0);
        mesh.Vertices.Add(1.0, 1.0, 0.0);
        mesh.Faces.AddFace(0, 1, 2);
        mesh.Faces.AddFace(0, 1, 3);
        mesh.Faces.AddFace(2, 1, 3);
        mesh.Faces.AddFace(0, 2, 3);

        points = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, line, out faceIds);

        Assert.That(points, Has.Length.EqualTo(2));
        Assert.AreEqual(new Point3d(0.9, 0.1, 0.0).DistanceTo(points[0]), 0.0, 1e-12);
        Assert.AreEqual(new Point3d(0.9, 0.1, 0.1).DistanceTo(points[1]), 0.0, 1e-12);
        Assert.That(faceIds, Has.Length.EqualTo(2));
        Assert.Contains(2, faceIds);
        Assert.Contains(3, faceIds);

        mesh.Dispose();
      }
    }
  }
}
