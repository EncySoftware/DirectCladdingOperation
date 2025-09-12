using CAMAPI.TechOperation;

namespace DirectCladdingOperationExtension
{
    public struct StrategyParams
    {
        public int PrintingStrategy { get; set; }
        public bool AllowReverse { get; set; }
        public int SortBy { get; set; }
        public bool AllowChangeStartPoint { get; set; }
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

    public class PropsParams
    {
        public static void ExtractStrategyParams(ICamApiTechOperation techOperation, out StrategyParams strategyParams)
        {
            var props = techOperation.XMLProp;
            var sortingProps = props.Ptr["Sort"];
            var SplitByLayer = sortingProps.Ptr["SplitByLayer"];

            strategyParams = new StrategyParams
            {
                PrintingStrategy = props.Int["PrintingStrategy"],
                AllowReverse = sortingProps.Bol["AllowReverse"],
                SortBy = sortingProps.Int["SortBy"],
                AllowChangeStartPoint = sortingProps.Bol["AllowChangeStartPoint"],
                SplitByLayerEnabled = SplitByLayer.Bol["Enabled"],
                SplitByLayerCount = SplitByLayer.Int["Count"]
            };
        }

        public static void ExtractLinkParams(ICamApiTechOperation techOperation, out LinksParams linksParams)
        {
            var props = techOperation.XMLProp;
            var LinksPlaceHolder = props.Ptr["LinksPlaceHolder"];

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

        public static void ExtractSafeLevelParams(ICamApiTechOperation techOperation, out SafeLevelParams safeLevelParams)
        {
            var props = techOperation.XMLProp;
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
    }
}