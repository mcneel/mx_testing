using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino;
using System.IO;
using Rhino.FileIO;
using System.Threading.Tasks;

namespace NetSDKTests
{
  [TestFixture]
  public class RhinoDocOpen
  {

    [Test]
    public void TestDocOpen_RH80580()
    {
      // https://discourse.mcneel.com/t/confusion-over-document-runtimeserialnumber-during-rhinodoc-open/182672
      // https://mcneel.myjetbrains.com/youtrack/issue/RH-82580

      //Create new file
      var newRhinoDoc = RhinoDoc.Create(string.Empty);
      var sn1 = newRhinoDoc.RuntimeSerialNumber;
      var snA = RhinoDoc.ActiveDoc.RuntimeSerialNumber;
      //Get a TempFileName
      var testFileName = Path.GetTempFileName() + ".3dm";
      //Save the File
      newRhinoDoc.WriteFile(testFileName, new FileWriteOptions());
      //Close the File
      newRhinoDoc.Dispose();
      //Open the saved file
      var openedDoc = RhinoDoc.Open(testFileName, out var wasAlreadyOpen);
      var sn2 = openedDoc.RuntimeSerialNumber;
      //Reopen the saved file
      var reopenedDoc = RhinoDoc.Open(testFileName, out wasAlreadyOpen);
      var sn3 = reopenedDoc.RuntimeSerialNumber;
      var activeDoc = RhinoDoc.ActiveDoc;
      var sn4 = activeDoc.RuntimeSerialNumber;
      Assert.That(sn4, Is.EqualTo(sn3));

      openedDoc.Dispose();
      File.Delete(testFileName);
    }
  }
}
