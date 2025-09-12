using CAMAPI.CurveTypes;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public struct FeatureInLayerOptimizer
{ 
    private static int[]? firstLayerTemplate = null;
    public static OperationData OptimizeFeatureLayersOrder(
        OperationData curvesGroupedBy,
        OperationGroup allCurves,
        ICamApiTechOperation techOperation)
    {
        if (curvesGroupedBy.Groups.Count < 1 || !techOperation.XMLProp.Ptr["Sort"].Bol["OptimizeOrder"])
            return curvesGroupedBy;

        OperationData optimizedData = new OperationData();

        var layersDict = new Dictionary<int, List<int>>();

        for (int groupIndex = 0; groupIndex < curvesGroupedBy.GroupInfo.Count; groupIndex++)
        {
            var (featureIdx, layerIdx) = curvesGroupedBy.GroupInfo[groupIndex];

            if (!layersDict.TryGetValue(layerIdx, out var layerGroups))
            {
                layerGroups = new List<int>();
                layersDict[layerIdx] = layerGroups;
            }
            layerGroups.Add(groupIndex);
        }

        foreach (var layerEntry in layersDict.OrderBy(x => x.Key))
        {
            int layerIndex = layerEntry.Key;
            var groupIndices = layerEntry.Value;

            if (groupIndices.Count <= 1)
            {
                optimizedData.Groups.Add([.. curvesGroupedBy.Groups[groupIndices[0]]]);
                optimizedData.GroupInfo.Add(curvesGroupedBy.GroupInfo[groupIndices[0]]);
                continue;
            }

            List<int> optimizedGroupIndices;

            if (layerIndex == 0)
            {
                List<OperationCurve> groupCurves = [];
                foreach (int groupIndex in groupIndices)
                {
                    int curveIndex = curvesGroupedBy.Groups[groupIndex][0];
                    groupCurves.Add(allCurves.GetOrderedItem(curveIndex));
                }

                int[] optimizedOrder = CurveOptimizer.OptimizeCurveOrder(groupCurves, techOperation);
                firstLayerTemplate = optimizedOrder;

                optimizedGroupIndices = optimizedOrder.Select(orderIndex => groupIndices[orderIndex]).ToList();
            }
            else
            {
                optimizedGroupIndices = firstLayerTemplate != null
                    ? ApplyTemplateOrder(groupIndices, firstLayerTemplate)
                    : [.. groupIndices];
            }


            foreach (int groupIndex in optimizedGroupIndices)
            {
                optimizedData.Groups.Add([.. curvesGroupedBy.Groups[groupIndex]]);
                optimizedData.GroupInfo.Add(curvesGroupedBy.GroupInfo[groupIndex]);
            }
        }

        return optimizedData;
    }

    private static List<int> ApplyTemplateOrder(List<int> currentIndices, int[] templateOrder)
    {
        if (currentIndices.Count != templateOrder.Length)
            return currentIndices;
        
        return templateOrder.Select(index => currentIndices[index]).ToList();
    }
}