using NUnit.Framework;
using Rhino;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace VxTests
{
  [TestFixture]
  public class DisplayFixture
  {
    [Test, UIThread, TestCaseSource(nameof(GetTestModels))]
    public void Regressions(string category, string test)
    {
      var basep = OpenRhinoSetup.PathForTest(nameof(DisplayFixture), category, test);
      if (RhinoDoc.ActiveDoc != null) RhinoDoc.ActiveDoc.Modified = false;

      var doc = RhinoDoc.Open(basep, out _);

      RhinoApp.Wait();

      var bitmap = doc.Views.First().DisplayPipeline.FrameBuffer;

      VxContext.CompareOrSave(nameof(DisplayFixture), category, test, bitmap);
    }

    public static IEnumerable<string[]> GetTestModels()
    {
      foreach(var root in VxContext.RootsFromHeading(nameof(DisplayFixture)))
      {
        foreach(var dirs in VxContext.DirsFromRoot(root))
        {
          foreach (var test in VxContext.ModelsFromDirs(dirs))
          {
            string testname = Path.GetFileNameWithoutExtension(test);
            string category = Path.GetDirectoryName(test.Substring(root.Length));

            yield return new string[] { category, testname };
          }
        }
      }
    }
  }
}
