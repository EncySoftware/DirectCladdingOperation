using System.Runtime.InteropServices;
using CAMAPI.Machine;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension;

public struct GeometryHelper
{
    public static T3DPoint GetMachineStartPoint(ICamApiTechOperation techOperation)
    {
        ICamApiMachine? machine = null;
        ICamApiMachineEvaluator? machineEvaluator = null;
        try
        {
            machine = techOperation.Machine;
            machineEvaluator = machine.CreateEvaluator();
            techOperation.InitMachineEvaluator(machineEvaluator, out var resultStatus);
            var machineLCS = machineEvaluator.GetAbsoluteMatrix();
            return new T3DPoint(machineLCS.vT.X, machineLCS.vT.Y, machineLCS.vT.Z);
        }
        finally
        {
            if (machineEvaluator != null)
                Marshal.ReleaseComObject(machineEvaluator);
            if (machine != null)
                Marshal.ReleaseComObject(machine);
        }
    }

    public GeometryHelper() { }

    public static T3DBox CalculateBoundingBox(OperationData operationData, OperationGroup sourceGroup)
    {
        if (operationData.Groups == null || operationData.Groups.Count == 0)
            return new T3DBox(T3DPoint.Zero, T3DPoint.Zero);

        var firstGroup = operationData.Groups[0];
        if (firstGroup.Count == 0)
            return new T3DBox(T3DPoint.Zero, T3DPoint.Zero);

        var curvesBoundingBox = new T3DBox(T3DPoint.Zero, T3DPoint.Zero);

        foreach (var group in operationData.Groups)
        {
            foreach (var curveIndex in group)
            {
                var operationCurve = sourceGroup.GetOrderedItem(curveIndex);
                var currentCurve = operationCurve.Curve;
                var curveBox = currentCurve.Box;
                curvesBoundingBox = curvesBoundingBox.AddBox(curveBox);
            }
        }

        return curvesBoundingBox;
    }

    public static double GetToolDiameter(ICamApiTechOperation techOperation)
    {
        // if (techOperation.Tool is ICamApiMillTool mt)
        // {
        //     return mt.Diameter;
        //     Marshal.ReleaseComObject(mt);
        // }
        return 1; //techOperation.XMLProp.Flt["TechOperation.TechTool.Diameter"];
    }

    public static double CalculateFeedLevel(T3DPoint point, SafeLevelParams safeLevelParams, double toolDiameter)
    {
        double feedLevel = point.Z + safeLevelParams.FeedSwitchRelValue;
        if (safeLevelParams.FeedSwitchRefType == 0)
            feedLevel = safeLevelParams.FeedSwitchAbsValue;
        if (safeLevelParams.FeedSwitchRefType == 3)
            feedLevel = point.Z +
                        0.01 * toolDiameter * safeLevelParams.FeedSwitchPercentValue;

        return feedLevel;
    }

    public static double CalculateLinkValue(LinksParams linksParams, ICamApiTechOperation techOperation)
    {
        double toolDiameter = GetToolDiameter(techOperation);
        double linkValue = 0.0;

        if (linksParams.LinksPlaceHolderType == 0)
            linkValue = linksParams.LinksPlaceHolderDistValue;
        else if (linksParams.LinksPlaceHolderType == 1)
            linkValue = 0.01 * toolDiameter * linksParams.LinksPlaceHolderPercValue;

        return linkValue;
    }
    
    public static void SetNextDistanceForCurves(OperationData curveGroups, OperationGroup curves, ICamApiTechOperation techOperation)
    {
        for (int groupIdx = 0; groupIdx < curveGroups.Groups.Count; groupIdx++)
        {
            var currentGroup = curveGroups.Groups[groupIdx];
            bool isLastGroup = groupIdx == curveGroups.Groups.Count - 1;

            for (int curveIdx = 0; curveIdx < currentGroup.Count; curveIdx++)
            {
                int opindex = currentGroup[curveIdx];
                var currentOpCurve = curves.GetOrderedItem(opindex);

                if (curveIdx < currentGroup.Count - 1)
                {
                    int nextOpIndex = currentGroup[curveIdx + 1];
                    OperationCurve nextOpCurve = curves.GetOrderedItem(nextOpIndex);
                    double nextDistance = T3DPoint.Distance(currentOpCurve.EndPoint, nextOpCurve.StartPoint);
                    currentOpCurve.NextDistance = nextDistance;
                }
                else
                {
                    if (isLastGroup)
                    {
                        T3DPoint toolEndPoint = GetMachineStartPoint(techOperation);
                        currentOpCurve.NextDistance = T3DPoint.Distance(currentOpCurve.EndPoint, toolEndPoint);
                    }
                    else
                    {
                        int nextGroupIdx = groupIdx + 1;
                        var nextGroup = curveGroups.Groups[nextGroupIdx];
                        int firstNextIndex = nextGroup[0];
                        OperationCurve firstNextCurve = curves.GetOrderedItem(firstNextIndex);
                        double nextDistance = T3DPoint.Distance(currentOpCurve.EndPoint, firstNextCurve.StartPoint);
                        currentOpCurve.NextDistance = nextDistance;
                    }
                }
            }
        }
    }
}