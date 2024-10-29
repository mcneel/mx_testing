using NUnit.Framework;
using Rhino.Geometry;
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
        box.SetEdgeSharpness(chain_edges, chain_dirs, chain_sharp, false);

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
      SubDEdge[] chainedges = { edgec1, edgec2, edgec3, edgec4 };
      bool[] chaindirs = { true, false, true, false };

      SubDFace face = box.Faces.Add(chainedges, chaindirs);
      Assert.That(face.LimitSurfaceCenterPoint.DistanceTo(new Point3d(0.2, 0.2, 0.2)), Is.LessThan(1e-6));
    }
  }
}
