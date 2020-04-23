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
    public class MeshSplit : AnyCommand<MeshSplit>
    {
        [Test, TestCaseSource(nameof(GetTestModels))]
        public void Model(string filename, string filepath)
        {
            MeshSplitImplementation.Instance.Model(Path.Combine(filepath, filename));
        }
    }
}