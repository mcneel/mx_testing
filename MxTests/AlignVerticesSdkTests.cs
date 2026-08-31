using NUnit.Framework;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class AlignVerticesSdkTests
  {
    const double Epsilon = 1e-9;

    static Mesh Triangle(Point3d a, Point3d b, Point3d c)
    {
      var mesh = new Mesh();
      mesh.Vertices.Add(a);
      mesh.Vertices.Add(b);
      mesh.Vertices.Add(c);
      mesh.Faces.AddFace(0, 1, 2);
      return mesh;
    }

    static Mesh Grid3x3()
    {
      var mesh = new Mesh();
      for (int j = 0; j < 3; j++)
        for (int i = 0; i < 3; i++)
          mesh.Vertices.Add(i, j, 0.0);

      mesh.Faces.AddFace(0, 1, 4, 3);
      mesh.Faces.AddFace(1, 2, 5, 4);
      mesh.Faces.AddFace(3, 4, 7, 6);
      mesh.Faces.AddFace(4, 5, 8, 7);
      return mesh;
    }

    static Mesh ClosedBox(double x0, double y0, double z0, double size)
    {
      var mesh = new Mesh();
      mesh.Vertices.Add(x0, y0, z0);
      mesh.Vertices.Add(x0 + size, y0, z0);
      mesh.Vertices.Add(x0 + size, y0 + size, z0);
      mesh.Vertices.Add(x0, y0 + size, z0);
      mesh.Vertices.Add(x0, y0, z0 + size);
      mesh.Vertices.Add(x0 + size, y0, z0 + size);
      mesh.Vertices.Add(x0 + size, y0 + size, z0 + size);
      mesh.Vertices.Add(x0, y0 + size, z0 + size);

      mesh.Faces.AddFace(0, 3, 2, 1);
      mesh.Faces.AddFace(4, 5, 6, 7);
      mesh.Faces.AddFace(0, 1, 5, 4);
      mesh.Faces.AddFace(1, 2, 6, 5);
      mesh.Faces.AddFace(2, 3, 7, 6);
      mesh.Faces.AddFace(3, 0, 4, 7);
      return mesh;
    }

    static Mesh[] TwoTrianglesAcrossAGap(double gap)
    {
      return new[]
      {
        Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)),
        Triangle(new Point3d(0, 0, gap), new Point3d(-1, 0, 0), new Point3d(0, -1, 0)),
      };
    }

    static List<Point3d> Points(Mesh mesh)
    {
      return Enumerable.Range(0, mesh.Vertices.Count).Select(i => mesh.Vertices.Point3dAt(i)).ToList();
    }

    static int MovedCount(IEnumerable<List<Point3d>> before, IEnumerable<Mesh> after)
    {
      return before.Zip(after, (b, m) => b.Where((p, i) => p.DistanceTo(m.Vertices.Point3dAt(i)) > Epsilon).Count()).Sum();
    }

    [Test]
    public void ClosedBoxHasNoNakedVertices()
    {
      SetupFixture.Prerequisites();

      var box = ClosedBox(0, 0, 0, 1);

      Assert.That(box.IsClosed, Is.True);
      Assert.That(box.GetNakedEdges(), Is.Null.Or.Empty);
    }

    [TestCase(0.05, ExpectedResult = 0)]
    [TestCase(0.09, ExpectedResult = 0)]
    [TestCase(0.2, ExpectedResult = 1)]
    public int DistanceDecidesWhatIsWithinReach(double distance)
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      MeshVertexList.Align(meshes, distance, null);

      return MovedCount(before, meshes);
    }

    [Test]
    public void ADistanceSpanningEverythingMovesMoreThanTheNearestPair()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      MeshVertexList.Align(meshes, 5.0, null);

      Assert.That(MovedCount(before, meshes), Is.GreaterThan(1));
    }

    [Test]
    public void WithinReachTheGapIsClosed()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);

      MeshVertexList.Align(meshes, 0.2, null);

      Assert.That(meshes[0].Vertices.Point3dAt(0).DistanceTo(meshes[1].Vertices.Point3dAt(0)),
        Is.LessThanOrEqualTo(Epsilon));
    }

    [TestCase(false, 0.0)]
    [TestCase(true, 0.05)]
    public void AverageDecidesWhereTheSurvivingVertexSits(bool average, double expectedZ)
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);

      MeshVertexList.Align(meshes, 0.2, average, null);

      Assert.That(meshes[0].Vertices.Point3dAt(0).Z, Is.EqualTo(expectedZ).Within(Epsilon));
      Assert.That(meshes[1].Vertices.Point3dAt(0).Z, Is.EqualTo(expectedZ).Within(Epsilon));
    }

    [Test]
    public void IndicesRestrictWhatMayMove()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      var excludeTheTouchingPair = meshes
        .Select(m => Enumerable.Range(0, m.Vertices.Count).Select(i => i != 0).ToList())
        .ToList();

      MeshVertexList.Align(meshes, 0.2, false, excludeTheTouchingPair);

      Assert.That(MovedCount(before, meshes), Is.Zero);
    }

    [Test]
    public void IndicesCoveringThePairAllowItToMove()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      var everything = meshes
        .Select(m => Enumerable.Range(0, m.Vertices.Count).Select(i => true).ToList())
        .ToList();

      MeshVertexList.Align(meshes, 0.2, false, everything);

      Assert.That(MovedCount(before, meshes), Is.GreaterThan(0));
    }

    [Test]
    public void IndicesOfTheWrongLengthAreRejected()
    {
      SetupFixture.Prerequisites();

      var mesh = Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0));

      Assert.Throws<ArgumentException>(() => mesh.Vertices.Align(0.2, new[] { true, false }));
    }

    [TestCase(false, ExpectedResult = true)]
    [TestCase(true, ExpectedResult = false)]
    public bool OnlyNakedExcludesInteriorVertices(bool onlyNaked)
    {
      SetupFixture.Prerequisites();

      var meshes = new[] { Grid3x3(), Triangle(new Point3d(1, 1, 0.05), new Point3d(5, 5, 0), new Point3d(5, 6, 0)) };
      var before = meshes.Select(Points).ToList();

      MeshVertexList.Align(meshes, 0.1, onlyNaked, false);

      return MovedCount(before, meshes) > 0;
    }

    [TestCase(false, ExpectedResult = true)]
    [TestCase(true, ExpectedResult = false)]
    public bool OnlyNakedLeavesClosedMeshesAlone(bool onlyNaked)
    {
      SetupFixture.Prerequisites();

      var meshes = new[] { ClosedBox(0, 0, 0, 1), ClosedBox(1.05, 0, 0, 1) };
      var before = meshes.Select(Points).ToList();

      MeshVertexList.Align(meshes, 0.1, onlyNaked, false);

      return MovedCount(before, meshes) > 0;
    }

    [Test]
    public void ASingleMeshAlignsAgainstItself()
    {
      SetupFixture.Prerequisites();

      var mesh = Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0));
      mesh.Vertices.Add(0, 0, 0.1);
      mesh.Vertices.Add(-1, 0, 0);
      mesh.Vertices.Add(0, -1, 0);
      mesh.Faces.AddFace(3, 4, 5);

      int moved = mesh.Vertices.Align(0.2);

      Assert.That(moved, Is.GreaterThan(0));
      Assert.That(mesh.Vertices.Point3dAt(0).DistanceTo(mesh.Vertices.Point3dAt(3)),
        Is.LessThanOrEqualTo(Epsilon));
    }


    [Test]
    public void CoincidentVerticesAreAlreadyAligned()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.0);
      var before = meshes.Select(Points).ToList();

      int moved = MeshVertexList.Align(meshes, 0.2, null);

      Assert.That(moved, Is.Zero);
      Assert.That(MovedCount(before, meshes), Is.Zero);
    }

    [Test]
    public void AZeroDistanceReachesNothing()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      MeshVertexList.Align(meshes, 0.0, null);

      Assert.That(MovedCount(before, meshes), Is.Zero);
    }

    [Test]
    public void AGapExactlyAtTheDistanceIsWithinReach()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.125);
      var before = meshes.Select(Points).ToList();

      MeshVertexList.Align(meshes, 0.125, null);

      Assert.That(MovedCount(before, meshes), Is.GreaterThan(0));
    }

    [Test]
    public void AveragingThreeMeshesLandsOnTheirCentroid()
    {
      SetupFixture.Prerequisites();

      var meshes = new[]
      {
        Triangle(new Point3d(0, 0, 0.0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)),
        Triangle(new Point3d(0, 0, 0.1), new Point3d(-1, 0, 0), new Point3d(0, -1, 0)),
        Triangle(new Point3d(0, 0, 0.2), new Point3d(2, 0, 0), new Point3d(0, 2, 0)),
      };

      MeshVertexList.Align(meshes, 0.25, true, null);

      foreach (var mesh in meshes)
        Assert.That(mesh.Vertices.Point3dAt(0).Z, Is.EqualTo(0.1).Within(Epsilon));
    }

    [Test]
    public void AligningTwiceMovesNothingTheSecondTime()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);

      MeshVertexList.Align(meshes, 0.2, null);

      var settled = meshes.Select(Points).ToList();
      int moved = MeshVertexList.Align(meshes, 0.2, null);

      Assert.That(moved, Is.Zero);
      Assert.That(MovedCount(settled, meshes), Is.Zero);
    }

    static Mesh Grid3x3(double xOffset)
    {
      var mesh = new Mesh();
      for (int j = 0; j < 3; j++)
        for (int i = 0; i < 3; i++)
          mesh.Vertices.Add(i + xOffset, j, 0.0);

      mesh.Faces.AddFace(0, 1, 4, 3);
      mesh.Faces.AddFace(1, 2, 5, 4);
      mesh.Faces.AddFace(3, 4, 7, 6);
      mesh.Faces.AddFace(4, 5, 8, 7);
      return mesh;
    }

    [Test]
    public void OnlyNakedStillJoinsNakedBorders()
    {
      SetupFixture.Prerequisites();

      var meshes = new[] { Grid3x3(0.0), Grid3x3(2.0625) };
      var interiorBefore = new[] { meshes[0].Vertices.Point3dAt(4), meshes[1].Vertices.Point3dAt(4) };

      MeshVertexList.Align(meshes, 0.125, true, false);

      foreach (int border in new[] { 0, 3, 6 })
        Assert.That(meshes[1].Vertices.Point3dAt(border).X, Is.EqualTo(2.0).Within(Epsilon),
          "naked border vertex " + border + " should have joined");

      Assert.That(meshes[0].Vertices.Point3dAt(4).DistanceTo(interiorBefore[0]), Is.LessThanOrEqualTo(Epsilon));
      Assert.That(meshes[1].Vertices.Point3dAt(4).DistanceTo(interiorBefore[1]), Is.LessThanOrEqualTo(Epsilon));
    }

    [Test]
    public void StaticIndicesOfTheWrongLengthAreRejected()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var tooShort = new[] { new[] { true }, new[] { true, true, true } };

      Assert.Throws<ArgumentException>(() => MeshVertexList.Align(meshes, 0.2, false, tooShort));
    }

    [Test]
    public void StaticIndicesOfTheWrongCountAreRejected()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var tooFew = new[] { new[] { true, true, true } };

      Assert.Throws<ArgumentException>(() => MeshVertexList.Align(meshes, 0.2, false, tooFew));
    }

    [Test]
    public void IndicesAreRoutedToTheirOwnMesh()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      var onlyTheSecondMeshMayMove = new[]
      {
        new[] { false, true, true },
        new[] { true, true, true },
      };

      MeshVertexList.Align(meshes, 0.2, false, onlyTheSecondMeshMayMove);

      Assert.That(MovedCount(before, meshes), Is.Zero,
        "excluding vertex 0 of the first mesh alone must prevent the join");
    }

    [TestCase(0.05, ExpectedResult = 0)]
    [TestCase(0.2, ExpectedResult = 1)]
    public int TheReturnValueCountsTheJoins(double distance)
    {
      SetupFixture.Prerequisites();

      return MeshVertexList.Align(TwoTrianglesAcrossAGap(0.1), distance, null);
    }

    [Test]
    public void TheReturnValueCountsVerticesThatMoved()
    {
      SetupFixture.Prerequisites();

      var mesh = new Mesh();
      foreach (var p in new[] {
        new Point3d(-4, 10, 0), new Point3d(-6, -13, 0), new Point3d(15, -13, 0),
        new Point3d(-4, 10, 0), new Point3d(18, 12, 0), new Point3d(16, -12, 0),
        new Point3d(-4, 10, 0), new Point3d(15, -13, 0), new Point3d(16, -12, 0) })
        mesh.Vertices.Add(p);
      mesh.Faces.AddFace(0, 1, 2);
      mesh.Faces.AddFace(3, 4, 5);
      mesh.Faces.AddFace(6, 7, 8);

      var before = Points(mesh);
      int moved = mesh.Vertices.Align(12.0);
      int actually = before.Where((q, i) => q.DistanceTo(mesh.Vertices.Point3dAt(i)) > Epsilon).Count();

      Assert.That(actually, Is.EqualTo(2));
      Assert.That(moved, Is.EqualTo(actually));
    }

    static Mesh ThreeTrianglesSharingCorners()
    {
      var mesh = new Mesh();
      foreach (var p in new[] {
        new Point3d(-4, 10, 0), new Point3d(-6, -13, 0), new Point3d(15, -13, 0),
        new Point3d(-4, 10, 0), new Point3d(18, 12, 0), new Point3d(16, -12, 0),
        new Point3d(-4, 10, 0), new Point3d(15, -13, 0), new Point3d(16, -12, 0) })
        mesh.Vertices.Add(p);

      mesh.Faces.AddFace(0, 1, 2);
      mesh.Faces.AddFace(3, 4, 5);
      mesh.Faces.AddFace(6, 7, 8);
      return mesh;
    }

    static string Faces(Mesh mesh)
    {
      return string.Join(" ", Enumerable.Range(0, mesh.Faces.Count)
        .Select(k => $"({mesh.Faces[k].A},{mesh.Faces[k].B},{mesh.Faces[k].C},{mesh.Faces[k].D})"));
    }

    [Test]
    public void OnlyTheCollapsedFaceIsCulled()
    {
      SetupFixture.Prerequisites();

      var mesh = ThreeTrianglesSharingCorners();

      mesh.Vertices.Align(12.0);
      mesh.Compact();

      Assert.That(mesh.Vertices.Count, Is.EqualTo(6));
      Assert.That(Faces(mesh), Is.EqualTo("(0,1,2,2) (3,4,5,5)"),
        "the third triangle collapses; the first two must survive unrewired");
    }

    [Test]
    public void AligningDoesNotRewireSurvivingFaces()
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Faces).ToList();

      MeshVertexList.Align(meshes, 0.2, null);

      Assert.That(meshes.Select(Faces).ToList(), Is.EqualTo(before));
    }

    [TestCase(false, ExpectedResult = 1)]
    [TestCase(true, ExpectedResult = 2)]
    public int AveragingMovesBothSidesAndSaysSo(bool average)
    {
      SetupFixture.Prerequisites();

      var meshes = TwoTrianglesAcrossAGap(0.1);
      var before = meshes.Select(Points).ToList();

      int moved = MeshVertexList.Align(meshes, 0.2, average, null);

      Assert.That(moved, Is.EqualTo(MovedCount(before, meshes)));
      return moved;
    }

    [Test]
    public void AveragingAlsoMergesCoincidentDuplicates()
    {
      SetupFixture.Prerequisites();

      var mesh = ThreeTrianglesSharingCorners();

      mesh.Vertices.Align(12.0, true);
      mesh.Compact();

      TestContext.WriteLine("after: V=" + mesh.Vertices.Count + " F=" + mesh.Faces.Count + " " + Faces(mesh));
      for (int i = 0; i < mesh.Vertices.Count; i++)
        TestContext.WriteLine("  v" + i + " = " + mesh.Vertices.Point3dAt(i));

      Assert.That(mesh.Faces.Count, Is.EqualTo(2),
        "the third triangle merges onto the others and must collapse");
      Assert.That(mesh.Vertices.Count, Is.EqualTo(6));
      Assert.That(mesh.IsValid, Is.True);

      foreach (int merged in new[] { 2, 5 })
      {
        Assert.That(mesh.Vertices.Point3dAt(merged).X, Is.EqualTo(15.5).Within(Epsilon));
        Assert.That(mesh.Vertices.Point3dAt(merged).Y, Is.EqualTo(-12.5).Within(Epsilon));
      }
    }

    static Mesh[] ThreeInAChain()
    {
      return new[]
      {
        Triangle(new Point3d(0.0,   0, 0), new Point3d(0, 5, 0), new Point3d(0, 6, 0)),
        Triangle(new Point3d(0.125, 0, 0), new Point3d(3, 5, 0), new Point3d(3, 6, 0)),
        Triangle(new Point3d(0.25,  0, 0), new Point3d(6, 5, 0), new Point3d(6, 6, 0)),
      };
    }

    [TestCase(false, 0.0,    0.0,    1)]
    [TestCase(true,  0.0625, 0.0625, 2)]
    public void AClaimedVertexIsNotStolenByAFartherTarget(bool average, double expectedA, double expectedB, int expectedMoved)
    {
      SetupFixture.Prerequisites();

      var meshes = ThreeInAChain();
      int moved = MeshVertexList.Align(meshes, 0.1875, average, null);

      Assert.That(moved, Is.EqualTo(expectedMoved));
      Assert.That(meshes[0].Vertices.Point3dAt(0).X, Is.EqualTo(expectedA).Within(Epsilon));
      Assert.That(meshes[1].Vertices.Point3dAt(0).X, Is.EqualTo(expectedB).Within(Epsilon));
      Assert.That(meshes[2].Vertices.Point3dAt(0).X, Is.EqualTo(0.25).Within(Epsilon),
        "C is out of reach of A and never claimed anything, so it stays put");

      Assert.That(meshes[0].Vertices.Point3dAt(0).DistanceTo(meshes[1].Vertices.Point3dAt(0)),
        Is.LessThanOrEqualTo(Epsilon),
        "A claimed B first and keeps it, so the two merge rather than being pulled apart");
    }


    [Test]
    public void AlignerSupportsTheDocumentedGeometryKinds()
    {
      SetupFixture.Prerequisites();

      var polyline = new PolylineCurve(new[] { new Point3d(0, 0, 0), new Point3d(1, 0, 0) });
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(1, 0, 0));
       var arc = new ArcCurve(new Arc(new Point3d(0, 0, 0), new Point3d(1, 1, 0), new Point3d(2, 0, 0)));

      Assert.That(Aligner.SupportsGeometry(Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0))), Is.True);
      Assert.That(Aligner.SupportsGeometry(new PointCloud(new[] { new Point3d(0, 0, 0) })), Is.True);
      Assert.That(Aligner.SupportsGeometry(polyline), Is.True);
      Assert.That(Aligner.SupportsGeometry(line), Is.True);
      Assert.That(Aligner.SupportsGeometry(arc), Is.False, "an arc has no control points on the curve");
      Assert.That(Aligner.SupportsGeometry(null), Is.False);
    }

    [Test]
    public void AlignerMovesAPolylineOntoAMesh()
    {
      SetupFixture.Prerequisites();

      var mesh = Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0));
      var polyline = new PolylineCurve(new[] { new Point3d(0, 0, 0.05), new Point3d(4, 0, 0), new Point3d(4, 4, 0) });

      int moved = Aligner.AlignVertices(new GeometryBase[] { mesh, polyline }, 0.1, true, false);

      Assert.That(moved, Is.EqualTo(1));
      Assert.That(polyline.Point(0).DistanceTo(new Point3d(0, 0, 0)), Is.LessThanOrEqualTo(Epsilon),
        "the polyline point is always eligible, so OnlyNaked does not exclude it");
      Assert.That(polyline.Point(1).DistanceTo(new Point3d(4, 0, 0)), Is.LessThanOrEqualTo(Epsilon));
    }

    [Test]
    public void AlignerAveragesAcrossGeometryKinds()
    {
      SetupFixture.Prerequisites();

      var mesh = Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0));
      var cloud = new PointCloud(new[] { new Point3d(0, 0, 0.1) });

      int moved = Aligner.AlignVertices(new GeometryBase[] { mesh, cloud }, 0.2, false, true);

      Assert.That(moved, Is.EqualTo(2));
      Assert.That(mesh.Vertices.Point3dAt(0).Z, Is.EqualTo(0.05).Within(Epsilon));
      Assert.That(cloud[0].Location.Z, Is.EqualTo(0.05).Within(Epsilon));
    }

    [Test]
    public void AlignerRejectsUnsupportedGeometry()
    {
      SetupFixture.Prerequisites();

      var mesh = Triangle(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0));
      var arc = new ArcCurve(new Arc(new Point3d(0, 0, 0), new Point3d(1, 1, 0), new Point3d(2, 0, 0)));

      Assert.Throws<ArgumentException>(() => Aligner.AlignVertices(new GeometryBase[] { mesh, arc }, 0.2, false, false));
      Assert.Throws<ArgumentNullException>(() => Aligner.AlignVertices(null, 0.2, false, false));
    }

    [Test]
    public void SuggestDistanceFindsTheGapBetweenObjects()
    {
      SetupFixture.Prerequisites();

      // two grids whose facing borders are 0.25 apart, well inside their own 1.0 spacing
      var meshes = new GeometryBase[] { Grid3x3(0.0), Grid3x3(2.25) };

      double suggested = Aligner.SuggestDistance(meshes);

      Assert.That(suggested, Is.EqualTo(0.25).Within(Epsilon),
        "the border vertices are nearer to the other grid than to their own neighbours");
    }

    [Test]
    public void SuggestDistanceClosesTheGapItReports()
    {
      SetupFixture.Prerequisites();

      var meshes = new[] { Grid3x3(0.0), Grid3x3(2.25) };
      double suggested = Aligner.SuggestDistance(meshes);

      int moved = Aligner.AlignVertices(meshes, suggested, false, false);

      Assert.That(moved, Is.EqualTo(3), "the three facing border vertices join");
      foreach (int border in new[] { 0, 3, 6 })
        Assert.That(meshes[1].Vertices.Point3dAt(border).X, Is.EqualTo(2.0).Within(Epsilon));
    }

    [Test]
    public void SuggestDistanceIsZeroWhenNothingIsShared()
    {
      SetupFixture.Prerequisites();

      // far apart: every vertex is nearest to one of its own
      var meshes = new GeometryBase[] { Grid3x3(0.0), Grid3x3(50.0) };

      Assert.That(Aligner.SuggestDistance(meshes), Is.Zero);
      Assert.Throws<ArgumentNullException>(() => Aligner.SuggestDistance(null));
    }

    [Test]
    public void SuggestDistanceSpansGeometryKinds()
    {
      SetupFixture.Prerequisites();

      // the polyline runs along the grid border, 0.25 above it, and both have 1.0 spacing of
      // their own, so every facing point is nearer to the other object than to its neighbours
      var grid = Grid3x3(0.0);
      var polyline = new PolylineCurve(new[] { new Point3d(0, 0, 0.25), new Point3d(1, 0, 0.25), new Point3d(2, 0, 0.25) });

      double suggested = Aligner.SuggestDistance(new GeometryBase[] { grid, polyline });

      Assert.That(suggested, Is.EqualTo(0.25).Within(Epsilon));
    }
    [Test]
    public void NullMeshesAreRejected()
    {
      SetupFixture.Prerequisites();

      Assert.Throws<ArgumentNullException>(() => MeshVertexList.Align(null, 0.2, null));
    }
  }
}
