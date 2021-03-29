using NUnit.Framework;
using Rhino;
using System;
using System.IO;
using System.Windows.Forms;

namespace VxTests
{
  [TestFixture]
  public class Display
  {
    [Test, UIThread]
    public void ClippingPlane()
    {
      var basep = OpenRhinoSetup.PathForTest(nameof(Display), nameof(ClippingPlane));
      var doc = RhinoDoc.Open(basep, out _);

      OpenRhinoSetup.MainForm.viewportControl1.Invalidate();
      Application.DoEvents();

      var bitmap = OpenRhinoSetup.Display.FrameBuffer;
      var path = Environment.ExpandEnvironmentVariables("%temp%\\test.png");
      bitmap.Save(path);

      //Assert.Fail("Some text");
    }
  }

}
