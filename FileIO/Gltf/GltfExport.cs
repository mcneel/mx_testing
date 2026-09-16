using NUnit.Framework;

namespace FileIO
{
  /// <summary>Round trips every model in the GltfExport folders through the glTF writer: open, write, structural checks, read back, compare both ends.</summary>
  [TestFixture]
  public class GltfExport : AnyGltfExportFixture<GltfExport>
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
