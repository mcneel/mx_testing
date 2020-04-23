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
using System.Text;
using System.Threading.Tasks;

namespace RhinoCommonDelayed
{
    class ResultMetrics
    {
        public double Measurement { get; set; }
        public bool? Closed { get; set; }
        public bool? Overlap { get; set; }
        public Polyline Polyline { get; set; }
        public Mesh Mesh { get; set; }
    }

    public abstract class MeasuredImplementationBase
    {

        internal void ParseAndExecuteNotes(string filepath, string notesIncipit)
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

                if (incipit.Trim() == notesIncipit)
                {
                    MeasuredTest(otherlines, file, filepath);
                }
                else
                    throw new NotSupportedException($"Unexpected type of test found in notes: {incipit}");
            }
        }

        internal void MeasuredTest(List<string> otherlines, File3dm file, string filepath)
        {
            var expected = ExtractExpectedValues(otherlines);
            ExtractInputsFromFile(file, out double final_tolerance, out IEnumerable<Mesh> input_meshes);

            string log_text;
            List<ResultMetrics> result_ordered;

            bool rv = OperateCommand(input_meshes, final_tolerance, out result_ordered, out log_text);

            try
            {
                CheckAssertions(expected, result_ordered, rv, log_text);
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
                        $"A {a.GetType().FullName} exception was thrown, then saving the file failed.",
                        new Exception[] { a, b });
                }

                throw;
            }
        }

        internal abstract bool OperateCommand(IEnumerable<Mesh> inputMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog);
        internal abstract void CheckAssertions(List<ResultMetrics> expected, List<ResultMetrics> returned, bool rv, string textLog);

        internal void ExtractInputsFromFile(File3dm file, out double final_tolerance, out IEnumerable<Mesh> all_meshes)
        {
            var items = file.Objects.Select(item => new { item.Id, item.Geometry }).ToList(); //buffer

            var original_meshes = items.Where(item => item.Geometry.ObjectType == ObjectType.Mesh);
            var curves = items.Where(item => item.Geometry.ObjectType == ObjectType.Curve).ToList();

            double doc_tolerance = file.Settings.ModelAbsoluteTolerance;
            double coefficient = Intersection.MeshIntersectionsTolerancesCoefficient;

            final_tolerance = doc_tolerance * coefficient;
            all_meshes = original_meshes.Select(a => a.Geometry as Mesh);
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
        }


        readonly static string[] separators = new string[] { " ", "\t", "- " };
        internal List<ResultMetrics> ExtractExpectedValues(List<string> otherlines)
        {
            var expected = otherlines
                .Select(
                    line =>
                    {
                        var split = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                        var rc = new ResultMetrics
                        {
                            Measurement = double.Parse(split[0], CultureInfo.InvariantCulture),
                        };

                        if (split.Length > 1)
                            rc.Closed = split[1].Equals("CLOSED", StringComparison.InvariantCultureIgnoreCase);

                        if (split.Length > 2)
                            rc.Overlap = split[2].StartsWith("OVERLAP", StringComparison.InvariantCultureIgnoreCase);

                        return rc;
                    }
                )
                .OrderBy(expectedresult => expectedresult.Measurement)
                .ToList();

            return expected;
        }
    }
}
