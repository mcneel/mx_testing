using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace NetSDKTests
{
  // RH-99012: Rhino.FileIO.WorksessionFile reads and writes Rhino worksession (.rws) files.
  // The golden files come from src4/opennurbs/tests/worksession_files. They were written by RhinoCore,
  // and each .txt is the Dump of what RhinoCore's reader read from the .rws next to it.
  [TestFixture]
  public class WorksessionFileTests
  {
    string m_folder;

    [SetUp]
    public void SetUp()
    {
      m_folder = Path.Combine(Path.GetTempPath(), "WorksessionFileTests_" + Path.GetRandomFileName());
      Directory.CreateDirectory(m_folder);
    }

    [TearDown]
    public void TearDown()
    {
      if (Directory.Exists(m_folder))
        Directory.Delete(m_folder, true);
    }

    static Guid TestId(int seed, short kind)
    {
      return new Guid(seed + 1, 0x1901, kind, 0x43, 0, 0, 0, 0, 0, 0, (byte)(seed % 256));
    }

    static WorksessionFileLayerValues TestValues(int seed)
    {
      var values = new WorksessionFileLayerValues
      {
        Visible = seed % 2 != 0,
        Locked = seed % 3 == 0,
        Color = Color.FromArgb(255, (seed * 37) % 256, (seed * 59) % 256, (seed * 83) % 256),
      };
      var layer = new Layer
      {
        Id = TestId(seed, 1),
        Name = "Layer " + seed,
        PlotColor = Color.FromArgb(255, (seed * 11) % 256, 20, 30),
        PlotWeight = 0.25 * (seed % 5),
        IsExpanded = seed % 2 == 1,
      };
      if (seed % 3 != 0)
      {
        layer.SetPerViewportVisible(TestId(seed, 2), false);
        layer.SetPerViewportColor(TestId(seed, 3), Color.FromArgb(255, seed % 256, 128, 64));
      }
      values.SetLayer(layer);
      return values;
    }

    static WorksessionFileModel TestModel(int seed, bool active, int overrideCount)
    {
      var model = new WorksessionFileModel("/Projects/Tower/Model " + seed + ".3dm")
      {
        IsActive = active,
        RelativePath = "./Model " + seed + ".3dm",
      };
      model.SetParentLayerValues(TestValues(1000 + seed));
      for (int i = 0; i < overrideCount; i++)
      {
        var layerOverride = new WorksessionFileLayerOverride { LayerId = TestId(100 * seed + i, 4) };
        layerOverride.SetFileValues(TestValues(2 * (100 * seed + i)));
        layerOverride.SetWorksessionValues(TestValues(2 * (100 * seed + i) + 1));
        model.AddLayerOverride(layerOverride);
      }
      return model;
    }

    static WorksessionFile TestFile()
    {
      var file = new WorksessionFile();
      file.AddModel(TestModel(1, true, 0));
      file.AddModel(TestModel(10, false, 3));
      file.AddModel(TestModel(11, false, 5));
      return file;
    }

    static void AssertSameLayer(Layer expected, Layer actual, string where)
    {
      Assert.That(actual.Id, Is.EqualTo(expected.Id), where + " layer id");
      Assert.That(actual.Name, Is.EqualTo(expected.Name), where + " layer name");
      Assert.That(actual.PlotColor.ToArgb(), Is.EqualTo(expected.PlotColor.ToArgb()), where + " print color");
      Assert.That(actual.PlotWeight, Is.EqualTo(expected.PlotWeight), where + " print width");
      Assert.That(actual.IsExpanded, Is.EqualTo(expected.IsExpanded), where + " expanded");
      Assert.That(actual.ModelIsVisible, Is.EqualTo(expected.ModelIsVisible), where + " model visible");
      Assert.That(actual.PerViewportIsVisibleInNewDetails, Is.EqualTo(expected.PerViewportIsVisibleInNewDetails), where + " visible in new details");
    }

    static void AssertSameValues(WorksessionFileLayerValues expected, WorksessionFileLayerValues actual, string where)
    {
      Assert.That(actual.Visible, Is.EqualTo(expected.Visible), where + " visible");
      Assert.That(actual.Locked, Is.EqualTo(expected.Locked), where + " locked");
      Assert.That(actual.Color.ToArgb(), Is.EqualTo(expected.Color.ToArgb()), where + " color");
      AssertSameLayer(expected.GetLayer(), actual.GetLayer(), where);
    }

    static void AssertSameFile(WorksessionFile expected, WorksessionFile actual)
    {
      Assert.That(actual.ModelCount, Is.EqualTo(expected.ModelCount), "model count");
      for (int i = 0; i < expected.ModelCount; i++)
      {
        var e = expected.GetModel(i);
        var a = actual.GetModel(i);
        string where = "model " + i;
        Assert.That(a.IsActive, Is.EqualTo(e.IsActive), where + " active");
        Assert.That(a.FullPath, Is.EqualTo(e.FullPath), where + " full path");
        Assert.That(a.RelativePath, Is.EqualTo(e.RelativePath), where + " relative path");
        AssertSameValues(e.GetParentLayerValues(), a.GetParentLayerValues(), where + " parent");
        Assert.That(a.LayerOverrideCount, Is.EqualTo(e.LayerOverrideCount), where + " override count");
        for (int j = 0; j < e.LayerOverrideCount; j++)
        {
          var eo = e.GetLayerOverride(j);
          var ao = a.GetLayerOverride(j);
          string whereOverride = where + " override " + j;
          Assert.That(ao.LayerId, Is.EqualTo(eo.LayerId), whereOverride + " layer id");
          AssertSameValues(eo.GetFileValues(), ao.GetFileValues(), whereOverride + " file");
          AssertSameValues(eo.GetWorksessionValues(), ao.GetWorksessionValues(), whereOverride + " worksession");
        }
      }
    }

    [Test]
    public void CurrentVersionIsFive()
    {
      Assert.That(WorksessionFile.CurrentVersion, Is.EqualTo(5));
    }

    [Test]
    public void LayerValuesDefaults()
    {
      using (var values = new WorksessionFileLayerValues())
      {
        Assert.That(values.Visible, Is.True);
        Assert.That(values.Locked, Is.False);
        Assert.That(values.Color.ToArgb(), Is.EqualTo(Color.Black.ToArgb()));
      }
    }

    [Test]
    public void ColorWithAlphaRoundTrips()
    {
      using (var values = new WorksessionFileLayerValues())
      {
        var color = Color.FromArgb(200, 10, 20, 30);
        values.Color = color;
        Assert.That(values.Color.ToArgb(), Is.EqualTo(color.ToArgb()));
      }
    }

    [Test]
    public void LayerAndPerViewportSettingsRoundTrip()
    {
      using (var values = new WorksessionFileLayerValues())
      {
        var viewportId = TestId(7, 9);
        var layer = new Layer { Name = "Per viewport" };
        layer.SetPerViewportVisible(viewportId, false);
        layer.SetPerViewportColor(viewportId, Color.FromArgb(255, 1, 2, 3));
        values.SetLayer(layer);

        var back = values.GetLayer();
        Assert.That(back.Name, Is.EqualTo("Per viewport"));
        Assert.That(back.HasPerViewportSettings(viewportId), Is.True);
        Assert.That(back.PerViewportIsVisible(viewportId), Is.False);
        Assert.That(back.PerViewportColor(viewportId).ToArgb(), Is.EqualTo(Color.FromArgb(255, 1, 2, 3).ToArgb()));
      }
    }

    [Test]
    public void LayerValuesCopyIsIndependent()
    {
      using (var values = TestValues(4))
      {
        using (var copy = new WorksessionFileLayerValues(values))
        {
          copy.Visible = !values.Visible;
          Assert.That(copy.Visible, Is.Not.EqualTo(values.Visible));
          AssertSameLayer(values.GetLayer(), copy.GetLayer(), "copy");
        }
      }
    }

    [Test]
    public void LayerOverrideValuesAreCopies()
    {
      using (var layerOverride = new WorksessionFileLayerOverride())
      {
        Assert.That(layerOverride.LayerId, Is.EqualTo(Guid.Empty));
        layerOverride.LayerId = TestId(3, 3);
        Assert.That(layerOverride.LayerId, Is.EqualTo(TestId(3, 3)));

        var fileValues = layerOverride.GetFileValues();
        fileValues.Visible = false;
        Assert.That(layerOverride.GetFileValues().Visible, Is.True, "GetFileValues returned the stored values");
        layerOverride.SetFileValues(fileValues);
        Assert.That(layerOverride.GetFileValues().Visible, Is.False);

        var worksessionValues = layerOverride.GetWorksessionValues();
        worksessionValues.Locked = true;
        layerOverride.SetWorksessionValues(worksessionValues);
        Assert.That(layerOverride.GetWorksessionValues().Locked, Is.True);
        Assert.That(layerOverride.GetFileValues().Locked, Is.False);
      }
    }

    [Test]
    public void ModelDefaultsAndPaths()
    {
      using (var model = new WorksessionFileModel())
      {
        Assert.That(model.IsActive, Is.False);
        Assert.That(model.FullPath, Is.EqualTo(string.Empty));
        Assert.That(model.RelativePath, Is.EqualTo(string.Empty));
        Assert.That(model.LayerOverrideCount, Is.Zero);

        const string fullPath = "/Projets/Fa\u00e7ade \u2013 \u6771\u4eac/Mod\u00e8le \U0001F3D7.3dm";
        const string relativePath = "./Mod\u00e8le \U0001F3D7.3dm";
        model.FullPath = fullPath;
        model.RelativePath = relativePath;
        Assert.That(model.FullPath, Is.EqualTo(fullPath));
        Assert.That(model.RelativePath, Is.EqualTo(relativePath));

        model.FullPath = null;
        Assert.That(model.FullPath, Is.EqualTo(string.Empty));
      }
    }

    [Test]
    public void ModelLayerOverrideList()
    {
      using (var model = new WorksessionFileModel())
      {
        var first = new WorksessionFileLayerOverride { LayerId = TestId(1, 5) };
        model.AddLayerOverride(first);
        model.AddLayerOverride(new WorksessionFileLayerOverride { LayerId = TestId(2, 5) });
        Assert.That(model.LayerOverrideCount, Is.EqualTo(2));
        Assert.That(model.GetLayerOverrides().Select(o => o.LayerId), Is.EqualTo(new[] { TestId(1, 5), TestId(2, 5) }));

        model.SetLayerOverride(1, new WorksessionFileLayerOverride { LayerId = TestId(3, 5) });
        Assert.That(model.GetLayerOverride(1).LayerId, Is.EqualTo(TestId(3, 5)));

        model.RemoveLayerOverrideAt(1);
        Assert.That(model.LayerOverrideCount, Is.EqualTo(1));
        Assert.That(model.GetLayerOverride(0).LayerId, Is.EqualTo(first.LayerId));

        model.ClearLayerOverrides();
        Assert.That(model.LayerOverrideCount, Is.Zero);
      }
    }

    [Test]
    public void BadIndexesAndNullArgumentsThrow()
    {
      using (var model = new WorksessionFileModel())
      {
        using (var layerOverride = new WorksessionFileLayerOverride())
        {
          using (var values = new WorksessionFileLayerValues())
          {
            Assert.Throws<ArgumentOutOfRangeException>(() => model.GetLayerOverride(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetLayerOverride(0, layerOverride));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.RemoveLayerOverrideAt(-1));
            Assert.Throws<ArgumentNullException>(() => model.AddLayerOverride(null));
            Assert.Throws<ArgumentNullException>(() => model.SetParentLayerValues(null));
            Assert.Throws<ArgumentNullException>(() => layerOverride.SetFileValues(null));
            Assert.Throws<ArgumentNullException>(() => values.SetLayer(null));
            Assert.Throws<ArgumentNullException>(() => new WorksessionFileModel((WorksessionFileModel)null));

            using (var file = TestFile())
            {
              Assert.Throws<ArgumentOutOfRangeException>(() => file.GetModel(3));
              Assert.Throws<ArgumentOutOfRangeException>(() => file.SetModel(-1, model));
              Assert.Throws<ArgumentOutOfRangeException>(() => file.RemoveModelAt(3));
              Assert.Throws<ArgumentNullException>(() => file.AddModel(null));
              Assert.Throws<ArgumentOutOfRangeException>(() => file.Version = -1);
              Assert.Throws<ArgumentNullException>(() => WorksessionFile.Read(null));
              Assert.Throws<ArgumentNullException>(() => WorksessionFile.ReadFileVersion(null));
              Assert.Throws<ArgumentNullException>(() => WorksessionFile.FromByteArray(null));
              Assert.Throws<ArgumentNullException>(() => file.Write(null));
            }
          }
        }
      }
    }

    [Test]
    public void ModelsAreCopies()
    {
      using (var file = TestFile())
      {
        Assert.That(file.ModelCount, Is.EqualTo(3));
        Assert.That(file.ActiveModelIndex, Is.Zero);
        Assert.That(file.GetModels(), Has.Length.EqualTo(3));

        var model = file.GetModel(1);
        model.FullPath = "/changed.3dm";
        Assert.That(file.GetModel(1).FullPath, Is.Not.EqualTo("/changed.3dm"), "GetModel returned the stored model");
        file.SetModel(1, model);
        Assert.That(file.GetModel(1).FullPath, Is.EqualTo("/changed.3dm"));

        file.RemoveModelAt(0);
        Assert.That(file.ModelCount, Is.EqualTo(2));
        Assert.That(file.ActiveModelIndex, Is.EqualTo(-1));

        using (var copy = new WorksessionFile(file))
        {
          copy.ClearModels();
          Assert.That(copy.ModelCount, Is.Zero);
          Assert.That(file.ModelCount, Is.EqualTo(2));
        }
      }
    }

    [Test]
    public void WriteAndReadRoundTrip()
    {
      using (var file = TestFile())
      {
        var path = Path.Combine(m_folder, "round trip.rws");
        Assert.That(file.Write(path), Is.True);
        Assert.That(WorksessionFile.ReadFileVersion(path), Is.EqualTo(5));

        using (var read = WorksessionFile.Read(path))
        {
          Assert.That(read, Is.Not.Null);
          Assert.That(read.Version, Is.EqualTo(5));
          AssertSameFile(file, read);
        }
      }
    }

    [Test]
    public void ByteArrayRoundTrip()
    {
      using (var file = TestFile())
      {
        var path = Path.Combine(m_folder, "bytes.rws");
        Assert.That(file.Write(path), Is.True);

        var bytes = file.ToByteArray();
        Assert.That(bytes, Is.EqualTo(File.ReadAllBytes(path)));

        using (var read = WorksessionFile.FromByteArray(bytes))
        {
          Assert.That(read, Is.Not.Null);
          AssertSameFile(file, read);

          Assert.That(WorksessionFile.FromByteArray(new byte[] { 1, 2, 3 }), Is.Null);
          Assert.That(WorksessionFile.FromByteArray(Array.Empty<byte>()), Is.Null);
        }
      }
    }

    [Test]
    public void FilesThatCannotBeRead()
    {
      var missing = Path.Combine(m_folder, "missing.rws");
      Assert.That(WorksessionFile.Read(missing), Is.Null);
      Assert.That(WorksessionFile.ReadFileVersion(missing), Is.Zero);

      var notWorksession = Path.Combine(m_folder, "not a worksession.rws");
      File.WriteAllText(notWorksession, "3D Geometry File Format");
      Assert.That(WorksessionFile.Read(notWorksession), Is.Null);
      Assert.That(WorksessionFile.ReadFileVersion(notWorksession), Is.Zero);
    }

    [Test]
    public void Version4KeepsPathsOnly()
    {
      using (var file = TestFile())
      {
        var path = Path.Combine(m_folder, "version 4.rws");
        Assert.That(file.Write(path, 4), Is.True);
        Assert.That(WorksessionFile.ReadFileVersion(path), Is.EqualTo(4));

        using (var read = WorksessionFile.Read(path))
        {
          Assert.That(read, Is.Not.Null);
          Assert.That(read.Version, Is.EqualTo(4));
          Assert.That(read.ModelCount, Is.EqualTo(3));
          Assert.That(read.GetModel(0).IsActive, Is.True);
          Assert.That(read.GetModel(1).FullPath, Is.EqualTo(file.GetModel(1).FullPath));
          Assert.That(read.GetModel(1).RelativePath, Is.Empty);
          Assert.That(read.GetModel(1).LayerOverrideCount, Is.Zero);
        }
      }
    }

    [Test]
    public void UnsupportedVersionsAreNotWritten()
    {
      using (var file = TestFile())
      {
        var path = Path.Combine(m_folder, "unsupported.rws");
        foreach (int version in new[] { -1, 0, 3, 6 })
        {
          Assert.That(file.Write(path, version), Is.False, "Write version " + version);
          Assert.That(file.ToByteArray(version), Is.Null, "ToByteArray version " + version);
        }
      }
    }

    [Test]
    public void DumpListsTheModels()
    {
      using (var file = TestFile())
      {
        var text = file.Dump();
        Assert.That(text, Does.Contain("Model 0 (active)"));
        Assert.That(text, Does.Contain("Model 2 (reference)"));
        Assert.That(text, Does.Contain(file.GetModel(1).FullPath));
      }
    }

    [Test]
    public void DisposedObjectsThrow()
    {
      var file = new WorksessionFile();
      file.Dispose();
      Assert.Throws<ObjectDisposedException>(() => _ = file.ModelCount);

      var model = new WorksessionFileModel();
      model.Dispose();
      Assert.Throws<ObjectDisposedException>(() => _ = model.FullPath);
    }

    static string GoldenFolder()
    {
      return Path.Combine(Path.GetDirectoryName(typeof(WorksessionFileTests).Assembly.Location), "worksession_files");
    }

    static IEnumerable<TestCaseData> GoldenFiles()
    {
      var folder = GoldenFolder();
      if (!Directory.Exists(folder))
        yield break;
      foreach (var path in Directory.GetFiles(folder, "*.rws").OrderBy(p => p, StringComparer.Ordinal))
        yield return new TestCaseData(path).SetName("GoldenFile_" + Path.GetFileNameWithoutExtension(path));
    }

    [Test]
    public void GoldenFilesArePresent()
    {
      Assert.That(GoldenFiles().Count(), Is.GreaterThanOrEqualTo(10), "golden files missing from " + GoldenFolder());
    }

    [TestCaseSource(nameof(GoldenFiles))]
    public void GoldenFile(string path)
    {
      using (var file = WorksessionFile.Read(path))
      {
        Assert.That(file, Is.Not.Null);
        var expected = File.ReadAllText(Path.ChangeExtension(path, ".txt")).Replace("\r\n", "\n");
        Assert.That(file.Dump().Replace("\r\n", "\n"), Is.EqualTo(expected));

        // Written again and read back, it reads the same.
        using (var reread = WorksessionFile.FromByteArray(file.ToByteArray()))
        {
          Assert.That(reread, Is.Not.Null);
          Assert.That(reread.Dump().Replace("\r\n", "\n"), Is.EqualTo(expected));
        }
      }
    }
  }
}
