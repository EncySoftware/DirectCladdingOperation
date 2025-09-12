using CAMAPI.TechOperation;

namespace DirectCladdingOperationExtension
{
    public enum ElementType
    {
        FirstElementInData,
        LastElementInData,
        LastElementInGroup,
        UsualElement
    }

    public enum MovementType
    {
        StraightBetweenCurves,
        SafeBetweenCurves,
        ShortBetweenCurves,
        LongBetweenCurves,
        RapidDistBetweenCurves
    }

    public class MovementTypeHelper
    {
        public static ElementType DetermineElementType(
            int groupIndex, int curveIndex,
            OperationData opData)
        {
            if (groupIndex == 0 && curveIndex == 0)
                return ElementType.FirstElementInData;

            List<List<int>> curveOrder = opData.Groups;
            if (groupIndex == curveOrder.Count - 1 && curveIndex == curveOrder[groupIndex].Count - 1)
                return ElementType.LastElementInData;

            if (curveIndex == curveOrder[groupIndex].Count - 1)
                return ElementType.LastElementInGroup;

            return ElementType.UsualElement;
        }

        public static MovementType DetermineFirstMovementType(
            ElementType elType,
            OperationCurve opCurve,
            LinksParams linksParams,
            ICamApiTechOperation techOperation)
        {
            double LinkValue = GeometryHelper.CalculateLinkValue(linksParams, techOperation);

            if (elType == ElementType.FirstElementInData)
            {
                return linksParams.FirstLinkType switch
                {
                    0 => MovementType.StraightBetweenCurves,
                    1 => MovementType.SafeBetweenCurves,
                    2 => MovementType.RapidDistBetweenCurves,
                    _ => MovementType.SafeBetweenCurves
                };
            }

            int linkType = opCurve.NextDistance < LinkValue
                ? linksParams.ShortLinkType
                : linksParams.LongLinkType;

            return linkType switch
            {
                0 => opCurve.NextDistance < LinkValue
                    ? MovementType.ShortBetweenCurves
                    : MovementType.LongBetweenCurves,
                2 => MovementType.RapidDistBetweenCurves,
                _ => MovementType.SafeBetweenCurves
            };
        }
        
        public static MovementType DetermineLastMovementType(
            ElementType elType,
            OperationCurve opCurve,
            LinksParams linksParams,
            ICamApiTechOperation techOperation)
        {
            double LinkValue = GeometryHelper.CalculateLinkValue(linksParams, techOperation);

            if (elType == ElementType.LastElementInData)
            {
                return linksParams.LastLinkType switch
                { 
                    0 => MovementType.StraightBetweenCurves,
                    1 => MovementType.SafeBetweenCurves,
                    2 => MovementType.RapidDistBetweenCurves,
                    _ => MovementType.SafeBetweenCurves
                };
            }

            bool isShortDistance = opCurve.NextDistance < LinkValue;
            int linkType = isShortDistance
                ? linksParams.ShortLinkType 
                : linksParams.LongLinkType;

            return (linkType, isShortDistance) switch
            {
                (0, true)  => MovementType.ShortBetweenCurves,
                (0, false) => MovementType.LongBetweenCurves,
                (2, _)     => MovementType.RapidDistBetweenCurves,
                _          => MovementType.SafeBetweenCurves
            };
        }
    }
}