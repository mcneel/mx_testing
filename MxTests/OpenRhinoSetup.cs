using NUnit.Framework;
using System;
using Rhino;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using System.Linq;

namespace MxTests
{
    [SetUpFixture]
    public class OpenRhinoSetup
    {
        public static string RhinoSystemDir { get; set; }

        private static Exception to_throw;

        static OpenRhinoSetup()
        {
            string dll_dir = TestContext.CurrentContext.TestDirectory; //this will be /bin
            string settingsfile = Path.Combine(dll_dir, "MxTests.testsettings.xml");

            if (File.Exists(settingsfile))
            {
                SettingsXml = XDocument.Load(settingsfile);
                RhinoSystemDir = SettingsXml.Descendants("RhinoSystemDirectory").FirstOrDefault()?.Value ?? null;
            }
            else
            {
                to_throw = new FileNotFoundException($"Settings file not found in {dll_dir}.");
            }
        }

        public static XDocument SettingsXml { get; set; }

        private IDisposable rhinoCore; //do NOT reference this by its RhinoCommon name

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (to_throw != null) throw to_throw;

            RhinoInside.Resolver.Initialize();
            if (RhinoSystemDir != null) RhinoInside.Resolver.RhinoSystemDirectory = RhinoSystemDir;
            else RhinoSystemDir = RhinoInside.Resolver.RhinoSystemDirectory;
            TestContext.WriteLine("RhinoSystemDir is: " + RhinoSystemDir + ".");

            ReferenceRhinoCommonToOpenRhino();
        }
        
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void ReferenceRhinoCommonToOpenRhino()
        {
            rhinoCore = new Rhino.Runtime.InProcess.RhinoCore(); //delayed as much as necessary
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            rhinoCore.Dispose();
            rhinoCore = null;
        }
    }
}

