using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// IGES models that do not import correctly yet.
  /// </summary>
  /// <remarks>
  /// The counterpart of <see cref="StepImportFuture"/>, with the same semantics: baselines here
  /// describe the wanted result, not the current one, so the fixture is [Explicit] and never runs
  /// on its own. A model that comes up green has been fixed - move it, and its .expected.txt, into
  /// the verified folder so that it starts guarding the fix. Use MX_IGES_REGEN_DRYRUN=1 to look at
  /// a regeneration without writing it; regenerating a future baseline replaces the goal with the
  /// bug.
  /// </remarks>
  [TestFixture, Explicit]
  public class IgesImportFuture : AnyIgesFixture<IgesImportFuture>
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
