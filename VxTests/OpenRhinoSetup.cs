using NUnit.Framework;
using System;
using Rhino;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace VxTests
{
  [SetUpFixture]
  public class OpenRhinoSetup
  {
    internal static string RhinoSystemDir { get; private set; }

    private static Exception to_throw;
    internal static string SettingsFile { get; private set; }

    internal const string SettingsFileName = "VxTests.testsettings.xml";
    internal static string SettingsDir { get; private set; }

    internal static string GetCallerFilePath([System.Runtime.CompilerServices.CallerFilePath] string filePath = "")
    {
      return filePath;
    }

    static OpenRhinoSetup()
    {
      SettingsDir = Path.GetDirectoryName(GetCallerFilePath());

        //TestContext.CurrentContext.TestDirectory; //this will be the location where the .dll is found
      SettingsFile = Path.Combine(SettingsDir, SettingsFileName);

      if (File.Exists(SettingsFile))
      {
        SettingsXml = XDocument.Load(SettingsFile);
        RhinoSystemDir = SettingsXml.Descendants("RhinoSystemDirectory").FirstOrDefault()?.Value ?? null;
      }
      else
        SettingsXml = new XDocument();
    }

    internal static XDocument SettingsXml { get; set; }

    internal static void ScanFolders(string heading, List<string> testModels)
    {
      var test_folders = new List<string>();
      foreach (var mdir in GetDirectoriesFor(heading))
      {
        bool optional = mdir.Attribute("optional")?.Value != "false"; //defaults to true

        string attempt = mdir.Value;
        if (!Path.IsPathRooted(mdir.Value))
        {
          attempt = Path.Combine(OpenRhinoSetup.SettingsDir, attempt);
          attempt = Path.GetFullPath(attempt);
        }

        if (Directory.Exists(attempt))
        {
          test_folders.Add(attempt);
        }
        else if (!optional)
        {
          to_throw = new InvalidOperationException($"Could not find required directory: \"{mdir.Value}\".");
          break;
        }
      }

      foreach (string folder in test_folders)
      {
        if (Directory.Exists(folder))
          testModels.AddRange(
              Directory.GetFiles(folder, @"*.3dm", SearchOption.AllDirectories)
              );
      }

      testModels.RemoveAll(f => Path.GetFileName(f).StartsWith("#", System.StringComparison.InvariantCultureIgnoreCase));
      testModels.RemoveAll(f => Path.GetFileName(f).EndsWith("bak", System.StringComparison.InvariantCultureIgnoreCase));
    }

    private static Rhino.Runtime.InProcess.RhinoCore rhinoCore;

    private static Thread uiThread;
    private static byte rhinoStarted;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
      Start();
    }

    public static void Start()
    {
      if (to_throw != null) throw to_throw;

      uiThread = new Thread(RunTests);
      uiThread.SetApartmentState(ApartmentState.STA);
      uiThread.IsBackground = false;
      uiThread.Start();

      while (Thread.VolatileRead(ref rhinoStarted) == 0)
      {
        Thread.Sleep(100);
      }
    }

    static void RunTests()
    {
      RhinoInside.Resolver.Initialize();
      if (RhinoSystemDir != null) RhinoInside.Resolver.RhinoSystemDirectory = RhinoSystemDir;
      else RhinoSystemDir = RhinoInside.Resolver.RhinoSystemDirectory;

      TestContext.WriteLine("RhinoSystemDir is: " + RhinoSystemDir + ".");

      rhinoCore = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "-appmode" }, Rhino.Runtime.InProcess.WindowStyle.Normal);
      RhinoApp.Initialized += Mainform_Shown;
      rhinoCore.Run();
    }

    private static void Mainform_Shown(object sender, EventArgs e)
    {
      RhinoApp.Idle += RhinoApp_Idle;
    }

    private static void RhinoApp_Idle(object sender, EventArgs e)
    {
      Thread.VolatileWrite(ref rhinoStarted, 1);
      RhinoApp.Idle -= RhinoApp_Idle;
      RhinoApp.Idle += ReadyToDoWork;
    }

    private static System.Collections.Concurrent.ConcurrentBag<Action> actions = new System.Collections.Concurrent.ConcurrentBag<Action>();

    static void ReadyToDoWork(object sender, EventArgs e)
    {
      while (actions.Count > 0)
      {
        if(actions.TryTake(out Action currentTask))
        {
          currentTask.Invoke();
          break;
        }
      }
    }

    public static void EnqueueAction(Action action)
    {
      actions.Add(action);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
      RhinoDoc.ActiveDoc.Modified = false;
      RhinoApp.Exit(true);
      (rhinoCore as IDisposable)?.Dispose();
    }

    public static IEnumerable<XElement> GetDirectoriesFor(string heading)
    {
      return SettingsXml.Descendants(heading).Descendants("ModelDirectory");
    }

    internal static string PathForTest(string heading, string category, string test)
    {
      var dirs = GetDirectoriesFor(heading);

      foreach (var mdir in dirs)
      {
        string attempt = mdir.Value;
        if (!Path.IsPathRooted(mdir.Value))
        {
          attempt = Path.Combine(OpenRhinoSetup.SettingsDir, attempt);
          attempt = Path.GetFullPath(attempt);
        }

        if (Directory.Exists(attempt))
        {
          var file = Path.Combine(attempt, category, test + ".3dm");
          if (File.Exists(file))
            return file;
        }
      }

      throw new FileNotFoundException($"Impossible to find file {test}.3dm for heading '{heading}' category '{category}'.");
    }
  }

  [TestFixture]
  public class _OpenRhinoTests
  {
    [Test]
    public void SettingsFileExists()
    {
      Trace.WriteLine($"Rhino folder is {OpenRhinoSetup.RhinoSystemDir}.");
      Thread.Sleep(2000);

      Assert.IsTrue(File.Exists(OpenRhinoSetup.SettingsFile),
          $"File setting does not exist. Expected '{OpenRhinoSetup.SettingsFile}' in '{OpenRhinoSetup.SettingsDir}'.");
    }
  }

  public class UIThreadAttribute : NUnitAttribute, NUnit.Framework.Interfaces.IWrapTestMethod
  {
    public TestCommand Wrap(TestCommand command)
    {
      return new UICommand(command);
    }
  }

  class UICommand : DelegatingTestCommand
  {
    public UICommand(TestCommand innerCommand) : base(innerCommand) { }

    public override TestResult Execute(TestExecutionContext context)
    {
      var compute = new Compute { Context = context, InnerCommand = this.innerCommand };
      lock (compute.SyncObject)
      {
        OpenRhinoSetup.EnqueueAction(compute.Run);
        while (compute.Block == 1)
          Monitor.Wait(compute.SyncObject);
      }
      if (compute.Exception != null) throw compute.Exception;
      return compute.Result;
    }

    class Compute
    {
      public Compute()
      {
        SyncObject = new object();
        Block = 1;
      }

      public TestResult Result { get; set; }
      public TestExecutionContext Context { get; set; }
      public TestCommand InnerCommand { get; set; }
      public Exception Exception { get; set; }
      public object SyncObject { get; }
      public volatile byte Block;

      public void Run()
      {
        try
        {
          Result = InnerCommand.Execute(Context);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
          Exception = ex;
        }
        lock (SyncObject)
        {
          Block = 0;
          Monitor.Pulse(SyncObject);
        }
      }
    }
  }

}
