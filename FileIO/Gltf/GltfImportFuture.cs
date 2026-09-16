using NUnit.Framework;

namespace FileIO
{
  /// <summary>glTF models that do not import correctly yet. Baselines state the wanted result; a green model moves to the verified folder.</summary>
  [TestFixture, Explicit]
  public class GltfImportFuture : AnyGltfFixture<GltfImportFuture>
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
