using NUnit.Framework;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetSDKTests
{
  /// <summary>
  /// Tests for the SubD .NET SDK added by RH-79547: edge sharpness, component identity and
  /// orientation, building faces, deleting and dissolving, and edge chains.
  /// </summary>
  [TestFixture]
  public class SubDNetSDKTests
  {
    const double kTol = 1e-6;

    static Point3d[] UnitBoxCorners()
    {
      return new Point3d[]
      {
        new Point3d(0.0, 0.0, 0.0),
        new Point3d(1.0, 0.0, 0.0),
        new Point3d(1.0, 1.0, 0.0),
        new Point3d(0.0, 1.0, 0.0),
        new Point3d(0.0, 0.0, 1.0),
        new Point3d(1.0, 0.0, 1.0),
        new Point3d(1.0, 1.0, 1.0),
        new Point3d(0.0, 1.0, 1.0)
      };
    }

    static SubD UnitBox(double edgeSharpness, uint n)
    {
      var subd = SubD.CreateSubDBox(UnitBoxCorners(), edgeSharpness, n, n, n);
      Assert.That(subd, Is.Not.Null);
      return subd;
    }

    #region SubDEdgeSharpness value type

    [Test]
    public void SharpnessIsAValueType()
    {
      // The default value is Smooth, so a SubDEdgeSharpness field needs no initialization.
      Assert.That(default(SubDEdgeSharpness), Is.EqualTo(SubDEdgeSharpness.Smooth));
      Assert.That(default(SubDEdgeSharpness).IsZero, Is.True);

      // Equality is by value, not by reference. This is the whole point of it being a
      // struct: two separately built sharpnesses that describe the same thing are equal.
      Assert.That(new SubDEdgeSharpness(2.0), Is.EqualTo(new SubDEdgeSharpness(2.0)));
      Assert.That(new SubDEdgeSharpness(2.0) == new SubDEdgeSharpness(2.0), Is.True);
      Assert.That(new SubDEdgeSharpness(2.0) != new SubDEdgeSharpness(3.0), Is.True);
      Assert.That(new SubDEdgeSharpness(1.0, 2.0), Is.Not.EqualTo(new SubDEdgeSharpness(2.0, 1.0)));

      // Equal values hash equally, so these work in dictionaries and sets.
      Assert.That(new SubDEdgeSharpness(1.0, 2.0).GetHashCode(),
        Is.EqualTo(new SubDEdgeSharpness(1.0, 2.0).GetHashCode()));

      // Reading a static does not allocate anything that needs disposing.
      Assert.That(SubDEdgeSharpness.Crease, Is.EqualTo(SubDEdgeSharpness.Crease));
    }

    [Test]
    public void SharpnessConstants()
    {
      Assert.That(SubDEdgeSharpness.MaximumValue, Is.EqualTo(4.0));
      Assert.That(SubDEdgeSharpness.SmoothValue, Is.EqualTo(0.0));
      Assert.That(SubDEdgeSharpness.CreaseValue, Is.EqualTo(5.0));
      Assert.That(SubDEdgeSharpness.Tolerance, Is.EqualTo(0.01));

      Assert.That(SubDEdgeSharpness.Smooth.IsZero, Is.True);
      Assert.That(SubDEdgeSharpness.Smooth.IsSharp, Is.False);
      Assert.That(SubDEdgeSharpness.Maximum.IsSharp, Is.True);

      // A crease is not a sharp edge, and Crease is deliberately not a valid sharpness.
      Assert.That(SubDEdgeSharpness.Crease.IsCrease, Is.True);
      Assert.That(SubDEdgeSharpness.Crease.IsSharp, Is.False);
      Assert.That(SubDEdgeSharpness.Crease.IsCreaseOrSharp, Is.True);
      Assert.That(SubDEdgeSharpness.Crease.IsValid, Is.False);
      Assert.That(SubDEdgeSharpness.Crease.IsValidOrCrease, Is.True);
      Assert.That(SubDEdgeSharpness.Crease.IsNotValid, Is.True);
      Assert.That(SubDEdgeSharpness.Crease.IsNotValidNorCrease, Is.False);
    }

    [Test]
    public void SharpnessTrendAndEnds()
    {
      var s = new SubDEdgeSharpness(1.0, 3.0);
      Assert.That(s.IsVariable, Is.True);
      Assert.That(s.IsConstant, Is.False);
      Assert.That(s[0], Is.EqualTo(1.0));
      Assert.That(s[1], Is.EqualTo(3.0));
      Assert.That(s.EndSharpness(0), Is.EqualTo(1.0));
      Assert.That(s.MinimumEndSharpness, Is.EqualTo(1.0));
      Assert.That(s.MaximumEndSharpness, Is.EqualTo(3.0));
      Assert.That(s.Average, Is.EqualTo(2.0));
      Assert.That(s.Delta, Is.EqualTo(2.0));

      // IsIncreasing is EndSharpness(0) < EndSharpness(1); the header used to say otherwise.
      Assert.That(s.IsIncreasing, Is.True);
      Assert.That(s.IsDecreasing, Is.False);
      Assert.That(s.Trend, Is.EqualTo(1));

      var r = s.Reversed();
      Assert.That(r, Is.EqualTo(new SubDEdgeSharpness(3.0, 1.0)));
      Assert.That(r.IsDecreasing, Is.True);
      Assert.That(r.Trend, Is.EqualTo(-1));

      // An out of range index is NaN, not a wrong number.
      Assert.That(double.IsNaN(s[2]), Is.True);
      Assert.That(double.IsNaN(s[-1]), Is.True);
    }

    [Test]
    public void SharpnessFromPercentage()
    {
      var half = SubDEdgeSharpness.FromConstantPercentage(50.0);
      Assert.That(half.IsConstant, Is.True);
      Assert.That(half[0], Is.EqualTo(SubDEdgeSharpness.MaximumValue / 2.0).Within(kTol));

      Assert.That(SubDEdgeSharpness.FromConstantPercentage(0.0), Is.EqualTo(SubDEdgeSharpness.Smooth));
      Assert.That(SubDEdgeSharpness.FromConstantPercentage(100.0)[0],
        Is.EqualTo(SubDEdgeSharpness.MaximumValue).Within(kTol));

      // Sharpness is stored as float, so a percentage that is not exact in binary needs a
      // tolerance on the round trip.
      var third = SubDEdgeSharpness.FromConstantPercentage(33.0);
      Assert.That(SubDEdgeSharpness.ToPercentage(third[0], 999.0), Is.EqualTo(33.0).Within(1e-4));

      var varying = SubDEdgeSharpness.FromIntervalPercentage(25.0, 75.0);
      Assert.That(varying[0], Is.EqualTo(1.0).Within(kTol));
      Assert.That(varying[1], Is.EqualTo(3.0).Within(kTol));
      Assert.That(SubDEdgeSharpness.FromIntervalPercentage(new Interval(25.0, 75.0)), Is.EqualTo(varying));

      // double.MaxValue is the percentage spelling of a crease.
      Assert.That(SubDEdgeSharpness.FromConstantPercentage(double.MaxValue), Is.EqualTo(SubDEdgeSharpness.Crease));
      Assert.That(SubDEdgeSharpness.FromIntervalPercentage(double.MaxValue, double.MaxValue),
        Is.EqualTo(SubDEdgeSharpness.Crease));

      // Out of range, and mixing a crease with a percentage, are both not valid.
      Assert.That(SubDEdgeSharpness.FromConstantPercentage(-1.0).IsValidOrCrease, Is.False);
      Assert.That(SubDEdgeSharpness.FromConstantPercentage(101.0).IsValidOrCrease, Is.False);
      Assert.That(SubDEdgeSharpness.FromIntervalPercentage(double.MaxValue, 50.0).IsValidOrCrease, Is.False);
    }

    [Test]
    public void SharpnessSanitizeAndSubdivide()
    {
      // Within Tolerance of an integer snaps to it.
      Assert.That(SubDEdgeSharpness.Sanitize(2.0 + SubDEdgeSharpness.Tolerance / 2.0), Is.EqualTo(2.0));
      Assert.That(SubDEdgeSharpness.Sanitize(-1.0), Is.EqualTo(0.0));
      Assert.That(SubDEdgeSharpness.Sanitize(-1.0, 7.0), Is.EqualTo(7.0));

      // Subdividing costs one level of sharpness, and never goes below zero.
      Assert.That(new SubDEdgeSharpness(3.0).Subdivided(0)[0], Is.EqualTo(2.0).Within(kTol));
      Assert.That(new SubDEdgeSharpness(0.5).Subdivided(0).IsZero, Is.True);
      Assert.That(new SubDEdgeSharpness(3.0).Subdivided(7).IsZero, Is.True);
    }

    [Test]
    public void SharpnessEdgeChainRamp()
    {
      var ramp = SubDEdgeSharpness.CreateEdgeChainSharpness(new Interval(0.0, 3.0), 5);
      Assert.That(ramp.Length, Is.EqualTo(5));
      // Consecutive links meet at the same value, so the chain reads as one smooth ramp.
      for (int i = 0; i + 1 < ramp.Length; i++)
        Assert.That(ramp[i][1], Is.EqualTo(ramp[i + 1][0]).Within(kTol));
      Assert.That(ramp[0][0], Is.EqualTo(0.0).Within(kTol));
      Assert.That(ramp[ramp.Length - 1][1], Is.EqualTo(3.0).Within(kTol));

      Assert.That(SubDEdgeSharpness.CreateEdgeChainSharpness(new Interval(0.0, 3.0), 0), Is.Empty);
    }

    [Test]
    public void SharpnessToPercentageText()
    {
      Assert.That(SubDEdgeSharpness.ToPercentage(SubDEdgeSharpness.MaximumValue, 999.0), Is.EqualTo(100.0));
      Assert.That(SubDEdgeSharpness.ToPercentage(SubDEdgeSharpness.CreaseValue, 999.0), Is.EqualTo(999.0));
      Assert.That(double.IsNaN(SubDEdgeSharpness.ToPercentage(-1.0, 999.0)), Is.True);

      // ToString is the percentage text, so a sharpness is readable in a debugger or log.
      Assert.That(new SubDEdgeSharpness(SubDEdgeSharpness.MaximumValue).ToString(), Does.Contain("100"));
      Assert.That(SubDEdgeSharpness.ToPercentageText(SubDEdgeSharpness.CreaseValue), Is.Not.Empty);
    }

    #endregion

    #region Sharpness on real geometry

    [Test]
    public void BoxEdgeSharpnessRoundTrip()
    {
      SubD box = UnitBox(2.0, 1);
      Assert.That(box.SharpEdgeCount(), Is.EqualTo(12));

      SubDEdgeSharpness range;
      Assert.That(box.SharpEdgeCount(out range), Is.EqualTo(12));
      Assert.That(range, Is.EqualTo(new SubDEdgeSharpness(2.0)));

      foreach (var edge in box.Edges)
      {
        Assert.That(edge.IsSharp, Is.True);
        Assert.That(edge.Sharpness, Is.EqualTo(new SubDEdgeSharpness(2.0)));
      }

      Assert.That(box.ClearEdgeSharpness(), Is.EqualTo(12));
      Assert.That(box.SharpEdgeCount(), Is.EqualTo(0));
      Assert.That(box.SharpEdgeCount(out range), Is.EqualTo(0));
      Assert.That(range, Is.EqualTo(SubDEdgeSharpness.Smooth));
    }

    [Test]
    public void SetAndReadEdgeSharpness()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      Assert.That(box.SharpEdgeCount(), Is.EqualTo(0));

      SubDEdge edge = box.Edges.First();
      edge.Sharpness = new SubDEdgeSharpness(2.0);
      Assert.That(edge.Sharpness, Is.EqualTo(new SubDEdgeSharpness(2.0)));
      Assert.That(edge.IsSharp, Is.True);
      Assert.That(box.SharpEdgeCount(), Is.EqualTo(1));

      // Both ends can differ.
      edge.Sharpness = new SubDEdgeSharpness(1.0, 3.0);
      Assert.That(edge.Sharpness, Is.EqualTo(new SubDEdgeSharpness(1.0, 3.0)));
      Assert.That(edge.EndSharpness(0, false), Is.EqualTo(1.0).Within(kTol));
      Assert.That(edge.EndSharpness(1, false), Is.EqualTo(3.0).Within(kTol));

      // Assigning Smooth makes it smooth again.
      edge.Sharpness = SubDEdgeSharpness.Smooth;
      Assert.That(edge.IsSharp, Is.False);
      Assert.That(box.SharpEdgeCount(), Is.EqualTo(0));

      Assert.That(() => edge.EndSharpness(2, false), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void CreaseEdgeSharpnessReporting()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      SubDEdge edge = box.Edges.First();
      edge.Tag = SubDEdgeTag.Crease;

      // A crease is reported as Crease by default, and as Smooth when asked not to use the
      // crease value. A crease edge is never a sharp edge.
      Assert.That(edge.Sharpness, Is.EqualTo(SubDEdgeSharpness.Crease));
      Assert.That(edge.GetSharpness(), Is.EqualTo(SubDEdgeSharpness.Crease));
      Assert.That(edge.GetSharpness(false), Is.EqualTo(SubDEdgeSharpness.Smooth));
      Assert.That(edge.IsSharp, Is.False);
      Assert.That(box.SharpEdgeCount(), Is.EqualTo(0));
    }

    [Test]
    public void SetChainSharpnessPerEdge()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      var edges = box.Edges.Take(5).ToArray();
      var ramp = SubDEdgeSharpness.CreateEdgeChainSharpness(new Interval(0.0, 3.0), edges.Length);

      Assert.That(box.SetEdgeSharpness(edges, ramp, false), Is.GreaterThan(0));

      SubDEdgeSharpness range;
      box.SharpEdgeCount(out range);
      Assert.That(range.MaximumEndSharpness, Is.EqualTo(3.0).Within(kTol));

      Assert.That(() => box.SetEdgeSharpness(edges, ramp.Take(2).ToArray(), false),
        Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void MovingAControlNetPointNeedsTheCacheCleared()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 3);
      SubDVertex vertex = box.Vertices.First;
      Point3d before = vertex.SurfacePoint();

      SubDVertex neighbor = box.Vertices.First(v => v.Id != vertex.Id);
      neighbor.SetControlNetPoint(neighbor.ControlNetPoint + new Vector3d(0.1, 0.1, 0.1), false);

      // The cached surface point is stale until it is cleared, and clearing only this
      // vertex is not enough because its neighbors feed into it.
      vertex.ClearSavedSubdivisionPoints(true);
      Assert.That(vertex.SurfacePoint().DistanceTo(before), Is.Not.EqualTo(0.0));
    }

    #endregion

    #region Component identity and orientation

    [Test]
    public void ComponentsCompareByValue()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      SubDEdge first = box.Edges.First();

      // Two separate lookups of the same edge are equal. Before SubDComponent was based on
      // a component pointer these compared unequal, because == was reference equality.
      SubDEdge again = box.Edges.Find(first.Id);
      Assert.That(again, Is.Not.SameAs(first));
      Assert.That(again, Is.EqualTo(first));
      Assert.That(again == first, Is.True);
      Assert.That(again.GetHashCode(), Is.EqualTo(first.GetHashCode()));

      // Different components of the same SubD are not equal.
      SubDEdge other = box.Edges.Skip(1).First();
      Assert.That(other, Is.Not.EqualTo(first));

      // Neither are components of different types that happen to share an id.
      SubDVertex vertex = box.Vertices.Find(first.Id);
      if (null != vertex)
        Assert.That((SubDComponent)vertex, Is.Not.EqualTo((SubDComponent)first));

      // Nor the same id in a different SubD.
      SubD other_box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      Assert.That(other_box.Edges.Find(first.Id), Is.Not.EqualTo(first));

      Assert.That(first == null, Is.False);
      Assert.That((SubDEdge)null == (SubDEdge)null, Is.True);
    }

    [Test]
    public void ComponentDirectionRoundTrips()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      SubDEdge edge = box.Edges.First();

      // Edges come out of the SubD in their natural orientation.
      Assert.That(edge.ComponentDirection, Is.False);

      edge.ComponentDirection = true;
      Assert.That(edge.ComponentDirection, Is.True);

      // Orientation is part of identity: the same edge referenced the other way round is
      // not the same reference.
      Assert.That(edge, Is.Not.EqualTo(box.Edges.Find(edge.Id)));

      edge.ComponentDirection = false;
      Assert.That(edge, Is.EqualTo(box.Edges.Find(edge.Id)));

      // ToString names the type, the orientation and the id.
      Assert.That(edge.ToString(), Is.EqualTo(string.Format("SubDEdge(+{0})", edge.Id)));
      edge.ComponentDirection = true;
      Assert.That(edge.ToString(), Is.EqualTo(string.Format("SubDEdge(-{0})", edge.Id)));
    }

    [Test]
    [TestCase(2u)]
    [TestCase(4u)]
    [TestCase(8u)]
    public void ComponentLookupByIdIsConsistent(uint n)
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, n);

      foreach (var edge in box.Edges.Take(50))
      {
        SubDEdge found = box.Edges.Find(edge.Id);
        Assert.That(found, Is.Not.Null);
        Assert.That(found.Id, Is.EqualTo(edge.Id));
        Assert.That(found, Is.EqualTo(edge));
      }

      foreach (var vertex in box.Vertices.Take(50))
        Assert.That(box.Vertices.Find(vertex.Id), Is.EqualTo(vertex));

      foreach (var face in box.Faces.Take(50))
        Assert.That(box.Faces.Find(face.Id), Is.EqualTo(face));
    }

    #endregion

    #region Building, deleting and dissolving

    [Test]
    public void CreateSubDBoxFromCornersAndFromBox()
    {
      SubD from_corners = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      Assert.That(from_corners.Faces.Count, Is.EqualTo(6));
      Assert.That(from_corners.Edges.Count, Is.EqualTo(12));
      Assert.That(from_corners.Vertices.Count, Is.EqualTo(8));

      var basebox = new Box(Plane.WorldXY,
        new Point3d[] { new Point3d(0.0, 0.0, 0.0), new Point3d(1.0, 1.0, 1.0) });
      SubD from_box = SubD.CreateSubDBox(basebox, SubDEdgeSharpness.SmoothValue, 1, 1, 1);
      Assert.That(from_box, Is.Not.Null);
      Assert.That(from_box.Faces.Count, Is.EqualTo(6));

      // A crease box has creased edges rather than sharp ones.
      SubD creased = UnitBox(SubDEdgeSharpness.CreaseValue, 1);
      Assert.That(creased.SharpEdgeCount(), Is.EqualTo(0));
      Assert.That(creased.Edges.First().Sharpness, Is.EqualTo(SubDEdgeSharpness.Crease));

      Assert.That(() => SubD.CreateSubDBox(UnitBoxCorners().Take(7), 0.0, 1, 1, 1),
        Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void DeleteFaceLeavesAHole()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      SubDFace face = box.Faces.First();

      Assert.That(box.DeleteComponents(new SubDFace[] { face }), Is.True);
      Assert.That(box.Faces.Count, Is.EqualTo(5));
      // The edges around the hole survive.
      Assert.That(box.Edges.Count, Is.EqualTo(12));
    }

    [Test]
    public void DeleteAndRebuildAFace()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      SubDFace face = box.Faces.First();
      uint face_id = face.Id;

      // Remember the boundary, oriented the way the face used it.
      var loop = new SubDEdge[face.EdgeCount];
      for (int i = 0; i < loop.Length; i++)
        loop[i] = face.EdgeAt(i);

      Assert.That(box.DeleteComponents(new SubDFace[] { face }), Is.True);
      Assert.That(box.Faces.Count, Is.EqualTo(5));

      // Put it back from the same edges. This is what the component direction is for: the
      // loop has to be traversable end to end.
      SubDFace rebuilt = box.Faces.Add(loop);
      Assert.That(rebuilt, Is.Not.Null);
      Assert.That(box.Faces.Count, Is.EqualTo(6));
      Assert.That(rebuilt.Id, Is.EqualTo(face_id));

      Assert.That(() => box.Faces.Add(loop.Take(2).ToArray()), Throws.TypeOf<ArgumentException>());
      Assert.That(() => box.Faces.Add(null), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void DissolvingAnEdgeMergesTwoFaces()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 2);
      int faces_before = box.Faces.Count;

      // An interior edge between two faces. Dissolving it should merge them, not leave a
      // hole, which is what separates DissolveOrDelete from Delete.
      SubDEdge interior = box.Edges.First(e => e.FaceCount == 2);
      uint merged = box.DissolveOrDeleteComponents(new SubDEdge[] { interior });

      Assert.That(merged, Is.GreaterThan(0));
      Assert.That(box.Faces.Count, Is.EqualTo(faces_before - 1));
    }

    [Test]
    public void FindOrAddEdgeDoesNotDuplicate()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      int edges_before = box.Edges.Count;

      SubDEdge existing = box.Edges.First();
      SubDEdge found = box.Edges.FindOrAdd(existing.VertexFrom, existing.VertexTo);
      Assert.That(found, Is.Not.Null);
      Assert.That(found.Id, Is.EqualTo(existing.Id));
      Assert.That(box.Edges.Count, Is.EqualTo(edges_before));
    }

    [Test]
    public void DeleteAndDissolveValidateTheirArguments()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      Assert.That(() => box.DeleteComponents((SubDComponent[])null), Throws.TypeOf<ArgumentNullException>());
      Assert.That(() => box.DissolveOrDeleteComponents((SubDComponent[])null), Throws.TypeOf<ArgumentNullException>());
      // An empty list is a no-op rather than an error.
      Assert.That(box.DeleteComponents(new SubDComponent[0]), Is.True);
      Assert.That(box.DissolveOrDeleteComponents(new SubDComponent[0]), Is.EqualTo(0));
      Assert.That(box.Faces.Count, Is.EqualTo(6));
    }

    #endregion

    #region Edge chains

    [Test]
    public void ChainGrowsFromASingleEdge()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      SubDEdge start = box.Edges.First();

      using (var chain = new SubDEdgeChain(box, start))
      {
        Assert.That(chain.EdgeCount, Is.EqualTo(1));
        Assert.That(chain.ParentSubD, Is.SameAs(box));
        Assert.That(chain.EdgeAt(0), Is.EqualTo(start));

        uint added = chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag);
        Assert.That(added, Is.GreaterThan(0));
        Assert.That(chain.EdgeCount, Is.EqualTo(added + 1));

        // Consecutive chain edges share a vertex: the end of one is the start of the next.
        for (int i = 0; i + 1 < (int)chain.EdgeCount; i++)
        {
          SubDVertex shared = chain.VertexAt(i + 1);
          Assert.That(shared, Is.Not.Null);
        }

        int count_before = (int)chain.EdgeCount;
        chain.Reverse();
        Assert.That(chain.EdgeCount, Is.EqualTo(count_before));

        Assert.That(chain.Edges.Length, Is.EqualTo(count_before));

        chain.Clear();
        Assert.That(chain.EdgeCount, Is.EqualTo(0));
      }
    }

    [Test]
    public void ChainEdgesCarryTheirDirection()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      using (var chain = new SubDEdgeChain(box, box.Edges.First()))
      {
        chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag);
        Assert.That(chain.EdgeCount, Is.GreaterThan(1));

        // The edges come back oriented the way the chain runs through them, which is what
        // makes them usable directly with SubD.Faces.Add.
        var edges = chain.Edges;
        Assert.That(edges.All(e => null != e), Is.True);
        AssertChainIsConnected(edges);
      }
    }

    [Test]
    public void SortEdgesIntoChains()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);

      // The four edges bounding one face form a single closed chain.
      SubDFace face = box.Faces.First();
      var loop = new SubDEdge[face.EdgeCount];
      for (int i = 0; i < loop.Length; i++)
        loop[i] = face.EdgeAt(i);

      var chains = SubDEdgeChain.SortEdgesIntoEdgeChains(box, loop);
      Assert.That(chains, Is.Not.Null);
      Assert.That(chains.Length, Is.EqualTo(1));
      Assert.That(chains[0].Length, Is.EqualTo(loop.Length));
      // Sorted chains are connected end to end.
      AssertChainIsConnected(chains[0]);

      Assert.That(SubDEdgeChain.SortEdgesIntoEdgeChains(box, new SubDEdge[0]), Is.Empty);
      Assert.That(() => SubDEdgeChain.SortEdgesIntoEdgeChains(null, loop),
        Throws.TypeOf<ArgumentNullException>());
      Assert.That(() => SubDEdgeChain.SortEdgesIntoEdgeChains(box, null),
        Throws.TypeOf<ArgumentNullException>());
    }

    // Returns the edges bounding one face, in boundary order.
    static SubDEdge[] FaceLoop(SubDFace face)
    {
      var loop = new SubDEdge[face.EdgeCount];
      for (int i = 0; i < loop.Length; i++)
        loop[i] = face.EdgeAt(i);
      return loop;
    }

    // Consecutive chain edges meet end to start, in the direction the chain runs through
    // them. This has to use the Relative accessors: VertexFrom and VertexTo ignore
    // ComponentDirection and so only line up on chains that happen to run forwards.
    static void AssertChainIsConnected(SubDEdge[] edges, string context = null)
    {
      for (int i = 0; i + 1 < edges.Length; i++)
      {
        Assert.That(edges[i].RelativeVertexTo, Is.EqualTo(edges[i + 1].RelativeVertexFrom),
          context ?? "chain edge " + i + " should meet edge " + (i + 1));
      }
    }

    static uint[] EdgeIds(SubDEdgeChain chain)
    {
      var edges = chain.Edges;
      var ids = new uint[edges.Length];
      for (int i = 0; i < edges.Length; i++)
        ids[i] = edges[i].Id;
      return ids;
    }

    [Test]
    public void BeginRestartsTheChain()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      SubDEdge first = box.Edges.First();
      SubDEdge other = box.Edges.First(e => e.Id != first.Id);

      using (var chain = new SubDEdgeChain(box, first))
      {
        chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag);
        Assert.That(chain.EdgeCount, Is.GreaterThan(1));

        // Begin clears whatever was there and restarts from one edge.
        Assert.That(chain.Begin(other), Is.EqualTo(1));
        Assert.That(chain.EdgeCount, Is.EqualTo(1));
        Assert.That(chain.EdgeAt(0), Is.EqualTo(other));

        Assert.That(() => chain.Begin(null), Throws.TypeOf<ArgumentNullException>());
      }
    }

    [Test]
    public void AddOneNeighborGrowsOneStepAtATime()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);

      using (var chain = new SubDEdgeChain(box, box.Edges.First()))
      {
        // One end at a time: at most one edge per call.
        uint added = chain.AddOneNeighbor(ChainDirection.Next, SubDChainType.MixedTag);
        Assert.That(added, Is.LessThanOrEqualTo(1));
        Assert.That(chain.EdgeCount, Is.EqualTo(1 + added));

        // Both ends at once: at most two.
        uint before = chain.EdgeCount;
        uint added_both = chain.AddOneNeighbor(ChainDirection.Both, SubDChainType.MixedTag);
        Assert.That(added_both, Is.LessThanOrEqualTo(2));
        Assert.That(chain.EdgeCount, Is.EqualTo(before + added_both));

        // Growing one step at a time never reaches further than growing all the way.
        uint stepwise = chain.EdgeCount;
        chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag);
        Assert.That(chain.EdgeCount, Is.GreaterThanOrEqualTo(stepwise));

        // Once it cannot grow any further, adding more is a no-op.
        Assert.That(chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag), Is.EqualTo(0));
        Assert.That(chain.AddOneNeighbor(ChainDirection.Both, SubDChainType.MixedTag), Is.EqualTo(0));
      }
    }

    [Test]
    public void AddEdgeAppendsConnectedEdges()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      SubDEdge[] loop = FaceLoop(box.Faces.First());

      // AddEdge cannot start a chain: on an empty one it does nothing.
      using (var empty = new SubDEdgeChain(box))
      {
        Assert.That(empty.AddEdge(loop[0]), Is.EqualTo(0));
        Assert.That(empty.EdgeCount, Is.EqualTo(0));
      }

      using (var chain = new SubDEdgeChain(box, loop[0]))
      {
        // Building a chain by hand, edge by edge, along a face boundary. AddEdge returns
        // how many edges it added, not the resulting length.
        Assert.That(chain.AddEdge(loop[1]), Is.EqualTo(1));
        Assert.That(chain.EdgeCount, Is.EqualTo(2));

        // The same edge twice is refused.
        Assert.That(chain.AddEdge(loop[1]), Is.EqualTo(0));
        Assert.That(chain.EdgeCount, Is.EqualTo(2));

        // Consecutive chain edges meet at a shared vertex.
        var edges = chain.Edges;
        AssertChainIsConnected(edges);

        // An edge that does not touch either end of the chain cannot be appended, so the
        // chain is left as it was.
        uint chain_start = edges[0].RelativeVertexFrom.Id;
        uint chain_end = edges[edges.Length - 1].RelativeVertexTo.Id;
        SubDEdge disconnected = box.Edges.First(e =>
          e.VertexFrom.Id != chain_start && e.VertexFrom.Id != chain_end &&
          e.VertexTo.Id != chain_start && e.VertexTo.Id != chain_end);
        uint before = chain.EdgeCount;
        Assert.That(chain.AddEdge(disconnected), Is.EqualTo(0));
        Assert.That(chain.EdgeCount, Is.EqualTo(before));
      }
    }

    [Test]
    public void TrimToRangeKeepsTheRunBetweenTwoEdges()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);

      using (var chain = new SubDEdgeChain(box, box.Edges.First()))
      {
        chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag);
        uint before = chain.EdgeCount;
        Assert.That(before, Is.GreaterThan(3));

        // Trimming to a single edge keeps that edge and drops everything else. Note this
        // is the opposite of what the underlying ON_SubDEdgeChain::RemoveEdges name
        // suggests: the range given is what survives.
        SubDEdge keep = chain.EdgeAt(1);
        uint removed = chain.TrimToRange(keep, keep);
        Assert.That(removed, Is.EqualTo(before - 1));
        Assert.That(chain.EdgeCount, Is.EqualTo(1));
        Assert.That(chain.EdgeAt(0).Id, Is.EqualTo(keep.Id));
      }

      using (var chain = new SubDEdgeChain(box, box.Edges.First()))
      {
        chain.AddAllNeighbors(ChainDirection.Both, SubDChainType.MixedTag);
        uint before = chain.EdgeCount;

        // Null means "keep the current end", so null for both trims nothing at all.
        Assert.That(chain.TrimToRange(null, null), Is.EqualTo(0));
        Assert.That(chain.EdgeCount, Is.EqualTo(before));

        // Dropping just the tail: keep from the current start up to the second edge.
        SubDEdge second = chain.EdgeAt(1);
        Assert.That(chain.TrimToRange(null, second), Is.EqualTo(before - 2));
        Assert.That(chain.EdgeCount, Is.EqualTo(2));

        // Clear is how you actually empty a chain.
        chain.Clear();
        Assert.That(chain.EdgeCount, Is.EqualTo(0));
      }
    }

    [Test]
    public void ReverseFlipsOrderAndIsAnInvolution()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      SubDEdge[] loop = FaceLoop(box.Faces.First());

      // An open chain of three edges, so reversing cannot be confused with rotating a
      // closed loop.
      using (var chain = new SubDEdgeChain(box, loop[0]))
      {
        Assert.That(chain.AddEdge(loop[1]), Is.EqualTo(1));
        Assert.That(chain.AddEdge(loop[2]), Is.EqualTo(1));
        Assert.That(chain.EdgeCount, Is.EqualTo(3));
        Assert.That(chain.IsClosedLoop, Is.False);

        uint[] forward = EdgeIds(chain);
        var forward_directions = chain.Edges.Select(e => e.ComponentDirection).ToArray();

        chain.Reverse();
        uint[] backward = EdgeIds(chain);
        Assert.That(backward, Is.EqualTo(forward.Reverse().ToArray()));

        // Reversing the chain also reverses how it traverses each edge.
        var backward_directions = chain.Edges.Select(e => e.ComponentDirection).ToArray();
        for (int i = 0; i < forward.Length; i++)
          Assert.That(backward_directions[i], Is.Not.EqualTo(forward_directions[forward.Length - 1 - i]));

        // Reversing twice gets back to the start.
        chain.Reverse();
        Assert.That(EdgeIds(chain), Is.EqualTo(forward));
      }
    }

    [Test]
    public void FaceBoundaryIsAClosedLoop()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      SubDFace face = box.Faces.First();
      SubDEdge[] loop = FaceLoop(face);

      using (var chain = new SubDEdgeChain(box, loop[0]))
      {
        for (int i = 1; i < loop.Length; i++)
          Assert.That(chain.AddEdge(loop[i]), Is.EqualTo(1), "face boundary edges should chain");

        Assert.That(chain.EdgeCount, Is.EqualTo((uint)loop.Length));
        Assert.That(chain.IsClosedLoop, Is.True);

        // A closed loop of N edges has N vertices, not N+1, so index N is past the end.
        for (int i = 0; i < loop.Length; i++)
          Assert.That(chain.VertexAt(i), Is.Not.Null);

        // Whether this particular loop counts as convex is a geometry question, so only
        // the relationship between the two overloads is asserted: strictly convex is a
        // stronger condition than convex.
        if (chain.IsConvexLoop(true))
          Assert.That(chain.IsConvexLoop(false), Is.True);
      }
    }

    [Test]
    public void OpenChainIsNeverAConvexLoop()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      using (var chain = new SubDEdgeChain(box, box.Edges.First()))
      {
        Assert.That(chain.IsClosedLoop, Is.False);
        Assert.That(chain.IsConvexLoop(false), Is.False);
        Assert.That(chain.IsConvexLoop(true), Is.False);
      }
    }

    // Note: the ChainDirection and SubDChainType values are deliberately looped over here
    // rather than passed as [TestCase] arguments. NUnit resolves attribute arguments while
    // building the test, before the fixture has set up assembly resolution, so a RhinoCommon
    // enum in a [TestCase] fails with FileNotFoundException on RhinoCommon.
    [Test]
    public void ChainGrowsInEachDirection()
    {
      var directions = new[] { ChainDirection.Next, ChainDirection.Previous, ChainDirection.Both };
      foreach (var direction in directions)
      {
        SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
        using (var chain = new SubDEdgeChain(box, box.Edges.First()))
        {
          uint added = chain.AddAllNeighbors(direction, SubDChainType.MixedTag);
          Assert.That(added, Is.GreaterThan(0), direction + ": a smooth box grid should chain in any direction");
          Assert.That(chain.EdgeCount, Is.EqualTo(1 + added), direction.ToString());
        }
      }
    }

    [Test]
    public void ChainTypeConstrainsGrowth()
    {
      var types = new[]
      {
        SubDChainType.MixedTag,
        SubDChainType.EqualEdgeTag,
        SubDChainType.EqualEdgeAndVertexTag,
        SubDChainType.EqualEdgeTagAndOrdinary
      };

      foreach (var chainType in types)
      {
        // A creased box has crease edges and corner vertices, so the stricter chain types
        // have something to refuse.
        SubD box = UnitBox(SubDEdgeSharpness.CreaseValue, 4);
        using (var chain = new SubDEdgeChain(box, box.Edges.First()))
        {
          uint added = chain.AddAllNeighbors(ChainDirection.Both, chainType);
          Assert.That(chain.EdgeCount, Is.EqualTo(1 + added), chainType.ToString());

          // Whatever the constraint, the result is still a connected chain.
          AssertChainIsConnected(chain.Edges, chainType.ToString());
        }
      }
    }

    [Test]
    public void ChainSurvivesRepeatedDispose()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      var chain = new SubDEdgeChain(box, box.Edges.First());
      Assert.That(chain.EdgeCount, Is.EqualTo(1));
      chain.Dispose();
      // Disposing twice must not throw or double free.
      Assert.That(() => chain.Dispose(), Throws.Nothing);
    }

    [Test]
    public void SortEdgesIntoChainsSplitsDisconnectedRuns()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);

      // Two face boundaries far enough apart to have no shared vertex give two chains.
      SubDFace[] faces = box.Faces.ToArray();
      SubDEdge[] first = FaceLoop(faces[0]);
      SubDEdge[] second = FaceLoop(faces[faces.Length - 1]);

      var ids = new HashSet<uint>();
      foreach (var e in first) ids.Add(e.Id);
      bool disjoint = second.All(e => !ids.Contains(e.Id));

      var input = new List<SubDEdge>(first);
      input.AddRange(second);
      var chains = SubDEdgeChain.SortEdgesIntoEdgeChains(box, input);

      Assert.That(chains, Is.Not.Null);
      if (disjoint)
        Assert.That(chains.Length, Is.GreaterThanOrEqualTo(2));

      // Every input edge lands in exactly one chain, and no chain is empty.
      int total = 0;
      foreach (var c in chains)
      {
        Assert.That(c.Length, Is.GreaterThan(0));
        total += c.Length;
      }
      Assert.That(total, Is.EqualTo(input.Count));

      // A null entry in the input is an error, not a silently dropped edge.
      Assert.That(() => SubDEdgeChain.SortEdgesIntoEdgeChains(box, new SubDEdge[] { first[0], null }),
        Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void ChainValidatesItsArguments()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 1);
      Assert.That(() => new SubDEdgeChain(null), Throws.TypeOf<ArgumentNullException>());
      Assert.That(() => new SubDEdgeChain(box, null), Throws.TypeOf<ArgumentNullException>());

      using (var chain = new SubDEdgeChain(box))
      {
        Assert.That(chain.EdgeCount, Is.EqualTo(0));
        Assert.That(chain.EdgeAt(0), Is.Null);
        Assert.That(chain.VertexAt(0), Is.Null);
        Assert.That(chain.IsClosedLoop, Is.False);
        Assert.That(() => chain.AddEdge(null), Throws.TypeOf<ArgumentNullException>());
      }
    }

    #endregion

    #region Large SubDs: id indexing, orientation and component index round trips

    // The reference version of these tests hardcoded expected component counts from a
    // closed-form formula and reached into SubDComponent by reflection. The counts are
    // derived from the actual SubD here, and the reflection is gone now that the relative
    // accessors and SubD.ComponentFromComponentIndex are public.

    [Test]
    [TestCase(2u)]
    [TestCase(3u)]
    [TestCase(4u)]
    [TestCase(5u)]
    [TestCase(6u)]
    [TestCase(7u)]
    public void BigSubDFromBoxIndexesById(uint power)
    {
      uint n = (uint)Math.Pow(2, power);   // faces per side
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, n);

      // A closed box grid: 6 sides of n*n faces, and Euler holds.
      Assert.That(box.Faces.Count, Is.EqualTo(6 * n * n));
      Assert.That(box.Vertices.Count - box.Edges.Count + box.Faces.Count, Is.EqualTo(2),
        "V - E + F should be 2 for a closed SubD");

      // Ids are dense and 1 based, and the largest id is the count. This is what makes
      // Find(Count) a valid lookup, and it is the assumption the id based refresh in
      // SubDComponent relies on.
      var vertex_max = box.Vertices.Find((uint)box.Vertices.Count);
      var edge_max = box.Edges.Find((uint)box.Edges.Count);
      var face_max = box.Faces.Find((uint)box.Faces.Count);
      Assert.That(vertex_max, Is.Not.Null);
      Assert.That(edge_max, Is.Not.Null);
      Assert.That(face_max, Is.Not.Null);
      Assert.That(vertex_max.Id, Is.EqualTo((uint)box.Vertices.Count));
      Assert.That(edge_max.Id, Is.EqualTo((uint)box.Edges.Count));
      Assert.That(face_max.Id, Is.EqualTo((uint)box.Faces.Count));

      // Enumeration and id lookup agree, for all three component lists.
      Assert.That(box.Vertices.Count(), Is.EqualTo(box.Vertices.Count));
      Assert.That(box.Edges.Count(), Is.EqualTo(box.Edges.Count));
      Assert.That(box.Faces.Count(), Is.EqualTo(box.Faces.Count));
      foreach (var e in box.Edges)
        Assert.That(box.Edges.Find(e.Id), Is.EqualTo(e));
    }

    [Test]
    [TestCase(2u)]
    [TestCase(4u)]
    [TestCase(8u)]
    public void RelativeAccessorsFollowComponentDirection(uint power)
    {
      uint n = (uint)Math.Pow(2, power);
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, n);

      // An interior edge, so both sides have a face.
      SubDEdge edge = box.Edges.First(e => e.FaceCount == 2);
      Assert.That(edge.ComponentDirection, Is.False);

      SubDVertex from_natural = edge.RelativeVertexFrom;
      SubDVertex to_natural = edge.RelativeVertexTo;
      SubDFace left_natural = edge.RelativeFaceLeft;
      SubDFace right_natural = edge.RelativeFaceRight;

      // In its natural orientation the relative accessors agree with the plain ones.
      Assert.That(from_natural, Is.EqualTo(edge.VertexFrom));
      Assert.That(to_natural, Is.EqualTo(edge.VertexTo));
      Assert.That(left_natural, Is.Not.Null);
      Assert.That(right_natural, Is.Not.Null);
      Assert.That(left_natural.Id, Is.Not.EqualTo(right_natural.Id));

      // Reversing the reference swaps both ends and both sides. This is the whole point of
      // the relative accessors: VertexFrom would not have changed.
      Assert.That(edge.ReverseComponentDirection(), Is.SameAs(edge));
      Assert.That(edge.ComponentDirection, Is.True);
      Assert.That(edge.RelativeVertexFrom, Is.EqualTo(to_natural));
      Assert.That(edge.RelativeVertexTo, Is.EqualTo(from_natural));
      Assert.That(edge.RelativeFaceLeft.Id, Is.EqualTo(right_natural.Id));
      Assert.That(edge.RelativeFaceRight.Id, Is.EqualTo(left_natural.Id));
      Assert.That(edge.VertexFrom, Is.EqualTo(from_natural), "VertexFrom ignores direction");

      // The face comes back oriented so its boundary runs with the edge, so reversing the
      // edge also flips the reported face direction.
      Assert.That(edge.RelativeFaceLeft.ComponentDirection,
        Is.Not.EqualTo(right_natural.ComponentDirection));

      edge.ReverseComponentDirection();
      Assert.That(edge.ComponentDirection, Is.False);
      Assert.That(edge.RelativeVertexFrom, Is.EqualTo(from_natural));

      Assert.That(() => edge.RelativeVertexAt(2), Throws.TypeOf<ArgumentOutOfRangeException>());
      Assert.That(() => edge.RelativeFaceAt(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void BoundaryEdgeHasOneRelativeFace()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 2);
      Assert.That(box.DeleteComponents(new SubDFace[] { box.Faces.First() }), Is.True);

      SubDEdge boundary = box.Edges.First(e => e.FaceCount == 1);
      SubDFace left = boundary.RelativeFaceLeft;
      SubDFace right = boundary.RelativeFaceRight;
      // Exactly one side has a face; the empty side is null, not a bogus face.
      Assert.That(null == left ^ null == right, Is.True);
    }

    [Test]
    [TestCase(2u)]
    [TestCase(6u)]
    public void ComponentIndexRoundTripsForAllTypes(uint power)
    {
      uint n = (uint)Math.Pow(2, power);
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, n);

      SubDVertex vertex = box.Vertices.Find((uint)box.Vertices.Count);
      SubDEdge edge = box.Edges.Find((uint)box.Edges.Count);
      SubDFace face = box.Faces.Find((uint)box.Faces.Count);

      ComponentIndex ci_vertex = vertex.ComponentIndex();
      Assert.That(ci_vertex.ComponentIndexType, Is.EqualTo(ComponentIndexType.SubdVertex));
      Assert.That((uint)ci_vertex.Index, Is.EqualTo(vertex.Id));
      Assert.That(box.ComponentFromComponentIndex(ci_vertex) as SubDVertex, Is.EqualTo(vertex));

      ComponentIndex ci_edge = edge.ComponentIndex();
      Assert.That(ci_edge.ComponentIndexType, Is.EqualTo(ComponentIndexType.SubdEdge));
      Assert.That((uint)ci_edge.Index, Is.EqualTo(edge.Id));
      Assert.That(box.ComponentFromComponentIndex(ci_edge) as SubDEdge, Is.EqualTo(edge));

      ComponentIndex ci_face = face.ComponentIndex();
      Assert.That(ci_face.ComponentIndexType, Is.EqualTo(ComponentIndexType.SubdFace));
      Assert.That((uint)ci_face.Index, Is.EqualTo(face.Id));
      Assert.That(box.ComponentFromComponentIndex(ci_face) as SubDFace, Is.EqualTo(face));
    }

    [Test]
    public void FaceSurfaceCenterNormalIgnoresComponentDirection()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 2);
      box.UpdateSurfaceMeshCache(true);

      SubDFace face = box.Faces.First();
      Vector3d normal = face.SurfaceCenterNormal;

      // SurfaceCenterNormal is a property of the face, not of the reference to it, so
      // flipping the reference must not flip the normal.
      face.ReverseComponentDirection();
      Assert.That(face.ComponentDirection, Is.True);
      Assert.That((face.SurfaceCenterNormal - normal).IsTiny(), Is.True);
    }

    [Test]
    public void ControlNetLineIgnoresComponentDirection()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 2);
      SubDEdge edge = box.Edges.First();
      Line line = edge.ControlNetLine;

      // ControlNetLine is built from VertexFrom and VertexTo, which are the natural ends,
      // so it does not follow ComponentDirection either.
      edge.ReverseComponentDirection();
      Line after = edge.ControlNetLine;
      Assert.That((after.From - line.From).IsTiny(), Is.True);
      Assert.That((after.To - line.To).IsTiny(), Is.True);
    }

    #endregion

    #region Expert path, reached by reflection

    // ON_SubDEdge::SetSharpnessForExperts writes sharpness straight onto an edge and skips
    // the neighbor and vertex updates that SubD.SetEdgeSharpness performs. It is interop
    // only, deliberately not public RhinoCommon API, so this test reaches it by reflection.
    //
    // What it documents is the hazard: after the expert write, the edge reports the new
    // sharpness but the cached subdivision and surface points are stale, and stay stale
    // until they are explicitly cleared. That is exactly why SubDEdge.Sharpness routes
    // through the parent SubD instead of calling this.

    static void SetSharpnessForExperts(SubDEdge edge, SubDEdgeSharpness sharpness)
    {
      const BindingFlags instance_flags = BindingFlags.NonPublic | BindingFlags.Instance;
      const BindingFlags static_flags = BindingFlags.NonPublic | BindingFlags.Static;

      // The edge's native pointer. Note this is an IntPtr: casting it to int truncates on
      // 64-bit, which is where SubD components actually live.
      MethodInfo non_const_pointer = typeof(SubDEdge).GetMethod("NonConstPointer", instance_flags);
      Assert.That(non_const_pointer, Is.Not.Null, "SubDEdge.NonConstPointer not found");
      IntPtr ptr_edge = (IntPtr)non_const_pointer.Invoke(edge, null);
      Assert.That(ptr_edge, Is.Not.EqualTo(IntPtr.Zero));

      // UnsafeNativeMethods is internal and has no namespace; get it out of the assembly a
      // public type lives in rather than hardcoding an assembly identity.
      Type unsafe_native_methods = typeof(SubD).Assembly.GetType("UnsafeNativeMethods");
      Assert.That(unsafe_native_methods, Is.Not.Null, "UnsafeNativeMethods not found");

      MethodInfo setter = unsafe_native_methods.GetMethod("ON_SubDEdge_SetSharpnessForExperts", static_flags);
      Assert.That(setter, Is.Not.Null, "ON_SubDEdge_SetSharpnessForExperts not found");

      // SubDEdgeSharpness is a blittable struct passed by value, so it goes through
      // reflection as a boxed argument with no pointer of its own.
      setter.Invoke(null, new object[] { ptr_edge, sharpness });
    }

    [Test]
    public void ExpertSharpnessWriteLeavesCachedPointsStale()
    {
      SubD box = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      box.UpdateSurfaceMeshCache(true);

      SubDEdge edge = box.Edges.First(e => e.FaceCount == 2);
      SubDVertex vertex = edge.VertexFrom;
      Point3d before = vertex.SurfacePoint();
      uint errors_before = SubD.ErrorCount;

      var sharpness = new SubDEdgeSharpness(0.5, 1.5);
      SetSharpnessForExperts(edge, sharpness);

      // The edge data itself did change.
      Assert.That(edge.Sharpness, Is.EqualTo(sharpness));
      Assert.That(edge.IsSharp, Is.True);

      // ...but nothing invalidated the cache, so the surface point is still the old one.
      Assert.That(vertex.SurfacePoint().DistanceTo(before), Is.LessThan(kTol),
        "the expert write should not have invalidated any cached point");

      // Clearing the neighborhood and rebuilding is what makes the change visible. Rank 1
      // is not always enough, which is why ClearSavedSubdivisionPoints takes a flag.
      vertex.ClearSavedSubdivisionPoints(true);
      edge.ClearSavedSubdivisionPoints(true);
      box.UpdateSurfaceMeshCache(false);

      Assert.That(vertex.SurfacePoint().DistanceTo(before), Is.GreaterThan(kTol),
        "after clearing the cache the sharpened surface point should have moved");

      // The supported path gets there without the manual cache handling.
      SubD other = UnitBox(SubDEdgeSharpness.SmoothValue, 4);
      other.UpdateSurfaceMeshCache(true);
      SubDEdge other_edge = other.Edges.First(e => e.Id == edge.Id);
      Point3d other_before = other_edge.VertexFrom.SurfacePoint();
      other_edge.Sharpness = sharpness;
      other.UpdateSurfaceMeshCache(false);
      Assert.That(other_edge.VertexFrom.SurfacePoint().DistanceTo(other_before), Is.GreaterThan(kTol));

      // Neither route should have tripped an internal error.
      Assert.That(SubD.ErrorCount, Is.EqualTo(errors_before));
    }

    #endregion

    #region Diagnostics

    [Test]
    public void ErrorCountDoesNotRiseOnValidWork()
    {
      uint before = SubD.ErrorCount;

      SubD box = UnitBox(2.0, 3);
      box.SharpEdgeCount();
      box.ClearEdgeSharpness();
      box.Edges.First().Sharpness = new SubDEdgeSharpness(1.0, 2.0);
      box.UpdateSurfaceMeshCache(false);

      Assert.That(SubD.ErrorCount, Is.EqualTo(before));
    }

    #endregion
  }
}
