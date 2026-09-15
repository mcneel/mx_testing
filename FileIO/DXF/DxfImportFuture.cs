using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// DXF models that do not import correctly yet. Same semantics as <see cref="IgesImportFuture"/>:
  /// baselines describe the wanted result, the fixture is [Explicit], a green model has been fixed
  /// and moves to the verified folder. MX_DXF_REGEN_DRYRUN=1 to look without writing.
  /// </summary>
  [TestFixture, Explicit]
  public class DxfImportFuture : AnyDxfFixture<DxfImportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StepOracle.AllKeys);
    }
  }
}
