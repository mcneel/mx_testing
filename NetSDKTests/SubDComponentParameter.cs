using NUnit.Framework;
using Rhino.Geometry;

namespace NetSDKTests
{
  /// <summary>
  /// Rhino.Geometry.SubDComponentParameter, the value that identifies a point on
  /// a SubD surface. The struct carries no native pointer and none of these
  /// members call into Rhino, so this fixture needs no running Rhino.
  /// </summary>
  /// <remarks>
  /// A SubD has no global (u,v) parameterisation. A surface point is a face, one
  /// of the corners of that face, and two parameters inside that corner running
  /// over [0,1/2], where (0,0) is the corner vertex and (1/2,1/2) is the centre
  /// of the face. See RH-71098.
  /// </remarks>
  [TestFixture]
  public class SubDComponentParameterTests
  {
    const double Tol = 1e-12;

    [Test]
    public void UnsetIsNotSet()
    {
      var p = SubDComponentParameter.Unset;

      Assert.That(p.IsSet, Is.False);
      Assert.That(p.IsVertexParameter, Is.False);
      Assert.That(p.IsEdgeParameter, Is.False);
      Assert.That(p.IsFaceParameter, Is.False);
      Assert.That(p.ComponentId, Is.EqualTo(0u));
      Assert.That(p.ComponentIndex, Is.EqualTo(ComponentIndex.Unset));
      Assert.That(p.ToString(), Is.EqualTo("Unset"));
    }

    [Test]
    public void UnsetAccessorsAreUnset()
    {
      var p = SubDComponentParameter.Unset;

      Assert.That(double.IsNaN(p.EdgeParameter), Is.True);
      Assert.That(p.FaceCornerIndex, Is.EqualTo(-1));
      Assert.That(p.FaceEdgeCount, Is.EqualTo(0));
      Assert.That(p.ActiveFaceId, Is.EqualTo(0u));
      Assert.That(p.FaceCornerParameters, Is.EqualTo(Point2d.Unset));
    }

    [Test]
    public void DefaultInstanceBehavesAsUnset()
    {
      // default(SubDComponentParameter) has all zero fields, which has to read as
      // unset rather than as a bogus vertex parameter.
      var p = default(SubDComponentParameter);

      Assert.That(p.IsSet, Is.False);
      Assert.That(p.ComponentId, Is.EqualTo(0u));
      Assert.That(p.ComponentIndex, Is.EqualTo(ComponentIndex.Unset));
    }

    [Test]
    public void CreateFaceParameterRoundTrips()
    {
      var p = SubDComponentParameter.CreateFaceParameter(17u, 4, 2, 0.125, 0.375);

      Assert.That(p.IsSet, Is.True);
      Assert.That(p.IsFaceParameter, Is.True);
      Assert.That(p.IsVertexParameter, Is.False);
      Assert.That(p.IsEdgeParameter, Is.False);
      Assert.That(p.ComponentId, Is.EqualTo(17u));
      Assert.That(p.FaceEdgeCount, Is.EqualTo(4));
      Assert.That(p.FaceCornerIndex, Is.EqualTo(2));
      Assert.That(p.FaceCornerParameters.X, Is.EqualTo(0.125).Within(Tol));
      Assert.That(p.FaceCornerParameters.Y, Is.EqualTo(0.375).Within(Tol));
      Assert.That(p.ComponentIndex, Is.EqualTo(new ComponentIndex(ComponentIndexType.SubdFace, 17)));

      // A face parameter has no separate active face; the face is the component.
      Assert.That(p.ActiveFaceId, Is.EqualTo(0u));
      Assert.That(double.IsNaN(p.EdgeParameter), Is.True);
    }

    [Test]
    public void CreateFaceParameterAcceptsTheWholeCornerDomain()
    {
      // The corner domain is closed: the corner vertex, both edge midpoints and
      // the face centre are all valid parameters.
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, 0.0, 0.0).IsSet, Is.True);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, 0.5, 0.0).IsSet, Is.True);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, 0.0, 0.5).IsSet, Is.True);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, 0.5, 0.5).IsSet, Is.True);
    }

    [Test]
    public void CreateFaceParameterRejectsBadInput()
    {
      // Zero is not a valid SubD component id.
      Assert.That(SubDComponentParameter.CreateFaceParameter(0u, 4, 0, 0.1, 0.1).IsSet, Is.False);
      // A face needs at least three edges.
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 2, 0, 0.1, 0.1).IsSet, Is.False);
      // The corner index has to be one of the face corners.
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 4, 0.1, 0.1).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, -1, 0.1, 0.1).IsSet, Is.False);
      // The corner parameters run over [0,1/2] and nothing outside it.
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, 0.75, 0.1).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, 0.1, 0.75).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, -0.1, 0.1).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateFaceParameter(1u, 4, 0, double.NaN, 0.1).IsSet, Is.False);
    }

    [Test]
    public void CreateFaceParameterSupportsNgons()
    {
      // Faces with five or more edges have no normalised (u,v) domain, which is
      // the whole reason the parameterisation is per corner.
      var p = SubDComponentParameter.CreateFaceParameter(9u, 5, 4, 0.25, 0.5);

      Assert.That(p.IsFaceParameter, Is.True);
      Assert.That(p.FaceEdgeCount, Is.EqualTo(5));
      Assert.That(p.FaceCornerIndex, Is.EqualTo(4));
    }

    [Test]
    public void CreateEdgeParameter()
    {
      var p = SubDComponentParameter.CreateEdgeParameter(21u, 0.25, 33u);

      Assert.That(p.IsSet, Is.True);
      Assert.That(p.IsEdgeParameter, Is.True);
      Assert.That(p.IsFaceParameter, Is.False);
      Assert.That(p.ComponentId, Is.EqualTo(21u));
      Assert.That(p.EdgeParameter, Is.EqualTo(0.25).Within(Tol));
      Assert.That(p.ActiveFaceId, Is.EqualTo(33u));
      Assert.That(p.ComponentIndex, Is.EqualTo(new ComponentIndex(ComponentIndexType.SubdEdge, 21)));

      // Edge parameters carry no face corner information.
      Assert.That(p.FaceCornerIndex, Is.EqualTo(-1));
      Assert.That(p.FaceCornerParameters, Is.EqualTo(Point2d.Unset));
    }

    [Test]
    public void CreateEdgeParameterRejectsBadInput()
    {
      Assert.That(SubDComponentParameter.CreateEdgeParameter(0u, 0.5, 0u).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateEdgeParameter(1u, -0.1, 0u).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateEdgeParameter(1u, 1.1, 0u).IsSet, Is.False);
      Assert.That(SubDComponentParameter.CreateEdgeParameter(1u, double.NaN, 0u).IsSet, Is.False);

      // Both ends of the edge are valid.
      Assert.That(SubDComponentParameter.CreateEdgeParameter(1u, 0.0, 0u).IsSet, Is.True);
      Assert.That(SubDComponentParameter.CreateEdgeParameter(1u, 1.0, 0u).IsSet, Is.True);
    }

    [Test]
    public void CreateVertexParameter()
    {
      var p = SubDComponentParameter.CreateVertexParameter(5u, 7u);

      Assert.That(p.IsSet, Is.True);
      Assert.That(p.IsVertexParameter, Is.True);
      Assert.That(p.ComponentId, Is.EqualTo(5u));
      Assert.That(p.ActiveFaceId, Is.EqualTo(7u));
      Assert.That(p.ComponentIndex, Is.EqualTo(new ComponentIndex(ComponentIndexType.SubdVertex, 5)));

      Assert.That(SubDComponentParameter.CreateVertexParameter(0u, 7u).IsSet, Is.False);
    }

    [Test]
    public void EqualityIsByValue()
    {
      var a = SubDComponentParameter.CreateFaceParameter(3u, 4, 1, 0.25, 0.5);
      var b = SubDComponentParameter.CreateFaceParameter(3u, 4, 1, 0.25, 0.5);
      var c = SubDComponentParameter.CreateFaceParameter(3u, 4, 2, 0.25, 0.5);

      Assert.That(a.Equals(b), Is.True);
      Assert.That(a == b, Is.True);
      Assert.That(a != b, Is.False);
      Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));

      Assert.That(a.Equals(c), Is.False);
      Assert.That(a != c, Is.True);

      // Boxed comparison goes through the object overload.
      Assert.That(a.Equals((object)b), Is.True);
      Assert.That(a.Equals("not a parameter"), Is.False);
    }

    [Test]
    public void AllUnsetParametersAreEqual()
    {
      var a = SubDComponentParameter.Unset;
      // Rejected input produces an unset parameter.
      var b = SubDComponentParameter.CreateFaceParameter(0u, 4, 0, 0.1, 0.1);

      Assert.That(b.IsSet, Is.False);
      Assert.That(a == b, Is.True);
      Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void ToStringDescribesTheComponent()
    {
      Assert.That(SubDComponentParameter.Unset.ToString(), Is.EqualTo("Unset"));
      Assert.That(SubDComponentParameter.CreateVertexParameter(5u, 0u).ToString(), Is.EqualTo("v5"));
      Assert.That(SubDComponentParameter.CreateEdgeParameter(21u, 0.25, 0u).ToString(), Does.StartWith("e21("));
      Assert.That(SubDComponentParameter.CreateFaceParameter(17u, 4, 2, 0.125, 0.375).ToString(), Does.StartWith("f17.2("));
    }
  }
}
