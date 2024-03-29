using NUnit.Framework;
using Rhino;
using Rhino.Commands;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MxTests
{
  internal class MeshBooleanBase
  {
    public abstract class MeshBooleanBaseImplementation
    : MeasuredBase
    {
      public abstract string FuncName { get; }

      internal override Type TargetType => typeof(Mesh);

      public void Model(string filepath, bool twoGroups)
      {
        ParseAndExecuteNotes(filepath, "AREA", twoGroups);
      }

      public abstract Mesh[] CreateBooleanOperation(
        IEnumerable<Mesh> meshes, IEnumerable<Mesh> possiblyOtherMeshes, MeshBooleanOptions options, out Rhino.Commands.Result commandResult);

      internal override bool OperateCommandOnGeometry(IEnumerable<object> inputMeshes,
          IEnumerable<object> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        bool rc = true;
        returned = new List<ResultMetrics>();
        textLog = null;

        // order of picked objects in Rhino command results in different output
        // at times. Try mimicing this by computing with lists in two different orders
        List<Mesh> inMeshes = new List<Mesh>(inputMeshes.Cast<Mesh>());

        MeshBooleanOptions options = new MeshBooleanOptions { TextLog = new TextLog(), Tolerance = tolerance, };

        var secondMeshesAsMeshes = secondMeshes?.Cast<Mesh>();

          Mesh[] temp = CreateBooleanOperation(inMeshes, secondMeshesAsMeshes, options, out Result commandResult);

          textLog = options.TextLog.ToString();

          if (commandResult != Result.Success || temp == null)
          {
            rc = false;
          }
          else
          {
              foreach(var m in temp)
              {
                double area = AreaMassProperties.Compute(m).Area;
                  returned.Add(new ResultMetrics
                  {
                    Measurement = area,
                    Mesh = m,
                    Closed = m.IsClosed,
                    TextInfo = ObtainVividDescription(m)
                  });
              }
          }

        returned.Sort((a, b) => a.Measurement.CompareTo(b.Measurement));
        return rc;
      }

      internal override void CheckAssertions(object file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
      {
        NUnit.Framework.Assert.IsTrue(rv, $"Return result of {FuncName} function was not Success, or function returned 'null'.");
        NUnit.Framework.Assert.IsEmpty(log_text ?? string.Empty, $"Textlog of function must be empty, but was: '{log_text}'");

        NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} meshes but expected {expected.Count}.");

        for (int i = 0; i < expected.Count; i++)
        {
          NUnit.Framework.Assert.AreEqual(expected[i].Measurement, result_ordered[i].Measurement, Math.Max(expected[i].Measurement * 10e-8, ((File3dm)file).Settings.ModelAbsoluteTolerance));
          
          if (expected[i].Closed.HasValue) NUnit.Framework.Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value,
              $"Mesh of area {expected[i].Measurement} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
        }

        base.CheckAssertions(file, expected, result_ordered, rv, log_text);
      }
    }
  }
}
