using NUnit.Framework;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

namespace MxTests
{
  [TestFixture]
  public class MeshSplit : AnyCommand<MeshSplit>
  {
    [Test, TestCaseSource(nameof(GetTestModels))]
    public override void Run(string filename, string filepath)
    {
      base.Run(filename, filepath);
      MeshSplitImplementation.Instance.Model(Path.Combine(filepath, filename));
    }

    internal class MeshSplitImplementation
    : MeasuredImplementationBase
    {
      static MeshSplitImplementation() { Instance = new MeshSplitImplementation(); }
      private MeshSplitImplementation() { }
      public static MeshSplitImplementation Instance { get; private set; }

      const string incipitString = "MEASURED SPLIT";

      public void Model(string filepath)
      {
        ParseAndExecuteNotes(filepath, incipitString, true);
      }

      internal override bool OperateMeshCommand(IEnumerable<Mesh> inputMeshes, IEnumerable<Mesh> secondMeshes, double tolerance, out List<ResultMetrics> returned, out string textLog)
      {
        bool rc = true;
        returned = new List<ResultMetrics>();

        using (var log = new TextLog())
        {
          foreach (var input in inputMeshes)
          {
            var new_returned = new ResultMetrics();
            Mesh[] temp = input.DuplicateMesh().Split(secondMeshes, tolerance, true, log, System.Threading.CancellationToken.None, null);

            if (temp == null)
            {
              rc = false;
              continue;
            }

            foreach (var m in temp)
            {
              returned.Add(new ResultMetrics
              {
                Measurement = AreaMassProperties.Compute(m).Area,
                Mesh = m,
                Closed = m.IsClosed
              }
              );
            }
          }
          textLog = log.ToString();
        }

        returned.Sort((a, b) => a.Measurement.CompareTo(b.Measurement));
        return rc;
      }

      internal override void CheckAssertions(File3dm file, List<ResultMetrics> expected, List<ResultMetrics> result_ordered, bool rv, string log_text)
      {
        NUnit.Framework.Assert.IsTrue(rv, "Return value of Mesh.Split() function was null.");
        NUnit.Framework.Assert.IsEmpty(log_text, "Textlog of function must be empty");

        NUnit.Framework.Assert.AreEqual(expected.Count, result_ordered.Count, $"Got {result_ordered.Count} splits but expected {expected.Count}.");

        for (int i = 0; i < expected.Count; i++)
        {
          NUnit.Framework.Assert.AreEqual(expected[i].Measurement, result_ordered[i].Measurement, Math.Max(expected[i].Measurement * 10e-8, file.Settings.ModelAbsoluteTolerance));
          if (expected[i].Closed.HasValue) NUnit.Framework.Assert.AreEqual(expected[i].Closed.Value, result_ordered[i].Closed.Value,
              $"Mesh of area {expected[i].Measurement} was not {(expected[i].Closed.Value ? "closed" : "open")} as expected.");
        }
      }
    }
  }
}
