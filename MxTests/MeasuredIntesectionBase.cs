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
using System.Linq;

namespace MxTests
{
    internal abstract class MeasuredMeshIntersectionsBase
      : MeasuredIntersectionsBase
    {
        internal override Type TargetType => typeof(Mesh);
    }

    internal abstract class MeasuredSurfaceIntersectionsBase
        : MeasuredIntersectionsBase
    {
        internal override Type TargetType => typeof(Surface);
    }

    internal abstract class MeasuredIntersectionsBase
      : MeasuredBase
    {
        const string incipitString = "MEASURED INTERSECTION";
        public static string IncipitString => incipitString;

        internal override void CheckAssertions(object file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
        {
            Assert.IsTrue(rv, "Return value of intersection function was false.");
            Assert.IsEmpty(log_text, "Textlog of function must be empty");

            NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} curves but expected {expected.Count}.");

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Measurement, result_ordered[i].Measurement, Math.Max(expected[i].Measurement * 10e-8, ((File3dm)file).Settings.ModelAbsoluteTolerance));

                if (expected[i].Closed.HasValue) Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value,
                    $"Curve of length {expected[i].Measurement} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
                if (expected[i].Overlap.HasValue) Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value,
                    $"Curve of length {expected[i].Measurement} was not {(expected[i].Overlap.Value ? "ovelapping" : "perforating")} as expected.");
                if (expected[i].Point != null) Assert.AreEqual(expected[i].Point, result_ordered[i].Point,
                    $"Point is different.");
            }
        }
    }
}
