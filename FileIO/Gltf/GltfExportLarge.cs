using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same round trip as GltfExport over files too large or slow for a default run. [Explicit]; no debug output.</summary>
  [TestFixture, Explicit]
  public class GltfExportLarge : AnyGltfExportFixture<GltfExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, GltfExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(GltfExportOracle.CountKeys);
    }
  }
}
