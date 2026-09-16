using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same checks as SkpImport over files too large or slow for a default run. [Explicit]; counts-and-bbox baselines.</summary>
  [TestFixture, Explicit]
  public class SkpImportLarge : AnySkpFixture<SkpImportLarge>
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
