using NUnit.Framework;
using Rhino;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Testing.Fixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetSDKTests
{
  [TestFixture]
  public class MatchSrf : RhinoTestFixture
  {    
    [Test]
    public void TesteEdgeToCurveMatch()
    {
      PlaneSurface ps = new PlaneSurface(Plane.WorldXY, new Interval(0, 1), new Interval(-0.5, 0.5));
      NurbsSurface ns = ps.ToNurbsSurface();
      ns.IncreaseDegreeU(3); ns.IncreaseDegreeV(3);

      Curve crv = Curve.CreateInterpolatedCurve(new[]
      {
        new Point3d(2, -2, 0),
        new Point3d(1.5, 0, 0),
        new Point3d(2, 2, 0)
      }, 3);

      Brep b = ns.ToBrep();


      BrepEdge east = b.Trims.Where(t => t.IsoStatus == IsoStatus.East).Select(t => t.Edge).FirstOrDefault();
      Assert.That(east, Is.Not.Null);
      BrepEdge.MatchSrfSettings settings = new BrepEdge.MatchSrfSettings(Continuity.C0_continuous, Continuity.C0_continuous);
      settings.MatchClosestPoints = true;
  
      bool rc = east.Match(crv, settings, out Brep matched, out _);
      Assert.That(rc, Is.True);
      Assert.That(matched, Is.Not.Null);
      Assert.That(matched.IsValid, Is.True);

      BrepEdge matchedEdge = matched.Trims.Where(t => t.IsoStatus == IsoStatus.East).Select(t => t.Edge).FirstOrDefault();
      Assert.That(matchedEdge, Is.Not.Null);

      const int N = 11;
      for(int i = 0; i < N; ++i)
      {
        double fr = (double)i / (N - 1);
        double t = matchedEdge.Domain.ParameterAt(fr);
        Point3d onEdge = matchedEdge.PointAt(t);
        crv.ClosestPoint(onEdge, out t);
        Point3d onCrv = crv.PointAt(t);

        double dist = onEdge.DistanceTo(onCrv);
        Assert.AreEqual(0, dist, 1e-3);
      }
    }

    
    [Test]
    public void TestEdgeTangentMatch()
    {
      PlaneSurface p0 = new PlaneSurface(Plane.WorldXY, new Interval(0, 1), new Interval(-.5, .5));
      PlaneSurface p1 = new PlaneSurface(Plane.WorldYZ, new Interval(-.5, .5), new Interval(-2, -1));
      NurbsSurface ns0 = p0.ToNurbsSurface(), ns1 = p1.ToNurbsSurface();
      ns0.IncreaseDegreeU(3); ns0.IncreaseDegreeV(3);
      ns1.IncreaseDegreeU(3); ns1.IncreaseDegreeV(3);

      Brep b0 = ns0.ToBrep(), b1 = ns1.ToBrep();
      BrepEdge west  = b0.Trims.Where(t => t.IsoStatus == IsoStatus.West).Select(t => t.Edge).FirstOrDefault(); 
      BrepEdge north = b1.Trims.Where(t => t.IsoStatus == IsoStatus.North).Select(t => t.Edge).FirstOrDefault();
      Assert.That(west, Is.Not.Null);
      Assert.That(north, Is.Not.Null);
      var settings = new BrepEdge.MatchSrfSettings(Continuity.C1_continuous, Continuity.C0_continuous);
      settings.ReverseMatchDirection = true;
      bool rc = west.Match(north, settings, out Brep matched, out _);
      Assert.That(rc, Is.True);

      Assert.That(matched, Is.Not.Null);
      Assert.That(matched.IsValid, Is.True);

      BrepEdge m0 = matched.Trims.Where(t => t.IsoStatus == IsoStatus.West).Select(t => t.Edge).FirstOrDefault();
      const int N = 11;
      for(int i = 0; i < N; ++i)
      {
        double fr = (double)i / (N - 1);
        double s = m0.Domain.ParameterAt(fr);
        Point3d onEdge = m0.PointAt(s);

        Assert.That(north.ClosestPoint(onEdge, out double t), Is.True);
        Point3d onTarget = north.PointAt(t);
        double dist = onTarget.DistanceTo(onEdge);
        Assert.AreEqual(0, dist, 1e-3);

        BrepTrim t0 = m0.Brep.Trims[m0.TrimIndices()[0]];
        Point3d uv = t0.PointAt(t0.Domain.ParameterAt(fr));
        Vector3d N0 = t0.Face.NormalAt(uv.X, uv.Y);

        double fr2 = north.Domain.NormalizedParameterAt(t);
        BrepTrim t1 = north.Brep.Trims[north.TrimIndices()[0]];
        uv = t1.PointAt(t1.Domain.ParameterAt(fr2));
        Vector3d N1 = t1.Face.NormalAt(uv.X, uv.Y);

        int parallel = N0.IsParallelTo(N1, RhinoMath.DefaultAngleTolerance);
        Assert.That(parallel, Is.Not.EqualTo(0));
      }
    }

    [Test]
    public void TestEdgeToCurvesMatch()
    {
      PlaneSurface ps = new PlaneSurface(Plane.WorldXY, new Interval(0, 1), new Interval(-0.5, 0.5));
      NurbsSurface ns = ps.ToNurbsSurface();
      ns.IncreaseDegreeU(3); ns.IncreaseDegreeV(3);

      Curve curve = Curve.CreateInterpolatedCurve(new[]
      {
        new Point3d(2, -2, 0),
        new Point3d(1.5, 0, 0),
        new Point3d(2, 2, 0)
      }, 3);

      Curve[] halves = curve.Split(curve.Domain.Mid);

      // curves that are not oriented head-to-tail should also work
      halves[0].Reverse();

      Brep b = ns.ToBrep();

      BrepEdge east = b.Trims.Where(t => t.IsoStatus == IsoStatus.East).Select(t => t.Edge).FirstOrDefault();
      Assert.That(east, Is.Not.Null);
      BrepEdge.MatchSrfSettings settings = new BrepEdge.MatchSrfSettings(Continuity.C0_continuous, Continuity.C0_continuous);
      settings.MatchClosestPoints = true;

      bool rc = east.Match(halves, settings, out Brep matched, out _);
      Assert.That(rc, Is.True);
      Assert.That(matched, Is.Not.Null);
      Assert.That(matched.IsValid, Is.True);

      BrepEdge matchedEdge = matched.Trims.Where(t => t.IsoStatus == IsoStatus.East).Select(t => t.Edge).FirstOrDefault();
      Assert.That(matchedEdge, Is.Not.Null);

      const int N = 11;
      for (int i = 0; i < N; ++i)
      {
        double fr = (double)i / (N - 1);
        double t = matchedEdge.Domain.ParameterAt(fr);
        Point3d onEdge = matchedEdge.PointAt(t);
        curve.ClosestPoint(onEdge, out t);
        Point3d onCrv = curve.PointAt(t);

        double dist = onEdge.DistanceTo(onCrv);
        Assert.AreEqual(0, dist, 1e-3);
      }
    }

    [Test]
    public void TestEdgeCurvatureEdgesMatch()
    {
      PlaneSurface p0 = new PlaneSurface(Plane.WorldXY, new Interval(0, 1), new Interval(-.5, .5));
      PlaneSurface p10 = new PlaneSurface(Plane.WorldYZ, new Interval(-.5, 0), new Interval(-2, -1));
      PlaneSurface p11 = new PlaneSurface(Plane.WorldYZ, new Interval(0, .5), new Interval(-2, -1));
      NurbsSurface ns0 = p0.ToNurbsSurface(), ns10 = p10.ToNurbsSurface(), ns11 = p11.ToNurbsSurface();
      ns0.IncreaseDegreeU(3); ns0.IncreaseDegreeV(3);
      ns10.IncreaseDegreeU(3); ns10.IncreaseDegreeV(3);
      ns11.IncreaseDegreeU(3); ns11.IncreaseDegreeV(3);
      ns10 = ns10.Reverse(0).ToNurbsSurface();

      Brep b0 = ns0.ToBrep();
      Brep b10 = ns10.ToBrep();
      Brep b11 = ns11.ToBrep();

      BrepEdge toMatch = b0.Trims.Where(t => t.IsoStatus == IsoStatus.West).Select(t => t.Edge).FirstOrDefault();
      BrepEdge target0 = b10.Trims.Where(t => t.IsoStatus == IsoStatus.North).Select(t => t.Edge).FirstOrDefault();
      BrepEdge target1 = b11.Trims.Where(t => t.IsoStatus == IsoStatus.North).Select(t => t.Edge).FirstOrDefault();

      var settings = new BrepEdge.MatchSrfSettings(Continuity.C2_continuous, Continuity.C0_continuous);
      bool ok = toMatch.Match(new Curve[] { target0, target1 }, settings, out Brep matched, out _);
      Assert.That(ok, Is.True);
      Assert.That(matched, Is.Not.Null);
      Assert.That(matched.IsValid, Is.True);

      BrepEdge matchedEdge = matched.Trims.Where(t => t.IsoStatus == IsoStatus.West).Select(t => t.Edge).FirstOrDefault();
      Assert.That(matchedEdge, Is.Not.Null);

      Brep[] breps = { b10, b11 };

      const int N = 11;
      for (int i = 0; i < N; ++i)
      {
        double fr = (double)i / (N - 1);
        double t = matchedEdge.Domain.ParameterAt(fr);
        Point3d onEdge = matchedEdge.PointAt(t);

        BrepTrim t0 = matchedEdge.Brep.Trims[matchedEdge.TrimIndices()[0]];
        Point3d uv = t0.PointAt(t0.Domain.ParameterAt(fr));
        Vector3d N0 = t0.Face.NormalAt(uv.X, uv.Y);
        SurfaceCurvature C0 = t0.Face.CurvatureAt(uv.X, uv.Y);

        double minDist = double.MaxValue;
        Vector3d minNormal = Vector3d.Unset;
        SurfaceCurvature minCurvature = null;
        foreach (var item in breps)
        {
          item.ClosestPoint(onEdge, out Point3d closest, out ComponentIndex ci, out double u, out double v, double.MaxValue, out Vector3d normal);
          double dist = closest.DistanceTo(onEdge);
          if (dist < minDist)
          {
            minDist = dist;
            minNormal = normal;
            if (ci.ComponentIndexType == ComponentIndexType.BrepFace)
            {
              minCurvature = item.Faces[ci.Index].CurvatureAt(u, v);
            }
            else if (ci.ComponentIndexType == ComponentIndexType.BrepEdge)
            {
              BrepEdge be = item.Edges[ci.Index];
              minCurvature = item.Faces[be.AdjacentFaces()[0]].CurvatureAt(u, v);
            }
            else
            {
              Assert.Fail();
            }
          }
        }

        Assert.AreEqual(0, minDist, 1e-3);
        int parallel = minNormal.IsParallelTo(N0);
        Assert.That(parallel, Is.Not.EqualTo(0));

        Assert.AreEqual(C0.Kappa(0), minCurvature.Kappa(0));
        Assert.AreEqual(C0.Kappa(1), minCurvature.Kappa(1));
      }
    }
  }
}
