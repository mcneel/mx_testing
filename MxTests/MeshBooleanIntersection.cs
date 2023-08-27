using NUnit.Framework;
using Rhino.Commands;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxTests
{
  [TestFixture]
  public class MeshBooleanIntersection : AnyCommand<MeshBooleanIntersection>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      (new MeshBooleanIntersectionImplementation()).Model(Path.Combine(filepath, filename), true);
    }

    class MeshBooleanIntersectionImplementation
    : MeshBooleanBase.MeshBooleanBaseImplementation
    {
      public override Mesh[] CreateBooleanOperation(IEnumerable<Mesh> meshes, IEnumerable<Mesh> possiblyOtherMeshes, MeshBooleanOptions options, out Result commandResult)
      {
        return Mesh.CreateBooleanIntersection(meshes, possiblyOtherMeshes, options, out commandResult);
      }

      public override string FuncName => nameof(Mesh.CreateBooleanIntersection);
    }
  }
}
