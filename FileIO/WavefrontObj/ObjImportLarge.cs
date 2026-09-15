using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same checks as ObjImport over files too large or slow for a default run. [Explicit]; models not committed; counts-and-bbox baselines by default.</summary>
  [TestFixture, Explicit]
  public class ObjImportLarge : AnyObjFixture<ObjImportLarge>
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
