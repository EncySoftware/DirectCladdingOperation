using CAMAPI.CurveTypes;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public struct GroupOptimizer
{
    public static void SortGroupsByOrderIndex(OperationData operationData, OperationGroup sourceGroup)
    {
        foreach (var group in operationData.Groups)
        {
            group.Sort(CompareByOrderIndex);
        }

        int CompareByOrderIndex(int index1, int index2)
        {
            OperationCurve curve1 = sourceGroup.GetOrderedItem(index1);
            OperationCurve curve2 = sourceGroup.GetOrderedItem(index2);

            T3DPoint center1 = CurveUniter.GetCurveCenter(curve1);
            T3DPoint center2 = CurveUniter.GetCurveCenter(curve2);

            return center1.Z.CompareTo(center2.Z);
        }
    }

    public static OperationData OptimizeGroupsOrder(
        OperationData groups, OperationGroup sourceGroup,
        ICamApiTechOperation techOperation, PropsParams propsParams)
    {
        if (groups.Groups.Count < 1 || !techOperation.XMLProp.Ptr["Sort"].Bol["OptimizeOrder"])
            return groups;
            
        var startPointChangeParams = propsParams.StartPointChangeParams;
        T3DPoint machinePoint;
        if (startPointChangeParams.StartPointChangeType == StartPointChangeType.Automatic)
            machinePoint = GeometryHelper.GetMachineStartPoint(techOperation);
        else
            machinePoint = startPointChangeParams.ManualStartPoint;

        OperationData result = new OperationData();
        var remainingGroups = new List<List<int>>(groups.Groups);

        T3DPoint referencePoint = machinePoint;
        while (remainingGroups.Count > 0)
        {
            int closestGroupIndex = FindClosestGroupIndex(remainingGroups, sourceGroup, referencePoint, out T3DPoint nearestPoint);

            var closestGroup = remainingGroups[closestGroupIndex];

            List<int> optimizedGroup = OptimizeSingleGroup(closestGroup, sourceGroup, referencePoint);
            result.Groups.Add(optimizedGroup);
            remainingGroups.RemoveAt(closestGroupIndex);

            referencePoint = GetGroupEndPoint(optimizedGroup, sourceGroup);
        }
        return result;
    }

    private static T3DPoint GetGroupEndPoint(List<int> groupIndices, OperationGroup sourceGroup)
    {
        if (groupIndices.Count == 0)
            return T3DPoint.Zero;

        int lastIndex = groupIndices.Last();
        OperationCurve lastCurve = sourceGroup.GetOrderedItem(lastIndex);
        return lastCurve.EndPoint;
    }

    private static int FindClosestGroupIndex(List<List<int>> groups, OperationGroup sourceGroup, T3DPoint referencePoint, out T3DPoint nearestPoint)
    {
        int closestIndex = 0;
        double minDistance = double.MaxValue;
        nearestPoint = referencePoint;

        for (int i = 0; i < groups.Count; i++)
        {
            var groupIndices = groups[i];
            if (groupIndices.Count == 0) continue;

            int firstCurveIndex = groupIndices.First();
            OperationCurve operationCurve = sourceGroup.GetOrderedItem(firstCurveIndex);
            ICamApiCurve currentCurve = operationCurve.Curve;

            double nearestParameter = currentCurve.FindNearestPoint(
                        referencePoint, currentCurve.TMin, currentCurve.TMax);
            T3DPoint currentNearestPoint = currentCurve.Get_Point(nearestParameter);

            double distance = T3DPoint.Distance(currentNearestPoint, referencePoint);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
                nearestPoint = currentNearestPoint;
            }
        }

        return closestIndex;
    }

    private static List<int> OptimizeSingleGroup(List<int> groupIndices, OperationGroup sourceGroup, T3DPoint referencePoint)
    {
        if (groupIndices.Count < 1)
            return groupIndices;

        List<int> sortedIndices = [];
        List<int> remainingIndices = [.. groupIndices];

        int firstCurveIndex = remainingIndices[0];
        sortedIndices.Add(firstCurveIndex);
        remainingIndices.RemoveAt(0);


        OperationCurve firstOperationCurve = sourceGroup.GetOrderedItem(firstCurveIndex);
        ICamApiCurve firstCurve = firstOperationCurve.Curve;
        double firstCurveNearestParam = firstCurve.FindNearestPoint(referencePoint, firstCurve.TMin, firstCurve.TMax);

        T3DPoint firstCurveNearestPoint = firstCurve.Get_Point(firstCurveNearestParam);
        double firstCurveDistance = T3DPoint.Distance(firstCurveNearestPoint, referencePoint);

        firstOperationCurve.EndPoint = firstCurveNearestPoint;
        firstOperationCurve.NextDistance = firstCurveDistance;

        referencePoint = firstOperationCurve.EndPoint;

        while (remainingIndices.Count > 0)
        {
            int closestIndex = FindClosestCurveInList(remainingIndices, sourceGroup, referencePoint, out T3DPoint nearestPoint);
            int closestCurveIndex = remainingIndices[closestIndex];

            sortedIndices.Add(closestCurveIndex);
            remainingIndices.RemoveAt(closestIndex);

            OperationCurve currentCurve = sourceGroup.GetOrderedItem(closestCurveIndex);
            referencePoint = currentCurve.EndPoint;
        }

        return sortedIndices;
    }

    private static int FindClosestCurveInList(List<int> indices, OperationGroup sourceGroup, T3DPoint referencePoint, out T3DPoint nearestPoint)
    {
        int closestIndex = 0;
        double minDistance = double.MaxValue;
        nearestPoint = referencePoint;

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
                minDistance = currentDistance;
                closestIndex = i;
                nearestPoint = currentPoint;
            }
        }

        return closestIndex;
    }
}