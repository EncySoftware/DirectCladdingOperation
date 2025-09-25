using CAMAPI.TechOperation;
using Geometry.VecMatrLib;
using static Geometry.VecMatrLib.VML;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DirectCladdingOperationExtension
{
    public struct StrategyParams
    {
        public int PrintingStrategy { get; set; }
        public bool AllowReverse { get; set; }
        public int SortBy { get; set; }
        //public bool AllowChangeStartPoint { get; set; }
        public bool SplitByLayerEnabled { get; set; }
        public int SplitByLayerCount { get; set; }
    }

    public struct SafeLevelParams
    {
        public int SafeLevelRefType { get; set; }
        public double SafeLevelAbsValue { get; set; }
        public double SafeLevelRelValue { get; set; }
        public int FeedSwitchRefType { get; set; }
        public double FeedSwitchAbsValue { get; set; }
        public double FeedSwitchRelValue { get; set; }
        public double FeedSwitchPercentValue { get; set; }
        public double RapidDistance { get; set; }
    }

    public struct LinksParams
    {
        public int LinksPlaceHolderType { get; set; }
        public double LinksPlaceHolderDistValue { get; set; }
        public double LinksPlaceHolderPercValue { get; set; }
        public int ShortLinkType { get; set; }
        public int LongLinkType { get; set; }
        public int FirstLinkType { get; set; }
        public int LastLinkType { get; set; }
    }

    public enum StartPointChangeType
    {
        Default = 0,
        Automatic = 1,
        Manual = 2

    }
    public struct StartPointChangeParams
    {
        public StartPointChangeType StartPointChangeType { get; set; }
        public T3DPoint ManualStartPoint{ get; set; }
    }

    public class PropsParams
    {
        public StrategyParams StrategyParams { get; set; }
        public SafeLevelParams SafeLevelParams { get; set; }
        public LinksParams LinksParams { get; set; }
        public StartPointChangeParams StartPointChangeParams { get; set; }

        public static void ExtractAllParams(ICamApiTechOperation techOperation,
            out PropsParams propsParams)
        {
            ExtractStrategyParams(techOperation, out StrategyParams strategyParams);
            ExtractSafeLevelParams(techOperation, out SafeLevelParams safeLevelParams);
            ExtractLinkParams(techOperation, out LinksParams linksParams);
            ExtractChangeStartPointParams(techOperation, out StartPointChangeParams startPointChangeParams);

            propsParams = new PropsParams
            {
                StrategyParams = strategyParams,
                SafeLevelParams = safeLevelParams,
                LinksParams = linksParams,
                StartPointChangeParams = startPointChangeParams
            };
        }

        public static void ExtractStrategyParams(ICamApiTechOperation techOperation, out StrategyParams strategyParams)
        {
            var props = techOperation.XMLProp;
            var sortingProps = props.Ptr["Sort"];
            var SplitByLayer = sortingProps.Ptr["SplitByLayer"];
            try
            {
                strategyParams = new StrategyParams
                {
                    PrintingStrategy = props.Int["PrintingStrategy"],
                    AllowReverse = sortingProps.Bol["AllowReverse"],
                    SortBy = sortingProps.Int["SortBy"],
                    // AllowChangeStartPoint = sortingProps.Bol["AllowChangeStartPoint"],
                    SplitByLayerEnabled = SplitByLayer.Bol["Enabled"],
                    SplitByLayerCount = SplitByLayer.Int["Count"]
                };
            }
            finally
            {
                Marshal.ReleaseComObject(props);
            }
        }

        public static void ExtractLinkParams(ICamApiTechOperation techOperation, out LinksParams linksParams)
        {
            var props = techOperation.XMLProp;
            var LinksPlaceHolder = props.Ptr["LinksPlaceHolder"];
            try
            {
                linksParams = new LinksParams
                {
                    LinksPlaceHolderType = LinksPlaceHolder.Int["ShortLink.ValueType"],
                    LinksPlaceHolderDistValue = LinksPlaceHolder.Flt["ShortLink.DistanceValue"],
                    LinksPlaceHolderPercValue = LinksPlaceHolder.Flt["ShortLink.PercentValue"],
                    ShortLinkType = props.Int["LinksPlaceHolder.ShortLinkType"],
                    LongLinkType = props.Int["LinksPlaceHolder.LongLinkType"],
                    FirstLinkType = props.Int["LinksPlaceHolder.FirstLinkType"],
                    LastLinkType = props.Int["LinksPlaceHolder.LastLinkType"]
                };
            }
            finally
            {
                Marshal.ReleaseComObject(props);
            }
        }

        public static void ExtractSafeLevelParams(ICamApiTechOperation techOperation, out SafeLevelParams safeLevelParams)
        {
            var props = techOperation.XMLProp;
            try
            {
                safeLevelParams = new SafeLevelParams
                {
                    SafeLevelRefType = props.Int["SafeLevel.ReferenceType"],
                    SafeLevelAbsValue = props.Flt["SafeLevel.AbsValue"],
                    SafeLevelRelValue = props.Flt["SafeLevel.RelValue"],
                    FeedSwitchRefType = props.Int["FeedSwitchLevel.ReferenceType"],
                    FeedSwitchAbsValue = props.Flt["FeedSwitchLevel.AbsValue"],
                    FeedSwitchRelValue = props.Flt["FeedSwitchLevel.RelValue"],
                    FeedSwitchPercentValue = props.Flt["FeedSwitchLevel.PercentValue"],
                    RapidDistance = props.Flt["RapidDistance"]
                };
            }
            finally
            {
                Marshal.ReleaseComObject(props);
            }
        }

        public static void ExtractChangeStartPointParams(ICamApiTechOperation techOperation, out StartPointChangeParams startPointChangeParams)
        {
            var props = techOperation.XMLProp;
            var sortProps = props.Ptr["Sort"];
            try
            {
                var lcs = (T3DMatrix)(techOperation.LCS);
                startPointChangeParams = new StartPointChangeParams
                {
                    StartPointChangeType = (StartPointChangeType)sortProps.Int["StartPoint.Mode"],
                    ManualStartPoint = 
                        lcs.GetLocalPoint(
                            p3d(sortProps.Flt["StartPoint.Point.X"],
                            sortProps.Flt["StartPoint.Point.Y"],
                            sortProps.Flt["StartPoint.Point.Z"])
                        )                    
                };
            }
            finally
            {
                Marshal.ReleaseComObject(props);
            }
        }
    }
}