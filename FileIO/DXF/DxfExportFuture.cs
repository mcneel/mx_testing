using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Models that do not survive a DXF export round trip yet. Same semantics as
  /// <see cref="IgesExportFuture"/>: baselines describe the wanted result, the fixture is
  /// [Explicit], a green model has been fixed and moves to <c>models\DXFfile-export\</c>.
  /// </summary>
  [TestFixture, Explicit]
  public class DxfExportFuture : AnyDxfExportFixture<DxfExportFuture>
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
