using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same round trip as DwgExport over files too large or slow for a default run. [Explicit]; counts-and-bbox baselines; no debug output.</summary>
  [TestFixture, Explicit]
  public class DwgExportLarge : AnyDwgExportFixture<DwgExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, DwgExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(DwgExportOracle.CountKeys);
    }
  }
}
