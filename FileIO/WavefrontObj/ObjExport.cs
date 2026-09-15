using NUnit.Framework;

namespace FileIO
{
  /// <summary>Round trips every model in the ObjExport folders through the OBJ writer: open, FileObj.Write, grammar checks, FileObj.Read back, compare both ends. Scans the ObjImport corpus plus models-OBJfile-export for .3dm sources.</summary>
  [TestFixture]
  public class ObjExport : AnyObjExportFixture<ObjExport>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, ObjExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(ObjExportOracle.DefaultKeys);
    }
  }
}
