using System.Diagnostics;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Rhino.Testing.Fixtures;

namespace NetSDKTests
{
  [SetUpFixture]
  public sealed class SetupFixture : RhinoSetupFixture
  {
    public override void OneTimeSetup()
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && Process.GetCurrentProcess().ProcessName.Equals("Rhinoceros"))
        return;
      base.OneTimeSetup();
    }

    public override void OneTimeTearDown()
    {
      base.OneTimeTearDown();
    }
  }
}
