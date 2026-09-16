using NUnit.Framework;

namespace FileIO
{
  /// <summary>Imports every SketchUp model in the SkpImport folders and checks it against its sidecar baseline.</summary>
  [TestFixture]
  public class SkpImport : AnySkpFixture<SkpImport>
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
