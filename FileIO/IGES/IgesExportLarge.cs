using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// The same round trip as <see cref="IgesExport"/>, over files too large or too slow for a
  /// default run.
  /// </summary>
  /// <remarks>
  /// Marked [Explicit] for the same reasons as <see cref="StepExportLarge"/>: an import, an export
  /// and a second import per model, over models that are deliberately not committed. New baselines
  /// default to counts and bounding box only, and debug output is off - keeping the written file
  /// and what it read back would add hundreds of megabytes to a failure that is already slow.
  /// </remarks>
  [TestFixture, Explicit]
  public class IgesExportLarge : AnyIgesExportFixture<IgesExportLarge>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      Execute(filename, filepath, IgesExportOracle.CountKeys, writeDebugModel: false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      ExecuteRegenerate(IgesExportOracle.CountKeys);
    }
  }
}
