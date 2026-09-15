using NUnit.Framework;

namespace FileIO
{
  /// <summary>Round trips every model in the DwgExport folders through the DWG writer: open, FileDwg.Write, binary header checks, FileDwg.Read back, compare both ends. Scans the DwgImport corpus plus models-DWGfile-export for .3dm sources.</summary>
  [TestFixture]
  public class DwgExport : AnyDwgExportFixture<DwgExport>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, DwgExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(DwgExportOracle.DefaultKeys);
    }
  }
}
