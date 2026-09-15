using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// The same checks as <see cref="DxfImport"/>, over files too large or slow for a default run.
  /// [Explicit]; models are not committed (folder git-ignored apart from baselines); new baselines
  /// default to counts and bounding box only.
  /// </summary>
  [TestFixture, Explicit]
  public class DxfImportLarge : AnyDxfFixture<DxfImportLarge>
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
