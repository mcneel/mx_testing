using NUnit.Framework;

namespace FileIO
{
  /// <summary>Models that do not survive a DWG export round trip yet. Baselines state the wanted result; a green model moves to models-DWGfile-export.</summary>
  [TestFixture, Explicit]
  public class DwgExportFuture : AnyDwgExportFixture<DwgExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, DwgExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(DwgExportOracle.DefaultKeys);
    }
  }
}
