using NUnit.Framework;

namespace FileIO
{
  /// <summary>
  /// The same checks as <see cref="IgesImport"/>, over files too large or too slow for a default
  /// run.
  /// </summary>
  /// <remarks>
  /// Marked [Explicit] for the same reasons as <see cref="StepImportLarge"/>: the models are not
  /// committed - the folder is git-ignored apart from the baselines - and one of them can take
  /// minutes and gigabytes. New baselines default to counts and bounding box only; regenerate an
  /// individual model with MX_IGES_REGEN_FIELDS=ALL when the mass properties are worth the wait.
  /// </remarks>
  [TestFixture, Explicit]
  public class IgesImportLarge : AnyIgesFixture<IgesImportLarge>
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
