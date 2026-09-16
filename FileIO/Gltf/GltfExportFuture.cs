using NUnit.Framework;

namespace FileIO
{
  /// <summary>Models that do not survive a glTF export round trip yet. Baselines state the wanted result.</summary>
  [TestFixture, Explicit]
  public class GltfExportFuture : AnyGltfExportFixture<GltfExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, GltfExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(GltfExportOracle.DefaultKeys);
    }
  }
}
