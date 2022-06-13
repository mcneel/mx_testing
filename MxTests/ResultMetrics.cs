using Rhino.Geometry;

namespace MxTests
{
  class ResultMetrics
  {
    public double Measurement { get; set; }
    public bool? Closed { get; set; }
    public bool? Overlap { get; set; }
    public object Polyline { get; set; }
    
    internal object Mesh { get; set; }

    internal object Curve { get; set; }

    internal object Point { get; set; }
  }
}
