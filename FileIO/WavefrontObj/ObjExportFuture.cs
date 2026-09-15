using NUnit.Framework;

namespace FileIO
{
  /// <summary>Models that do not survive an OBJ export round trip yet. Baselines state the wanted result; a green model moves to models-OBJfile-export.</summary>
  [TestFixture, Explicit]
  public class ObjExportFuture : AnyObjExportFixture<ObjExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, ObjExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(ObjExportOracle.DefaultKeys);
    }
  }
}
