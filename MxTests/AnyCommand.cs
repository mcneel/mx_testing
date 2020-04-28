using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MxTests
{
    abstract public class AnyCommand<T> where T : AnyCommand<T>
    {
        internal static readonly List<string> g_test_models = new List<string>();

        static AnyCommand()
        {
            OpenRhinoSetup.ScanFolders(typeof(T).Name, g_test_models);
        }

        public static IEnumerable<string[]> GetTestModels()
        {
            return g_test_models.Select(p => new string[] { Path.GetFileName(p), Path.GetDirectoryName(p) });
        }

        [Test]
        public void ThereAreDataDrivenModels()
        {
            Assert.IsNotEmpty(g_test_models, $"There are no data driven models for '{GetType().Name}'.");
        }
    }
}
