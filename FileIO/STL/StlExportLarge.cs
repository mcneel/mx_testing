using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same round trip as StlExport over files too large or slow for a default run. [Explicit]; no debug output.</summary>
  [TestFixture, Explicit]
  public class StlExportLarge : AnyStlExportFixture<StlExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, StlExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StlExportOracle.CountKeys);
    }
  }
}
