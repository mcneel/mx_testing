using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Round trips every model in the DxfExport folders through the DXF writer: open it, export via
  /// FileDwg.Write, check the pair-grammar structure, read the result back via FileDwg.Read, and
  /// check both ends against the sidecar baseline. Scans the DxfImport corpus plus
  /// <c>models\DXFfile-export\</c> for <c>.3dm</c> sources.
  /// </summary>
  [TestFixture]
  public class DxfExport : AnyDxfExportFixture<DxfExport>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, DxfExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(DxfExportOracle.DefaultKeys);
    }
  }
}
