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
        public static string RhinoSystemDir { get; private set; }

        private static Exception to_throw;
        public static string SettingsFile { get; private set; }

        public const string SettingsFileName = "MxTests.testsettings.xml";
        public static string SettingsDir { get; private set; }

        static OpenRhinoSetup()
        {
            SettingsDir = TestContext.CurrentContext.TestDirectory; //this will be /bin
            SettingsFile = Path.Combine(SettingsDir, SettingsFileName);

            if (File.Exists(SettingsFile))
            {
                SettingsXml = XDocument.Load(SettingsFile);
                RhinoSystemDir = SettingsXml.Descendants("RhinoSystemDirectory").FirstOrDefault()?.Value ?? null;
            }
            else
                SettingsXml = new XDocument();
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

    [TestFixture]
    class OpenRhinoTests
    {
        [Test]
        public void SettingsFileExists()
        {
            Assert.IsTrue(File.Exists(OpenRhinoSetup.SettingsFile),
                $"File setting does not exist. Expected '{OpenRhinoSetup.SettingsFile}' in '{OpenRhinoSetup.SettingsDir}'.");
        }
    }
}
