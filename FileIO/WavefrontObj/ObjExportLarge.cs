using NUnit.Framework;

namespace FileIO
{
  /// <summary>The same round trip as ObjExport over files too large or slow for a default run. [Explicit]; counts-and-bbox baselines; no debug output.</summary>
  [TestFixture, Explicit]
  public class ObjExportLarge : AnyObjExportFixture<ObjExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, ObjExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(ObjExportOracle.CountKeys);
    }
  }
}
