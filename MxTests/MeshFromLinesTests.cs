using NUnit.Framework;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace MxTests
{
  [TestFixture]
  public class MeshFromLinesTests
  {
    static Curve[] EdgeLines(IList<Point3d> vertices, params int[] pairs)
    {
      var lines = new Curve[pairs.Length / 2];
      for (int i = 0; i < lines.Length; i++)
        lines[i] = new LineCurve(vertices[pairs[2 * i]], vertices[pairs[2 * i + 1]]);
      return lines;
    }

    static double MeshArea(Mesh mesh)
    {
      var amp = AreaMassProperties.Compute(mesh);
      Assert.That(amp, Is.Not.Null);
      return amp.Area;
    }

    [Test]
    public void BoxFromItsTwelveEdges()
    {
      SetupFixture.Prerequisites();
      // RH-97201. Every side of a box points 54.7 degrees away from the nearest direction
      // the sweep looks from, and seen from any of them the third edge at a corner falls on
      // the bisector of the corner it has to be told apart from. Ranking those two by the
      // angle between the projections keeps them apart; ranking them in 3D made them equal
      // and left the answer to the order the edges happened to be stored in.
      var corners = new[] {
        new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(1, 1, 0), new Point3d(0, 1, 0),
        new Point3d(0, 0, 1), new Point3d(1, 0, 1), new Point3d(1, 1, 1), new Point3d(0, 1, 1) };

      Curve[] lines = EdgeLines(corners,
        0, 1, 1, 2, 2, 3, 3, 0,
        4, 5, 5, 6, 6, 7, 7, 4,
        0, 4, 1, 5, 2, 6, 3, 7);

      foreach (int valence in new[] { 4, 5, 6 })
        using (Mesh mesh = Mesh.CreateFromLines(lines, valence, 1e-6))
        {
          Assert.That(mesh, Is.Not.Null, $"valence {valence}");
          Assert.That(mesh.Faces.QuadCount, Is.EqualTo(6), $"valence {valence}");
          Assert.That(mesh.Faces.TriangleCount, Is.EqualTo(0), $"valence {valence}");
          Assert.That(mesh.IsClosed, Is.True, $"valence {valence}");
          Assert.That(MeshArea(mesh), Is.EqualTo(6.0).Within(1e-9), $"valence {valence}");
        }
    }

    [Test]
    public void TriangulatedSolidsFromTheirEdges()
    {
      SetupFixture.Prerequisites();
      // At a maximum valence of three the coplanar walk is skipped, so these come out of
      // the angular sweep alone. Both solids are axis aligned, which is the arrangement
      // the sweep finds hardest.
      var octahedron = new[] {
        new Point3d(1, 0, 0), new Point3d(-1, 0, 0), new Point3d(0, 1, 0),
        new Point3d(0, -1, 0), new Point3d(0, 0, 1), new Point3d(0, 0, -1) };

      Curve[] octahedronLines = EdgeLines(octahedron,
        0, 2, 2, 1, 1, 3, 3, 0,
        0, 4, 2, 4, 1, 4, 3, 4,
        0, 5, 2, 5, 1, 5, 3, 5);

      var box = new[] {
        new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(1, 1, 0), new Point3d(0, 1, 0),
        new Point3d(0, 0, 1), new Point3d(1, 0, 1), new Point3d(1, 1, 1), new Point3d(0, 1, 1) };

      Curve[] boxLines = EdgeLines(box,
        0, 1, 1, 2, 2, 3, 3, 0,
        4, 5, 5, 6, 6, 7, 7, 4,
        0, 4, 1, 5, 2, 6, 3, 7,
        0, 2, 4, 6, 0, 5, 1, 6, 2, 7, 3, 4); // one diagonal per side

      foreach (int valence in new[] { 3, 4 })
      {
        using (Mesh mesh = Mesh.CreateFromLines(octahedronLines, valence, 1e-6))
        {
          Assert.That(mesh, Is.Not.Null, $"octahedron, valence {valence}");
          Assert.That(mesh.Faces.TriangleCount, Is.EqualTo(8), $"octahedron, valence {valence}");
          Assert.That(mesh.IsClosed, Is.True, $"octahedron, valence {valence}");
          // eight equilateral triangles of side root two
          Assert.That(MeshArea(mesh), Is.EqualTo(4.0 * Math.Sqrt(3.0)).Within(1e-9), $"octahedron, valence {valence}");
        }

        using (Mesh mesh = Mesh.CreateFromLines(boxLines, valence, 1e-6))
        {
          Assert.That(mesh, Is.Not.Null, $"triangulated box, valence {valence}");
          Assert.That(mesh.Faces.TriangleCount, Is.EqualTo(12), $"triangulated box, valence {valence}");
          Assert.That(mesh.Faces.QuadCount, Is.EqualTo(0), $"triangulated box, valence {valence}");
          Assert.That(mesh.IsClosed, Is.True, $"triangulated box, valence {valence}");
          Assert.That(MeshArea(mesh), Is.EqualTo(6.0).Within(1e-9), $"triangulated box, valence {valence}");
        }
      }
    }

    [TestCase(0.0)]
    [TestCase(0.31)]
    [TestCase(1.10)]
    [TestCase(2.70)]
    public void LShapedPrismFromItsEdges(double rotation)
    {
      SetupFixture.Prerequisites();
      // RH-97201. Six sides and two L-shaped ends, so a maximum valence of four can only
      // reach the sides and six is needed for the ends as well. The rotations take the
      // sides away from the axes, where a wrong reading is easiest to get away with.
      var profile = new[] {
        new Point2d(0, 0), new Point2d(2, 0), new Point2d(2, 1),
        new Point2d(1, 1), new Point2d(1, 2), new Point2d(0, 2) };

      var xform = Transform.Rotation(rotation, new Vector3d(1, 2, 3), new Point3d(-4, 5, 6));

      var corners = new List<Point3d>();
      foreach (Point2d p in profile)
      {
        corners.Add(xform * new Point3d(p.X, p.Y, 0));
        corners.Add(xform * new Point3d(p.X, p.Y, 1));
      }

      var pairs = new List<int>();
      for (int i = 0; i < profile.Length; i++)
      {
        int j = (i + 1) % profile.Length;
        pairs.AddRange(new[] { 2 * i, 2 * j });          // bottom
        pairs.AddRange(new[] { 2 * i + 1, 2 * j + 1 });  // top
        pairs.AddRange(new[] { 2 * i, 2 * i + 1 });      // side
      }
      Curve[] lines = EdgeLines(corners, pairs.ToArray());

      // A quad cannot cap the six-sided profile, so only the six sides are meshed.
      using (Mesh sides = Mesh.CreateFromLines(lines, 4, 1e-6))
      {
        Assert.That(sides, Is.Not.Null);
        Assert.That(sides.Faces.QuadCount, Is.EqualTo(6));
        Assert.That(sides.Faces.TriangleCount, Is.EqualTo(0));
        Assert.That(sides.IsClosed, Is.False);
        Assert.That(MeshArea(sides), Is.EqualTo(8.0).Within(1e-9));
      }

      // Room for the caps: sides (8) plus both L-shaped ends (3 each).
      using (Mesh solid = Mesh.CreateFromLines(lines, 6, 1e-6))
      {
        Assert.That(solid, Is.Not.Null);
        Assert.That(solid.IsClosed, Is.True);
        Assert.That(solid.Ngons.Count, Is.EqualTo(2)); // the two L-shaped ends
        Assert.That(MeshArea(solid), Is.EqualTo(14.0).Within(1e-9));
      }
    }
  }
}
