using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace RhinoCommonDelayedTests
{
  public static class MinorImplmentations
  {

    public static double IntersectionMeshRay(
      double meshOffset, bool meshFlip,
      double x, double y, double z,
      double vx, double vy, double vz)
    {
      Mesh m = new Mesh();
      m.Vertices.Add(meshOffset, meshOffset, meshOffset);
      m.Vertices.Add(meshOffset + 1.0, meshOffset + 0.5, meshOffset);
      m.Vertices.Add(meshOffset + 0.5, meshOffset + 1.0, meshOffset);

      if (meshFlip)
        m.Faces.AddFace(new MeshFace(0, 1, 2));
      else
        m.Faces.AddFace(new MeshFace(0, 2, 1));

      Ray3d ray = new Ray3d(new Point3d(x, y, z), new Vector3d(vx, vy, vz));

      return Intersection.MeshRay(m, ray);
    }

    public static bool MeshIsPointInside(double radius, int u, int v, double x, double y, double z)
    {
      Mesh sphere = Mesh.CreateFromSphere(new Sphere(new Point3d(), radius), u, v);

      return sphere.IsPointInside(new Point3d(x, y, z), 0.0, true);
    }
  }
}
