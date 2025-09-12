using CAMAPI.CurveTypes;
using CAMAPI.MCDFormerTypes;
using Geometry.VecMatrLib;
using STTypes;

namespace DirectCladdingOperationExtension;

/// <summary>
/// Converts curve from ICamApiCurve to commands of ICamApiCLDReceiver
/// </summary>
class CurveConverter : ICamApiAbstractCurveReceiver, ICamApiCurveArcsReceiver
{

    ICamApiCLDReceiver fTargetReceiver = null!;
    T3DPoint fLastPoint;

    public ICamApiCLDReceiver TargetReceiver
    {
        get => fTargetReceiver;
        set => fTargetReceiver = value;
    }

    public void StartCurve3D(TST3DPoint p)
    {
        fTargetReceiver.CutTo(p);
        fLastPoint = p;
    }

    public void StopCurve(bool IsClosed)
    {
    }

    public void CutTo3D(TST3DPoint p)
    {
        fTargetReceiver.CutTo(p);
        fLastPoint = p;
    }

    public void ArcTo3D(TST3DPoint pc, TST3DPoint ep, double R)
    {
        fTargetReceiver.ArcTo3d(ep, pc, T3DPoint.UnitZ, R, false);
        fLastPoint = ep;
    }

    public void AddCircle(TST3DPoint pc, double R)
    {
        fTargetReceiver.ArcTo3d(fLastPoint, pc, T3DPoint.UnitZ, R, true);
    }
}