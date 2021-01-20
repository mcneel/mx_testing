using Rhino.Geometry;

namespace MxTests
{
  class ResultMetrics
  {
    public double Measurement { get; set; }
    public bool? Closed { get; set; }
    public bool? Overlap { get; set; }
    public Polyline Polyline { get; set; }
    public Mesh Mesh { get; set; }

    public Curve Curve { get; set; }

    public Point3d? Point { get; set; }
  }
}
