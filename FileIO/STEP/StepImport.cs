using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Imports every STEP model in the StepImport folders and checks it against its sidecar baseline.
  /// This is the everyday suite: small conformance models that import in well under a second each.
  /// Very large assemblies belong in <see cref="StepImportLarge"/>.
  /// </summary>
  [TestFixture]
  public class StepImport : AnyStepFixture<StepImport>
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
