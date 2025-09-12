using Geometry.VecMatrLib;


namespace DirectCladdingOperationExtension;

public struct CurveUniter
{
    public static OperationData GroupCurvesByCenter(OperationGroup operationGroup)
    {
        const double tolerance = 2; 
        var groups = new OperationData();

        for (int i = 0; i < operationGroup.Group.Count; i++)
        {
            var operationCurve = operationGroup.Group[i];
            var center = GetCurveCenter(operationCurve);
            bool addedToExistingGroup = false;

            for (int j = 0; j < groups.Groups.Count; j++)
            {
                List<int> currentGroupIndices = groups.Groups[j];
                var groupCenter = GetGroupCenter(currentGroupIndices, operationGroup);
                if (ArePointsOnSameLine(groupCenter, center, tolerance))
                {
                    groups.Groups[j].Add(i);
                    addedToExistingGroup = true;
                    break;
                }
            }

            if (!addedToExistingGroup)
            {
                groups.Groups.Add(new List<int> { i }); 
            }
        }

        return groups;
    }

    public static OperationData GroupCurvesByZLayer(OperationGroup operationGroup, double zTolerance = 0.3)
    {
        var groups = new OperationData();
        var zLayers = new Dictionary<double, List<int>>();

        for (int i = 0; i < operationGroup.Group.Count; i++)
        {
            var operationCurve = operationGroup.Group[i];
            var center = GetCurveCenter(operationCurve);
            double z = center.Z;

            
            bool addedToExistingLayer = false;
            foreach (var layerZ in zLayers.Keys)
            {
                if (Math.Abs(layerZ - z) <= zTolerance)
                {
                    zLayers[layerZ].Add(i);
                    addedToExistingLayer = true;
                    break;
                }
            }

            if (!addedToExistingLayer)
            {
                zLayers[z] = new List<int> { i };
            }
        }

        var sortedLayers = zLayers.OrderBy(kv => kv.Key);
    
        foreach (var layer in sortedLayers)
        {
            groups.Groups.Add(layer.Value);
        }

        return groups;
    }
    
    public static T3DPoint GetCurveCenter(OperationCurve operationCurve)
    {
        var curve = operationCurve.Curve;
        return 0.5 * ((T3DPoint)curve.Box.Min + curve.Box.Max);
    }

    private static T3DPoint GetGroupCenter(List<int> groupIndices, OperationGroup operationGroup)
    {
        if (groupIndices.Count == 0)
            return new T3DPoint(0, 0, 0);

        T3DPoint sum = new T3DPoint(0, 0, 0);
        
        foreach (int curveIndex in groupIndices)
        {
            OperationCurve curve = operationGroup.GetOrderedItem(curveIndex);
            sum += GetCurveCenter(curve);
        }
        
        return sum / groupIndices.Count;
    }

    private static bool ArePointsOnSameLine(T3DPoint a, T3DPoint b, double tolerance)
    {
        return Math.Abs(a.X - b.X) < tolerance && 
                Math.Abs(a.Y - b.Y) < tolerance;
    }
}