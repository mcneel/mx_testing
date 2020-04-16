using NUnit.Framework;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MxTests
{
    public class MeshIntersect
    {
        static readonly List<string> g_test_models;

        static MeshIntersect()
        {
            var folders = OpenRhinoSetup.TestFolders;

            g_test_models = new List<string>();
            foreach (string folder in folders)
            {
                var folder_intersect = Path.Combine(folder, "MeshIntersect");
                if (Directory.Exists(folder_intersect))
                    g_test_models.AddRange(
                        Directory.GetFiles(folder_intersect, @"*.3dm", SearchOption.AllDirectories)
                        );
            }

            //g_test_models.RemoveAll(f => Path.GetFileName(f).StartsWith("#", StringComparison.InvariantCultureIgnoreCase));
        }

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ThereAreDataDrivenModels()
        {
            Assert.IsNotEmpty(g_test_models, "There are no data driven models.");
        }

        public static IEnumerable<string> TestModels => g_test_models;
        const string measuredIntersectionsString = "MEASURED INTERSECTIONS";

        [Test, TestCaseSource(nameof(TestModels))]
        public void Model(string filename)
        {
            using (var file = File3dm.Read(filename))
            {
                var notes = file.Notes.Notes;

                if (string.IsNullOrWhiteSpace(notes))
                    throw new NotSupportedException("Expected notes with information on processing.");

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
                    CountedIntersectionsTest(otherlines, file);
                }
                else
                    throw new NotSupportedException($"Unexpected type of test found in notes: {incipit}");
            }
        }

        private static void CountedIntersectionsTest(List<string> otherlines, File3dm file)
        {
            var expected = ExtractExpectedPolylinesLengths(otherlines);

            var items = file.Objects.Select(item => new { item.Id, item.Geometry}).ToList(); //buffer

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

            for (int i=0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Length, result_ordered[i].Length, 0.002);
                if (expected[i].Closed.HasValue) Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value);
                if (expected[i].Overlap.HasValue) Assert.AreEqual(expected[i].Overlap.Value, result_ordered[i].Overlap.Value);
            }
        }

        class ResultMetrics
        {
            public double Length { get; set; }
            public bool? Closed { get; set; }
            public bool? Overlap { get; set; }
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