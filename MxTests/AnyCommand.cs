using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxTests
{
  /// <summary>
  /// AnyCommand is a class that provides basic command integration for testing.
  /// In order to avoid exposing RhinoCommon types and proviking a too early dll search,
  /// it's suggested that all RhinoCommon-specific implementation happens in an internal or private class.
  /// </summary>
  /// <typeparam name="T">The name of the command itself.
  /// This is used for non-reflective introspection.</typeparam>
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

    /// <summary>
    /// Contains a base implementation for running tests.
    /// </summary>
    /// <param name="filename">The file name</param>
    /// <param name="filepath">The file path</param>
    public virtual void Run(string filename, string filepath)
    {
      Console.WriteLine($"SettingsFile: {OpenRhinoSetup.SettingsFile}");
      Console.WriteLine($"SettingsDir: {OpenRhinoSetup.SettingsDir}");
      Console.WriteLine($"RhinoSystemDir: {OpenRhinoSetup.RhinoSystemDir}");
      Console.WriteLine($"RhinoCommon: {typeof(Rhino.Geometry.Mesh).Assembly.Location}");
      Console.WriteLine($"Resolver.RhinoSystemDirectory: {RhinoInside.Resolver.RhinoSystemDirectory}");
      Console.WriteLine($"Resolver.RhinoSystemDirectory: {RhinoInside.Resolver.RhinoSystemDirectory}");
      Console.WriteLine($"Test filename: {filename}");
      Console.WriteLine($"Path: {filepath}");

      OpenRhinoSetup.Prerequisites();
    }
  }

}
