using NUnit.Framework;

namespace FileIO
{
  /// <summary>Round trips every model in the StlExport folders through the STL writer: open, FileStl.Write, binary-size-formula or ASCII-grammar checks, FileStl.Read back, compare both ends.</summary>
  [TestFixture]
  public class StlExport : AnyStlExportFixture<StlExport>
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
