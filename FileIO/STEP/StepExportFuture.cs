using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Models that do not survive a STEP export round trip yet.
  /// </summary>
  /// <remarks>
  /// The export counterpart of <see cref="StepImportFuture"/>. A model lives here when its sidecar
  /// says what the round trip *should* produce and Rhino does not produce it: a bug to fix rather
  /// than a regression to guard, so the fixture is [Explicit] and never runs on its own.
  ///
  /// Run it on purpose - select the fixture in Test Explorer, or
  /// --filter "FullyQualifiedName~StepExportFuture" - to see where things stand. A model that comes
  /// up green has been fixed: move it, and its .exported.txt, into <c>models\STEPfile-export\</c> so
  /// that it starts guarding the fix.
  ///
  /// As in the import future folder, the baselines here are written by hand or taken from a Rhino
  /// that got the model right: they describe the wanted result, not the current one. Regenerating
  /// one overwrites that with whatever Rhino produces today, which is the very thing being
  /// complained about - so use MX_STEPEXPORT_REGEN_DRYRUN=1 to look, and only regenerate
  /// deliberately.
  /// </remarks>
  [TestFixture, Explicit]
  public class StepExportFuture : AnyStepExportFixture<StepExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, StepExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StepExportOracle.DefaultKeys);
    }
  }
}
