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
    class ResultMetrics
    {
        public double Length { get; set; }
        public bool? Closed { get; set; }
        public bool? Overlap { get; set; }
        public Polyline Polyline { get; set; }
    }

    public static class MeshIntersectImplementation
    {
        const string measuredIntersectionsString = "MEASURED INTERSECTIONS";

        public static void Model(string filepath)
        {
            using (var file = File3dm.Read(filepath))
            {
                var notes = file.Notes.Notes;

                if (string.IsNullOrWhiteSpace(notes))
                    throw new System.NotSupportedException("Expected notes with information on processing.");

                var otherlines = new List<string>();
                string incipit = null;
                using (var tr = new StringReader(notes))
                {
                    incipit = tr.ReadLine();
                    string new_line;
                    while ((new_line = tr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(new_line) ||
                            new_line.Trim().StartsWith("#", false, CultureInfo.InvariantCulture))
                            continue;
                        otherlines.Add(new_line);
                    }
                }

                if (incipit.Trim() == measuredIntersectionsString)
                {
                    MeshIntersectImplementation.CountedIntersectionsTest(otherlines, file, filepath);
                }
                else
                    throw new System.NotSupportedException($"Unexpected type of test found in notes: {incipit}");
            }
        }

        static void CountedIntersectionsTest(List<string> otherlines, File3dm file, string filepath)
        {
            var expected = ExtractExpectedPolylinesLengths(otherlines);

            var items = file.Objects.Select(item => new { item.Id, item.Geometry }).ToList(); //buffer

            var original_meshes = items.Where(item => item.Geometry.ObjectType == ObjectType.Mesh);
            var curves = items.Where(item => item.Geometry.ObjectType == ObjectType.Curve).ToList();

            double doc_tolerance = file.Settings.ModelAbsoluteTolerance;
            double coefficient = Intersection.MeshIntersectionsTolerancesCoefficient;

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

            List<ResultMetrics> result_ordered = null;
            try
            {
                var results = intersections != null ? intersections.Select(a => new ResultMetrics { Closed = a.IsClosed, Length = a.Length, Overlap = false, Polyline = a }) : Array.Empty<ResultMetrics>();
                if (overlaps != null) results = results.Concat(overlaps.Select(a => new ResultMetrics { Closed = a.IsClosed, Length = a.Length, Overlap = true, Polyline = a }));

                result_ordered = results.OrderBy(a => a.Length).ToList();

                PerformAssertions(expected, result_ordered, rv, log_text);
            }
            catch (AssertionException a)
            {
                string new_name = Path.Combine(Path.GetDirectoryName(filepath), "#" + Path.GetFileName(filepath));

                try
                {
                    File.Copy(filepath, new_name, true);
                    using (File3dm new_file = File3dm.Read(new_name))
                    {
                        Layer debug_layer = new Layer { Name = "DEBUG", Color = Color.Red };
                        new_file.AllLayers.Add(debug_layer);
                        debug_layer = new_file.AllLayers.FindName(debug_layer.Name, Guid.Empty);
                        if (result_ordered != null)
                            foreach (var result in result_ordered)
                            {
                                if (result == null) continue;
                                new_file.Objects.AddCurve(result.Polyline.ToPolylineCurve(),
                                    new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });
                            }
                        new_file.Write(new_name, new File3dmWriteOptions());
                    }
                }
                catch (Exception b)
                {
                    throw new AggregateException(
                        $"An exception of type {a.GetType().FullName} were thrown, then saving the file failed.",
                        new Exception[] { a, b });
                }

                throw;
            }
        }

        private static void PerformAssertions(List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
        {
            Assert.IsTrue(rv, "Return value of Intersection.MeshMesh function");
            Assert.IsEmpty(log_text, "Textlog of function must be empty");

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
                            rc.Overlap = split[2].StartsWith("OVERLAP", StringComparison.InvariantCultureIgnoreCase);

                        return rc;
                    }
                )
                .OrderBy(expectedresult => expectedresult.Length)
                .ToList();

            return expected;
        }
    }
}
