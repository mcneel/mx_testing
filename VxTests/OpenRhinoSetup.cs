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

namespace MxTests
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

    private Rhino.Runtime.InProcess.RhinoCore rhinoCore; //do NOT reference this by its RhinoCommon name

    [OneTimeSetUp, STAThread]
    public void OneTimeSetUp()
    {
      if (to_throw != null) throw to_throw;

      RhinoInside.Resolver.Initialize();
      if (RhinoSystemDir != null) RhinoInside.Resolver.RhinoSystemDirectory = RhinoSystemDir;
      else RhinoSystemDir = RhinoInside.Resolver.RhinoSystemDirectory;
      TestContext.WriteLine("RhinoSystemDir is: " + RhinoSystemDir + ".");

      rhinoCore = new Rhino.Runtime.InProcess.RhinoCore(new string[] { "-appmode" }); //delayed as much as necessary

      MainForm mf = new MainForm();
      mf.Show();

      for (int i = 0; i < System.Windows.Forms.Application.OpenForms.Count; i++)
      {
        var window = System.Windows.Forms.Application.OpenForms[i];
        if (window.Visible)
        {
          window.Closed += (s, e) => MainForm.Shutdown();
          break;
        }
      }
      System.Windows.Forms.Application.Run();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
      (rhinoCore as IDisposable)?.Dispose();
      rhinoCore = null;
    }
  }

  [TestFixture]
  public class _OpenRhinoTests
  {
    [Test]
    public void SettingsFileExists()
    {
      Trace.WriteLine($"Rhino folder is {OpenRhinoSetup.RhinoSystemDir}.");

      Assert.IsTrue(File.Exists(OpenRhinoSetup.SettingsFile),
          $"File setting does not exist. Expected '{OpenRhinoSetup.SettingsFile}' in '{OpenRhinoSetup.SettingsDir}'.");
    }
  }

  class MainForm : Eto.Forms.Form
  {
    static Rhino.Runtime.InProcess.RhinoCore _rhinoCore;

    internal static void Shutdown()
    {
      _rhinoCore.Dispose();
      System.Windows.Forms.Application.Exit();
    }

    Rhino.UI.Controls.ViewportControl _viewportControl;
    public MainForm()
    {
      Title = "Rhino.Inside";
      ClientSize = new Eto.Drawing.Size(400, 400);
      _viewportControl = new Rhino.UI.Controls.ViewportControl();
      Content = _viewportControl;

      var viewMenu = new Eto.Forms.ButtonMenuItem { Text = "&View" };
      BuildDisplayModesMenu(viewMenu.Items);
      Menu = new Eto.Forms.MenuBar()
      {
        Items =
        {
          new Eto.Forms.ButtonMenuItem
          {
            Text = "&File",
            Items =
            {
              new Eto.Forms.ButtonMenuItem(new Eto.Forms.Command((s,e)=>OpenFile())) { Text = "Open..." }
            }
          },
          viewMenu

        }
      };

    }

    void BuildDisplayModesMenu(Eto.Forms.MenuItemCollection collection)
    {
      Rhino.Display.DisplayModeDescription[] modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
      foreach (var mode in modes)
      {
        var menuitem = new Eto.Forms.ButtonMenuItem((s, e) =>
        {
          _viewportControl.Viewport.DisplayMode = mode;
          _viewportControl.Refresh();
        });
        menuitem.Text = mode.EnglishName;
        collection.Add(menuitem);
      }
    }

    void OpenFile()
    {
      var ofd = new Eto.Forms.OpenFileDialog();
      ofd.Filters.Add(new Eto.Forms.FileFilter("Rhino 3dm", ".3dm"));
      if (ofd.ShowDialog(this) == Eto.Forms.DialogResult.Ok)
      {
        Title = $"Rhino.Inside ({ofd.FileName})";
        Rhino.RhinoDoc.Open(ofd.FileName, out bool alreadyOpen);
        _viewportControl.Viewport.ZoomExtents();
        _viewportControl.Refresh();
      }
    }


  }
}
