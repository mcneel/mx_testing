using NUnit.Framework;
using RhinoCommonDelayedTests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MxTests
{
  [TestFixture]
  public class MinorTests
  {
    [TestCase(0.0, false,   0.5, 0.5, -1.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]
    [TestCase(0.0, true,   0.5, 0.5, -1.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]

    [TestCase(-100.0, false,  -99.5, -99.5, -101.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]
    [TestCase(-100.0, true,   -99.5, -99.5, -101.0,  0.0, 0.0, 1.0,   ExpectedResult = 1.0)]

    [TestCase(0.0, false,   0.5, 0.5, -2.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]
    [TestCase(0.0, true,   0.5, 0.5, -2.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]

    [TestCase(-100.0, false,  -99.5, -99.5, -102.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]
    [TestCase(-100.0, true,   -99.5, -99.5, -102.0,  0.0, 0.0, 1.0,   ExpectedResult = 2.0)]

    [TestCase(0.0, false, 0.5, 0.5, -1.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(0.0, true, 0.5, 0.5, -1.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(-100.0, false, -99.5, -99.5, -101.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(-100.0, true, -99.5, -99.5, -101.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(0.0, false, 0.5, 0.5, -2.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(0.0, true, 0.5, 0.5, -2.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]

    [TestCase(-100.0, false, -99.5, -99.5, -102.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    [TestCase(-100.0, true, -99.5, -99.5, -102.0, 0.0, 0.0, -1.0, ExpectedResult = -1.0)]
    public double IntersectionMeshRay(double meshOffset, bool meshFlip,   double x, double y, double z,   double vx, double vy, double vz)
    {
      return MinorImplmentations.IntersectionMeshRay(meshOffset, meshFlip, x, y, z, vx, vy, vz);
    }

    [TestCase(1.0, 10, 10, 0.0,0.0,0.0, ExpectedResult = true)]
    [TestCase(100.0, 10, 10, 99,99,99, ExpectedResult = false)]

    [TestCase(100.0, 100, 100, 70,70,0, ExpectedResult = true)]
    public bool MeshIsPointInside(double radius, int u, int v, double x, double y, double z)
    {
      return MinorImplmentations.MeshIsPointInside(radius, u, v, x, y, z);
    }
  }
}
