using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;

using NUnit.Framework;

namespace MxTests
{
  [Serializable]
  [XmlRoot("Settings")]
  public sealed class MxTestSettings
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

    static readonly MxTestSettings s_settings;

    static SetupFixture()
    {
      string settingsFile = Settings.SettingsFile;

      if (File.Exists(settingsFile))
      {
        try
        {
          XmlSerializer serializer = new XmlSerializer(typeof(MxTestSettings));
          s_settings = Rhino.Testing.Configs.Deserialize<MxTestSettings>(serializer, settingsFile);

          return;
        }
        catch (Exception) { }
      }

      s_settings = new MxTestSettings();
    }

    public override void OneTimeSetup()
    {
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

    internal static void ScanFolders(string fixture, List<string> testModels)
    {
      foreach (ModelDirectory mdir in s_settings.ModelDirectories
                                                .Where(md => md.Fixture.Equals(fixture, StringComparison.InvariantCultureIgnoreCase)))
      {
        string testFolder = mdir.Location;
        if (!Path.IsPathRooted(testFolder))
        {
          testFolder = Path.Combine(Settings.SettingsDir, testFolder);
          testFolder = Path.GetFullPath(testFolder);
        }

        if (Directory.Exists(testFolder))
        {
          testModels.AddRange(
            Directory.GetFiles(testFolder, @"*.3dm", SearchOption.AllDirectories)
                     .Where(f =>
                     {
                       string fname = Path.GetFileName(f);

                       bool isCommentedOut = fname.StartsWith("#", StringComparison.InvariantCultureIgnoreCase);
                       bool isBackup = fname.EndsWith("bak", StringComparison.InvariantCultureIgnoreCase);

                       return !isCommentedOut && !isBackup;
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
