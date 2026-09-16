using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same round trip as SkpExport over files too large or slow for a default run. [Explicit]; no debug output.</summary>
  [TestFixture, Explicit]
  public class SkpExportLarge : AnySkpExportFixture<SkpExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, SkpExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(SkpExportOracle.CountKeys);
    }
  }
}
