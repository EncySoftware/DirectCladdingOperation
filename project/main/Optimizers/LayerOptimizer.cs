using CAMAPI.CurveTypes;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public struct LayerOptimizer
{
    private static int[]? firstLayerTemplate = null;

    public static OperationData OptimizeLayersOrder(
        OperationData curvesGroupedBy, OperationGroup allCurves,
        ICamApiTechOperation techOperation, PropsParams propsParams)
    {
        if (curvesGroupedBy.Groups.Count < 1 || !techOperation.XMLProp.Ptr["Sort"].Bol["OptimizeOrder"])
            return curvesGroupedBy;

        OperationData optimizedData = new OperationData();

        for (int layerIndex = 0; layerIndex < curvesGroupedBy.Groups.Count; layerIndex++)
        {
            var groupIndices = curvesGroupedBy.Groups[layerIndex];

            if (groupIndices.Count <= 1)
            {
                optimizedData.Groups.Add([.. groupIndices]);
                continue;
            }

            List<int> optimizedIndices;

            if (layerIndex == 0)
            {
                List<OperationCurve> groupCurves = [];
                foreach (int index in groupIndices)
                {
                    groupCurves.Add(allCurves.GetOrderedItem(index));
                }

                int[] optimizedOrder = CurveOptimizer.OptimizeCurveOrder(groupCurves, techOperation, propsParams);

                firstLayerTemplate = optimizedOrder;

                optimizedIndices = optimizedOrder.Select(orderIndex => groupIndices[orderIndex]).ToList();
            }
            else
            {
                optimizedIndices = firstLayerTemplate != null
                    ? ApplyTemplateOrder(groupIndices, firstLayerTemplate)
                    : [.. groupIndices];
            }
            optimizedData.Groups.Add(optimizedIndices);
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