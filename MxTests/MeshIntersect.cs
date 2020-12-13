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
  public class MeshIntersect : AnyCommand<MeshIntersect>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public void Model(string filename, string filepath)
    {
      Console.WriteLine($"SettingsFile: {OpenRhinoSetup.SettingsFile}");
      Console.WriteLine($"SettingsDir: {OpenRhinoSetup.SettingsDir}");
      Console.WriteLine($"RhinoSystemDir: {OpenRhinoSetup.RhinoSystemDir}");
      Console.WriteLine($"RhinoCommon: {typeof(Rhino.Geometry.Mesh).Assembly.Location}");
      Console.WriteLine($"Resolver.RhinoSystemDirectory: {RhinoInside.Resolver.RhinoSystemDirectory}");
      Console.WriteLine($"Resolver.RhinoSystemDirectory: {RhinoInside.Resolver.RhinoSystemDirectory}");
      Console.WriteLine($"Test filename: {filename}");
      Console.WriteLine($"Path: {filepath}");
      MeshIntersectImplementation.Instance.Model(Path.Combine(filepath, filename));
    }
  }
}
