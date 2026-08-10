using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using System.IO;

namespace NetSDKTests
{
  [TestFixture]
  public class ViewInfoWindowPosition
  {
    const double Tolerance = 1e-9;

    // The 2x2 layout a template wants: left, right, top, bottom as fractions of the
    // frame window.
    static readonly double[][] Quadrants =
    {
      new[] { 0.0, 0.5, 0.0, 0.5 }, // Perspective, top left
      new[] { 0.5, 1.0, 0.0, 0.5 }, // Top,         top right
      new[] { 0.0, 0.5, 0.5, 1.0 }, // Front,       bottom left
      new[] { 0.5, 1.0, 0.5, 1.0 }, // Right,       bottom right
    };

    static readonly string[] ViewNames = { "Perspective", "Top", "Front", "Right" };

    // https://mcneel.myjetbrains.com/youtrack/issue/RH-96963
    //
    // ViewInfo exposed no access to ON_3dmView::m_position, so headlessly authored
    // views all landed full-window and opened stacked instead of laid out.
    [Test]
    public void TestWindowPositionRoundTrip_RH96963()
    {
      var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".3dm");

      try
      {
        using (var file = new File3dm())
        {
          for (int i = 0; i < Quadrants.Length; i++)
          {
            using (var view = new ViewInfo())
            {
              var q = Quadrants[i];
              view.Name = ViewNames[i];
              view.SetWindowPosition(q[0], q[1], q[2], q[3]);

              // Must be readable back off the in-memory object, before any file IO.
              AssertPosition(view, i, "in memory");

              file.AllViews.Add(view);
            }
          }

          Assert.That(file.Write(path, 8), Is.True, "failed to write {0}", path);
        }

        // The positions have to survive the TCODE_VIEW_POSITION chunk, which is what
        // the workaround in rhino-template-tool was patching by hand.
        using (var reread = File3dm.Read(path))
        {
          Assert.That(reread.AllViews.Count, Is.EqualTo(Quadrants.Length));

          for (int i = 0; i < Quadrants.Length; i++)
          {
            var view = reread.AllViews[i];
            Assert.That(view.Name, Is.EqualTo(ViewNames[i]));
            AssertPosition(view, i, "after round trip");
          }
        }
      }
      finally
      {
        if (File.Exists(path))
          File.Delete(path);
      }
    }

    [Test]
    public void TestMaximizedRoundTrip_RH96963()
    {
      var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".3dm");

      try
      {
        using (var file = new File3dm())
        {
          using (var maximized = new ViewInfo())
          {
            maximized.Name = "Maximized";
            maximized.SetWindowPosition(0.0, 1.0, 0.0, 1.0);
            maximized.Maximized = true;
            Assert.That(maximized.Maximized, Is.True, "Maximized did not stick");
            file.AllViews.Add(maximized);
          }

          using (var restored = new ViewInfo())
          {
            restored.Name = "Restored";
            restored.Maximized = true;
            // SetWindowPosition documents that it preserves the Maximized state.
            restored.SetWindowPosition(0.25, 0.75, 0.25, 0.75);
            Assert.That(restored.Maximized, Is.True, "SetWindowPosition clobbered Maximized");

            restored.Maximized = false;
            // ...and setting Maximized must not clobber the position.
            restored.GetWindowPosition(out var left, out var right, out var top, out var bottom);
            Assert.That(left, Is.EqualTo(0.25).Within(Tolerance), "Maximized clobbered left");
            Assert.That(right, Is.EqualTo(0.75).Within(Tolerance), "Maximized clobbered right");
            Assert.That(top, Is.EqualTo(0.25).Within(Tolerance), "Maximized clobbered top");
            Assert.That(bottom, Is.EqualTo(0.75).Within(Tolerance), "Maximized clobbered bottom");

            file.AllViews.Add(restored);
          }

          Assert.That(file.Write(path, 8), Is.True, "failed to write {0}", path);
        }

        using (var reread = File3dm.Read(path))
        {
          Assert.That(reread.AllViews.FindName("Maximized").Maximized, Is.True,
            "Maximized did not survive the round trip");
          Assert.That(reread.AllViews.FindName("Restored").Maximized, Is.False,
            "Restored view came back maximized");
        }
      }
      finally
      {
        if (File.Exists(path))
          File.Delete(path);
      }
    }

    static void AssertPosition(ViewInfo view, int index, string stage)
    {
      var q = Quadrants[index];
      view.GetWindowPosition(out var left, out var right, out var top, out var bottom);

      Assert.That(left, Is.EqualTo(q[0]).Within(Tolerance), "{0} left {1}", ViewNames[index], stage);
      Assert.That(right, Is.EqualTo(q[1]).Within(Tolerance), "{0} right {1}", ViewNames[index], stage);
      Assert.That(top, Is.EqualTo(q[2]).Within(Tolerance), "{0} top {1}", ViewNames[index], stage);
      Assert.That(bottom, Is.EqualTo(q[3]).Within(Tolerance), "{0} bottom {1}", ViewNames[index], stage);
    }
  }
}
