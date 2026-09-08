using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Threading;

namespace MxTests
{
  // RH-97536. An intersection curve that stops inside the mesh being split separates nothing, so the
  // split hands the mesh back whole. MeshSplitOptions.CompleteOpenCuts joins each such open end to the
  // nearest border of the mesh, or to another cut, along edges the mesh already has.
  //
  // The cases are synthetic rather than models because the model fixtures call Mesh.Split through the
  // overload that has no options object, so a 3dm cannot turn the option on. What is covered here is
  // the managed route: the options object, its defaults, and the inheritance that lets a
  // MeshBooleanOptions instance stand in for a MeshSplitOptions one.
  [TestFixture]
  public class MeshSplitOpenCutsTests
  {
    const double g_tolerance = 1e-6;

    // nx-by-ny unit quads in the XY plane, corner at the origin.
    static Mesh GridSheet(int nx, int ny)
    {
      var mesh = new Mesh();
      for (int j = 0; j <= ny; j++)
        for (int i = 0; i <= nx; i++)
          mesh.Vertices.Add(i, j, 0.0);
      for (int j = 0; j < ny; j++)
        for (int i = 0; i < nx; i++)
        {
          int a = j * (nx + 1) + i;
          mesh.Faces.AddFace(a, a + 1, a + nx + 2, a + nx + 1);
        }
      mesh.Normals.ComputeNormals();
      return mesh;
    }

    // One quad in the plane x = x0, spanning y and z.
    static Mesh StripX(double x0, double y0, double y1, double z0 = -1.0, double z1 = 1.0)
    {
      var mesh = new Mesh();
      mesh.Vertices.Add(x0, y0, z0);
      mesh.Vertices.Add(x0, y1, z0);
      mesh.Vertices.Add(x0, y1, z1);
      mesh.Vertices.Add(x0, y0, z1);
      mesh.Faces.AddFace(0, 1, 2, 3);
      mesh.Normals.ComputeNormals();
      return mesh;
    }

    static Mesh Box(Interval x, Interval y, Interval z)
    {
      var box = Mesh.CreateFromBox(new BoundingBox(x.T0, y.T0, z.T0, x.T1, y.T1, z.T1), 1, 1, 1);
      Assert.That(box, Is.Not.Null);
      return box;
    }

    static double Area(Mesh mesh)
    {
      var amp = AreaMassProperties.Compute(mesh);
      Assert.That(amp, Is.Not.Null);
      return amp.Area;
    }

    static double TotalArea(IEnumerable<Mesh> meshes)
    {
      double total = 0.0;
      foreach (var m in meshes) total += Area(m);
      return total;
    }

    static MeshSplitOptions Options(bool completeOpenCuts, TextLog log = null)
    {
      return new MeshSplitOptions
      {
        Tolerance = g_tolerance,
        SplitAtCoplanar = true,
        CreateNgons = false,
        CompleteOpenCuts = completeOpenCuts,
        TextLog = log
      };
    }

    [Test]
    public void OptionDefaultsAreOffAndInheritedByBooleanOptions()
    {
      Assert.That(new MeshSplitOptions().CompleteOpenCuts, Is.False,
        "CompleteOpenCuts must stay opt-in.");
      Assert.That(new MeshSplitOptions().SplitAtCoplanar, Is.True);
      Assert.That(new MeshSplitOptions().CreateNgons, Is.True);

      // The inheritance is the API contract that lets one options instance serve both operations.
      Assert.That(new MeshBooleanOptions(), Is.InstanceOf<MeshSplitOptions>(),
        "MeshBooleanOptions must derive from MeshSplitOptions.");
      Assert.That(new MeshBooleanOptions().CompleteOpenCuts, Is.False);
    }

    // The cut enters from the y = 0 border and stops in the middle of the sheet.
    [Test]
    public void OneOpenEnd_OffLeavesMeshWhole_OnSplitsInTwo()
    {
      var sheet = GridSheet(8, 8);
      var strip = StripX(4.3, -1.0, 4.5);
      double sheetArea = Area(sheet);

      using (var offLog = new TextLog())
      using (var onLog = new TextLog())
      {
        var off = sheet.DuplicateMesh().Split(new[] { strip }, Options(false, offLog));
        Assert.That(off, Is.Not.Null);
        Assert.That(off.Length, Is.EqualTo(1), "An open cut separates nothing by default.");
        Assert.That(offLog.ToString(), Does.Not.Contain("was added"),
          "Nothing was added, so nothing may be reported.");

        var on = sheet.DuplicateMesh().Split(new[] { strip }, Options(true, onLog));
        Assert.That(on, Is.Not.Null);
        Assert.That(on.Length, Is.EqualTo(2), "The completed cut must separate the sheet.");
        Assert.That(TotalArea(on), Is.EqualTo(sheetArea).Within(1e-9),
          "Completing a cut moves no vertex and drops no face.");

        // A best-effort cut must reach the command line: the added stretch follows mesh edges.
        string text = onLog.ToString();
        Assert.That(text, Does.Contain("Warning"), $"log was: {text}");
        Assert.That(text, Does.Contain("a polyline was added to permit the split"),
          $"Expected the singular warning; log was: {text}");
        // The warning must tell the user how to see the open end for themselves.
        Assert.That(text, Does.Contain("_MeshIntersect"), $"log was: {text}");
        Assert.That(text, Does.Contain("_ShowEnds"), $"log was: {text}");
      }
    }

    // Both ends of the cut are interior: each is joined to the nearest border.
    [Test]
    public void TwoOpenEnds_OffLeavesMeshWhole_OnSplitsInTwo()
    {
      var sheet = GridSheet(8, 8);
      var strip = StripX(4.3, 2.0, 6.0);
      double sheetArea = Area(sheet);

      var off = sheet.DuplicateMesh().Split(new[] { strip }, Options(false));
      Assert.That(off, Is.Not.Null);
      Assert.That(off.Length, Is.EqualTo(1));

      using (var onLog = new TextLog())
      {
        var on = sheet.DuplicateMesh().Split(new[] { strip }, Options(true, onLog));
        Assert.That(on, Is.Not.Null);
        Assert.That(on.Length, Is.EqualTo(2));
        Assert.That(TotalArea(on), Is.EqualTo(sheetArea).Within(1e-9));

        // Two ends completed, so the warning takes its plural form and reports both.
        string text = onLog.ToString();
        Assert.That(text, Does.Contain("Warning"), $"log was: {text}");
        Assert.That(text, Does.Contain("polylines were added to permit the split"),
          $"Expected the plural warning; log was: {text}");
        Assert.That(text, Does.Contain("_MeshIntersect"), $"The plural form must carry the advice too; log was: {text}");
        Assert.That(text, Does.Contain("_ShowEnds"), $"The plural form must carry the advice too; log was: {text}");
      }
    }

    // A cut that already runs border to border is not the option's business: same pieces either way.
    [Test]
    public void BorderToBorderCut_IsUnchangedByTheOption()
    {
      var sheet = GridSheet(8, 8);
      var strip = StripX(4.3, -1.0, 9.0);

      using (var onLog = new TextLog())
      {
        var off = sheet.DuplicateMesh().Split(new[] { strip }, Options(false));
        var on = sheet.DuplicateMesh().Split(new[] { strip }, Options(true, onLog));
        Assert.That(off, Is.Not.Null);
        Assert.That(on, Is.Not.Null);
        Assert.That(off.Length, Is.EqualTo(2));
        Assert.That(on.Length, Is.EqualTo(off.Length), "The option must not alter a cut that already fences.");
        Assert.That(onLog.ToString(), Does.Not.Contain("was added"),
          "A cut that already fences must not warn.");

        var areasOff = new List<double>();
        var areasOn = new List<double>();
        foreach (var m in off) areasOff.Add(Area(m));
        foreach (var m in on) areasOn.Add(Area(m));
        areasOff.Sort();
        areasOn.Sort();
        for (int i = 0; i < areasOff.Count; i++)
          Assert.That(areasOn[i], Is.EqualTo(areasOff[i]).Within(1e-9));
      }
    }

    // A closed target has no border to reach, so the two open ends are joined to each other.
    [Test]
    public void ClosedTarget_OpenEndsCloseALoop()
    {
      var box = Box(new Interval(0, 8), new Interval(0, 8), new Interval(0, 8));
      var strip = StripX(4.3, 2.0, 6.0, 3.0, 10.0); // crosses the top face only
      double boxArea = Area(box);

      var off = box.DuplicateMesh().Split(new[] { strip }, Options(false));
      Assert.That(off, Is.Not.Null);
      Assert.That(off.Length, Is.EqualTo(1));

      using (var onLog = new TextLog())
      {
        var on = box.DuplicateMesh().Split(new[] { strip }, Options(true, onLog));
        Assert.That(on, Is.Not.Null);
        Assert.That(on.Length, Is.EqualTo(2));
        Assert.That(TotalArea(on), Is.EqualTo(boxArea).Within(1e-9));
        Assert.That(onLog.ToString(), Does.Contain("was added to permit the split"),
          $"log was: {onLog}");
      }
    }

    // A MeshBooleanOptions carries the split members through, which is the point of the inheritance.
    [Test]
    public void BooleanOptionsCanDriveASplit()
    {
      var sheet = GridSheet(8, 8);
      var strip = StripX(4.3, -1.0, 4.5);

      var options = new MeshBooleanOptions
      {
        Tolerance = g_tolerance,
        SplitAtCoplanar = true,
        CreateNgons = false,
        CompleteOpenCuts = true,
        CancellationToken = CancellationToken.None
      };

      var pieces = sheet.DuplicateMesh().Split(new[] { strip }, options);
      Assert.That(pieces, Is.Not.Null);
      Assert.That(pieces.Length, Is.EqualTo(2),
        "A MeshBooleanOptions must be usable as the MeshSplitOptions of a split.");
    }

    // The overloads that predate the option must keep the old behaviour.
    [Test]
    public void LegacyOverloadDoesNotCompleteOpenCuts()
    {
      var sheet = GridSheet(8, 8);
      var strip = StripX(4.3, -1.0, 4.5);

      var pieces = sheet.DuplicateMesh().Split(new[] { strip }, g_tolerance, true, false, null,
        CancellationToken.None, null);
      Assert.That(pieces, Is.Not.Null);
      Assert.That(pieces.Length, Is.EqualTo(1),
        "The overload without an options object must not complete open cuts.");
    }
  }
}
