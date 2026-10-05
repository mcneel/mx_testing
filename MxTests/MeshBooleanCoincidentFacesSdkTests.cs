using NUnit.Framework;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxTests
{
  // Adapted, with the author's permission, from the script MeshBooleanSplit_coincident_faces.py (version
  // "2026-09-28 final-6") by Teo (@Teo on discourse.mcneel.com), attached to the report "Rhino 9 Beta:
  // MeshBooleanSplit regression with coincident faces" (RH-99121):
  // https://discourse.mcneel.com/t/rhino-9-beta-meshbooleansplit-regression-with-coincident-faces/223094
  // Permission to adapt and run the script in MxTesting and internally:
  // https://discourse.mcneel.com/t/rhino-9-beta-meshbooleansplit-regression-with-coincident-faces/223094/5
  // Every result is judged against the geometrically correct answer; the script's native commands and document baking are not reachable from the SDK.
  [TestFixture]
  public class MeshBooleanCoincidentFacesSdkTests
  {
    public enum BooleanOperation { Split, Difference, Intersection }

    public enum BoxMeshing
    {
      BrepCoarse,
      BrepCoarseWithDoublePrecision,
      ScriptBuiltUnwelded,
      ScriptBuiltWelded,
      BrepCoarseTriangulated,
      Subdivided,
    }

    public enum Motion { None, Rotation90AboutZ, Rotation37AboutAxis123 }

    public sealed class CoincidentCase
    {
      public string Id;
      public string Description;
      public BoxMeshing TargetMeshing;
      public int TargetDivisions;
      public double[] TargetLo;
      public double[] TargetHi;
      public BoxMeshing CutterMeshing;
      public int CutterDivisions;
      public double[] CutterLo;
      public double[] CutterHi;
      public Motion Motion;
      public double Tolerance; // 0 = the overloads without MeshBooleanOptions
      public override string ToString() => Id;
    }

    sealed class ExpectedVolumes
    {
      public List<double> Split = new List<double>();
      public List<double> Difference = new List<double>();
      public List<double> Intersection = new List<double>();
      public bool SplitMayReturnNothing;

      public List<double> Of(BooleanOperation op)
      {
        switch (op)
        {
          case BooleanOperation.Split: return Split;
          case BooleanOperation.Difference: return Difference;
          default: return Intersection;
        }
      }
    }

    const double g_volume_epsilon = 1e-6;
    // Case data stays free of RhinoCommon types: test sources are enumerated before RhinoCommon can load.
    static readonly double[] g_a_lo = { -5, -5, -5 };
    static readonly double[] g_a_hi = { 5, 5, 5 };
    static readonly double[] g_origin = { 0, 0, 0 };
    static readonly double[] g_five = { 5, 5, 5 };
    static readonly int[][] g_box_faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 } };
    static readonly BooleanOperation[] g_all_operations = { BooleanOperation.Split, BooleanOperation.Difference, BooleanOperation.Intersection };
    static readonly double[] g_gaps = { 2e-9, 3e-9, 5e-9, 1e-8, 3e-8, 7e-8, 1e-7, 1e-6, 1e-3 };
    static readonly double[] g_gaps_at_tolerance_1e6 = { 2e-9, 1e-8, 3e-8, 7e-8, 1e-7 };
    static readonly double[] g_table_recesses = { 0.0, 1e-10, 1e-8, 1e-7, 1e-6, 1e-5, 1e-4, 1e-3, 5e-3, 2e-2 };
    static readonly double[] g_table_tolerances = { 1e-12, 0.0, 1e-6, 1e-4, 1e-2 }; // 0 = the MeshBooleanOptions default

    static Point3d[] CornersOfBox(double[] l, double[] h)
    {
      var lo = new Point3d(l[0], l[1], l[2]);
      var hi = new Point3d(h[0], h[1], h[2]);
      return new[]
      {
        new Point3d(lo.X, lo.Y, lo.Z), new Point3d(hi.X, lo.Y, lo.Z), new Point3d(hi.X, hi.Y, lo.Z), new Point3d(lo.X, hi.Y, lo.Z),
        new Point3d(lo.X, lo.Y, hi.Z), new Point3d(hi.X, lo.Y, hi.Z), new Point3d(hi.X, hi.Y, hi.Z), new Point3d(lo.X, hi.Y, hi.Z),
      };
    }

    static BoundingBox BoundingBoxOfCorners(double[] lo, double[] hi)
    {
      return new BoundingBox(lo[0], lo[1], lo[2], hi[0], hi[1], hi[2]);
    }

    // Double-precision vertices, outward normals; unwelded has 4 vertices per box face, like a box meshed from a Brep.
    static Mesh ScriptBuiltBox(double[] lo, double[] hi, bool welded)
    {
      Point3d[] c = CornersOfBox(lo, hi);
      var m = new Mesh();
      m.Vertices.UseDoublePrecisionVertices = true;
      if (welded)
      {
        foreach (Point3d p in c) m.Vertices.Add(p.X, p.Y, p.Z);
        foreach (int[] f in g_box_faces) m.Faces.AddFace(f[0], f[1], f[2], f[3]);
      }
      else
      {
        foreach (int[] f in g_box_faces)
        {
          int i0 = m.Vertices.Count;
          foreach (int k in f) m.Vertices.Add(c[k].X, c[k].Y, c[k].Z);
          m.Faces.AddFace(i0, i0 + 1, i0 + 2, i0 + 3);
        }
      }
      m.Normals.ComputeNormals();
      return m;
    }

    // Single-precision vertices, as Mesh.CreateFromBrep gives them.
    static Mesh BoxMeshedFromBrep(double[] lo, double[] hi, bool forceDoublePrecision)
    {
      var m = new Mesh();
      Brep brep = Brep.CreateFromBox(BoundingBoxOfCorners(lo, hi));
#pragma warning disable CS0618 // the report's meshes come from MeshingParameters.Coarse, which differs from FastRenderMesh
      foreach (Mesh part in Mesh.CreateFromBrep(brep, MeshingParameters.Coarse)) m.Append(part);
#pragma warning restore CS0618
      m.Normals.ComputeNormals();
      m.Compact();
      if (forceDoublePrecision) m.Vertices.UseDoublePrecisionVertices = true;
      return m;
    }

    static Mesh BuildBox(BoxMeshing meshing, int divisions, double[] lo, double[] hi)
    {
      switch (meshing)
      {
        case BoxMeshing.BrepCoarse: return BoxMeshedFromBrep(lo, hi, false);
        case BoxMeshing.BrepCoarseWithDoublePrecision: return BoxMeshedFromBrep(lo, hi, true);
        case BoxMeshing.ScriptBuiltUnwelded: return ScriptBuiltBox(lo, hi, false);
        case BoxMeshing.ScriptBuiltWelded: return ScriptBuiltBox(lo, hi, true);
        case BoxMeshing.BrepCoarseTriangulated:
          {
            Mesh m = BoxMeshedFromBrep(lo, hi, false);
            m.Faces.ConvertQuadsToTriangles();
            m.Normals.ComputeNormals();
            return m;
          }
        default:
          {
            Mesh m = Mesh.CreateFromBox(BoundingBoxOfCorners(lo, hi), divisions, divisions, divisions);
            m.Normals.ComputeNormals();
            m.Compact();
            return m;
          }
      }
    }

    static Transform TransformOfMotion(Motion motion)
    {
      switch (motion)
      {
        case Motion.Rotation90AboutZ: return Transform.Rotation(1.0, 0.0, Vector3d.ZAxis, Point3d.Origin);
        case Motion.Rotation37AboutAxis123: return Transform.Rotation(RhinoMath.ToRadians(37.0), new Vector3d(1.0, 2.0, 3.0), new Point3d(0.3, 0.7, -0.2));
        default: return Transform.Identity;
      }
    }

    static void CountNakedAndNonManifoldEdges(Mesh m, out int naked, out int nonManifold)
    {
      naked = 0;
      nonManifold = 0;
      var edges = m.TopologyEdges;
      for (int i = 0; i < edges.Count; i++)
      {
        int n = edges.GetConnectedFaces(i).Length;
        if (n == 1) naked++;
        else if (n > 2) nonManifold++;
      }
    }

    static double VolumeOrNaN(Mesh m)
    {
      try { return m.Volume(); }
      catch (Exception) { return double.NaN; }
    }

    static bool IsClosedManifoldSolid(Mesh m)
    {
      CountNakedAndNonManifoldEdges(m, out int naked, out int nonManifold);
      double v = VolumeOrNaN(m);
      return m.IsClosed && naked == 0 && nonManifold == 0 && !double.IsNaN(v) && Math.Abs(v) > g_volume_epsilon;
    }

    static bool VolumesAgree(double actual, double expected)
    {
      return Math.Abs(actual - expected) <= 1e-4 * Math.Max(1.0, Math.Abs(expected));
    }

    static bool MeshHasVertexWithinMillionthOf(Mesh m, Point3d p)
    {
      for (int i = 0; i < m.Vertices.Count; i++)
        if (m.Vertices.Point3dAt(i).DistanceTo(p) < 1e-6) return true;
      return false;
    }

    static int CompareByDecreasingAbsoluteVolume(Mesh a, Mesh b)
    {
      return Math.Abs(VolumeOrNaN(b)).CompareTo(Math.Abs(VolumeOrNaN(a)));
    }

    static List<Mesh> SortedByDecreasingAbsoluteVolume(IEnumerable<Mesh> meshes)
    {
      var sorted = new List<Mesh>(meshes);
      sorted.Sort(CompareByDecreasingAbsoluteVolume);
      return sorted;
    }

    // Comparable description of a result: piece count, then each piece largest first.
    static string SignatureOfMeshes(IEnumerable<Mesh> meshes)
    {
      List<Mesh> sorted = SortedByDecreasingAbsoluteVolume(meshes);
      var text = new StringBuilder();
      text.Append("n=").Append(sorted.Count).Append(" [");
      for (int i = 0; i < sorted.Count; i++)
      {
        Mesh m = sorted[i];
        CountNakedAndNonManifoldEdges(m, out int naked, out int nonManifold);
        if (i > 0) text.Append("; ");
        text.Append(string.Format(CultureInfo.InvariantCulture, "{0} V{1} F{2} nk{3} nm{4} vol={5:F3}",
          m.IsClosed ? "closed" : "open", m.Vertices.Count, m.Faces.Count, naked, nonManifold, VolumeOrNaN(m)));
      }
      return text.Append("]").ToString();
    }

    static Mesh[] RunOperation(BooleanOperation op, Mesh target, Mesh cutter, double tolerance)
    {
      Mesh[] targets = { target };
      Mesh[] cutters = { cutter };
      Mesh[] result;
      if (tolerance == 0.0)
      {
        switch (op)
        {
          case BooleanOperation.Split: result = Mesh.CreateBooleanSplit(targets, cutters); break;
          case BooleanOperation.Difference: result = Mesh.CreateBooleanDifference(targets, cutters); break;
          default: result = Mesh.CreateBooleanIntersection(targets, cutters); break;
        }
      }
      else
      {
        var options = new MeshBooleanOptions { Tolerance = tolerance };
        switch (op)
        {
          case BooleanOperation.Split: result = Mesh.CreateBooleanSplit(targets, cutters, options, out Result _); break;
          case BooleanOperation.Difference: result = Mesh.CreateBooleanDifference(targets, cutters, options, out Result _); break;
          default: result = Mesh.CreateBooleanIntersection(targets, cutters, options, out Result _); break;
        }
      }
      var meshes = new List<Mesh>();
      if (result != null)
        foreach (Mesh m in result)
          if (m != null) meshes.Add(m);
      return meshes.ToArray();
    }

    static double VolumeOfBox(double[] lo, double[] hi)
    {
      double dx = hi[0] - lo[0];
      double dy = hi[1] - lo[1];
      double dz = hi[2] - lo[2];
      double area = dx * dy;
      return area * dz;
    }

    static double OverlapAlongAxis(double tlo, double thi, double clo, double chi)
    {
      return Math.Max(0.0, Math.Min(thi, chi) - Math.Max(tlo, clo));
    }

    // When nothing is cut off, Split may return nothing or the unchanged target.
    static ExpectedVolumes ExpectedVolumesOfBoxes(double[] tlo, double[] thi, double[] clo, double[] chi)
    {
      double overlapX = OverlapAlongAxis(tlo[0], thi[0], clo[0], chi[0]);
      double overlapY = OverlapAlongAxis(tlo[1], thi[1], clo[1], chi[1]);
      double overlapZ = OverlapAlongAxis(tlo[2], thi[2], clo[2], chi[2]);
      double overlapXY = overlapX * overlapY;
      double overlap = overlapXY * overlapZ;
      double remainder = VolumeOfBox(tlo, thi) - overlap;
      var expected = new ExpectedVolumes();
      if (remainder > g_volume_epsilon) expected.Difference.Add(remainder);
      if (overlap > g_volume_epsilon) expected.Intersection.Add(overlap);
      expected.Split.AddRange(expected.Difference);
      expected.Split.AddRange(expected.Intersection);
      expected.Split.Sort();
      expected.SplitMayReturnNothing = expected.Difference.Count == 0 || expected.Intersection.Count == 0;
      return expected;
    }

    static bool ResultIsGeometricallyCorrect(Mesh[] meshes, List<double> expected, bool mayReturnNothing)
    {
      if (mayReturnNothing && meshes.Length == 0) return true;
      if (expected.Count == 0) return meshes.Length == 0;
      if (meshes.Length != expected.Count) return false;
      var volumes = new List<double>();
      foreach (Mesh m in meshes)
      {
        if (!IsClosedManifoldSolid(m)) return false;
        volumes.Add(Math.Abs(VolumeOrNaN(m)));
      }
      volumes.Sort();
      var sortedExpected = new List<double>(expected);
      sortedExpected.Sort();
      for (int i = 0; i < volumes.Count; i++)
        if (!VolumesAgree(volumes[i], sortedExpected[i])) return false;
      return true;
    }

    static string VolumesText(List<double> volumes)
    {
      var parts = new List<string>();
      foreach (double v in volumes) parts.Add(v.ToString("G6", CultureInfo.InvariantCulture));
      return "[" + string.Join(", ", parts) + "]";
    }

    static CoincidentCase BoxesCase(string id, string description, BoxMeshing meshing, double[] targetLo, double[] targetHi, double[] cutterLo, double[] cutterHi)
    {
      return new CoincidentCase
      {
        Id = id, Description = description,
        TargetMeshing = meshing, TargetLo = targetLo, TargetHi = targetHi,
        CutterMeshing = meshing, CutterLo = cutterLo, CutterHi = cutterHi,
      };
    }

    static CoincidentCase MainCase(string id, string description, BoxMeshing meshing)
    {
      return BoxesCase(id, description, meshing, g_a_lo, g_a_hi, g_origin, g_five);
    }

    static CoincidentCase GapCase(string id, string description, double gap, double tolerance)
    {
      double hi = 5.0 + gap;
      CoincidentCase c = BoxesCase(id, description, BoxMeshing.ScriptBuiltUnwelded, g_a_lo, g_a_hi, g_origin, new[] { hi, hi, hi });
      c.Tolerance = tolerance;
      return c;
    }

    static string FormatNumber(double value)
    {
      return value.ToString("G", CultureInfo.InvariantCulture);
    }

    static IEnumerable<CoincidentCase> AllCoincidentCases()
    {
      BoxMeshing brep = BoxMeshing.BrepCoarse;
      yield return MainCase("T01", "B=(0..5)^3, 3 shared faces [main case]", brep);
      yield return BoxesCase("T02", "B=(0,0,0)..(4,4,5), 1 shared face", brep, g_a_lo, g_a_hi, g_origin, new double[] { 4, 4, 5 });
      yield return BoxesCase("T03", "B=(0,0,0)..(4,5,5), 2 shared faces", brep, g_a_lo, g_a_hi, g_origin, new double[] { 4, 5, 5 });
      yield return BoxesCase("T04", "B=(0,0,0)..(5,5,3), 2 shared faces", brep, g_a_lo, g_a_hi, g_origin, new double[] { 5, 5, 3 });
      yield return BoxesCase("T05", "B=(-2,-2,0)..(2,2,5), patch in the middle of A's +Z box face", brep, g_a_lo, g_a_hi, new double[] { -2, -2, 0 }, new double[] { 2, 2, 5 });
      yield return BoxesCase("T06", "B=(0,0,0)..(5,5,8), shared on x=5 and y=5, B passes through z=5", brep, g_a_lo, g_a_hi, g_origin, new double[] { 5, 5, 8 });
      yield return BoxesCase("T07", "roles swapped: target B=(0..5)^3, cutter A (nothing to cut)", brep, g_origin, g_five, g_a_lo, g_a_hi);
      yield return BoxesCase("T08", "opposite orientation: B=(5,0,0)..(10,5,5) touches A from outside (nothing to cut)", brep, g_a_lo, g_a_hi, new double[] { 5, 0, 0 }, new double[] { 10, 5, 5 });
      yield return BoxesCase("T09", "control: B=(-1..1)^3 strictly inside A, no shared faces", brep, g_a_lo, g_a_hi, new double[] { -1, -1, -1 }, new double[] { 1, 1, 1 });

      yield return MainCase("T10", "script-built boxes, double precision, welded", BoxMeshing.ScriptBuiltWelded);
      yield return MainCase("T11", "script-built boxes, double precision, unwelded", BoxMeshing.ScriptBuiltUnwelded);
      yield return MainCase("T12", "triangulated boxes", BoxMeshing.BrepCoarseTriangulated);
      CoincidentCase t13 = MainCase("T13", "subdivided, vertices aligned (A 10x10x10, B 5x5x5)", BoxMeshing.Subdivided);
      t13.TargetDivisions = 10;
      t13.CutterDivisions = 5;
      yield return t13;
      CoincidentCase t14 = MainCase("T14", "subdivided, vertices not aligned (A 3x3x3, B 7x7x7)", BoxMeshing.Subdivided);
      t14.TargetDivisions = 3;
      t14.CutterDivisions = 7;
      yield return t14;
      CoincidentCase t15 = MainCase("T15", "rotated 90 deg (exact)", brep);
      t15.Motion = Motion.Rotation90AboutZ;
      yield return t15;
      CoincidentCase t16 = MainCase("T16", "script-built double-precision boxes, rotated 37 deg about (1,2,3)", BoxMeshing.ScriptBuiltUnwelded);
      t16.Motion = Motion.Rotation37AboutAxis123;
      yield return t16;
      CoincidentCase t17 = MainCase("T17", "single precision (CreateFromBrep), rotated 37 deg", brep);
      t17.Motion = Motion.Rotation37AboutAxis123;
      yield return t17;
      CoincidentCase t18 = MainCase("T18", "CreateFromBrep + UseDoublePrecisionVertices before rotating 37 deg", BoxMeshing.BrepCoarseWithDoublePrecision);
      t18.Motion = Motion.Rotation37AboutAxis123;
      yield return t18;

      for (int k = 0; k < g_gaps.Length; k++)
        yield return GapCase(string.Format(CultureInfo.InvariantCulture, "G{0:D2}", k + 1), "d = " + FormatNumber(g_gaps[k]), g_gaps[k], 0.0);

      for (int k = 0; k < g_gaps_at_tolerance_1e6.Length; k++)
        yield return GapCase(string.Format(CultureInfo.InvariantCulture, "GT{0:D2}", k + 1), "d = " + FormatNumber(g_gaps_at_tolerance_1e6[k]) + ", Tolerance = 1e-6", g_gaps_at_tolerance_1e6[k], 1e-6);
      CoincidentCase gt06 = MainCase("GT06", "single precision rotated 37 deg (as T17), Tolerance = 1e-6", brep);
      gt06.Motion = Motion.Rotation37AboutAxis123;
      gt06.Tolerance = 1e-6;
      yield return gt06;
    }

    public static IEnumerable<TestCaseData> CoincidentCaseOperations()
    {
      foreach (CoincidentCase c in AllCoincidentCases())
        foreach (BooleanOperation op in g_all_operations)
          yield return new TestCaseData(c, op).SetName(c.Id + " " + op + ": " + c.Description);
    }

    public static IEnumerable<TestCaseData> ToleranceTableCells()
    {
      foreach (double d in g_table_recesses)
        foreach (double t in g_table_tolerances)
          yield return new TestCaseData(d, t).SetName("Split with B=(0..5-d)^3: d = " + FormatNumber(d) + ", Tolerance = " + (t == 0.0 ? "default" : FormatNumber(t)));
    }

    [TestCaseSource(nameof(CoincidentCaseOperations))]
    public void BoxesWithSharedFacesGiveTheGeometricallyCorrectPieces(CoincidentCase c, BooleanOperation op)
    {
      SetupFixture.Prerequisites();

      ExpectedVolumes expected = ExpectedVolumesOfBoxes(c.TargetLo, c.TargetHi, c.CutterLo, c.CutterHi);
      Mesh target = BuildBox(c.TargetMeshing, c.TargetDivisions, c.TargetLo, c.TargetHi);
      Mesh cutter = BuildBox(c.CutterMeshing, c.CutterDivisions, c.CutterLo, c.CutterHi);
      if (c.Motion != Motion.None)
      {
        Transform xf = TransformOfMotion(c.Motion);
        target.Transform(xf);
        cutter.Transform(xf);
      }

      Mesh[] result = RunOperation(op, target, cutter, c.Tolerance);
      bool mayReturnNothing = op == BooleanOperation.Split && expected.SplitMayReturnNothing;
      bool correct = ResultIsGeometricallyCorrect(result, expected.Of(op), mayReturnNothing);
      if (!correct)
      {
        double t = c.Tolerance == 0.0 ? new MeshBooleanOptions().Tolerance : c.Tolerance;
        double[] lo = CutterCornerWithFacesWithinToleranceOnTheTarget(c.CutterLo, c.TargetLo, c.TargetHi, t);
        double[] hi = CutterCornerWithFacesWithinToleranceOnTheTarget(c.CutterHi, c.TargetLo, c.TargetHi, t);
        if (!SameCorner(lo, c.CutterLo) || !SameCorner(hi, c.CutterHi))
        {
          ExpectedVolumes onTarget = ExpectedVolumesOfBoxes(c.TargetLo, c.TargetHi, lo, hi);
          correct = ResultIsGeometricallyCorrect(result, onTarget.Of(op), op == BooleanOperation.Split && onTarget.SplitMayReturnNothing);
        }
      }
      Assert.That(correct, Is.True,
        $"{c.Id} {op}: expected volumes {VolumesText(expected.Of(op))}, got {SignatureOfMeshes(result)}");
    }

    // A cutter face within the tolerance of a target face lies on it: the answer may be that of the cutter with those faces moved there.
    static double[] CutterCornerWithFacesWithinToleranceOnTheTarget(double[] cutter, double[] targetLo, double[] targetHi, double tolerance)
    {
      var moved = (double[])cutter.Clone();
      for (int axis = 0; axis < 3; axis++)
      {
        if (Math.Abs(cutter[axis] - targetHi[axis]) <= tolerance) moved[axis] = targetHi[axis];
        else if (Math.Abs(cutter[axis] - targetLo[axis]) <= tolerance) moved[axis] = targetLo[axis];
      }
      return moved;
    }

    static bool SameCorner(double[] a, double[] b)
    {
      return a[0] == b[0] && a[1] == b[1] && a[2] == b[2];
    }

    [TestCaseSource(nameof(ToleranceTableCells))]
    public void SplitIsCorrectAcrossTheToleranceTable(double recess, double tolerance)
    {
      SetupFixture.Prerequisites();

      double t = tolerance == 0.0 ? new MeshBooleanOptions().Tolerance : tolerance;
      double hi = 5.0 - recess;
      double[] cutterHi = { hi, hi, hi };
      ExpectedVolumes expected = ExpectedVolumesOfBoxes(g_a_lo, g_a_hi, g_origin, cutterHi);
      Mesh target = ScriptBuiltBox(g_a_lo, g_a_hi, false);
      Mesh cutter = ScriptBuiltBox(g_origin, cutterHi, false);

      Mesh[] result = RunOperation(BooleanOperation.Split, target, cutter, t);
      bool correct = ResultIsGeometricallyCorrect(result, expected.Split, false);
      if (!correct && recess > 0.0 && recess < t)
      {
        double[] onTarget = CutterCornerWithFacesWithinToleranceOnTheTarget(cutterHi, g_a_lo, g_a_hi, t);
        correct = ResultIsGeometricallyCorrect(result, ExpectedVolumesOfBoxes(g_a_lo, g_a_hi, g_origin, onTarget).Split, false);
      }
      Assert.That(correct, Is.True,
        $"d = {FormatNumber(recess)}, Tolerance = {FormatNumber(t)}: expected volumes {VolumesText(expected.Split)}, got {SignatureOfMeshes(result)}");
    }

    // The marker (5, 2.5, 2.5) is a vertex only of the subdivided box; it lies inside the shared patch of x = 5.
    [TestCase(false, TestName = "Shared patch of the cutter (B marked) is never in the outer piece")]
    [TestCase(true, TestName = "Shared patch of the target (A marked) is never in the outer piece")]
    public void SharedPatchIsNeverInTheOuterPiece(bool markTheTarget)
    {
      SetupFixture.Prerequisites();

      var mark = new Point3d(5.0, 2.5, 2.5);
      Mesh target = markTheTarget ? BuildBox(BoxMeshing.Subdivided, 4, g_a_lo, g_a_hi) : BuildBox(BoxMeshing.BrepCoarse, 0, g_a_lo, g_a_hi);
      Mesh cutter = markTheTarget ? BuildBox(BoxMeshing.BrepCoarse, 0, g_origin, g_five) : BuildBox(BoxMeshing.Subdivided, 2, g_origin, g_five);

      List<Mesh> split = SortedByDecreasingAbsoluteVolume(RunOperation(BooleanOperation.Split, target, cutter, 0.0));
      Assert.That(split, Is.Not.Empty);
      Assert.That(MeshHasVertexWithinMillionthOf(split[0], mark), Is.False, $"Split: the outer piece carries the shared patch; got {SignatureOfMeshes(split)}");

      Mesh[] difference = RunOperation(BooleanOperation.Difference, target, cutter, 0.0);
      foreach (Mesh m in difference)
        Assert.That(MeshHasVertexWithinMillionthOf(m, mark), Is.False, $"Difference carries the shared patch; got {SignatureOfMeshes(difference)}");
    }

    [Test]
    public void MainCaseIsDeterministic()
    {
      SetupFixture.Prerequisites();

      foreach (BooleanOperation op in g_all_operations)
      {
        var signatures = new List<string>();
        for (int run = 0; run < 3; run++)
        {
          Mesh target = BuildBox(BoxMeshing.BrepCoarse, 0, g_a_lo, g_a_hi);
          Mesh cutter = BuildBox(BoxMeshing.BrepCoarse, 0, g_origin, g_five);
          signatures.Add(SignatureOfMeshes(RunOperation(op, target, cutter, 0.0)));
        }
        Assert.That(signatures[1], Is.EqualTo(signatures[0]), $"{op}: run 2 differs from run 1");
        Assert.That(signatures[2], Is.EqualTo(signatures[0]), $"{op}: run 3 differs from run 1");
      }
    }
  }
}
