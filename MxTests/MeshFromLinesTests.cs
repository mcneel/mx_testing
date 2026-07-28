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

    // A generator of its own, so the seed a failing case is named after keeps reproducing
    // that same case on either platform and on whatever runtime comes next. System.Random
    // has changed its algorithm before now.
    sealed class Rng
    {
      ulong m_state;

      public Rng(int seed) { m_state = (ulong)seed; }

      public double Next()
      {
        unchecked
        {
          m_state = m_state * 6364136223846793005UL + 1442695040888963407UL;
          ulong z = m_state;
          z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
          z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
          z ^= z >> 31;
          return (z >> 11) * (1.0 / (1UL << 53));
        }
      }

      public double Next(double low, double high) { return low + (high - low) * Next(); }
      public int NextIndex(int count) { return (int)(Next() * count); }
      public bool NextBool() { return Next() < 0.5; }
    }

    static Vector3d RandomAxis(Rng rng)
    {
      while (true)
      {
        var axis = new Vector3d(rng.Next(-1.0, 1.0), rng.Next(-1.0, 1.0), rng.Next(-1.0, 1.0));
        if (axis.Length > 0.25 && axis.Unitize())
          return axis;
      }
    }

    const int SweepCases = 360;

    // The edges of a solid the way one case of the sweep asks for them. Seed zero hands them
    // over untouched, axis aligned and in the order they are written; every other seed moves
    // the solid off the axes, reverses an edge here and there and shuffles the order they
    // arrive in, since an answer resting on the input order or on the axes only shows itself
    // once both are taken away.
    static Curve[] SweptEdgeLines(int seed, IList<Point3d> vertices, params int[] pairs)
    {
      if (seed == 0)
        return EdgeLines(vertices, pairs);

      var rng = new Rng(seed);
      Transform rotation = Transform.Rotation(
        rng.Next(0.0, 2.0 * Math.PI), RandomAxis(rng),
        new Point3d(rng.Next(-2.0, 2.0), rng.Next(-2.0, 2.0), rng.Next(-2.0, 2.0)));
      Transform move = Transform.Translation(
        rng.Next(-10.0, 10.0), rng.Next(-10.0, 10.0), rng.Next(-10.0, 10.0));
      Transform xform = move * rotation;

      var moved = new Point3d[vertices.Count];
      for (int i = 0; i < moved.Length; i++)
        moved[i] = xform * vertices[i];

      pairs = (int[])pairs.Clone();
      for (int i = 0; i < pairs.Length; i += 2)
        if (rng.NextBool())
          (pairs[i], pairs[i + 1]) = (pairs[i + 1], pairs[i]);

      Curve[] lines = EdgeLines(moved, pairs);

      for (int i = lines.Length - 1; i > 0; i--)
      {
        int j = rng.NextIndex(i + 1);
        (lines[i], lines[j]) = (lines[j], lines[i]);
      }
      return lines;
    }

    [Test]
    public void BoxFromItsTwelveEdges([Range(0, SweepCases - 1)] int seed)
    {
      SetupFixture.Prerequisites();
      // RH-97201. Every side of a box points 54.7 degrees away from the nearest direction
      // the sweep looks from, and seen from any of them the third edge at a corner falls on
      // the bisector of the corner it has to be told apart from. Ranking those two by the
      // angle between the projections keeps them apart; ranking them in 3D made them equal
      // and left the answer to the order the edges happened to be stored in, which is what
      // the sweep of seeds takes away.
      var corners = new[] {
        new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(1, 1, 0), new Point3d(0, 1, 0),
        new Point3d(0, 0, 1), new Point3d(1, 0, 1), new Point3d(1, 1, 1), new Point3d(0, 1, 1) };

      Curve[] lines = SweptEdgeLines(seed, corners,
        0, 1, 1, 2, 2, 3, 3, 0,
        4, 5, 5, 6, 6, 7, 7, 4,
        0, 4, 1, 5, 2, 6, 3, 7);

      foreach (int valence in new[] { 4, 5, 6 })
        using (Mesh mesh = Mesh.CreateFromLines(lines, valence, 1e-6))
        {
          string where = $"seed {seed}, valence {valence}";
          Assert.That(mesh, Is.Not.Null, where);
          Assert.That(mesh.Faces.QuadCount, Is.EqualTo(6), where);
          Assert.That(mesh.Faces.TriangleCount, Is.EqualTo(0), where);
          Assert.That(mesh.IsClosed, Is.True, where);
          Assert.That(MeshArea(mesh), Is.EqualTo(6.0).Within(1e-9), where);
        }
    }

    [Test]
    public void TriangulatedSolidsFromTheirEdges([Range(0, SweepCases - 1)] int seed)
    {
      SetupFixture.Prerequisites();
      // At a maximum valence of three the coplanar walk is skipped, so these come out of
      // the angular sweep alone. At seed zero both solids are axis aligned, which is the
      // arrangement the sweep finds hardest.
      var octahedron = new[] {
        new Point3d(1, 0, 0), new Point3d(-1, 0, 0), new Point3d(0, 1, 0),
        new Point3d(0, -1, 0), new Point3d(0, 0, 1), new Point3d(0, 0, -1) };

      Curve[] octahedronLines = SweptEdgeLines(seed, octahedron,
        0, 2, 2, 1, 1, 3, 3, 0,
        0, 4, 2, 4, 1, 4, 3, 4,
        0, 5, 2, 5, 1, 5, 3, 5);

      var box = new[] {
        new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(1, 1, 0), new Point3d(0, 1, 0),
        new Point3d(0, 0, 1), new Point3d(1, 0, 1), new Point3d(1, 1, 1), new Point3d(0, 1, 1) };

      Curve[] boxLines = SweptEdgeLines(seed, box,
        0, 1, 1, 2, 2, 3, 3, 0,
        4, 5, 5, 6, 6, 7, 7, 4,
        0, 4, 1, 5, 2, 6, 3, 7,
        0, 2, 4, 6, 0, 5, 1, 6, 2, 7, 3, 4); // one diagonal per side

      foreach (int valence in new[] { 3, 4 })
      {
        string where = $"octahedron, seed {seed}, valence {valence}";
        using (Mesh mesh = Mesh.CreateFromLines(octahedronLines, valence, 1e-6))
        {
          Assert.That(mesh, Is.Not.Null, where);
          Assert.That(mesh.Faces.TriangleCount, Is.EqualTo(8), where);
          Assert.That(mesh.IsClosed, Is.True, where);
          // eight equilateral triangles of side root two
          Assert.That(MeshArea(mesh), Is.EqualTo(4.0 * Math.Sqrt(3.0)).Within(1e-9), where);
        }

        where = $"triangulated box, seed {seed}, valence {valence}";
        using (Mesh mesh = Mesh.CreateFromLines(boxLines, valence, 1e-6))
        {
          Assert.That(mesh, Is.Not.Null, where);
          Assert.That(mesh.Faces.TriangleCount, Is.EqualTo(12), where);
          Assert.That(mesh.Faces.QuadCount, Is.EqualTo(0), where);
          Assert.That(mesh.IsClosed, Is.True, where);
          Assert.That(MeshArea(mesh), Is.EqualTo(6.0).Within(1e-9), where);
        }
      }
    }

    [Test]
    public void LShapedPrismFromItsEdges([Range(0, SweepCases - 1)] int seed)
    {
      SetupFixture.Prerequisites();
      // RH-97201. Six sides and two L-shaped ends, so a maximum valence of four can only
      // reach the sides and six is needed for the ends as well. The sweep takes the sides
      // away from the axes, where a wrong reading is easiest to get away with.
      var profile = new[] {
        new Point2d(0, 0), new Point2d(2, 0), new Point2d(2, 1),
        new Point2d(1, 1), new Point2d(1, 2), new Point2d(0, 2) };

      var corners = new List<Point3d>();
      foreach (Point2d p in profile)
      {
        corners.Add(new Point3d(p.X, p.Y, 0));
        corners.Add(new Point3d(p.X, p.Y, 1));
      }

      var pairs = new List<int>();
      for (int i = 0; i < profile.Length; i++)
      {
        int j = (i + 1) % profile.Length;
        pairs.AddRange(new[] { 2 * i, 2 * j });          // bottom
        pairs.AddRange(new[] { 2 * i + 1, 2 * j + 1 });  // top
        pairs.AddRange(new[] { 2 * i, 2 * i + 1 });      // side
      }
      Curve[] lines = SweptEdgeLines(seed, corners, pairs.ToArray());

      // A quad cannot cap the six-sided profile, so only the six sides are meshed.
      using (Mesh sides = Mesh.CreateFromLines(lines, 4, 1e-6))
      {
        Assert.That(sides, Is.Not.Null, $"seed {seed}");
        Assert.That(sides.Faces.QuadCount, Is.EqualTo(6), $"seed {seed}");
        Assert.That(sides.Faces.TriangleCount, Is.EqualTo(0), $"seed {seed}");
        Assert.That(sides.IsClosed, Is.False, $"seed {seed}");
        Assert.That(MeshArea(sides), Is.EqualTo(8.0).Within(1e-9), $"seed {seed}");
      }

      // Room for the caps: sides (8) plus both L-shaped ends (3 each).
      using (Mesh solid = Mesh.CreateFromLines(lines, 6, 1e-6))
      {
        Assert.That(solid, Is.Not.Null, $"seed {seed}");
        Assert.That(solid.IsClosed, Is.True, $"seed {seed}");
        Assert.That(solid.Ngons.Count, Is.EqualTo(2), $"seed {seed}"); // the two L-shaped ends
        Assert.That(MeshArea(solid), Is.EqualTo(14.0).Within(1e-9), $"seed {seed}");
      }
    }
  }
}
