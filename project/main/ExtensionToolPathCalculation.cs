using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CAMAPI.CurveTypes;
using CAMAPI.DotnetHelper;
using CAMAPI.EventHandler;
using CAMAPI.Extensions;
using CAMAPI.Machine;
using CAMAPI.MCDFormerTypes;
using CAMAPI.ModelFormerTypes;
using CAMAPI.ResultStatus;
using CAMAPI.TechOperation;
using CAMAPI.TechSolvers;
using Geometry.VecMatrLib;
using STCustomPropTypes;
using STTypes;

namespace DirectCladdingOperationExtension;

/// <summary>
/// Extension for exampling - how to calculate tool path
/// </summary>
public class ExtensionToolPathCalculation :
    IExtension,
    ICamApiTechOperationSolver
{
    /// <summary>
    /// Additional information about extension, provided in json file. It initializes in main CAM application
    /// </summary>
    public IExtensionInfo? Info { get; set; }
    
    private ICamApiEventHandler? _operationEventHandler;

    public void InitSolver(ICamApiTechOperationSolverInitializeContext context, out TResultStatus resultStatus)
    {
        resultStatus = default;
        try
        {
            using var operationCom = new ComWrapper<ICamApiTechOperation>(context.TechOperation);
            var operation = operationCom.Instance
                ?? throw new Exception("TechOperation container is not initialized");
            _operationEventHandler ??= new OperationEventHandler();
            operation.RegisterHandler("OperationSolverExtension", _operationEventHandler, new ListString(), out resultStatus);
        }
        catch (Exception e)
        {
            resultStatus.Code = TResultStatusCode.rsError;
            resultStatus.Description = e.Message;
        }
    }

    public void FinalizeSolver()
    {
        _operationEventHandler = null;
    }

    public bool GetPropIterator(string pageId, out IST_CustomPropIterator? iterator,
        out TResultStatus resultStatus)
    {
        resultStatus = default;
        iterator = null;
        return false;
    }

    public void OnPropFilterChanged(string parameterName, string value)
    {
        //
    }

    public void MakeWorkPath(ICamApiCLDReceiver? cldReceiver, ICamApiTechOperation? techOperation, out TResultStatus ret)
    {
        ret = default;
        try
        {
            if (techOperation == null || cldReceiver == null)
                return;
            using var cldFormerCom = new ComWrapper<ICamApiCLDReceiver>(cldReceiver);
            var cldFormer = cldFormerCom.Instance
                ?? throw new Exception("CLDReceiver container is not initialized");
            using var operationCom = new ComWrapper<ICamApiTechOperation>(techOperation);
            var operation = operationCom.Instance;
            if (operation == null)
                return;

            // Get all curves, we have to directCladding
            var curves = GetCurves(operation);
            if (curves.Group.Count == 0)
                return;

            PropsParams.ExtractAllParams(techOperation, out PropsParams propsParams);

            OperationData processedCurves;
            switch (propsParams.StrategyParams.PrintingStrategy)
            {
                case 0:
                    var optimizedSingleCurves = HelixOptimizer.GetOptimizedSingleCurves(curves, techOperation);

                    processedCurves = new OperationData
                    {
                        Groups =
                        [
                            optimizedSingleCurves.OrderIndex.ToList()
                        ]
                    };
                    break;
                case 1:
                default:
                    processedCurves = CurveOptimizer.GetOptimizedCurveGroups(curves, techOperation, propsParams.StrategyParams.SortBy);
                    break;
                
            }
            
            GeometryHelper.SetNextDistanceForCurves(processedCurves, curves, operation);
            ExecuteCladding(curves, processedCurves, cldFormer, operation, propsParams);

        }
        catch (Exception e)
        {
            ret.Code = TResultStatusCode.rsError;
            ret.Description = e.Message;
        }
    }
    
    private OperationGroup GetCurves(ICamApiTechOperation techOperation)
    {
        if (techOperation == null)
            throw new Exception("OperationSolver container is not initialized");

        
        var jobAssignment = techOperation.ModelFormerJobAssignment;
        var curves = new OperationGroup();
        try
        {
            for (var i = 0; i < jobAssignment.Count; i++)
            {
                var modelItem = jobAssignment.Item[i];
                if (modelItem is not ICamApiCurvesArrayModelItem curvesItem)
                    continue;

                var curveList = curvesItem.GetCurveList(techOperation.LCS);
                for (var j = 0; j < curveList.Count; j++)
                {
                    var abstractCurve = curveList.Curve[j];
                    if (abstractCurve is not ICamApiCurve curve)
                        continue;

                    if (curve.Box.IsEmpty != 0)
                        continue;
                    var opCurve = new OperationCurve(curve);
                    curves.Group.Add(opCurve);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(jobAssignment);
        }
        return curves;
    }

    private void ExecuteCladding(
        OperationGroup curves,
        OperationData curveGroups,
        ICamApiCLDReceiver cldFormer,
        ICamApiTechOperation techOperation,
        PropsParams propsParams)
    {
        var props = techOperation.XMLProp;
        try
        {
            var curveConverter = new CurveConverter();
            curveConverter.TargetReceiver = cldFormer;


            var boundingBox = GeometryHelper.CalculateBoundingBox(curveGroups, curves);
            var boundingBoxMaxZ = boundingBox.Max.Z;
            var toolDiameter = GeometryHelper.GetToolDiameter(techOperation);

            var strategyName = (propsParams.StrategyParams.PrintingStrategy == 0)
                    ? $"Helical"
                    : (propsParams.StrategyParams.SortBy == 0)
                        ? $"Features"
                        : $"Layers";

            cldFormer.BeginItem(TCLDItemType.aitGroup, "Strategy", strategyName);

            if (propsParams.StrategyParams.PrintingStrategy == 1
                    && propsParams.StrategyParams.SortBy == 0
                    && propsParams.StrategyParams.SplitByLayerEnabled
                    && propsParams.StrategyParams.SplitByLayerCount > 1)
            {
                ProcessFeatureLayerStructure(curves, curveGroups, curveConverter,
                    cldFormer, techOperation,
                    propsParams,
                    boundingBoxMaxZ, toolDiameter);
            }
            else
            {
                ProcessNormalGroups(curves, curveGroups, curveConverter,
                    cldFormer, techOperation,
                    propsParams,
                    boundingBoxMaxZ, toolDiameter);
            }
            
            cldFormer.EndItem();
        }
        finally
        {
            Marshal.ReleaseComObject(props);
        }
    }

    
    private void ProcessFeatureLayerStructure(
        OperationGroup curves,
        OperationData curveGroups,
        CurveConverter curveConverter,
        ICamApiCLDReceiver cldFormer,
        ICamApiTechOperation techOperation,
        PropsParams propsParams,
        double boundingBoxMaxZ,
        double toolDiameter)
    {
        OperationData optimizedCurveGroups = FeatureInLayerOptimizer.OptimizeFeatureLayersOrder(curveGroups, curves, techOperation);

        LayerGroupsDictionary layersDict = [];
        
        for (int index = 0; index < optimizedCurveGroups.GroupInfo.Count; index++)
        {
            var (FeatureIdx, LayerIdx) = optimizedCurveGroups.GroupInfo[index];
            CurveGroupInfo groupInfo = new(FeatureIdx, LayerIdx, index);
            layersDict.AddGroup(groupInfo);
        }

        CurveOptimizer.UpdateStartPoints(optimizedCurveGroups, curves, techOperation);
        foreach (var layerKey in layersDict)
        {
            cldFormer.BeginItem(TCLDItemType.aitGroup, "Layer", $"Layer {layerKey.Key + 1}");

            List<CurveGroupInfo> layerItems = layersDict[layerKey.Key];
            var featuresDict = new Dictionary<int, List<CurveGroupInfo>>();

            foreach (var item in layerItems)
            {
                if (!featuresDict.TryGetValue(item.FeatureIdx, out List<CurveGroupInfo>? value))
                {
                    value = [];
                    featuresDict[item.FeatureIdx] = value;
                }

                value.Add(item);
            }


            foreach (var featureKey in featuresDict.Keys)
            {
                cldFormer.BeginItem(TCLDItemType.aitGroup, "Feature", $"Feature {featureKey + 1}");

                var featureItems = featuresDict[featureKey];
                foreach (var item in featureItems)
                {
                    ProcessGroup(curves, optimizedCurveGroups, curveConverter,
                        item.CurveIdx,
                        cldFormer, techOperation,
                        propsParams,
                        boundingBoxMaxZ, toolDiameter, isBatch: true);
                }
                cldFormer.EndItem(); // Feature
            }
            cldFormer.EndItem(); // Layer
        }
    }
    
    
    
    private void ProcessNormalGroups(
        OperationGroup curves,
        OperationData curveGroups,
        CurveConverter curveConverter,
        ICamApiCLDReceiver cldFormer,
        ICamApiTechOperation techOperation,
        PropsParams propsParams,
        double boundingBoxMaxZ,
        double toolDiameter)
    {
        for (int groupIdx = 0; groupIdx < curveGroups.Groups.Count; groupIdx++)
        {
            string groupName = propsParams.StrategyParams.PrintingStrategy == 0
                    ? "Direct Cladding"
                    : propsParams.StrategyParams.SortBy == 0
                            ? $"Feature {groupIdx + 1}"
                            : $"Layer {groupIdx + 1}";

            cldFormer.BeginItem(TCLDItemType.aitGroup, "Group", groupName);

            ProcessGroup(curves, curveGroups, curveConverter,
                    groupIdx,
                    cldFormer, techOperation,
                    propsParams,
                    boundingBoxMaxZ, toolDiameter, isBatch: false);

            cldFormer.EndItem();
        }
    }

    private void ProcessGroup(
        OperationGroup curves,
        OperationData curveGroups,
        CurveConverter curveConverter,
        int groupIdx,
        ICamApiCLDReceiver cldFormer,
        ICamApiTechOperation techOperation,
        PropsParams propsParams,
        double boundingBoxMaxZ,
        double toolDiameter,
        bool isBatch)
    {
        var currentGroup = curveGroups.Groups[groupIdx];

        var strategyParams = propsParams.StrategyParams;
        var safeLevelParams = propsParams.SafeLevelParams;
        var linksParams = propsParams.LinksParams;
        var startPointChangeParams = propsParams.StartPointChangeParams;

        for (int curveIdx = 0; curveIdx < currentGroup.Count; curveIdx++)
        {
            int curveIndex = currentGroup[curveIdx];
            var currentOpCurve = curves.GetOrderedItem(curveIndex);
            var currentCurve = currentOpCurve.Curve;

            if (strategyParams.AllowReverse && currentCurve.IsClosed)
                currentCurve.Inverse();

            var firstCurvePoint = currentCurve.KnotPoint[0];
            var lastCurvePoint = currentCurve.KnotPoint[currentCurve.QntP];
            double nearestParameter = 0.0;

            if (strategyParams.PrintingStrategy == 1
                    && strategyParams.AllowChangeStartPoint
                    && currentCurve.IsClosed)
            {
                firstCurvePoint = currentOpCurve.StartPoint;
                lastCurvePoint = currentOpCurve.EndPoint;
                var tempParameter = currentCurve.FindNearestPoint(currentOpCurve.StartPoint, currentCurve.TMin, currentCurve.TMax);
                nearestParameter = tempParameter;
                //nearestParameter = currentOpCurve.NearestParameter; 
            }

            double safeLevel = boundingBoxMaxZ + safeLevelParams.SafeLevelRelValue;
            if (safeLevelParams.SafeLevelRefType == 0)
                safeLevel = safeLevelParams.SafeLevelAbsValue;

            double feedFirstLevel = GeometryHelper.CalculateFeedLevel(firstCurvePoint, safeLevelParams, toolDiameter);
            double feedLastLevel = GeometryHelper.CalculateFeedLevel(lastCurvePoint, safeLevelParams, toolDiameter);
            double RapidDistFirstLevel = firstCurvePoint.Z + safeLevelParams.RapidDistance;
            double RapidDistLastLevel = lastCurvePoint.Z + safeLevelParams.RapidDistance;

            string itemName = isBatch
                    ? $"Curve {currentGroup[curveIdx]}"
                    : strategyParams.PrintingStrategy == 0
                            ? $"Helical {curveIdx + 1}"
                            : $"Curve {currentGroup[curveIdx]}";

            cldFormer.BeginItem(TCLDItemType.aitGroup, "Direct", itemName);

            ElementType elType = MovementTypeHelper.DetermineElementType(
                            groupIdx, curveIdx,
                            curveGroups
                        );

            OperationCurve prevOpCurve = currentOpCurve;
            if (elType != ElementType.FirstElementInData)
            {
                if (curveIdx == 0)
                {
                    var prevGroup = curveGroups.Groups[groupIdx - 1];
                    int lastCurveIndex = prevGroup.Count - 1;
                    int prevOpIndex = prevGroup[lastCurveIndex];
                    prevOpCurve = curves.GetOrderedItem(prevOpIndex);
                }
                else
                {
                    int prevCurveIndex = curveIdx - 1;
                    int prevOpIndex = currentGroup[prevCurveIndex];
                    prevOpCurve = curves.GetOrderedItem(prevOpIndex);
                }

            }
            MovementType mFirsttype = MovementTypeHelper.DetermineFirstMovementType(
                elType,
                prevOpCurve,
                linksParams,
                techOperation);

            switch (mFirsttype)
            {
                case MovementType.StraightBetweenCurves:
                    cldFormer.OutEffector(1, true);
                    cldFormer.OutFeed((int)TFeedTypeFlag.affWorking, 200.0, false);
                    cldFormer.CutTo(firstCurvePoint);
                    break;
                case MovementType.SafeBetweenCurves:
                    var safeFirstPoint = firstCurvePoint;
                    safeFirstPoint.Z = safeLevel < feedFirstLevel
                                        ? feedFirstLevel
                                        : safeLevel;
                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affRapid);
                    cldFormer.CutTo(safeFirstPoint);

                    var feedFirstPoint = firstCurvePoint;
                    feedFirstPoint.Z = feedFirstLevel;
                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affRapid);
                    cldFormer.CutTo(feedFirstPoint);

                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affPlunge);
                    cldFormer.CutTo(firstCurvePoint);

                    cldFormer.OutEffector(1, true);
                    cldFormer.OutFeed((int)TFeedTypeFlag.affWorking, 200.0, false);
                    cldFormer.CutTo(firstCurvePoint);

                    break;
                case MovementType.ShortBetweenCurves:
                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affNext);
                    cldFormer.CutTo(firstCurvePoint);

                    cldFormer.OutEffector(1, true);
                    cldFormer.OutFeed((int)TFeedTypeFlag.affWorking, 200.0, false);
                    cldFormer.CutTo(firstCurvePoint);

                    break;
                case MovementType.LongBetweenCurves:
                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affLongNext);
                    cldFormer.CutTo(firstCurvePoint);

                    cldFormer.OutEffector(1, true);
                    cldFormer.OutFeed((int)TFeedTypeFlag.affWorking, 200.0, false);
                    cldFormer.CutTo(firstCurvePoint);

                    break;
                case MovementType.RapidDistBetweenCurves:
                    var RapidFirstPoint = firstCurvePoint;
                    RapidFirstPoint.Z = RapidDistFirstLevel < feedFirstLevel
                                        ? feedFirstLevel
                                        : RapidDistFirstLevel;
                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affRapid);
                    cldFormer.CutTo(RapidFirstPoint);

                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affPlunge);
                    cldFormer.CutTo(firstCurvePoint);

                    cldFormer.OutEffector(1, true);
                    cldFormer.OutFeed((int)TFeedTypeFlag.affWorking, 200.0, false);
                    cldFormer.CutTo(firstCurvePoint);

                    break;
            }

            if (strategyParams.AllowChangeStartPoint && currentCurve.IsClosed)
            {
                currentCurve.SavePartToReceiver(curveConverter, nearestParameter, currentCurve.TMax, 0);
                currentCurve.SavePartToReceiver(curveConverter, currentCurve.TMin, nearestParameter, 0);
            }
            else
                currentCurve.SavePartToReceiver(curveConverter, currentCurve.TMin, currentCurve.TMax, 0);

            MovementType mLastType = MovementTypeHelper.DetermineLastMovementType(
                elType,
                currentOpCurve,
                linksParams,
                techOperation);

            switch (mLastType)
            {
                case MovementType.StraightBetweenCurves:
                    break;
                case MovementType.SafeBetweenCurves:
                    var feedLastPoint = lastCurvePoint;
                    feedLastPoint.Z = feedLastLevel;
                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affReturn);
                    cldFormer.CutTo(feedLastPoint);

                    var safeLastPoint = lastCurvePoint;
                    safeLastPoint.Z = safeLevel < feedLastLevel
                                    ? feedLastLevel
                                    : safeLevel;

                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affRapid);
                    cldFormer.CutTo(safeLastPoint);
                    break;
                case MovementType.ShortBetweenCurves:
                    break;
                case MovementType.LongBetweenCurves:
                    break;
                case MovementType.RapidDistBetweenCurves:
                    var safeRapidLastPoint = lastCurvePoint;
                    safeRapidLastPoint.Z = RapidDistLastLevel < feedLastLevel
                                    ? feedLastLevel
                                    : RapidDistLastLevel;

                    cldFormer.OutStandardFeed((int)TFeedTypeFlag.affRapid);
                    cldFormer.CutTo(safeRapidLastPoint);
                    break;
            }

            cldFormer.EndItem();
        }
    }
}