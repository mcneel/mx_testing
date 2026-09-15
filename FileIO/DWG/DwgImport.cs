using NUnit.Framework;

namespace FileIO
{
  /// <summary>Imports every DWG model in the DwgImport folders and checks it against its sidecar baseline.</summary>
  [TestFixture]
  public class DwgImport : AnyDwgFixture<DwgImport>
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
