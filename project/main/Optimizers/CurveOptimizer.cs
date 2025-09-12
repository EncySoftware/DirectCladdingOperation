using CAMAPI.CurveTypes;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public struct CurveOptimizer
{
    private static class SortBy
    {
        public const int Layer = 1;
        public const int Feature = 0;
    }

    public static OperationData GetOptimizedCurveGroups(OperationGroup curves,
        ICamApiTechOperation techOperation, int sortBy)
    {
        curves.InitOrder();

        OperationData curvesGroupedBy = sortBy == SortBy.Layer
            ? OptimizeByLayer(curves, techOperation)
            : OptimizeByFeature(curves, techOperation);

        UpdateStartPoints(curvesGroupedBy, curves, techOperation);

        return curvesGroupedBy;
    }

    private static OperationData OptimizeByLayer(
        OperationGroup curves,
        ICamApiTechOperation techOperation)
    {
        OperationData curvesGroupedBy = CurveUniter.GroupCurvesByZLayer(curves);
        curvesGroupedBy = LayerOptimizer.OptimizeLayersOrder(curvesGroupedBy, curves, techOperation);

        return curvesGroupedBy;
    }

    private static OperationData OptimizeByFeature(
        OperationGroup curves,
        ICamApiTechOperation techOperation)
    {
        OperationData curvesGroupedBy = CurveUniter.GroupCurvesByCenter(curves);

        var splitByLayer = techOperation.XMLProp.Ptr["Sort"].Ptr["SplitByLayer"];
        bool isSplitByLayerEnabled = splitByLayer.Bol["Enabled"];

        if (isSplitByLayerEnabled)
        {
            int batchSize = splitByLayer.Int["Count"];
            if (batchSize > 1)
            {
                return ProcessFeaturesInLayers(curvesGroupedBy, batchSize);
            }
        }

        GroupOptimizer.SortGroupsByOrderIndex(curvesGroupedBy, curves);
        curvesGroupedBy = GroupOptimizer.OptimizeGroupsOrder(curvesGroupedBy, curves, techOperation);

        return curvesGroupedBy;
    }

    public static void UpdateStartPoints(
        OperationData operationData,
        OperationGroup sourceGroup,
        ICamApiTechOperation techOperation)
    {
        if (!techOperation.XMLProp.Ptr["Sort"].Bol["AllowChangeStartPoint"])
            return;

        T3DPoint machinePoint = GeometryHelper.GetMachineStartPoint(techOperation);
        T3DPoint referencePoint = machinePoint;

        for (int groupIdx = 0; groupIdx < operationData.Groups.Count; groupIdx++)
        {
            var layerGroup = operationData.Groups[groupIdx];
            if (layerGroup.Count == 0) continue;

            for (int j = 0; j < layerGroup.Count; j++)
            {
                int curveIndex = layerGroup[j];
                OperationCurve operationCurve = sourceGroup.GetOrderedItem(curveIndex);
                ICamApiCurve currentCurve = operationCurve.Curve;

                double nearestParameter = currentCurve.FindNearestPoint(
                            referencePoint, currentCurve.TMin, currentCurve.TMax);
                T3DPoint currentNearestPoint = currentCurve.Get_Point(nearestParameter);

                operationCurve.NearestParameter = nearestParameter;
                operationCurve.NearestPoint = currentNearestPoint;
                operationCurve.StartPoint = currentNearestPoint;
                operationCurve.EndPoint = currentNearestPoint;

                referencePoint = currentNearestPoint;
            }
        }
    }

    public static int[] OptimizeCurveOrder(List<OperationCurve> curves, ICamApiTechOperation techOperation)
    {
        int count = curves.Count;
        if (count <= 1)
            return count == 1
                ? [0]
                : [];

        int[] newOrder = new int[count];
        bool[] visited = new bool[count];

        T3DPoint machinePoint = GeometryHelper.GetMachineStartPoint(techOperation);
        int startIndex = FindNearestToPoint(curves, machinePoint);
        newOrder[0] = startIndex;
        visited[startIndex] = true;

        T3DPoint endPoint = curves[startIndex].EndPoint;
        for (int i = 1; i < count; i++)
        {
            int nearestIndex = -1;
            double minDistance = double.MaxValue;
            OperationCurve bestCandidate = curves[startIndex];
            for (int j = 0; j < count; j++)
            {
                if (!visited[j])
                {
                    OperationCurve candidateICurve = curves[j];
                    ICamApiCurve candidateCurve = candidateICurve.Curve;

                    double nearestParam = candidateCurve.FindNearestPoint(
                        endPoint, candidateCurve.TMin, candidateCurve.TMax);

                    T3DPoint nearestPoint = candidateCurve.Get_Point(nearestParam);

                    double distance = T3DPoint.Distance(endPoint, nearestPoint);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestIndex = j;
                        bestCandidate = candidateICurve;
                    }
                }
            }

            if (nearestIndex != -1)
            {
                newOrder[i] = nearestIndex;
                visited[nearestIndex] = true;
                endPoint = bestCandidate.EndPoint;
            }
            else
            {
                for (int j = 0; j < count; j++)
                {
                    if (!visited[j])
                    {
                        newOrder[i] = j;
                        visited[j] = true;
                        endPoint = curves[j].EndPoint;
                        break;
                    }
                }
            }
        }
        return newOrder;
    }

    private static int FindNearestToPoint(List<OperationCurve> curves, T3DPoint point)
    {
        int nearestIndex = 0;
        double minDistance = double.MaxValue;

        for (int i = 0; i < curves.Count; i++)
        {
            OperationCurve curve = curves[i];
            ICamApiCurve apiCurve = curve.Curve;

            double nearestParam = apiCurve.FindNearestPoint(point, apiCurve.TMin, apiCurve.TMax);
            T3DPoint nearestPoint = apiCurve.Get_Point(nearestParam);
            double distance = T3DPoint.Distance(point, nearestPoint);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    public static OperationData ProcessFeaturesInLayers(OperationData operationData, int batchSize)
    {
        OperationData result = new OperationData();

        List<List<int>> inputGroups = operationData.Groups;

        int maxBatches = inputGroups.Max(group =>
            (int)Math.Ceiling((double)group.Count / batchSize));

        for (int batchNumber = 0; batchNumber < maxBatches; batchNumber++)
        {
            for (int regionIndex = 0; regionIndex < inputGroups.Count; regionIndex++)
            {
                List<int> currentRegion = inputGroups[regionIndex];
                int startIndex = batchNumber * batchSize;

                bool hasElementsInThisBatch = startIndex < currentRegion.Count;
                if (hasElementsInThisBatch)
                {
                    int elementsToTake = Math.Min(batchSize, currentRegion.Count - startIndex);

                    List<int> currentBatch = currentRegion.GetRange(startIndex, elementsToTake);

                    result.Groups.Add(currentBatch);
                    result.GroupInfo.Add((regionIndex, batchNumber));
                }
            }
        }

        return result;
    }  
}