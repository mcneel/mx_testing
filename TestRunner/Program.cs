using System;
using System.Collections;
using System.IO;
using System.Reflection;

class Program
{
  static void Main()
  {
    var this_location = Assembly.GetExecutingAssembly().Location;
    var plug_dir = Path.GetDirectoryName(this_location);
    var test_dir = Path.GetDirectoryName(plug_dir);

    var test_dll = Path.Combine(test_dir, "MxTests.dll");
    Assembly assembly = Assembly.LoadFrom(test_dll);

    var type = assembly.GetType("MxTests.RhinoRunner.CommandTestRunner", true, false);
    var testRunner = (ITestRunner)AppDomain.CurrentDomain.CreateInstanceAndUnwrap(assembly.FullName, type.FullName);


  }

}



public interface ITestRunner
{
  void InitAssembly(Hashtable baseSettings);

  string LoadTests();

  string ExploreTests();
  string ExploreTests(string filter);

  string RunTests();
  string RunTests(string filter);

  string RunTestByName(string name);
}


