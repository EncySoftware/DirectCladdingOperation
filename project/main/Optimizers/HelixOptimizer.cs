using CAMAPI.CurveTypes;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public struct HelixOptimizer
{
    public static OperationGroup GetOptimizedSingleCurves(OperationGroup sourceGroup, ICamApiTechOperation techOperation)
    {
        sourceGroup.InitOrder();
        for (int i = 0; i < sourceGroup.Group.Count; i++)
        {
            OperationCurve operationCurve = sourceGroup.Group[i];
            if (operationCurve.StartPoint.Z > operationCurve.EndPoint.Z)
                operationCurve.Curve.Inverse();
        }
        return techOperation.XMLProp.Ptr["Sort"].Bol["OptimizeOrder"]
            ? SortCurvesByDistance(sourceGroup, techOperation)
            : sourceGroup;
    }

    public static OperationGroup SortCurvesByDistance(OperationGroup sourceGroup, ICamApiTechOperation techOperation)
    {
        List<int> sortedIndices = [];
        List<int> remainingIndices = Enumerable.Range(0, sourceGroup.Group.Count).ToList();


        T3DPoint machinePoint = GeometryHelper.GetMachineStartPoint(techOperation);
        T3DPoint referencePoint = machinePoint;


        while (remainingIndices.Count > 0)
        {
            int closestIndex = FindClosestCurveIndex(remainingIndices, sourceGroup, referencePoint);
            int closestCurveIndex = remainingIndices[closestIndex];

            sortedIndices.Add(closestCurveIndex);
            remainingIndices.RemoveAt(closestIndex);

            referencePoint = sourceGroup.GetOrderedItem(closestCurveIndex).EndPoint;
        }

        OperationGroup resultGroup = new OperationGroup();
        foreach (int index in sortedIndices)
        {
            resultGroup.Group.Add(sourceGroup.GetOrderedItem(index));
        }
        resultGroup.InitOrder();

        return resultGroup;
    }

    private static int FindClosestCurveIndex(List<int> indices, OperationGroup sourceGroup, T3DPoint referencePoint)
    {
        int closestIndex = 0;
        double minDistance = double.MaxValue;

        for (int i = 0; i < indices.Count; i++)
        {
            int currentIndex = indices[i];

            OperationCurve operationCurve = sourceGroup.GetOrderedItem(currentIndex);
            ICamApiCurve currentCurve = operationCurve.Curve;

            double currentParameter = currentCurve.FindNearestPoint(referencePoint, currentCurve.TMin, currentCurve.TMax);
            T3DPoint currentPoint = currentCurve.Get_Point(currentParameter);
            double currentDistance = T3DPoint.Distance(currentPoint, referencePoint);

            if (currentDistance < minDistance)
            {
                operationCurve.NextDistance = currentDistance;
                minDistance = currentDistance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}