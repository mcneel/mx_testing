using NUnit.Framework;
using Rhino.Testing.Fixtures;

namespace NetSDKTests
{
  [SetUpFixture]
  public sealed class SetupFixture : RhinoSetupFixture
  {
    public override void OneTimeSetup()
    {
      base.OneTimeSetup();
    }

    public override void OneTimeTearDown()
    {
      base.OneTimeTearDown();
    }
  }
}
