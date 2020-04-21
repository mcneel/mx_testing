using NUnit.Framework;
using RhinoCommonDelayed;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MxTests
{
    [TestFixture]
    public class MeshIntersect
    {
        static readonly List<string> g_test_models = new List<string>();
        private static InvalidOperationException to_throw;

        static MeshIntersect()
        {
            var test_folders = new List<string>();
            foreach (var mdir in OpenRhinoSetup.SettingsXml.Descendants("MX").Descendants("ModelDirectory"))
            {
                bool optional = mdir.Attribute("optional")?.Value != "false"; //defaults to true

                string attempt = mdir.Value;
                if (!Path.IsPathRooted(mdir.Value)) attempt = Path.Combine(OpenRhinoSetup.RhinoSystemDir, attempt);

                if (Directory.Exists(attempt))
                {
                    test_folders.Add(attempt);
                }
                else if (!optional)
                {
                    to_throw = new InvalidOperationException($"Could not find required directory: \"{mdir.Value}\".");
                    break;
                }
            }

            foreach (string folder in test_folders)
            {
                var folder_intersect = Path.Combine(folder, "MeshIntersect");
                if (Directory.Exists(folder_intersect))
                    g_test_models.AddRange(
                        Directory.GetFiles(folder_intersect, @"*.3dm", SearchOption.AllDirectories)
                        );
            }

            g_test_models.RemoveAll(f => Path.GetFileName(f).StartsWith("#", System.StringComparison.InvariantCultureIgnoreCase));
            g_test_models.RemoveAll(f => Path.GetFileName(f).EndsWith("bak", System.StringComparison.InvariantCultureIgnoreCase));
        }

        public MeshIntersect()
        {
            if (to_throw != null) throw to_throw;
        }

        [Test]
        public void ThereAreDataDrivenModels()
        {
            Assert.IsNotEmpty(g_test_models, "There are no data driven models.");
        }


        public static IEnumerable<string[]> GetTestModels() => g_test_models.Select(p => new string[] { Path.GetFileName(p), Path.GetDirectoryName(p) });

        [Test, TestCaseSource(nameof(GetTestModels))]
        public void Model(string filename, string filepath)
        {
            MeshIntersectImplementation.Model(Path.Combine(filepath, filename));
        }
    }
}