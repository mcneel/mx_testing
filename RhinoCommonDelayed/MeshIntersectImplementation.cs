using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RhinoCommonDelayed
{
    class ResultMetrics
    {
        public double Length { get; set; }
        public bool? Closed { get; set; }
        public bool? Overlap { get; set; }
    }

    public static class MeshIntersectImplementation
    {
        public static void CountedIntersectionsTest(List<string> otherlines, object file_)
        {
            File3dm file = (File3dm)file_;

            var expected = ExtractExpectedPolylinesLengths(otherlines);

            var items = file.Objects.Select(item => new { item.Id, item.Geometry }).ToList(); //buffer

            var original_meshes = items.Where(item => item.Geometry.ObjectType == ObjectType.Mesh);
            var curves = items.Where(item => item.Geometry.ObjectType == ObjectType.Curve).ToList();

            double doc_tolerance = file.Settings.ModelAbsoluteTolerance;
            //double coefficient = Intersection.MeshIntersectionsTolerancesCoefficient; //not exposed? why?
            double coefficient = 0.0001;
            double final_tolerance = doc_tolerance * coefficient;

            IEnumerable<Mesh> all_meshes = original_meshes.Select(a => a.Geometry as Mesh);

            if (curves.Count > 0)
            {
                BoundingBox box = BoundingBox.Empty;
                foreach (var orig in original_meshes.Select(a => a.Geometry))
                    box.Union(orig.GetBoundingBox(true));

                var test_cplane = file.AllNamedConstructionPlanes.FindName("Test");
                if (test_cplane == null) throw new NotSupportedException("Curves exist in the document, but there's no Test named CPlane");

                var meshed_curves = curves.Select(
                    c =>
                        Mesh.CreateFromCurveExtrusion(
                            (Curve)c.Geometry,
                            test_cplane.Plane.ZAxis,
                            MeshingParameters.Default,
                            box
                            ));

                all_meshes = all_meshes.Concat(meshed_curves);
            }

            bool rv;
            string log_text;
            Polyline[] intersections;
            Polyline[] overlaps;

            using (TextLog log = new TextLog())
            {
                rv = Intersection.MeshMesh(all_meshes, final_tolerance,
                    out intersections, true, out overlaps, false, out _, log,
                    System.Threading.CancellationToken.None, null);
                log_text = log.ToString();
            }

            Assert.IsTrue(rv, "Return value of Intersection.MeshMesh function");
            Assert.IsEmpty(log_text, "Textlog of function must be empty");

            var results = intersections != null ? intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Length = a.Length, Overlap = false }) : Array.Empty<ResultMetrics>();
            if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Length = a.Length, Overlap = true }));

            var result_ordered = results.OrderBy(a => a.Length).ToList();

            Assert.AreEqual(expected.Count, result_ordered.Count, "The amount of expected resulting intersections was different.");

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Length, result_ordered[i].Length, 0.002);
                if (expected[i].Closed.HasValue) Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value, 
                    $"Curve of length {expected[i].Length} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
                if (expected[i].Overlap.HasValue) Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value,
                    $"Curve of length {expected[i].Length} was not {(expected[i].Overlap.Value ? "ovelapping" : "perforating")} as expected.");
            }
        }

        readonly static string[] separators = new string[] { " ", "\t", "- " };
        private static List<ResultMetrics> ExtractExpectedPolylinesLengths(List<string> otherlines)
        {
            var expected = otherlines
                .Select(
                    line =>
                    {
                        var split = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                        var rc = new ResultMetrics
                        {
                            Length = double.Parse(split[0], CultureInfo.InvariantCulture),
                        };

                        if (split.Length > 1)
                            rc.Closed = split[1].Equals("CLOSED", StringComparison.InvariantCultureIgnoreCase);

                        if (split.Length > 2)
                            rc.Closed = split[1].StartsWith("OVERLAP", StringComparison.InvariantCultureIgnoreCase);

                        return rc;
                    }
                )
                .OrderBy(expectedresult => expectedresult.Length)
                .ToList();

            return expected;
        }
    }
}
