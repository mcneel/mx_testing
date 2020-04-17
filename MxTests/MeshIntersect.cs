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
    [TestFixture]
    public class MeshIntersect
    {
        static readonly List<string> g_test_models = new List<string>();

        static MeshIntersect()
        {
            foreach (string folder in OpenRhinoSetup.TestFolders)
            {
                var folder_intersect = Path.Combine(folder, "MeshIntersect");
                if (Directory.Exists(folder_intersect))
                    g_test_models.AddRange(
                        Directory.GetFiles(folder_intersect, @"*.3dm", SearchOption.AllDirectories)
                        );
            }

            g_test_models.RemoveAll(f => Path.GetFileName(f).StartsWith("#", StringComparison.InvariantCultureIgnoreCase));
            g_test_models.RemoveAll(f => Path.GetFileName(f).EndsWith("bak", StringComparison.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ThereAreDataDrivenModels()
        {
            Assert.IsNotEmpty(g_test_models, "There are no data driven models.");
        }


        public static IEnumerable<string[]> GetTestModels() => g_test_models.Select(p => new string[] { Path.GetFileName(p), Path.GetDirectoryName(p) });
        const string measuredIntersectionsString = "MEASURED INTERSECTIONS";

        [Test, TestCaseSource(nameof(GetTestModels))]
        public void Model(string filename, string filepath)
        {
            using (var file = File3dm.Read(Path.Combine(filepath, filename)))
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
                    RhinoCommonDelayed.MeshIntersectImplementation.CountedIntersectionsTest(otherlines, file);
                }
                else
                    throw new NotSupportedException($"Unexpected type of test found in notes: {incipit}");
            }
        }
    }




}