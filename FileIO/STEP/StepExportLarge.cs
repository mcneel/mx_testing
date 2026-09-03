using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// The same round trip as <see cref="StepExport"/>, over assemblies of hundreds of megabytes.
  /// </summary>
  /// <remarks>
  /// Marked [Explicit] so that Run All Tests never picks it up. One of these models costs an import,
  /// an export and a second import, so it is roughly three times what <see cref="StepImportLarge"/>
  /// costs - minutes, and gigabytes of memory. The models are deliberately kept out of the
  /// repository, so the folder is normally absent altogether and the fixture stays quiet. Run it by
  /// selecting the fixture in Test Explorer, or with --filter "FullyQualifiedName~StepExportLarge".
  ///
  /// New baselines here default to counts and bounding box only, with no mass properties: computing
  /// area and volume over both ends of a full vehicle assembly costs far more than the round trip
  /// itself. Add them to an individual model's .exported.txt by hand, or regenerate that model with
  /// MX_STEPEXPORT_REGEN_FIELDS=ALL, when the extra confidence is worth the wait.
  ///
  /// Debug output is off: keeping the written STEP file and saving what it read back would add a
  /// gigabyte of writes to a failure that is already slow.
  /// </remarks>
  [TestFixture, Explicit]
  public class StepExportLarge : AnyStepExportFixture<StepExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, StepExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StepExportOracle.CountKeys);
    }
  }
}
