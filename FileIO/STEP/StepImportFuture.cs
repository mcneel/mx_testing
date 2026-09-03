using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// STEP models that do not import correctly yet.
  /// </summary>
  /// <remarks>
  /// The counterpart of the mesh suites' "-future" folders. A model lives here when its sidecar
  /// baseline says what the import *should* produce and Rhino does not produce it: a bug to fix
  /// rather than a regression to guard, so the fixture is [Explicit] and never runs on its own.
  ///
  /// Run it on purpose - select the fixture in Test Explorer, or
  /// --filter "FullyQualifiedName~StepImportFuture" - to see where things stand. A model that
  /// comes up green has been fixed: move it, and its .expected.txt, into the verified folder so
  /// that it starts guarding the fix.
  ///
  /// Note that the baselines here are written by hand, or taken from a Rhino that got the model
  /// right: they describe the wanted result, not the current one. Regenerating one overwrites that
  /// with whatever Rhino produces today, which is the very thing being complained about - so use
  /// MX_STEP_REGEN_DRYRUN=1 to look, and only regenerate deliberately.
  /// </remarks>
  [TestFixture, Explicit]
  public class StepImportFuture : AnyStepFixture<StepImportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(StepOracle.AllKeys);
    }
  }
}
