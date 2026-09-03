using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// Round trips every model in the StepExport folders through the STEP writer: open it, export to
  /// STEP, read the result back, and check both ends against the sidecar baseline.
  /// </summary>
  /// <remarks>
  /// This is the everyday export suite. It scans the same <c>models\STEPfile\</c> corpus that
  /// <see cref="StepImport"/> does - a file that is worth guarding on the way in is worth guarding
  /// on the way out - plus <c>models\STEPfile-export\</c> for <c>.3dm</c> sources, which are the
  /// only way to put Rhino-native geometry (extrusions, blocks, open surfaces) through the writer.
  /// Very large assemblies belong in <see cref="StepExportLarge"/>.
  /// </remarks>
  [TestFixture]
  public class StepExport : AnyStepExportFixture<StepExport>
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
