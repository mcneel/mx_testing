using NUnit.Framework;
using Rhino.Geometry;
using System.Threading;

namespace MxTests
{
  // Mesh.Split and Mesh.SelfSplit: an empty array when nothing was cut, null when the split failed or was cancelled.
  [TestFixture]
  public class MeshSplitResultSdkTests
  {
    const double g_tolerance = 1e-6;

    static Mesh Box(double x0, double y0, double z0, double x1, double y1, double z1)
    {
      var box = Mesh.CreateFromBox(new BoundingBox(x0, y0, z0, x1, y1, z1), 1, 1, 1);
      Assert.That(box, Is.Not.Null);
      return box;
    }

    static MeshSplitOptions Options(CancellationToken cancel)
    {
      return new MeshSplitOptions { Tolerance = g_tolerance, CancellationToken = cancel };
    }

    [Test]
    public void SplitByAMeshThatDoesNotTouchGivesAnEmptyArray()
    {
      var pieces = Box(0, 0, 0, 1, 1, 1).Split(new[] { Box(5, 5, 5, 6, 6, 6) }, Options(CancellationToken.None));
      Assert.That(pieces, Is.Not.Null, "Nothing was cut: that is a result, not a failure.");
      Assert.That(pieces, Is.Empty);
    }

    [Test]
    public void SplitThatCutsGivesThePieces()
    {
      var pieces = Box(0, 0, 0, 2, 2, 2).Split(new[] { Box(1, 1, 1, 3, 3, 3) }, Options(CancellationToken.None));
      Assert.That(pieces, Is.Not.Null);
      Assert.That(pieces.Length, Is.GreaterThan(1));
    }

    [Test]
    public void CancelledSplitGivesNull()
    {
      using (var source = new CancellationTokenSource())
      {
        source.Cancel();
        var pieces = Box(0, 0, 0, 2, 2, 2).Split(new[] { Box(1, 1, 1, 3, 3, 3) }, Options(source.Token));
        Assert.That(pieces, Is.Null);
      }
    }

    [Test]
    public void SelfSplitOfAMeshThatDoesNotCrossItselfGivesAnEmptyArray()
    {
      var pieces = Box(0, 0, 0, 1, 1, 1).SelfSplit(Options(CancellationToken.None));
      Assert.That(pieces, Is.Not.Null, "Nothing was cut: that is a result, not a failure.");
      Assert.That(pieces, Is.Empty);
    }

    [Test]
    public void CancelledSelfSplitGivesNull()
    {
      var crossing = Box(0, 0, 0, 2, 2, 2);
      crossing.Append(Box(1, 1, 1, 3, 3, 3));
      using (var source = new CancellationTokenSource())
      {
        source.Cancel();
        Assert.That(crossing.SelfSplit(Options(source.Token)), Is.Null);
      }
    }
  }
}
