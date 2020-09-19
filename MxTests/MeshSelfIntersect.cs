using NUnit.Framework;
using RhinoCommonDelayedTests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MxTests
{
    [TestFixture]
    public class MeshSelfIntersect : AnyCommand<MeshSelfIntersect>
    {
        [Test, TestCaseSource(nameof(GetTestModels))]
        public void Model(string filename, string filepath)
        {
            MeshSelfIntersectImplementation.Instance.Model(Path.Combine(filepath, filename));
        }
    }
}
