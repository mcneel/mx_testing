using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Imports every IGES model in the IgesImport folders and checks it against its sidecar baseline.
  /// This is the everyday suite: small models that import in well under a second each. Very large
  /// files belong in <see cref="IgesImportLarge"/>.
  /// </summary>
  [TestFixture]
  public class IgesImport : AnyIgesFixture<IgesImport>
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
