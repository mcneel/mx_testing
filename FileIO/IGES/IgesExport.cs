using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Round trips every model in the IgesExport folders through the IGES writer: open it, export to
  /// IGES, check the file's fixed-format structure, read the result back, and check both ends
  /// against the sidecar baseline.
  /// </summary>
  /// <remarks>
  /// Scans the same <c>models\IGESfile\</c> corpus that <see cref="IgesImport"/> does - a file
  /// worth guarding on the way in is worth guarding on the way out - plus
  /// <c>models\IGESfile-export\</c> for <c>.3dm</c> sources, the only way to put Rhino-native
  /// geometry (extrusions, blocks, meshes, open surfaces) through the writer.
  /// </remarks>
  [TestFixture]
  public class IgesExport : AnyIgesExportFixture<IgesExport>
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
