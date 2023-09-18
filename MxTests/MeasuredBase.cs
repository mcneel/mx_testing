using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MxTests
{
    /// <summary>
    /// Provides a basic implementation for File3dm-based tests that run from a folder.
    /// </summary>
    internal abstract class MeasuredBase
    {
        //Mesh, etc
        internal abstract Type TargetType {  get; }

        internal void ParseAndExecuteNotes(string filepath, string notesIncipit, bool twoGroups)
        {
            string filename = Path.GetFileName(filepath);
            if (filename.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
            {
                Assert.Throws<AssertionException>(
                    delegate { RunParseExecuteNotes(filepath, notesIncipit, twoGroups, true); },
                    "Expected failure, but test succeeded.");
            }
            else
                RunParseExecuteNotes(filepath, notesIncipit, twoGroups, false);
        }


        private void RunParseExecuteNotes(string filepath, string notesIncipit, bool twoGroups, bool shouldThrow)
        {
            using (var file = File3dm.Read(filepath))
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

                if (incipit.Trim().StartsWith(notesIncipit, StringComparison.InvariantCultureIgnoreCase))
                {
                    MeasuredTest(otherlines, file, filepath, twoGroups, shouldThrow);
                }
                else
                    throw new NotSupportedException($"Unexpected type of test found in notes: {incipit}");
            }
        }
        

        internal void MeasuredTest(List<string> otherlines, object file, string filepath, bool twoGroups, bool shouldThrow)
        {
            var expected = ExtractExpectedValues(otherlines);
            ExtractInputsFromFile(file, twoGroups, out double final_tolerance, out IEnumerable<object> inputMeshes, out IEnumerable<object> secondMeshesGroup);

            bool rv = OperateCommandOnGeometry(
              inputMeshes, secondMeshesGroup, final_tolerance,
              out List<ResultMetrics> returned, out string log_text);

            try
            {
                CheckAssertions(file, expected, returned, rv, log_text);
            }
            catch (AssertionException a) when (!shouldThrow)
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
                        if (returned != null)
                            foreach (var result in returned)
                            {
                                if (result == null) continue;

                                if (result.Polyline != null)
                                    new_file.Objects.AddCurve(((Polyline)result.Polyline).ToPolylineCurve(),
                                        new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });
                                else if (result.Curve != null)
                                    new_file.Objects.AddCurve((Curve)result.Curve,
                                        new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });

                                if (result.Mesh != null)
                                    new_file.Objects.AddMesh((Mesh)result.Mesh,
                                        new ObjectAttributes { LayerIndex = debug_layer.Index, ColorSource = ObjectColorSource.ColorFromLayer });

                                if (result.Point != null)
                                    new_file.Objects.AddPoint((Point3d)result.Point,
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

        internal abstract bool OperateCommandOnGeometry
          (IEnumerable<object> inputMeshes, IEnumerable<object> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog);
        internal virtual void CheckAssertions(object file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
        { 
            for (int i = 0; i < expected.Count; i++)
            {
              if (expected[i].TextInfo != null) NUnit.Framework.Assert.AreEqual(expected[i].TextInfo, result_ordered[i].TextInfo,
                  $"Expected different geometry description:");
            }
        }

        internal virtual double ToleranceCoefficient => Rhino.Geometry.Intersect.Intersection.MeshIntersectionsTolerancesCoefficient;

        internal virtual void ExtractInputsFromFile(
            object file, bool usesSecondGroup, out double final_tolerance, out IEnumerable<object> surfaces, out IEnumerable<object> secondSurfacesGroup)
        {
            final_tolerance = 0;
            surfaces = null;
            secondSurfacesGroup = null;
            
            secondSurfacesGroup = null;

            var items = ((File3dm)file).Objects.Cast<object>().Select(
                item => new { ((File3dmObject)item).Id, Geometry = (object)((File3dmObject)item).Geometry, Attributes = (object)((File3dmObject)item).Attributes }).ToList(); //buffer

            var curves = items.Where(item => ((GeometryBase)item.Geometry).ObjectType == ObjectType.Curve).ToList();

            double doc_tolerance = ((File3dm)file).Settings.ModelAbsoluteTolerance;
            double coefficient = ToleranceCoefficient;

            final_tolerance = doc_tolerance * coefficient;

            uint filter = (uint)(TargetType == typeof(Mesh) ? ObjectType.Mesh : ObjectType.Surface | ObjectType.Brep);

            var specific_items = items.Where(a => ((uint)(((GeometryBase)a.Geometry).ObjectType) & filter) != 0).Select(
                i => new { i.Id, i.Geometry, i.Attributes });

            if (curves.Count > 0)
            {
                BoundingBox box = BoundingBox.Empty;
                foreach (GeometryBase orig in items.Select(a => a.Geometry))
                    box.Union(orig.GetBoundingBox(true));

                var test_cplane = ((File3dm)file).AllNamedConstructionPlanes.FindName("Test");
                if (test_cplane == null) throw new NotSupportedException("Curves exist in the document, but there's no CPlane named 'Test' to extrude them.");

                var box_obj = (object)box;
                var test_cplane_obj = (object)test_cplane;

                var meshed_curves = curves.Select(
                    c => new
                    {
                        c.Id,
                        Geometry = (object)((
                            TargetType.IsAssignableFrom(typeof(Mesh)) ?
                            (object)Mesh.CreateFromCurveExtrusion(
                            (Curve)c.Geometry,
                            ((ConstructionPlane)test_cplane_obj).Plane.ZAxis,
                            MeshingParameters.Default,
                            ((BoundingBox)box_obj)
                            ) :
                            (object)Surface.CreateExtrusion(
                            (Curve)c.Geometry,
                            test_cplane.Plane.ZAxis
                            ).ToBrep()
                            )),
                        Attributes = (object)c.Attributes
                    });

                specific_items = specific_items.Concat(meshed_curves);
            }
            
            if (usesSecondGroup)
            {
                var layers = ((File3dm)file).AllLayers.Cast<object>().OrderBy(l => ((Layer)l).Name).Cast<Layer>().ToList();
                if (layers.Count < 2) throw new InvalidOperationException("At least two layers are required for operations that take 2 inputs.");
                var layer0index = layers[0].Index;
                var layer1index = layers[1].Index;

                surfaces = specific_items.Where(i => ((ObjectAttributes)i.Attributes).LayerIndex == layer0index).Select(i => i.Geometry);
                secondSurfacesGroup = specific_items.Where(i => ((ObjectAttributes)i.Attributes).LayerIndex == layer1index).Select(i => i.Geometry);
            }
            else surfaces = specific_items.Select(i => i.Geometry);
        }


        readonly static string[] separators = new string[] { " ", "\t", "- " };
        internal static List<ResultMetrics> ExtractExpectedValues(List<string> otherlines)
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

                        int open_bracket_index = -1;
                        for (int i = 0; i < split.Length; i++)
                        {
                            if (split[i].Length == 0) continue;
                            if (split[i].StartsWith("[", StringComparison.InvariantCultureIgnoreCase))  { open_bracket_index = i; break; }
                        }

                      Console.WriteLine(open_bracket_index);

                        if (open_bracket_index != -1)
                        {
                            rc.TextInfo =
                              string.Join(" ",
                                split.Skip(open_bracket_index)
                                  );

                             rc.TextInfo = SimplifyDescription(rc.TextInfo);
                        }

                      Console.WriteLine(rc.TextInfo);

                      return rc;
                    }
                )
                .OrderBy(expectedresult => expectedresult.Measurement)
                .ToList();

            return expected;
        }

      public static string ObtainVividDescription(Mesh m)
      {
        string rc = HostUtils.DescribeGeometry(m);
        rc = SimplifyDescription(rc);

        return rc;
      }

      public static string SimplifyDescription(string rc)
      { 
        if (rc == null) return null;

        rc = Regex.Replace(rc, @"[\s\(\)\[\]\{\}\;\:]+", " ");
        rc = rc.Trim();

        return rc;
      }
    }
}
