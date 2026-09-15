using NUnit.Framework;

namespace FileIO
{
  /// <summary>DWG models that do not import correctly yet. Same semantics as the other -future fixtures: baselines state the wanted result; a green model has been fixed and moves to the verified folder. MX_DWG_REGEN_DRYRUN=1 to look without writing.</summary>
  [TestFixture, Explicit]
  public class DwgImportFuture : AnyDwgFixture<DwgImportFuture>
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
