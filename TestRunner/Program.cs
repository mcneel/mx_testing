using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Xml;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

class Program
{
  [STAThread]
  static void Main()
  {
    var this_location = Assembly.GetExecutingAssembly().Location;
    var test_dir = Path.GetDirectoryName(this_location);
    //var  = Path.GetDirectoryName(plug_dir);
    
    var dll_name = "MxTests.dll";
    var test_dll = Path.Combine(test_dir, dll_name);

    if (!File.Exists(test_dll)) {
       test_dll = Path.Combine(test_dir, @"..\..\MxTests\bin\", dll_name);
       test_dll = Path.GetFullPath(test_dll);
    }

    Assembly assembly = Assembly.LoadFrom(test_dll);

    var type = assembly.GetType("MxTests.RhinoRunner.CommandTestRunner", true, false);
    var testRunner = (ITestRunner)assembly.CreateInstance(type.FullName);

    testRunner.InitAssembly(null);
    testRunner.LoadTests();
    string exploration = testRunner.ExploreTests();

    var xml = XDocument.Parse(exploration);

    var fixtures_elements = xml.Root.Descendants("test-suite").Where(
      e =>
         e.Attribute("type")?.Value == "TestFixture"
      );

    Console.WriteLine("0: ALL");
    int count = 1;
    foreach (var fixtures_element in fixtures_elements)
    {
      Console.WriteLine(count.ToString(CultureInfo.InvariantCulture) + ": " + fixtures_element.Attribute("fullname").Value);
      count++;
    }

    Console.WriteLine("\nType the index you want to test and press Enter:");
    int result = GetCommandLineInteger();

    if (result == 0)
    {
      testRunner.RunTests();
      return;
    }
    Console.WriteLine();

    if (result > count) { Console.WriteLine("Missing item."); return; }

    count = 1;

    var cases = fixtures_elements.ElementAt(result-1).Elements();

    if (cases.Count() == 1)
    {
      testRunner.RunTestByName(cases.ElementAt(0).Attribute("fullname").Value);
      return;
    }

    foreach (var element in cases)
    {
      if (element.Name == "test-suite")
      {
        Console.WriteLine(count++.ToString(CultureInfo.InvariantCulture) + ": " + element.Attribute("fullname").Value + "(suite)");
        foreach (var item in element.Elements())
        {
          string fullname = item.Attribute("fullname").Value.Replace("\\\\", "\\");
          var match = Regex.Match(fullname, "\\\"[\\S]*?\\\"");
          if (match.Success)
          {
            fullname = match.Value;
          }
          Console.WriteLine("|- " + count++.ToString(CultureInfo.InvariantCulture) + ": " + fullname);
        }
      }
      else
        Console.WriteLine(count++.ToString(CultureInfo.InvariantCulture) + ": " + element.Attribute("fullname").Value);
    }

    Console.WriteLine("\nType the index you want to test and press Enter:");
    result = GetCommandLineInteger();



  }

  private static int GetCommandLineInteger()
  {
    int n;
    while (!int.TryParse(Console.ReadLine(), out n))
    {
      Console.WriteLine("Integers only allowed.");
      return -1;
    }
    return n;
  }
}



public interface ITestRunner
{
  void InitAssembly(Hashtable baseSettings);

  string LoadTests();

  string ExploreTests();

  string RunTests();
  string RunTestsWithFilter(string filter);

  string RunTestByName(string name);
}


