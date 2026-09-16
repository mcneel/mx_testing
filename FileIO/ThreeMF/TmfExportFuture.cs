using NUnit.Framework;

namespace FileIO
{
  /// <summary>Models that do not survive a 3MF export round trip yet. Baselines state the wanted result.</summary>
  [TestFixture, Explicit]
  public class TmfExportFuture : AnyTmfExportFixture<TmfExportFuture>
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
