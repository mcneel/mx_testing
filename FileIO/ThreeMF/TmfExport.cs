using NUnit.Framework;

namespace FileIO
{
  /// <summary>Round trips every model in the TmfExport folders through the 3MF writer: open, write, structural checks, read back, compare both ends.</summary>
  [TestFixture]
  public class TmfExport : AnyTmfExportFixture<TmfExport>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, TmfExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(TmfExportOracle.DefaultKeys);
    }
  }
}
