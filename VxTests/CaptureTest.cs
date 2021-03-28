using NUnit.Framework;
using Rhino;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VxTests
{
  [TestFixture]
  public class CaptureTest
  {
    [Test, UIThread]
    public void CompareImage()
    {
      var bmp = OpenRhinoSetup.MainForm.viewportControl1.Display.FrameBuffer;

      var path = Environment.ExpandEnvironmentVariables("%appdata%\\Test\\test.png");
      bmp.Save(path);

      //Assert.Fail("Some text");
    }
  }

}
