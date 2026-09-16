using NUnit.Framework;

namespace FileIO
{
  /// <summary>Round trips every model in the SkpExport folders through the SketchUp writer: open, write, structural checks, read back, compare both ends.</summary>
  [TestFixture]
  public class SkpExport : AnySkpExportFixture<SkpExport>
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
