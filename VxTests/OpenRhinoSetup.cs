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
using WinFormsApp;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
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
      foreach (var mdir in OpenRhinoSetup.SettingsXml.Descendants(heading).Descendants("ModelDirectory"))
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

    private Rhino.Runtime.InProcess.RhinoCore rhinoCore;

    public static MainForm MainForm { get; set; }
    private Thread uiThread;
    private byte rhinoStarted;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
      if (to_throw != null) throw to_throw;

      uiThread = new Thread(RunTests);
      uiThread.SetApartmentState(ApartmentState.STA);
      uiThread.IsBackground = false;
      uiThread.Start();

      while(Thread.VolatileRead(ref rhinoStarted) == 0) {
        Thread.Sleep(100);
      }
    }

    void RunTests()
    {
      RhinoInside.Resolver.Initialize();
      if (RhinoSystemDir != null) RhinoInside.Resolver.RhinoSystemDirectory = RhinoSystemDir;
      else RhinoSystemDir = RhinoInside.Resolver.RhinoSystemDirectory;

      TestContext.WriteLine("RhinoSystemDir is: " + RhinoSystemDir + ".");

      MainForm = new MainForm();
      MainForm.Shown += Mainform_Shown;
      Application.Run(MainForm);
    }

    private void Mainform_Shown(object sender, EventArgs e)
    {
      Application.Idle += RhinoApp_Idle;
    }

    private void RhinoApp_Idle(object sender, EventArgs e)
    {
      Thread.VolatileWrite(ref rhinoStarted, 1);
      //Application.Idle -= RhinoApp_Idle;
      //Application.Idle += ReadyToDoWork;
    }

    private static System.Collections.Concurrent.ConcurrentBag<Task> tasks = new System.Collections.Concurrent.ConcurrentBag<Task>();

    void ReadyToDoWork(object sender, EventArgs e)
    {
      while (tasks.Count > 0)
      {
        if(tasks.TryTake(out Task currentTask))
        {
          currentTask.RunSynchronously();
          break;
        }
      }
    }

    public static void EnqueTask(Task task)
    {
      tasks.Add(task);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
      (rhinoCore as IDisposable)?.Dispose();
      rhinoCore = null;
      MainForm.Invoke((Action)delegate { MainForm.Close(); });
      //RhinoApp.Exit(true);
      //if (uiThread != null) uiThread.Abort();
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
      RhinoApp.InvokeAndWait(compute.Run);
      if (compute.Exception != null) throw compute.Exception;
      return compute.Result;
    }

    class Compute
    {
      public TestResult Result { get; set; }
      public TestExecutionContext Context { get; set; }
      public TestCommand InnerCommand { get; set; }
      public Exception Exception { get; set; }

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
      }
    }
  }

}
