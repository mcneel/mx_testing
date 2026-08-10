using NUnit.Framework;
using Rhino;
using Rhino.FileIO;
using System;
using System.IO;

namespace NetSDKTests
{
  [TestFixture]
  public class File3dmRevisionHistory
  {
    // https://mcneel.myjetbrains.com/youtrack/issue/RH-96836
    //
    // ON_3dmRevisionHistory_GetDate hands back struct tm's zero-based tm_mon (0-11).
    // File3dm.Created / File3dm.LastEdited passed it straight into new DateTime(year,
    // month, ...), which wants 1-12: every date came back one month early, and a file
    // with a January revision date threw ArgumentOutOfRangeException.
    [Test]
    public void TestCreatedAndLastEditedMonth_RH96836()
    {
      // A one-month error is at least 28 days. A +/- 2 day window is far narrower
      // than that and far wider than any clock skew or UTC-vs-local difference in
      // how the revision history is stored, so it discriminates cleanly without
      // pinning the test to a timezone.
      var lower = DateTime.Now.AddDays(-2);

      var doc = RhinoDoc.Create(string.Empty);
      var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".3dm");

      try
      {
        Assert.That(doc.WriteFile(path, new FileWriteOptions()), Is.True, "failed to write {0}", path);
        doc.Dispose();

        var upper = DateTime.Now.AddDays(2);

        DateTime created, lastEdited;
        using (var file = File3dm.Read(path))
        {
          // Before the fix LastEdited threw outright when the stored month was
          // January, so read it before asserting anything.
          created = file.Created;
          lastEdited = file.LastEdited;
        }

        Assert.That(created, Is.GreaterThan(lower).And.LessThan(upper),
          "File3dm.Created is off by roughly a month");
        Assert.That(lastEdited, Is.GreaterThan(lower).And.LessThan(upper),
          "File3dm.LastEdited is off by roughly a month");

        // File3dm.ReadRevisionHistory already compensated with its own month+1, so
        // it must still agree with the properties -- this is the double-increment
        // regression guard for the fix.
        Assert.That(
          File3dm.ReadRevisionHistory(path, out _, out _, out _, out var createdOn, out var lastEditedOn),
          Is.True, "ReadRevisionHistory failed");

        Assert.That(createdOn, Is.EqualTo(created), "Created disagrees with ReadRevisionHistory");
        Assert.That(lastEditedOn, Is.EqualTo(lastEdited), "LastEdited disagrees with ReadRevisionHistory");
      }
      finally
      {
        if (File.Exists(path))
          File.Delete(path);
      }
    }

    // The case RH-96836 was reported against: a file whose revision history carries a
    // January date. tm_mon is 0 for January, so the unfixed getter called
    // new DateTime(year, 0, day, ...) and threw ArgumentOutOfRangeException -- not
    // merely a wrong value, but a hard failure on a date that IS set, violating the
    // documented "returns DateTime.MinValue when not set" contract.
    [Test]
    public void TestJanuaryRevisionDate_RH96836()
    {
      var path = Path.Combine(
        Path.GetDirectoryName(typeof(File3dmRevisionHistory).Assembly.Location),
        "models",
        "Small Objects - Centimeters.3dm");

      Assert.That(File.Exists(path), Is.True, "missing fixture {0}", path);

      DateTime created, lastEdited;
      using (var file = File3dm.Read(path))
      {
        created = file.Created;
        lastEdited = file.LastEdited;
      }

      // Stored tm_mon = 0. This is the assertion that threw before the fix.
      Assert.That(lastEdited, Is.EqualTo(new DateTime(2008, 1, 23)).Within(TimeSpan.FromDays(1)),
        "LastEdited should be 2008-01-23");

      // Stored tm_mon = 7, i.e. the plain off-by-one half of the same bug.
      Assert.That(created, Is.EqualTo(new DateTime(2005, 8, 16)).Within(TimeSpan.FromDays(1)),
        "Created should be 2005-08-16");

      Assert.That(
        File3dm.ReadRevisionHistory(path, out _, out _, out _, out var createdOn, out var lastEditedOn),
        Is.True, "ReadRevisionHistory failed");

      Assert.That(createdOn, Is.EqualTo(created), "Created disagrees with ReadRevisionHistory");
      Assert.That(lastEditedOn, Is.EqualTo(lastEdited), "LastEdited disagrees with ReadRevisionHistory");
    }
  }
}
