using NUnit.Framework;
using Rhino;
using System;
using System.IO;
using System.Linq;

namespace VxTests
{
  [TestFixture]
  public class Display
  {
    [Test, UIThread]
    public void Regressions()
    {


      var basep = OpenRhinoSetup.PathForTest(nameof(Display), nameof(Regressions));
      var doc = RhinoDoc.Open(basep, out _);

      RhinoApp.Wait();

      var bitmap = doc.Views.First().DisplayPipeline.FrameBuffer;


      
      //Assert.Fail("Some text");
    }
  }

}
