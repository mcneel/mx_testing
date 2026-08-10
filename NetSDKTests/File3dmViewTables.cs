using NUnit.Framework;
using Rhino.DocObjects;
using Rhino.FileIO;
using System.IO;

namespace NetSDKTests
{
  [TestFixture]
  public class File3dmViewTables
  {
    const string ModelViewName = "RH96867_ModelView";
    const string NamedViewName = "RH96867_NamedView";

    // https://mcneel.myjetbrains.com/youtrack/issue/RH-96867
    //
    // File3dm.AllViews cached its (namedViews: false) table into m_named_view_table,
    // so whichever of AllViews / NamedViews was touched first decided what BOTH
    // returned. Touch AllViews first and NamedViews silently became the model views
    // list: reads mirrored it and Adds landed in it.
    [Test]
    public void TestViewTablesStaySeparate_AllViewsFirst_RH96867()
    {
      RunViewTableRoundTrip(allViewsFirst: true);
    }

    [Test]
    public void TestViewTablesStaySeparate_NamedViewsFirst_RH96867()
    {
      RunViewTableRoundTrip(allViewsFirst: false);
    }

    static void RunViewTableRoundTrip(bool allViewsFirst)
    {
      var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".3dm");

      try
      {
        using (var file = new File3dm())
        {
          // The access order is the whole point: before the fix the first property
          // touched won the cache field for both.
          if (allViewsFirst)
          {
            AddView(file.AllViews, ModelViewName);
            AddView(file.AllNamedViews, NamedViewName);
          }
          else
          {
            AddView(file.AllNamedViews, NamedViewName);
            AddView(file.AllViews, ModelViewName);
          }

          AssertTables(file, "in memory");

          Assert.That(file.Write(path, 8), Is.True, "failed to write {0}", path);
        }

        // The native tables were always fine; this proves the managed layer put each
        // view in the table it actually names, not just that it reports it that way.
        using (var reread = File3dm.Read(path))
        {
          AssertTables(reread, "after round trip");
        }
      }
      finally
      {
        if (File.Exists(path))
          File.Delete(path);
      }
    }

    static void AddView(File3dmViewTable table, string name)
    {
      using (var view = new ViewInfo())
      {
        view.Name = name;
        table.Add(view);
      }
    }

    static void AssertTables(File3dm file, string stage)
    {
      Assert.That(file.AllViews.Count, Is.EqualTo(1), "AllViews.Count {0}", stage);
      Assert.That(file.AllNamedViews.Count, Is.EqualTo(1), "AllNamedViews.Count {0}", stage);
      Assert.That(file.AllViews[0].Name, Is.EqualTo(ModelViewName), "AllViews content {0}", stage);
      Assert.That(file.AllNamedViews[0].Name, Is.EqualTo(NamedViewName), "AllNamedViews content {0}", stage);

      // Views / NamedViews are the IList<ViewInfo> faces of the same two tables and
      // share the cache fields, so they have to agree.
      Assert.That(file.Views.Count, Is.EqualTo(1), "Views.Count {0}", stage);
      Assert.That(file.NamedViews.Count, Is.EqualTo(1), "NamedViews.Count {0}", stage);
      Assert.That(file.Views[0].Name, Is.EqualTo(ModelViewName), "Views content {0}", stage);
      Assert.That(file.NamedViews[0].Name, Is.EqualTo(NamedViewName), "NamedViews content {0}", stage);
    }
  }
}
