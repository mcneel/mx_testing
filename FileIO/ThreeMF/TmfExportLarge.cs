using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same round trip as TmfExport over files too large or slow for a default run. [Explicit]; no debug output.</summary>
  [TestFixture, Explicit]
  public class TmfExportLarge : AnyTmfExportFixture<TmfExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, TmfExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(TmfExportOracle.CountKeys);
    }
  }
}
