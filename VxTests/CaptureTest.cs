using NUnit.Framework;
using Rhino;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VxTests
{
  [TestFixture]
  class CaptureTest
  {
    [Test]
    public void CompareImage()
    {
      var task = new Task(() =>
      {

        var bmp = OpenRhinoSetup.MainForm.viewportControl1.Display.FrameBuffer;

        var path = Environment.ExpandEnvironmentVariables("%appdata%\\Test\\test.png");
        bmp.Save(path);

      });

      OpenRhinoSetup.EnqueTask(
        task
      );

      task.Wait();
    }
  }

}
