using NUnit.Framework;
using Rhino;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace VxTests
{
  [TestFixture]
  public class Display
  {
    [Test, UIThread, TestCaseSource(nameof(GetTestModels))]
    public void Regressions(string category, string test)
    {
      var basep = OpenRhinoSetup.PathForTest(nameof(Display), category, test);
      var doc = RhinoDoc.Open(basep, out _);

      RhinoApp.Wait();

      var bitmap = doc.Views.First().DisplayPipeline.FrameBuffer;

      VxContext.CompareOrSave(nameof(Display), category, test, bitmap);
    }

    public static IEnumerable<string[]> GetTestModels()
    {
      foreach(var root in VxContext.RootsFromHeading(nameof(Display)))
      {
        foreach(var dirs in VxContext.DirsFromRoot(root))
        {
          foreach (var test in VxContext.ModelsFromDirs(dirs))
          {
            yield return new string[] { test.Substring(root.Length), Path.GetFileNameWithoutExtension(test) };
          }
        }
      }
    }
  }
}
