using Rhino;
using Rhino.Geometry;
using NUnit.Framework;
using System;
using Rhino.Testing.Fixtures;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Constraints;

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

  [TestFixture]
  public class TestFilletEdgeVariableRadiusFixture : RhinoTestFixture
  {
    Brep[] Fillet(Brep box, IEnumerable<int> edges, IDictionary<int, IList<BrepEdgeFilletDistance>> distances, BlendType bt, RailType rt, IResolveConstraint a)
    {
      Brep[] result = Brep.CreateFilletEdgesVariableRadius(box, edges, distances, bt, rt, false, 1e-3, Math.PI / 180.0);
      Assert.That(result, a);
      return result;
    }

    [Test]
    public void TestFilletEdgeVariableRadius(
      [Values(BlendType.Fillet, BlendType.Chamfer, BlendType.Blend)] BlendType bt,
      [Values(RailType.RollingBall, RailType.DistanceBetweenRails, RailType.DistanceFromEdge)] RailType rt)
    {
      Brep box = new Box(new BoundingBox(0, 0, 0, 10, 10, 10)).ToBrep();

      IDictionary<int, IList<BrepEdgeFilletDistance>> distances = new Dictionary<int, IList<BrepEdgeFilletDistance>>();
      IEnumerable<int> edges = Enumerable.Range(0, box.Edges.Count);
      foreach (var ei in edges)
      {
        List<BrepEdgeFilletDistance> edgeDistances = new List<BrepEdgeFilletDistance>(3);
        BrepEdge edge = box.Edges[ei];
        edgeDistances.Add(new BrepEdgeFilletDistance(edge.Domain.Min, 1.0));
        edgeDistances.Add(new BrepEdgeFilletDistance(edge.Domain.Mid, 0.5));
        edgeDistances.Add(new BrepEdgeFilletDistance(edge.Domain.Max, 1.0));
        distances[ei] = edgeDistances;
      }

      // null parameters should throw an exception
      Assert.Throws<ArgumentNullException>(() => Fillet(null, edges, distances, bt, rt, Is.False));
      Assert.Throws<ArgumentNullException>(() => Fillet(box, null, distances, bt, rt, Is.False));
      Assert.Throws<ArgumentNullException>(() => Fillet(box, edges, null, bt, rt, Is.False));

      // without any edges or distances should fail
      Fillet(box, Array.Empty<int>(), new Dictionary<int, IList<BrepEdgeFilletDistance>>(), bt, rt, Is.Empty);

      // with edges but without distances should fail
      Fillet(box, edges, new Dictionary<int, IList<BrepEdgeFilletDistance>>(), bt, rt, Is.Empty);

      // without any edges but with distances should fail
      Fillet(box, Array.Empty<int>(), distances, bt, rt, Is.Empty);

      // with box, edges and distances should not fail
      Brep[] result = Fillet(box, edges, distances, bt, rt, Is.Not.Empty);
      Assert.That(result.Length, Is.EqualTo(1));

      Brep filleted = result[0];
      Assert.That(filleted, Is.Not.Null);
      Assert.That(filleted.IsValid, Is.True);

      // 6 faces, 12 edges and 8 corners makes 26 faces
      Assert.That(filleted.Faces.Count, Is.EqualTo(26));
    }
  }
}
