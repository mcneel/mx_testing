using NUnit.Framework;
using System;
using Rhino;
using System.IO;
using System.Collections.Generic;

namespace MxTests
{
    [SetUpFixture]
    public sealed class OpenRhinoSetup : IDisposable
    {
        private static readonly string[] test_folders;
        static OpenRhinoSetup()
        {
            string test_dir = TestContext.CurrentContext.TestDirectory; //this will be /bin
            string base_dir = Directory.GetParent(test_dir).FullName;
            string public_models = Path.Combine(base_dir, "models");

            var test_folders_list = new List<string>();

            if (Directory.Exists(public_models)) test_folders_list.Add(public_models);

            test_folders = test_folders_list.ToArray();
        }
        
        private IDisposable rhinoCore;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            RhinoInside.Resolver.Initialize();
            RhinoInside.Resolver.RhinoSystemDirectory = @"C:\dev\github\mcneel\rhino\src4\bin\Debug";

            ReferenceRhinoCommonToOpenRhino();


        }

        public static IEnumerable<string> TestFolders => test_folders;

        void ReferenceRhinoCommonToOpenRhino()
        {
            rhinoCore = new Rhino.Runtime.InProcess.RhinoCore();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            rhinoCore.Dispose();
            rhinoCore = null;
        }

        [Test]
        public void ThereAreDataDrivenFolders()
        {
            Assert.IsNotEmpty(test_folders);
        }

        public void Dispose()
        {
            if (rhinoCore != null)
                OneTimeTearDown();
            GC.SuppressFinalize(this);
        }
    }
}