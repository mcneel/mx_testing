using NUnit.Framework;

namespace FileIO
{
  /// <summary>Models that do not survive an STL export round trip yet. Baselines state the wanted result.</summary>
  [TestFixture, Explicit]
  public class StlExportFuture : AnyStlExportFixture<StlExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, StlExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StlExportOracle.DefaultKeys);
    }
  }
}
