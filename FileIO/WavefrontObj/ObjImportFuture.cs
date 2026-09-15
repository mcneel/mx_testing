using NUnit.Framework;

namespace FileIO
{
  /// <summary>OBJ models that do not import correctly yet. Baselines state the wanted result; a green model moves to the verified folder. MX_OBJ_REGEN_DRYRUN=1 to look without writing.</summary>
  [TestFixture, Explicit]
  public class ObjImportFuture : AnyObjFixture<ObjImportFuture>
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
