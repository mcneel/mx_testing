using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VxTests
{
  static class VxContext
  {
    public static IEnumerable<string> RootsFromHeading(string heading)
    {
      foreach (var mdir in OpenRhinoSetup.GetDirectoriesFor(heading))
      {
        string dir = mdir.Value;
        if (!Path.IsPathRooted(mdir.Value))
        {
          dir = Path.Combine(OpenRhinoSetup.SettingsDir, dir);
          dir = Path.GetFullPath(dir);
        }

        if (Directory.Exists(dir))
        {
           yield return dir;
        }
      }
    }

    public static IEnumerable<string> DirsFromRoot(string root)
    {
      foreach(var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
      {
        if (Directory.GetFiles(dir, "*.3dm", SearchOption.TopDirectoryOnly).Length > 0)
        {
          yield return dir;
        }
      }
    }

    public static IEnumerable<string> ModelsFromDirs(string dir)
    {
      return Directory.GetFiles(dir, "*.3dm", SearchOption.TopDirectoryOnly);
    }

    public static string GetStorageFolder()
    {
      var path = Path.GetTempPath();
      var folder = Path.Combine(path, "VxTests");

      if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
      return folder;
    }

    public static void CompareOrSave(string heading, string category, string test, Bitmap bitmap)
    {
      string folder = GetStorageFolder();
      string file = Path.Combine(folder, heading, $"{category}-{test}.png");

      if (File.Exists(file))
      {
        bool equal = BitmapsEqual((Bitmap)Image.FromFile(file), bitmap, 0.40);

        Assert.IsTrue(equal, $"Image for {category} {test} differs.");
      }
      else
      {
        var new_dir = Path.GetDirectoryName(file);
        if (!Directory.Exists(new_dir)) Directory.CreateDirectory(new_dir);
        bitmap.Save(file);
        Assert.Inconclusive($"Image for {category} {test} didn't have a comparison.");
      }
    }

    public static bool BitmapsEqual(Bitmap bitmap1, Bitmap bitmap2, double tolerance)
    {
      if (bitmap1.Size != bitmap2.Size)
      {
        return false;
      }

      tolerance *= (255 * 3);

      for (int x = 0; x < bitmap1.Width; x++)
      {
        for (int y = 0; y < bitmap1.Height; y++)
        {
          Color c1 = bitmap1.GetPixel(x, y);
          Color c2 = bitmap2.GetPixel(x, y);

          int diff = Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);

          if (diff > tolerance)
          {
            return false;
          }
        }
      }

      return true;
    }
  }
}
