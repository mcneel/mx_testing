using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same checks as StlImport over files too large or slow for a default run. [Explicit]; counts-and-bbox baselines by default.</summary>
  [TestFixture, Explicit]
  public class StlImportLarge : AnyStlFixture<StlImportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StepOracle.CountKeys);
    }
  }
}
