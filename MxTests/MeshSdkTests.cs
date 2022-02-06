using NUnit.Framework;
using Rhino.Collections;
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
      MinorImplmentations.MeshRayMultiple();
    }

    [Test]
    public void MeshLineOther()
    {
      MinorImplmentations.MeshLine_RH62831();
    }

    [Test]
    public void CenterBoxCreateContourCurvesTests([Values(0.1, 1, 10, 100)] double size, [Range(0, 360, 22.5)] double angle, [Values(2, 4, 6, 8, 10)] double dist)
    {
      MinorImplmentations.CheckCenterBoxWithSizeAndOneHorizontalPlane(size);
      MinorImplmentations.CheckCenterBoxWithSizeAndOneRotatedPlane(size, angle);
      MinorImplmentations.CheckCenterBoxWithSizeAndSeveralHorizontalPlanes(size, dist);
    }

    [Test]
    public void SphereCreateContourCurvesTest([Values(1, 10, 100)] double size, [Range(1, 360, 18)] double angle, [Values(2, 4, 6, 8, 10)] double dist)
    {
      MinorImplmentations.CheckSphereWithRadiusAndOneHorizontalPlane(size);
      MinorImplmentations.CheckSphereWithRadiusAndOneRotatedPlane(size, angle);
      MinorImplmentations.CheckSphereWithRadiusAndSeveralHorizontalPlanes(size, dist);
    }

    [Test]
    public void RectangleCreateContourCurvesTest([Values(0.1, 1, 10, 100)] double width, [Values(0.1, 1, 10, 100)] double height/*, [Range(0, 360, 18)] double angle*/)
    {
      MinorImplmentations.CheckRectangleWithDifferentSidesAndOneHorizontalPlane(width, height);
      //MinorImplmentations.CheckRotatedRectangleWithDifferentSidesAndOneHorizontalPlane(width, height, angle);
    }

    [Test]
    public void CheckWindingTests([Range(-80, 80, 5)] double angleX, [Range(-80, 80, 5)] double angleY)
    {
      MinorImplmentations.CheckWindingInSimpleCurve();
      MinorImplmentations.CheckWindingInFlippedCurve();
      MinorImplmentations.CheckWindingInRotatedCurve(angleX, angleY);
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
        Point3d center = new Point3d(0, 0, 0);
        using (var mesh = Mesh.CreateFromSphere(new Sphere(center, 1), 10, 10))
        {
          Line line = new Line(20, 0, 0, 30, 0, 0);

          var points = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, line, out int[] faceIds);

          Assert.IsEmpty(faceIds);
          Assert.IsEmpty(points);
        }
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

      internal static void MeshRayMultiple()
      {
        string meshtext = "{\"version\":10000,\"archive3dm\":70,\"opennurbs\":-1911523059,\"data\":\"+n8CAHYDAAAAAAAA+/8CABQAAAAAAAAA5NTXTkfp0xG/5QAQgwEi8C25G1z8/wIAPgMAAAAAAAA4CgAAAAYAAADSHTOevfjl/9IdM569+OX/0h0znr345f/SHTOevfjl/9IdM569+OX/0h0znr345f/SHTOevfjl/9IdM569+OX/AAAAAAAAAAAAAAAAAAAAAAEA4MABAMDAAAAAAAAAAEEBAKBAAAAAAAAAgD8AAIA/AACAPwAAgL8AAIC/AACAvwAAgD8AAIA/AACAvwAAgL8AAAAAAAAAAAABAAAABQEACAUEAQECBAUDBQgHBgMFCQkJBQYGeAAAAJEjC5IAAAAAQQAAAAAAAAAAAADAQAAAAEAAAAAAAADgwAAAAEAAAAAAAACAvwAAgL8AAAAAAABAwAAAoEAAAAAAAACAPwAAAAAAAAAAAABAQAAAoMAAAAAAAACgQAAAgMAAAAAAAACgQAAAAMAAAAAAAABAwAAAwMAAAAAAeAAAACsKiYoAAAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAIC/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAQKQAAAAAAAAAAQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPA/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADwPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8D8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPA/AAAAAGk8mKMAAAMAAQCAAEBlAAAAAAAAAAEAAAAAAAAACgAAAPAAAAAw8YKiAQCAAEBAAAAAAAAAAHjaY2AAAQUHBqxAAiaOJi9zALv4h/2oNAxwQNWLoKu3x24vB1SdyAFUcZh+ARziDAew2ysBFwcAqDgMsn5YH8f+CwvkAAAAAAAAHMAAAAAAAAAYwAAAAAAAAAAAAAAAAAAAIEAAAAAAAAAUQAAAAAAAAAAA25435P9/AoAAAAAAAAAAAA==\"}";

        System.Web.Script.Serialization.JavaScriptSerializer serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        Mesh m = (Mesh)Mesh.FromJSON(serializer.Deserialize<Dictionary<string, string>>(meshtext));

        Rhino.Geometry.Intersect.Intersection.MeshRay(m, new Ray3d(new Point3d(1, 0, 0.3), new Vector3d(0, 0, -1)), out int[] faces);

        Assert.That(faces, Has.Length.EqualTo(6));
        Assert.That(faces, Contains.Item(0));
        Assert.That(faces, Contains.Item(1));
        Assert.That(faces, Contains.Item(2));
        Assert.That(faces, Contains.Item(3));
        Assert.That(faces, Contains.Item(4));
        Assert.That(faces, Contains.Item(5));
      }

      internal static void MeshLine_RH62831()
      {
        var lines = new List<Line>
        {
          new Line(-8, -8, 0, -8, -8, 34.6410161513775),
          new Line(-8, -6, 0, -8, -6, 34.6410161513775),
          new Line(-8, -4, 0, -8, -4, 34.6410161513775),
          new Line(-8, -2, 0, -8, -2, 34.6410161513775),
          new Line(-8, 0, 0, -8, 0, 34.6410161513775),
          new Line(-8, 2, 0, -8, 2, 34.6410161513775),
          new Line(-8, 4, 0, -8, 4, 34.6410161513775),
          new Line(-8, 6, 0, -8, 6, 34.6410161513775),
          new Line(-8, 8, 0, -8, 8, 34.6410161513775),
          new Line(-6, -8, 0, -6, -8, 34.6410161513775),
          new Line(-6, -6, 0, -6, -6, 34.6410161513775),
          new Line(-6, -4, 0, -6, -4, 34.6410161513775),
          new Line(-6, -2, 0, -6, -2, 34.6410161513775),
          new Line(-6, 0, 0, -6, 0, 34.6410161513775),
          new Line(-6, 2, 0, -6, 2, 34.6410161513775),
          new Line(-6, 4, 0, -6, 4, 34.6410161513775),
          new Line(-6, 6, 0, -6, 6, 34.6410161513775),
          new Line(-6, 8, 0, -6, 8, 34.6410161513775),
          new Line(-4, -8, 0, -4, -8, 34.6410161513775),
          new Line(-4, -6, 0, -4, -6, 34.6410161513775),
          new Line(-4, -4, 0, -4, -4, 34.6410161513775),
          new Line(-4, -2, 0, -4, -2, 34.6410161513775),
          new Line(-4, 0, 0, -4, 0, 34.6410161513775),
          new Line(-4, 2, 0, -4, 2, 34.6410161513775),
          new Line(-4, 4, 0, -4, 4, 34.6410161513775),
          new Line(-4, 6, 0, -4, 6, 34.6410161513775),
          new Line(-4, 8, 0, -4, 8, 34.6410161513775),
          new Line(-2, -8, 0, -2, -8, 34.6410161513775),
          new Line(-2, -6, 0, -2, -6, 34.6410161513775),
          new Line(-2, -4, 0, -2, -4, 34.6410161513775),
          new Line(-2, -2, 0, -2, -2, 34.6410161513775),
          new Line(-2, 0, 0, -2, 0, 34.6410161513775),
          new Line(-2, 2, 0, -2, 2, 34.6410161513775),
          new Line(-2, 4, 0, -2, 4, 34.6410161513775),
          new Line(-2, 6, 0, -2, 6, 34.6410161513775),
          new Line(-2, 8, 0, -2, 8, 34.6410161513775),
          new Line(0, -8, 0, 0, -8, 34.6410161513775),
          new Line(0, -6, 0, 0, -6, 34.6410161513775),
          new Line(0, -4, 0, 0, -4, 34.6410161513775),
          new Line(0, -2, 0, 0, -2, 34.6410161513775),
          new Line(0, 0, 0, 0, 0, 34.6410161513775),
          new Line(0, 2, 0, 0, 2, 34.6410161513775),
          new Line(0, 4, 0, 0, 4, 34.6410161513775),
          new Line(0, 6, 0, 0, 6, 34.6410161513775),
          new Line(0, 8, 0, 0, 8, 34.6410161513775),
          new Line(2, -8, 0, 2, -8, 34.6410161513775),
          new Line(2, -6, 0, 2, -6, 34.6410161513775),
          new Line(2, -4, 0, 2, -4, 34.6410161513775),
          new Line(2, -2, 0, 2, -2, 34.6410161513775),
          new Line(2, 0, 0, 2, 0, 34.6410161513775),
          new Line(2, 2, 0, 2, 2, 34.6410161513775),
          new Line(2, 4, 0, 2, 4, 34.6410161513775),
          new Line(2, 6, 0, 2, 6, 34.6410161513775),
          new Line(2, 8, 0, 2, 8, 34.6410161513775),
          new Line(4, -8, 0, 4, -8, 34.6410161513775),
          new Line(4, -6, 0, 4, -6, 34.6410161513775),
          new Line(4, -4, 0, 4, -4, 34.6410161513775),
          new Line(4, -2, 0, 4, -2, 34.6410161513775),
          new Line(4, 0, 0, 4, 0, 34.6410161513775),
          new Line(4, 2, 0, 4, 2, 34.6410161513775),
          new Line(4, 4, 0, 4, 4, 34.6410161513775),
          new Line(4, 6, 0, 4, 6, 34.6410161513775),
          new Line(4, 8, 0, 4, 8, 34.6410161513775),
          new Line(6, -8, 0, 6, -8, 34.6410161513775),
          new Line(6, -6, 0, 6, -6, 34.6410161513775),
          new Line(6, -4, 0, 6, -4, 34.6410161513775),
          new Line(6, -2, 0, 6, -2, 34.6410161513775),
          new Line(6, 0, 0, 6, 0, 34.6410161513775),
          new Line(6, 2, 0, 6, 2, 34.6410161513775),
          new Line(6, 4, 0, 6, 4, 34.6410161513775),
          new Line(6, 6, 0, 6, 6, 34.6410161513775),
          new Line(6, 8, 0, 6, 8, 34.6410161513775),
          new Line(8, -8, 0, 8, -8, 34.6410161513775),
          new Line(8, -6, 0, 8, -6, 34.6410161513775),
          new Line(8, -4, 0, 8, -4, 34.6410161513775),
          new Line(8, -2, 0, 8, -2, 34.6410161513775),
          new Line(8, 0, 0, 8, 0, 34.6410161513775),
          new Line(8, 2, 0, 8, 2, 34.6410161513775),
          new Line(8, 4, 0, 8, 4, 34.6410161513775),
          new Line(8, 6, 0, 8, 6, 34.6410161513775),
          new Line(8, 8, 0, 8, 8, 34.6410161513775)
        };

        var points = new Point3dList
        {
          { -8, -4, 1.72511018308212 },
          { -8, -2, 4.44099373713433 },
          { -8, 0, 5.68082254597788 },
          { -8, 2, 4.44099373713433 },
          { -8, 4, 1.72511018308212 },
          { -6, -6, 4.95454764477821 },
          { -6, -4, 6.19437645362176 },
          { -6, -2, 7.23319513568864 },
          { -6, 0, 7.78673244351695 },
          { -6, 2, 7.23319513568864 },
          { -6, 4, 6.19437645362176 },
          { -6, 6, 4.95454764477821 },
          { -4, -8, 1.72511018308213 },
          { -4, -6, 6.19437645362176 },
          { -4, -4, 8.01601510369893 },
          { -4, -2, 8.56955241152724 },
          { -4, 0, 9.12308971935555 },
          { -4, 2, 8.56955241152724 },
          { -4, 4, 8.01601510369893 },
          { -4, 6, 6.19437645362176 },
          { -4, 8, 1.72511018308213 },
          { -2, -8, 4.44099373713434 },
          { -2, -6, 7.23319513568864 },
          { -2, -4, 8.56955241152724 },
          { -2, -2, 9.43739086465589 },
          { -2, 0, 9.60217526524068 },
          { -2, 2, 9.4373908646559 },
          { -2, 4, 8.56955241152724 },
          { -2, 6, 7.23319513568864 },
          { -2, 8, 4.44099373713433 },
          { 0, -8, 5.68082254597788 },
          { 0, -6, 7.78673244351695 },
          { 0, -4, 9.12308971935555 },
          { 0, -2, 9.60217526524069 },
          { 0, 0, 10 },
          { 0, 2, 9.60217526524068 },
          { 0, 4, 9.12308971935555 },
          { 0, 6, 7.78673244351695 },
          { 0, 8, 5.68082254597788 },
          { 2, -8, 4.44099373713433 },
          { 2, -6, 7.23319513568864 },
          { 2, -4, 8.56955241152724 },
          { 2, -2, 9.43739086465589 },
          { 2, 0, 9.60217526524068 },
          { 2, 2, 9.43739086465589 },
          { 2, 4, 8.56955241152724 },
          { 2, 6, 7.23319513568864 },
          { 2, 8, 4.44099373713433 },
          { 4, -8, 1.72511018308212 },
          { 4, -6, 6.19437645362176 },
          { 4, -4, 8.01601510369893 },
          { 4, -2, 8.56955241152724 },
          { 4, 0, 9.12308971935555 },
          { 4, 2, 8.56955241152724 },
          { 4, 4, 8.01601510369893 },
          { 4, 6, 6.19437645362176 },
          { 4, 8, 1.72511018308212 },
          { 6, -6, 4.95454764477821 },
          { 6, -4, 6.19437645362176 },
          { 6, -2, 7.23319513568864 },
          { 6, 0, 7.78673244351695 },
          { 6, 2, 7.23319513568864 },
          { 6, 4, 6.19437645362176 },
          { 6, 6, 4.95454764477821 },
          { 8, -4, 1.72511018308212 },
          { 8, -2, 4.44099373713433 },
          { 8, 0, 5.68082254597788 },
          { 8, 2, 4.44099373713433 },
          { 8, 4, 1.72511018308212 }
        };

        Point3dList results;

        using (var sphere = Mesh.CreateFromSphere(new Sphere(Point3d.Origin, 10), 8, 8))
        {
          results = new Rhino.Collections.Point3dList();
          for (int i = 0; i < lines.Count; i++)
          {
            var callA = Rhino.Geometry.Intersect.Intersection.MeshLine(sphere, lines[i]);
            var callB = Rhino.Geometry.Intersect.Intersection.MeshLine(sphere, lines[i], out _);
            results.AddRange(callA);

            Assert.That(callA.Length, Is.EqualTo(callB.Length));
            for (int j = 0; j < callA.Length; j++)
              Assert.That(callA[j], Is.EqualTo(callB[j]));
          }
        }

        //test1
        Assert.That(results, Has.Count.EqualTo(points.Count));

        //test2
        for (int i = 0; i < points.Count; i++)
        {
          Assert.That(results[i].DistanceTo(points[i]), Is.LessThan(1e-10));
        }
      }

      internal static void CheckCenterBoxWithSizeAndOneHorizontalPlane(double size)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var points = GeometryCollections.CreatePointsForCenterBoxOfSpecifiedSide(size);
        var plane = new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis);

        using (var mesh = Mesh.CreateFromBox(points, 1, 1, 1))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, plane, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, plane);
        };
        var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        Assert.AreEqual(1, numberOrientations);
      }

      internal static void CheckCenterBoxWithSizeAndOneRotatedPlane(double size, double angle)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var points = GeometryCollections.CreatePointsForCenterBoxOfSpecifiedSide(size);
        var plane = new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis);
        plane.Rotate(angle, Vector3d.XAxis);
        using (var mesh = Mesh.CreateFromBox(points, 1, 1, 1))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, plane, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, plane);
        }
        var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        Assert.AreEqual(1, numberOrientations);
      }

      internal static void CheckCenterBoxWithSizeAndSeveralHorizontalPlanes(double size, double dist)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var points = GeometryCollections.CreatePointsForCenterBoxOfSpecifiedSide(size);
        var sizeScaled = size * 0.95;
        var numberPlanes = (int)(sizeScaled / dist) + 1;
        var planes = GeometryCollections.CreateSetOfHorizontalPlanes(-sizeScaled / 2, numberPlanes, dist);

        using (var mesh = Mesh.CreateFromBox(points, 1, 1, 1))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, new Point3d(0, 0, -sizeScaled / 2), new Point3d(0, 0, sizeScaled / 2), dist, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, planes);
        }
        //var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        //Assert.AreEqual(1, numberOrientations);
      }

      internal static void CheckSphereWithRadiusAndOneHorizontalPlane(double radius)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var sphere = new Sphere(Point3d.Origin, radius);
        var plane = new Plane(Point3d.Origin, Vector3d.ZAxis);
        using (var mesh = Mesh.CreateFromSphere(sphere, 6, 6))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, plane, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, plane);
        }
        var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        Assert.AreEqual(1, numberOrientations);
      }

      internal static void CheckSphereWithRadiusAndOneRotatedPlane(double radius, double angle)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var sphere = new Sphere(Point3d.Origin, radius);
        var plane = new Plane(Point3d.Origin, Vector3d.ZAxis);
        var angleRadians = (Math.PI / 180) * angle;
        plane.Rotate(angleRadians, Vector3d.XAxis);

        using (var mesh = Mesh.CreateFromSphere(sphere, 10, 10))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, plane, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, plane);
        }
        var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        Assert.AreEqual(1, numberOrientations);
      }

      internal static void CheckSphereWithRadiusAndSeveralHorizontalPlanes(double radius, double dist)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var radiusScaled = radius * 0.95;
        var numberPlanes = (int)(radiusScaled / dist) + 1;
        var sphere = new Sphere(Point3d.Origin, radius);
        var planes = GeometryCollections.CreateSetOfHorizontalPlanes(-radiusScaled / 2, numberPlanes, dist);

        using (var mesh = Mesh.CreateFromSphere(sphere, 6, 6))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, new Point3d(0, 0, -radiusScaled / 2), new Point3d(0, 0, radiusScaled / 2), dist, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, planes);
        }
        var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        Assert.AreEqual(1, numberOrientations);
      }

      internal static bool UseNewMeshIntersections
      {
        get
        {
          return (bool)typeof(Intersection).GetMethod("get_UseNewMeshIntersections",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, Array.Empty<object>());
        }
        set
        {
          typeof(Intersection).GetMethod("set_UseNewMeshIntersections",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        .Invoke(null, new object[] { value });
        }
      }

      internal static void CheckRectangleWithDifferentSidesAndOneHorizontalPlane(double width, double height)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var planeRect = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        var wInterval = new Interval(-width / 2, width / 2);
        var hInterval = new Interval(-height / 2, height / 2);
        var rect = new Rectangle3d(planeRect, wInterval, hInterval);
        var plane = new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis);

        using (var mesh = Mesh.CreateFromClosedPolyline(rect.ToPolyline()))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, plane, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, plane);

          var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

          try
          {
            //Assert
            Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
            Assert.AreEqual(1, numberOrientations);
          }
          catch (Exception ex)
          {
            DumpObjects(
              new GeometryBase[] { new Point(plane.Origin), mesh },
              crvsArray,
              polylinesArray.Select(pl => pl.ToPolylineCurve()));
            throw;
          }
        }
      }

      public static void DumpObjects(IEnumerable<GeometryBase> data = null, IEnumerable<GeometryBase> expected = null, IEnumerable<GeometryBase> result = null)
      {
        using (var dump = new File3dm())
        {
          int data_layer = dump.AllLayers.AddLayer("data", System.Drawing.Color.Black);
          int expected_layer = dump.AllLayers.AddLayer("expected", System.Drawing.Color.DarkBlue);
          int result_layer = dump.AllLayers.AddLayer("result", System.Drawing.Color.Red);

          using (var oa = new Rhino.DocObjects.ObjectAttributes())
          {
            oa.LayerIndex = data_layer;
            foreach (var geom in data) dump.Objects.Add(geom, oa);

            oa.LayerIndex = expected_layer;
            foreach (var geom in expected) dump.Objects.Add(geom, oa);

            oa.LayerIndex = result_layer;
            foreach (var geom in result) dump.Objects.Add(geom, oa);
          }

          dump.Write(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "dump.3dm"),
            new File3dmWriteOptions() { SaveAnalysisMeshes = false, SaveRenderMeshes = false, SaveUserData = false, Version = 6 }
            );
        }
      }

      internal static void CheckRotatedRectangleWithDifferentSidesAndOneHorizontalPlane(double width, double height, double angle)
      {
        //Arrange
        Curve[] crvsArray;
        Polyline[] polylinesArray;
        var planeRect = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        var wInterval = new Interval(-width / 2, width / 2);
        var hInterval = new Interval(-height / 2, height / 2);

        var angleRadians = (Math.PI / 180) * angle;
        planeRect.Rotate(angleRadians, Vector3d.ZAxis);

        var rect = new Rectangle3d(planeRect, wInterval, hInterval);

        var plane = new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis);

        using (var mesh = Mesh.CreateFromClosedPolyline(rect.ToPolyline()))
        {
          //Act
          crvsArray = Mesh.CreateContourCurves(mesh, plane, 1e-7);
          polylinesArray = Intersection.MeshPlane(mesh, plane);
        }
        var numberOrientations = GetNumberOfCurveOrientationEnum(crvsArray);

        //Assert
        Assert.AreEqual(crvsArray.Length, polylinesArray.Length);
        Assert.AreEqual(1, numberOrientations);
      }

      internal static void CheckWindingInSimpleCurve()
      {
        //Arrange
        CurveOrientation orientation;
        using (var crv = GeometryCollections.CreatePlanarInterpolatedCurve())
        {
          crv.Domain = new Interval(0.0, 1.0);

          var pt0 = crv.PointAt(0.0);
          var pt1 = crv.PointAt(0.25);
          var pt2 = crv.PointAt(0.75);

          var vtX = new Vector3d(pt1 - pt0);
          var vtY = new Vector3d(pt2 - pt0);

          var plane = new Plane(pt0, vtX, vtY);

          //Act
          orientation = crv.ClosedCurveOrientation(plane.ZAxis);
        }

        //Assert
        Assert.AreEqual(CurveOrientation.CounterClockwise, orientation);
      }

      internal static void CheckWindingInFlippedCurve()
      {
        //Arrange
        CurveOrientation orientation;
        using (var crv = GeometryCollections.CreatePlanarInterpolatedCurve())
        {
          crv.Domain = new Interval(0.0, 1.0);
          crv.Reverse();

          var pt0 = crv.PointAt(0.0);
          var pt1 = crv.PointAt(0.25);
          var pt2 = crv.PointAt(0.75);

          var vtX = new Vector3d(pt1 - pt0);
          var vtY = new Vector3d(pt2 - pt0);

          var plane = new Plane(pt0, vtX, vtY);

          //Act
          orientation = crv.ClosedCurveOrientation(plane.ZAxis);
        }

        //Assert
        Assert.AreEqual(CurveOrientation.Clockwise, orientation);
      }


      internal static void CheckWindingInRotatedCurve(double angleX, double angleY)
      {
        //Arrange
        CurveOrientation orientation;

        using (var crv = GeometryCollections.CreatePlanarInterpolatedCurve())
        {
          crv.Domain = new Interval(0.0, 1.0);

          var pt0 = crv.PointAt(0.0);
          var angleRadiansY = (Math.PI / 180) * angleY;
          var angleRadiansX = (Math.PI / 180) * angleX;
          crv.Rotate(angleRadiansX, Vector3d.XAxis, pt0);
          crv.Rotate(angleRadiansY, Vector3d.YAxis, pt0);

          var pt1 = crv.PointAt(0.25);
          var pt2 = crv.PointAt(0.75);

          var vtX = new Vector3d(pt1 - pt0);
          var vtY = new Vector3d(pt2 - pt0);

          var plane = new Plane(pt0, vtX, vtY);

          //Act
          orientation = crv.ClosedCurveOrientation(plane.ZAxis);
        }

        //Assert
        Assert.AreEqual(CurveOrientation.CounterClockwise, orientation);
      }

      internal static int GetNumberOfCurveOrientationEnum(Curve[] curves)
      {
        var opt = curves.ToList().Select(x => (int)Enum.Parse(typeof(CurveOrientation), x.ClosedCurveOrientation().ToString()));
        var hashSet = new HashSet<int>(opt);
        return hashSet.Count;
      }

    }

    internal static class GeometryCollections
    {
      internal static IEnumerable<Point3d> CreatePointsForCubeOfSpecifiedSide(double side)
      {
        var points = new List<Point3d>
        {
          new Point3d(0, 0, 0),
          new Point3d(side, 0, 0),
          new Point3d(side, side, 0),
          new Point3d(0, side, 0),
          new Point3d(0, 0, side),
          new Point3d(side, 0, side),
          new Point3d(side, side, side),
          new Point3d(0, side, side),
        };
        return points;
      }
      internal static IEnumerable<Point3d> CreatePointsForCenterBoxOfSpecifiedSide(double side)
      {
        var points = new List<Point3d>
        {
          new Point3d(-side/2, -side/2, -side/2),
          new Point3d(side/2, -side/2, -side/2),
          new Point3d(side/2, side/2, -side/2),
          new Point3d(-side/2, side/2, -side/2),
          new Point3d(-side/2, -side/2, side/2),
          new Point3d(side/2, -side/2, side/2),
          new Point3d(side/2, side/2, side/2),
          new Point3d(-side/2, side/2, side/2),
        };
        return points;
      }
      internal static IEnumerable<Plane> CreateSetOfHorizontalPlanes(double initZ, int number, double dist)
      {
        var planes = new List<Plane>();
        for (int i = 0; i < number; i++)
        {
          var z = initZ + (i * dist);
          planes.Add(new Plane(new Point3d(0, 0, z), Vector3d.ZAxis));
        }
        return planes;
      }
      internal static Curve CreatePlanarInterpolatedCurve()
      {
        var points = new List<Point3d>
          {
          new Point3d(0, 10, 0),
          new Point3d(-5, 3, 0),
          new Point3d(-10, -10, 0),
          new Point3d(2, -15, 0),
          new Point3d(10, -2, 0),
          new Point3d(12, 5, 0),
          new Point3d(16, 7, 0),
          new Point3d(20, 2, 0),
        };
        var crv = Curve.CreateInterpolatedCurve(points, 3);
        return crv;
      }
    }

  }
}
