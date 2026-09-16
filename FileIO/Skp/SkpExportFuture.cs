using NUnit.Framework;

namespace FileIO
{
  /// <summary>Models that do not survive a SketchUp export round trip yet. Baselines state the wanted result.</summary>
  [TestFixture, Explicit]
  public class SkpExportFuture : AnySkpExportFixture<SkpExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, SkpExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(SkpExportOracle.DefaultKeys);
    }
  }
}
