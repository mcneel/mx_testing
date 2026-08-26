using NUnit.Framework;

namespace MxTests
{
  /// <summary>
  /// The same checks as <see cref="StepImport"/>, over assemblies of hundreds of megabytes.
  /// </summary>
  /// <remarks>
  /// Marked [Explicit] so that Run All Tests never picks it up: a single one of these models can
  /// take minutes to import and gigabytes of memory, and the files are deliberately kept out of the
  /// repository, so the folder is normally absent altogether. Run it by selecting the fixture in
  /// Test Explorer, or with --filter "FullyQualifiedName~StepImportLarge".
  ///
  /// New baselines here default to counts and bounding box only, with no mass properties: computing
  /// area and volume over every solid of a full vehicle assembly costs far more than the import
  /// itself. Add them to an individual model's .expected.txt by hand, or regenerate that model with
  /// MX_STEP_REGEN_FIELDS=ALL, when the extra confidence is worth the wait.
  /// </remarks>
  [TestFixture, Explicit]
  public class StepImportLarge : AnyStepFixture<StepImportLarge>
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
