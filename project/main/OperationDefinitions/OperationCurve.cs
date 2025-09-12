using CAMAPI.CurveTypes;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public class OperationCurve
{
    public ICamApiCurve Curve { get; }
    public T3DPoint StartPoint { get; set; }
    public T3DPoint EndPoint { get; set; }
    public T3DPoint NearestPoint { get; set; }
    public double NearestParameter { get; set; }
    public double NextDistance { get; set; }
   

    public OperationCurve(ICamApiCurve curve)
    {
        Curve = curve;
        StartPoint = curve.KnotPoint[0];
        EndPoint = curve.KnotPoint[curve.QntP];
    }
}