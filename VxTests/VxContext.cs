using System;
using System.Collections.Generic;
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
  }
}
