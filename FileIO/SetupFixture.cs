using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;

using NUnit.Framework;
using System.Runtime.InteropServices;

namespace FileIO
{
  [Serializable]
  [XmlRoot("Settings")]
  public sealed class FileIOTestSettings
  {
    [XmlElement]
    public string RhinoSystemDirectory { get; set; } = string.Empty;

    [XmlElement]
    public bool Enabled { get; set; } = false;

    [XmlArray]
    public List<ModelDirectory> ModelDirectories { get; set; } = new List<ModelDirectory>();
  }

  [Serializable]
  [XmlRoot("ModelDirectory")]
  public sealed class ModelDirectory
  {
    [XmlAttribute]
    public string Fixture { get; set; } = string.Empty;

    [XmlAttribute]
    public string Location { get; set; } = string.Empty;

    [XmlAttribute]
    public bool Optional { get; set; } = true;

    [XmlAttribute]
    public bool Enabled { get; set; } = true;
  }

  [SetUpFixture]
  public sealed class SetupFixture : Rhino.Testing.Fixtures.RhinoSetupFixture
  {
    public static Rhino.Testing.Configs Settings => Rhino.Testing.Configs.Current;

    static readonly FileIOTestSettings s_settings;

    static SetupFixture()
    {
      string settingsFile = Settings.SettingsFile;

      if (File.Exists(settingsFile))
      {
        try
        {
          XmlSerializer serializer = new XmlSerializer(typeof(FileIOTestSettings));
          s_settings = Rhino.Testing.Configs.Deserialize<FileIOTestSettings>(serializer, settingsFile);

          return;
        }
        catch (Exception) { }
      }

      s_settings = new FileIOTestSettings();
    }

    public override void OneTimeSetup()
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
        System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals("Rhinoceros", StringComparison.OrdinalIgnoreCase))
        return;

      base.OneTimeSetup();

      // your custom setup
    }

    public override void OneTimeTearDown()
    {
      base.OneTimeTearDown();

      // you custom teardown
    }

    internal static void Prerequisites()
    {
      if (!s_settings.Enabled) Assert.Ignore("All tests are ignored");
    }

    /// <summary>
    /// Collects the test models of one fixture from every <see cref="ModelDirectory"/> declared for it.
    /// </summary>
    /// <param name="fixture">Name of the fixture class, matched against the Fixture attribute.</param>
    /// <param name="testModels">Receives the full paths of the models found.</param>
    /// <param name="extensions">File extensions to pick up, dot included, matched case-insensitively.
    /// Matching happens in managed code rather than through a search pattern, because on Windows a
    /// three-letter pattern such as "*.stp" also matches longer extensions like ".stpbak".</param>
    internal static void ScanFolders(string fixture, List<string> testModels, string[] extensions)
    {
      foreach (ModelDirectory mdir in s_settings.ModelDirectories
                                                .Where(md => md.Fixture.Equals(fixture, StringComparison.InvariantCultureIgnoreCase)))
      {
        string testFolder = mdir.Location;
        if (!Path.IsPathRooted(testFolder))
        {
          if (Path.DirectorySeparatorChar == '/')
            testFolder = testFolder.Replace('\\', '/');

          testFolder = Path.Combine(Settings.SettingsDir, testFolder);
          testFolder = Path.GetFullPath(testFolder);
        }

        if (Directory.Exists(testFolder))
        {
          testModels.AddRange(
            Directory.EnumerateFiles(testFolder, "*", SearchOption.AllDirectories)
                     .Where(f =>
                     {
                       string fname = Path.GetFileName(f);

                       bool isWanted = extensions.Any(
                         e => Path.GetExtension(fname).Equals(e, StringComparison.InvariantCultureIgnoreCase));
                       bool isCommentedOut = fname.StartsWith("#", StringComparison.InvariantCultureIgnoreCase);
                       bool isBackup = fname.EndsWith("bak", StringComparison.InvariantCultureIgnoreCase);

                       return isWanted && !isCommentedOut && !isBackup;
                     })
            );
        }
        else if (!mdir.Optional)
        {
          throw new InvalidOperationException($"Could not find required directory: \"{mdir.Location}\".");
        }
      }
    }
  }
}
