using NUnit.Framework;
using Rhino.Commands;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class MeshBooleanUnion : AnyCommand<MeshBooleanUnion>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      (new MeshBooleanUnionImplementation()).Model(Path.Combine(filepath, filename), false);
    }

    [Test, Explicit]
    public void Regenerate()
    {
      var impl = new MeshBooleanUnionImplementation();
      int n = 0;
      foreach (var path in g_test_models) if (impl.RegenerateOracle(path, "AREA", false)) n++;
      if (n == 0) Assert.Ignore($"No models matched MX_REGEN='{Environment.GetEnvironmentVariable("MX_REGEN")}'.");
    }

    class MeshBooleanUnionImplementation
    : MeshBooleanBase.MeshBooleanBaseImplementation
    {
      public override Mesh[] CreateBooleanOperation(IEnumerable<Mesh> meshes, IEnumerable<Mesh> possiblyOtherMeshes, MeshBooleanOptions options, out Result commandResult)
      {
        return Mesh.CreateBooleanUnion(meshes, options, out commandResult);
      }

      public override string FuncName => nameof(Mesh.CreateBooleanUnion);
    }
  }
}
