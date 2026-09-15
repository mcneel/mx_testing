using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Models that do not survive an IGES export round trip yet.
  /// </summary>
  /// <remarks>
  /// The export counterpart of <see cref="IgesImportFuture"/>, with the same semantics: the
  /// baselines describe the wanted result, not the current one, so the fixture is [Explicit] and
  /// never runs on its own. A model that comes up green has been fixed - move it, and its
  /// .exported.txt, into <c>models\IGESfile-export\</c> so that it starts guarding the fix. Use
  /// MX_IGESEXPORT_REGEN_DRYRUN=1 to look at a regeneration without writing it.
  /// </remarks>
  [TestFixture, Explicit]
  public class IgesExportFuture : AnyIgesExportFixture<IgesExportFuture>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, IgesExportOracle.DefaultKeys, writeDebugModel: true);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(IgesExportOracle.DefaultKeys);
    }
  }
}
