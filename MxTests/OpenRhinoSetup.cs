using NUnit.Framework;
using System;
using Rhino;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

namespace MxTests
{
    [SetUpFixture]
    public class OpenRhinoSetup
    {
        private static readonly List<string> test_folders = new List<string>();
        static OpenRhinoSetup()
        {
            string test_dir = TestContext.CurrentContext.TestDirectory; //this will be /bin

            string base_dir = Directory.GetParent(test_dir).FullName;
            string public_models = Path.Combine(base_dir, "models");

            if (Directory.Exists(public_models)) test_folders.Add(public_models);
        }

        private IDisposable rhinoCore;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            RhinoInside.Resolver.Initialize();
            RhinoInside.Resolver.RhinoSystemDirectory = @"C:\dev\github\mcneel\rhino\src4\bin\Debug";

            ReferenceRhinoCommonToOpenRhino();
        }
        
        void ReferenceRhinoCommonToOpenRhino()
        {
            rhinoCore = new Rhino.Runtime.InProcess.RhinoCore();
        }

        public static IEnumerable<string> TestFolders => test_folders;



        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            rhinoCore.Dispose();
            rhinoCore = null;
        }
    }

    public class OpenRhinoSetupTests
    {
        [Test]
        public void ThereAreDataDrivenFolders()
        {
            Assert.IsNotEmpty(OpenRhinoSetup.TestFolders);
        }
    }
}

