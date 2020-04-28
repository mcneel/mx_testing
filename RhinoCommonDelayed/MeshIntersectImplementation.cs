using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace RhinoCommonDelayed
{
    public class MeshIntersectImplementation
        : MeasuredImplementationBase
    {
        static MeshIntersectImplementation() { Instance = new MeshIntersectImplementation(); }
        private MeshIntersectImplementation() { }
        public static MeshIntersectImplementation Instance { get; private set; }

        const string incipitString = "MEASURED INTERSECTION";

        public void Model(string filepath)
        {
            ParseAndExecuteNotes(filepath, incipitString, false);
        }

        internal override bool OperateCommand(IEnumerable<Mesh> inputMeshes, IEnumerable<Mesh> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog)
        {
            Polyline[] intersections;
            Polyline[] overlaps;
            bool rc;

            using (TextLog log = new TextLog())
            {
                rc = Intersection.MeshMesh(inputMeshes, tolerance,
                    out intersections, true, out overlaps, false, out _, log,
                    System.Threading.CancellationToken.None, null);
                textLog = log.ToString();
            }

            returned = null;
            var results = intersections != null ? intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = false, Polyline = a }) : Array.Empty<ResultMetrics>();
            if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = true, Polyline = a }));
            returned = results.OrderBy(a => a.Measurement).ToList();

            return rc;
        }

        internal override void CheckAssertions(List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
        {
            Assert.IsTrue(rv, "Return value of Intersection.MeshMesh function");
            Assert.IsEmpty(log_text, "Textlog of function must be empty");

            NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} polylines but expected {expected.Count}.");

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Measurement, result_ordered[i].Measurement, 0.002);
                if (expected[i].Closed.HasValue) Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value,
                    $"Curve of length {expected[i].Measurement} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
                if (expected[i].Overlap.HasValue) Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value,
                    $"Curve of length {expected[i].Measurement} was not {(expected[i].Overlap.Value ? "ovelapping" : "perforating")} as expected.");
            }
        }
    }
}
