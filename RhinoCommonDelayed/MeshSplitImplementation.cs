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

namespace RhinoCommonDelayed
{
    public class MeshSplitImplementation
        : MeasuredImplementationBase
    {
        static MeshSplitImplementation(){ Instance = new MeshSplitImplementation(); }
        private MeshSplitImplementation() {}
        public static MeshSplitImplementation Instance { get; private set; }

        const string incipitString = "MEASURED SPLITS";

        public void Model(string filepath)
        {
            ParseAndExecuteNotes(filepath, incipitString);
        }

        internal override bool OperateCommand(IEnumerable<Mesh> allMeshes, double tolerance, out List<ResultMetrics> result_ordered, out string log_text)
        {
            throw new NotImplementedException();
            /*Polyline[] intersections;
            Polyline[] overlaps;

            using (TextLog log = new TextLog())
            {
                rv = Intersection.MeshMesh(allMeshes, final_tolerance,
                    out intersections, true, out overlaps, false, out _, log,
                    System.Threading.CancellationToken.None, null);
                log_text = log.ToString();
            }

            result_ordered = null;
            var results = intersections != null ? intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = false, Polyline = a }) : Array.Empty<ResultMetrics>();
            if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Measurement = a.Length, Overlap = true, Polyline = a }));
            result_ordered = results.OrderBy(a => a.Measurement).ToList();*/
        }

        internal override void CheckAssertions(List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
        {
            throw new NotImplementedException();
            /*NUnit.Framework.Assert.IsTrue(rv, "Return value of Intersection.MeshMesh function");
            NUnit.Framework.Assert.IsEmpty(log_text, "Textlog of function must be empty");

            NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, "The amount of expected resulting intersections was different.");

            for (int i = 0; i < expected.Count; i++)
            {
                NUnit.Framework.Assert.AreEqual(expected[i].Measurement, result_ordered[i].Measurement, 0.002);
                if (expected[i].Closed.HasValue) NUnit.Framework.Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value,
                    $"Curve of length {expected[i].Measurement} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
                if (expected[i].Overlap.HasValue) NUnit.Framework.Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value,
                    $"Curve of length {expected[i].Measurement} was not {(expected[i].Overlap.Value ? "ovelapping" : "perforating")} as expected.");
            }
            */
        }
    }
}
