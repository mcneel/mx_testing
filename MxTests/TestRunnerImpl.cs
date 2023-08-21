using NUnit;
using NUnit.Framework;
using NUnit.Framework.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static NUnit.Framework.Api.FrameworkController;

namespace MxTests.RhinoRunner
{
  class TestAssemblyReference
  {
    public static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
    public static readonly Type[] TestTypes = Assembly.GetTypes().Where(
      t => t.Assembly.GetName().Name.Equals(AssemblyName) &&
      !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false) &&
        t.IsDefined(typeof(TestFixtureAttribute), inherit: false)
         ).ToArray();

    public static readonly MethodInfo[] TestMethods = TestTypes
      .SelectMany(t => t.GetMethods())
      .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false)
        .Any()).ToArray();

    public static readonly string AssemblyName = Assembly.GetName().Name;

    public static readonly IReadOnlyList<string> TestNames = Array
      .ConvertAll(TestMethods, m => $"{m.DeclaringType?.Namespace}.{m.DeclaringType?.Name}.{m.Name}")
      .ToList().AsReadOnly();
  }

  /*
  class MyHandlerClass : ICallbackEventHandler
  {
    string value;

    public string GetCallbackResult()
    {
      return value;
    }

    public void RaiseCallbackEvent(string eventArgument)
    {
      value = eventArgument;
    }

    public override string ToString()
    {
      return value;
    }
  }

  class TestRunSettings
  {
    public FrameworkController Controller { get; set; }

    public MyHandlerClass Handler { get; set; } = new MyHandlerClass();
    public Hashtable Settings { get; set; } = new Hashtable()
    {
      { FrameworkPackageSettings.RunOnMainThread, true },
          { FrameworkPackageSettings.SynchronousEvents, false }
    };

    public override string ToString()
    {
      return Handler.ToString();
    }
  }

  public class CommandTestRunner : ITestRunner
  {
    TestRunSettings testRunSettings;

    public void InitAssembly(Hashtable baseSettings)
    {
      var rc = new TestRunSettings();

      if (baseSettings == null) baseSettings = new Hashtable();

      baseSettings[FrameworkPackageSettings.RunOnMainThread] = true;
      baseSettings[FrameworkPackageSettings.SynchronousEvents] = false;

      // Create the controller
      rc.Controller = new FrameworkController(Assembly.GetExecutingAssembly(), "MxTests", baseSettings);

      testRunSettings = rc;
    }

    public string LoadTests()
    {
      new LoadTestsAction(testRunSettings.Controller, testRunSettings.Handler);
      return testRunSettings.ToString();
    }

    public string ExploreTests()
    {
      return ExploreTestsWithFilter("<filter/>");
    }

    public string ExploreTestsWithFilter(string filter)
    {
      new ExploreTestsAction(testRunSettings.Controller, filter, testRunSettings.Handler);

      return testRunSettings.ToString();
    }

    public string RunTests()
    {
      return RunTestsWithFilter("<filter/>");
    }

    public string RunTestsWithFilter(string filter)
    {
      new RunTestsAction(testRunSettings.Controller, filter, testRunSettings.Handler);

      return testRunSettings.ToString();
    }

    public string RunTestByName(string name)
    {
      return RunTestsWithFilter($"<filter><test>{name}</test></filter>");
    }
  }
  */
}
