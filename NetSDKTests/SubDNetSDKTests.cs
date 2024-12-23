using NUnit.Framework;
using Rhino.Geometry;
using System;
using System.Linq;
using System.Reflection;

namespace NetSDKTests
{
  [TestFixture]
  public class SubDNetSDKTests
  {
    [Test]
    public void TestSubDCrease()
    {
      {
        Point3d[] corners = {
          new Point3d(0.0, 0.0, 0.0),
          new Point3d(1.0, 0.0, 0.0),
          new Point3d(1.0, 1.0, 0.0),
          new Point3d(0.0, 1.0, 0.0),
          new Point3d(0.0, 0.0, 1.0),
          new Point3d(1.0, 0.0, 1.0),
          new Point3d(1.0, 1.0, 1.0),
          new Point3d(0.0, 1.0, 1.0)
        };
        SubD box = SubD.CreateSubDBox(corners, 2.0, 10, 10, 10);

        Assert.That(box.SharpEdgeCount(), Is.EqualTo(120));
        box.ClearEdgeSharpness();
        Assert.That(box.SharpEdgeCount(), Is.EqualTo(0));
        box.Edges.Find(93).Sharpness = SubDEdgeSharpness.FromConstantPercentage(25.0);
        box.Edges.Find(511).Sharpness = new SubDEdgeSharpness(2.0);
        box.Edges.Find(512).Sharpness = SubDEdgeSharpness.Crease;
        Assert.That(box.SharpEdgeCount(out SubDEdgeSharpness range), Is.EqualTo(2));
        Assert.That(range, Is.EqualTo(new SubDEdgeSharpness(1.0, 2.0)));

        Box basebox = new Box(Plane.WorldXY, new Point3d[] { new Point3d(0.0, 0.0, 0.0), new Point3d(1.0, 1.0, 1.0) });
        box = SubD.CreateSubDBox(basebox, 2.0, 10, 10, 10);

        SubDEdge edge93 = box.Edges.Find(93);
        SubDEdge edge511 = box.Edges.Find(511);

        Assert.That(edge93.GetSharpness(), Is.EqualTo(new SubDEdgeSharpness(2.0)));
        Assert.That(edge93.Sharpness, Is.EqualTo(new SubDEdgeSharpness(2.0)));
        Assert.That(edge511.GetSharpness(), Is.EqualTo(SubDEdgeSharpness.Smooth));
        Assert.That(edge511.Sharpness, Is.EqualTo(SubDEdgeSharpness.Smooth));

        edge511.Tag = SubDEdgeTag.Crease;
        Assert.That(edge511.GetSharpness(false), Is.EqualTo(SubDEdgeSharpness.Smooth));
        Assert.That(edge511.Sharpness, Is.EqualTo(SubDEdgeSharpness.Crease));
        edge511.Tag = SubDEdgeTag.Smooth;

        edge511.Sharpness = new SubDEdgeSharpness(3.0);
        Assert.That(edge511.Sharpness, Is.EqualTo(new SubDEdgeSharpness(3.0)));

        SubDVertex vertex5 = box.Vertices.Find(5);
        Assert.That(vertex5.ControlNetPoint.DistanceTo(new Point3d(0.0, 0.0, 0.4)), Is.LessThan(10 ^ -6));
        Assert.That(vertex5.EdgeAt(2), Is.EqualTo(edge511));
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.004166666666666667, 0.004166666666666667, 0.4000000000000001)), Is.LessThan(1e-6));

        SubDVertex vertex126 = box.Vertices.Find(126);
        vertex126.SetControlNetPoint(vertex126.ControlNetPoint + new Vector3d(0.1, 0.1, 0.1), false);
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.004166666666666667, 0.004166666666666667, 0.4000000000000001)), Is.LessThan(1e-6));
        vertex5.ClearSavedSubdivisionPoints(false);
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.004166666666666667, 0.004166666666666667, 0.4000000000000001)), Is.LessThan(1e-6));
        vertex5.ClearSavedSubdivisionPoints(true);  // Need to update neighbors for the surface point to get a true update
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.008072916666666667, 0.008072916666666667, 0.4039062500000001)), Is.LessThan(1e-6));

        BindingFlags binding_flags = BindingFlags.NonPublic | BindingFlags.Instance;
        MethodInfo edge_ptr_method = edge511.GetType().GetMethod("NonConstPointer", binding_flags);
        int edge_ptr = (int)edge_ptr_method.Invoke(edge511, null);
        SubDEdgeSharpness sharpness = new SubDEdgeSharpness(0.5, 1.5);
        MethodInfo sharpness_ptr_method = sharpness.GetType().GetMethod("ConstPointer", binding_flags);
        int sharpness_ptr = (int)sharpness_ptr_method.Invoke(sharpness, null);
        binding_flags = BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo set_sharpness_experts = System.Type.GetType(
          "RhinoCommon.UnsafeNativeMethods, " +
          "RhinoCommon.UnsafeNativeMethods, Version=1.0.0.0, Culture=neutral, " +
          "PublicKeyToken=b77a5c561934e089"
        ).GetMethod("ON_SubDEdge_SetSharpnessForExperts", binding_flags);

        set_sharpness_experts.Invoke(null, new object[] { edge_ptr, sharpness_ptr });
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.008072916666666667, 0.008072916666666667, 0.4039062500000001)), Is.LessThan(1e-6));
        edge511.ClearSavedSubdivisionPoints(false);
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.008072916666666667, 0.008072916666666667, 0.4039062500000001)), Is.LessThan(1e-6));
        uint subd_error_count = SubD.ErrorCount;
        box.UpdateSurfaceMeshCache(false);
        Assert.That(SubD.ErrorCount, Is.EqualTo(subd_error_count + 4));  // 4 corner points of the 4 faces around vertex1 don't seal up
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.008072916666666667, 0.008072916666666667, 0.4039062500000001)), Is.LessThan(1e-6));
        edge511.ClearSavedSubdivisionPoints(true);  // In complex cases, updating rank 2 neighborhood is needed!
        Assert.That(SubD.ErrorCount, Is.EqualTo(subd_error_count + 4));
        box.UpdateSurfaceMeshCache(false);
        Assert.That(vertex5.SurfacePoint().DistanceTo(new Point3d(0.007682291666666667, 0.007682291666666666, 0.40351562500000004)), Is.LessThan(1e-6));
        Assert.That(SubD.ErrorCount, Is.EqualTo(subd_error_count + 4));

        SubDEdgeSharpness[] chain_sharp = new SubDEdgeSharpness[5];
        Assert.That(SubDEdgeSharpness.SetEdgeChainSharpness(new Interval(0.0, 3.0), chain_sharp), Is.EqualTo(5));
        Assert.That(chain_sharp[2], Is.EqualTo(new SubDEdgeSharpness(1.0, 1.5)));
        SubDEdge edge841 = box.Edges.Find(841);
        SubDEdge edge481 = box.Edges.Find(481);
        SubDEdge edge572 = box.Edges.Find(572);
        SubDEdge edge491 = box.Edges.Find(491);
        SubDEdge edge851 = box.Edges.Find(851);
        SubDEdge[] chain_edges = { edge841, edge481, edge572, edge491, edge851 };
        bool[] chain_dirs = { true, false, false, true, false };
        foreach (var tuple in chain_edges.Zip(chain_dirs, (x, y) => (edge: x, dir: y)))
        {
          tuple.edge.ComponentDirection = tuple.dir;
        }
        box.SetEdgeSharpness(chain_edges, chain_sharp, false);

        Assert.That(box.SharpEdgeCount(out range), Is.EqualTo(126));
        Assert.That(range, Is.EqualTo(new SubDEdgeSharpness(0.0, 3.0)));

        SubDEdgeSharpness[] chain_sharp2;
        SubDEdgeSharpness.GetEdgeChainSharpness(new Interval(0.0, 3.0), 5, out chain_sharp2);
        Assert.That(chain_sharp2, Is.EquivalentTo(chain_sharp));

        SubDVertex vertex124 = box.Vertices.Find(124);
        Assert.That(vertex124.ControlNetPoint.DistanceTo(new Point3d(0.1, 0.0, 0.2)), Is.LessThan(1e-6));
        Assert.That(edge572.VertexTo, Is.EqualTo(vertex124));
        Assert.That(vertex124.SurfacePoint().DistanceTo(new Point3d(0.09110702872482943, 0, 0.19110702872482946)), Is.LessThan(1e-6));

        // SubD symbox = SubD.CreateSubDBox(corners, SubDEdgeSharpness.CreaseValue, 10, 10, 10);
        // symbox.Reflect(0.5);
        // edge3 = symbox.Edges.Find(1);
        // edge4 = symbox.Edges.Find(10);

        // Assert.That(edge3.GetSharpness(true), Is.EqualTo(SubDEdgeSharpness.Crease));
        // Assert.That(edge4.Sharpness, Is.EqualTo(SubDEdgeSharpness.Smooth));

        // edge4.SetSharpness(2.0, true);

        // Assert.That(edge4.GetSharpness(), Is.EqualTo(new SubDEdgeSharpness(2.0)));
        // Assert.That(symbox.Edges[50].Sharpness, Is.EqualTo(new SubDEdgeSharpness(2.0)));
      }
    }

    [Test]
    public void TestSubDAddFace()
    {
      Box basebox = new Box(Plane.WorldXY, new Point3d[] { new Point3d(0.0, 0.0, 0.0), new Point3d(1.0, 1.0, 1.0) });
      SubD box = SubD.CreateSubDBox(basebox, 2.0, 10, 10, 10);

      SubDEdge edgec1 = box.Edges.Find(21);
      SubDEdge edgec2 = box.Edges.Find(22);
      SubDEdge edgec3 = box.Edges.Find(23);
      SubDEdge edgec4 = box.Edges.Find(24);
      SubDEdge[] chain_edges = { edgec1, edgec2, edgec3, edgec4 };
      bool[] chain_dirs = { true, false, true, false };
      foreach (var tuple in chain_edges.Zip(chain_dirs, (x, y) => (edge: x, dir: y)))
      {
        tuple.edge.ComponentDirection = tuple.dir;
      }

      SubDFace face = box.Faces.Add(chain_edges);
      Assert.That(face.LimitSurfaceCenterPoint.DistanceTo(new Point3d(0.2, 0.2, 0.2)), Is.LessThan(1e-6));
    }

    [Test]
    [TestCase(2U)]
    [TestCase(3U)]
    [TestCase(4U)]
    [TestCase(5U)]
    [TestCase(6U)]
    [TestCase(7U)]
    public void TestBigSubDIndexing(uint power)
    {
      if (power < 2 || power > 7U)
        return;
      uint two_pow_x = (uint)Math.Pow(2, power);
      uint box_size = two_pow_x - 1U;
      Box basebox = new Box(Plane.WorldXY, new Point3d[] { new Point3d(0.0, 0.0, 0.0), new Point3d(box_size, box_size, box_size) });
      Mesh meshbox = Mesh.CreateFromBox(basebox, (int)box_size, (int)box_size, (int)box_size);
      SubD box = SubD.CreateFromMesh(meshbox);

      uint vid_max = 6 * ((uint)Math.Pow(2, 2 * power) - (uint)Math.Pow(2, power + 1)) + 8;
      uint eid_max = 2 * vid_max - 4;
      uint fid_max = vid_max - 2;

      Assert.That(vid_max, Is.EqualTo(box.Vertices.Count));
      Assert.That(eid_max, Is.EqualTo(box.Edges.Count));
      Assert.That(fid_max, Is.EqualTo(box.Faces.Count));

      SubDVertex vertex_max = box.Vertices.Find(vid_max);
      SubDEdge edge_max = box.Edges.Find(eid_max);
      SubDFace face_max_minus_1 = box.Faces.Find(fid_max - 1U);

      Assert.That(vertex_max.Id, Is.EqualTo(box.Vertices.Count));
      Assert.That(edge_max.Id, Is.EqualTo(box.Edges.Count));
      Assert.That(face_max_minus_1.Id, Is.EqualTo(box.Faces.Count - 1U));

      uint vid_corner = 2 * two_pow_x * two_pow_x;
      uint eid_corner = 4 * two_pow_x * (two_pow_x + 1) - 8;
      SubDEdge edge_corner = box.Edges.Find(eid_corner);
      SubDVertex vertex_corner = box.Vertices.Find(vid_corner);

      Assert.That(vertex_max.ControlNetPoint.DistanceTo(new Point3d(0.0, 1.0, box_size - 1.0)), Is.LessThan(1e-6));

      if (power == 2U)
      {
        edge_max.ReverseComponentDirection();
        Assert.That(vertex_max.EdgeAt(3).ComponentDirection, Is.True);
        Assert.That(edge_max.ComponentDirection, Is.True);
      }
      else
      {
        Assert.That(vertex_max.EdgeAt(3).ComponentDirection, Is.False);
        Assert.That(edge_max.ComponentDirection, Is.False);
      }
      Assert.That(vertex_max.EdgeAt(3), Is.EqualTo(edge_max));
      Assert.That(edge_max.RelativeVertexFrom, Is.EqualTo(vertex_max));
      Assert.That(edge_max.RelativeVertexTo.Id, Is.EqualTo(vid_max - 1U));
      Assert.That(edge_max.RelativeFaceRight.ReverseComponentDirection(), Is.EqualTo(face_max_minus_1));
      Assert.That(edge_max.RelativeFaceLeft.Id, Is.EqualTo(fid_max - two_pow_x));

      edge_max.ComponentDirection = true;
      if (power == 2U)
      {
        edge_max.ReverseComponentDirection();
        Assert.That(vertex_max.EdgeAt(3).ComponentDirection, Is.True);
        Assert.That(edge_max.ComponentDirection, Is.False);
      }
      else
      {
        Assert.That(vertex_max.EdgeAt(3).ComponentDirection, Is.False);
        Assert.That(edge_max.ComponentDirection, Is.True);
      }
      Assert.That(vertex_max.EdgeAt(3).ReverseComponentDirection(), Is.EqualTo(edge_max));
      Assert.That(edge_max.RelativeVertexFrom.Id, Is.EqualTo(vid_max - 1U));
      Assert.That(edge_max.RelativeVertexTo, Is.EqualTo(vertex_max));
      Assert.That(edge_max.RelativeFaceRight.Id, Is.EqualTo(fid_max - two_pow_x));
      Assert.That(edge_max.RelativeFaceLeft, Is.EqualTo(face_max_minus_1));

      Assert.That(vertex_corner.ControlNetPoint.DistanceTo(new Point3d(box_size, box_size, box_size)), Is.LessThan(1e-6));
      Assert.That(edge_corner.RelativeVertexTo, Is.EqualTo(vertex_corner));
      edge_corner.ReverseComponentDirection();
      for (int i = 0; i < vertex_max.EdgeCount; i++)
      {
        Console.WriteLine(vertex_max.EdgeAt(i));
      }
      Assert.That(vertex_max.EdgeAt(power == 2U ? 3 : 2), Is.EqualTo(edge_corner));
      BindingFlags binding_flags = BindingFlags.NonPublic | BindingFlags.Instance;
      MethodInfo edge_cptr_method = edge_max.GetType().GetMethod("ConstSubDComponentPtr", binding_flags);
      SubDComponent.SubDComponentPtr edge_cptr = (SubDComponent.SubDComponentPtr)edge_cptr_method.Invoke(edge_max, null);
      // TODO: Make a tests that forces assigning to a part of memory after UInt32.MaxValue?
      // Assert.That((uint)edge_cptr.BasePtr, Is.GreaterThan(UInt32.MaxValue));
      Assert.That(IntPtr.Size, Is.EqualTo(8));

      ComponentIndex ci_vertex_max = vertex_max.ComponentIndex();
      Assert.That(ci_vertex_max.ComponentIndexType, Is.EqualTo(ComponentIndexType.SubdVertex));
      Assert.That(ci_vertex_max.Index, Is.EqualTo(vid_max));
      SubDVertex vertex_max_from_ci = SubDComponent.FromComponentIndex(box, ci_vertex_max) as SubDVertex;
      Assert.That(vertex_max_from_ci, Is.EqualTo(vertex_max));

      box.UpdateSurfaceMeshCache(true);
      Assert.That((face_max_minus_1.SurfaceCenterNormal - new Vector3d(-0.9899494936611665, 0.0, 0.1414213562373095)).IsTiny(), Is.True);
      face_max_minus_1.ReverseComponentDirection();
      Assert.That((face_max_minus_1.SurfaceCenterNormal - new Vector3d(-0.9899494936611665, 0.0, 0.1414213562373095)).IsTiny(), Is.True);

      Line emax_cnetline = edge_max.ControlNetLine;
      Line expected = new Line(box.Vertices.Find(vid_max).ControlNetPoint, new Vector3d(0.0, 1.0, 0.0));
      if (power == 2u)
        expected = new Line(expected.To, expected.From);
      Console.WriteLine(box.Vertices.Find(vid_max).ControlNetPoint);
      Console.WriteLine(emax_cnetline);
      Assert.That((emax_cnetline.From - expected.From).IsTiny(), Is.True);
      Assert.That((emax_cnetline.To - expected.To).IsTiny(), Is.True);
      edge_max.ReverseComponentDirection();
      Assert.That((emax_cnetline.From - expected.From).IsTiny(), Is.True);
      Assert.That((emax_cnetline.To - expected.To).IsTiny(), Is.True);
    }

    [Test]
    [TestCase(2U)]
    [TestCase(3U)]
    [TestCase(8U)]
    [TestCase(9U)]
    public void TestBigSubDIndexingDirectSubDFromBox(uint power)
    {
      if (power < 2)
        return;
      uint two_pow_x = (uint)Math.Pow(2, power);
      uint box_size = two_pow_x - 1U;
      Box basebox = new Box(Plane.WorldXY, new Point3d[] { new Point3d(0.0, 0.0, 0.0), new Point3d(box_size, box_size, box_size) });
      SubD box = SubD.CreateSubDBox(basebox, box_size);

      uint vid_max = 6 * ((uint)Math.Pow(2, 2 * power) - (uint)Math.Pow(2, power + 1)) + 8;
      uint eid_max = 2 * vid_max - 4;
      uint fid_max = vid_max - 2;

      Assert.That(vid_max, Is.EqualTo(box.Vertices.Count));
      Assert.That(eid_max, Is.EqualTo(box.Edges.Count));
      Assert.That(fid_max, Is.EqualTo(box.Faces.Count));

      SubDVertex vertex_max = box.Vertices.Find(vid_max);
      SubDEdge edge_max = box.Edges.Find(eid_max);
      SubDFace face_max = box.Faces.Find(fid_max);

      Assert.That(vertex_max.Id, Is.EqualTo(box.Vertices.Count));
      Assert.That(edge_max.Id, Is.EqualTo(box.Edges.Count));
      Assert.That(face_max.Id, Is.EqualTo(box.Faces.Count));

      Assert.That(vertex_max.ControlNetPoint.DistanceTo(new Point3d(box_size, box_size, box_size)), Is.LessThan(1e-6));

      Assert.That(edge_max.ComponentDirection, Is.False);
      Assert.That(edge_max.RelativeVertexTo, Is.EqualTo(vertex_max));
      Assert.That(edge_max.RelativeVertexFrom.Id, Is.EqualTo(vid_max - two_pow_x - 1U));
      Assert.That(edge_max.RelativeFaceRight.ReverseComponentDirection(), Is.EqualTo(face_max));

      BindingFlags binding_flags = BindingFlags.NonPublic | BindingFlags.Instance;
      MethodInfo edge_cptr_method = edge_max.GetType().GetMethod("ConstSubDComponentPtr", binding_flags);
      SubDComponent.SubDComponentPtr edge_cptr = (SubDComponent.SubDComponentPtr)edge_cptr_method.Invoke(edge_max, null);
      //Assert.That(edge_cptr.BasePtr, Is.GreaterThan(Math.Pow(2, 32)));

      ComponentIndex ci_vertex_max = vertex_max.ComponentIndex();
      Assert.That(ci_vertex_max.ComponentIndexType, Is.EqualTo(ComponentIndexType.SubdVertex));
      Assert.That(ci_vertex_max.Index, Is.EqualTo(vid_max));
      SubDVertex vertex_max_from_ci = SubDComponent.FromComponentIndex(box, ci_vertex_max) as SubDVertex;
      Assert.That(vertex_max_from_ci, Is.EqualTo(vertex_max));

      ComponentIndex ci_edge_max = edge_max.ComponentIndex();
      Assert.That(ci_edge_max.ComponentIndexType, Is.EqualTo(ComponentIndexType.SubdEdge));
      Assert.That(ci_edge_max.Index, Is.EqualTo(eid_max));
      SubDEdge edge_max_from_ci = SubDComponent.FromComponentIndex(box, ci_edge_max) as SubDEdge;
      Assert.That(edge_max_from_ci, Is.EqualTo(edge_max));
    }
  }
}
