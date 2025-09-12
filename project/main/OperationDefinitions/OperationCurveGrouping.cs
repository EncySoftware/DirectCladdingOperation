using CAMAPI.CurveTypes;
using CAMAPI.TechOperation;
using Geometry.VecMatrLib;

namespace DirectCladdingOperationExtension
{
    public struct CurveGroupInfo
    {
        public int FeatureIdx;
        public int LayerIdx;
        public int CurveIdx;
        
        public CurveGroupInfo(int featureIdx, int layerIdx, int curveIdx)
        {
            FeatureIdx = featureIdx;
            LayerIdx = layerIdx;
            CurveIdx = curveIdx;
        }
    }

    public class LayerGroupsDictionary : Dictionary<int, List<CurveGroupInfo>>
    {
        public void AddGroup(CurveGroupInfo groupInfo)
        {
            if (!ContainsKey(groupInfo.LayerIdx))
            {
                this[groupInfo.LayerIdx] = new List<CurveGroupInfo>();
            }
            this[groupInfo.LayerIdx].Add(groupInfo);
        }
        
        public List<int> GetSortedLayerKeys()
        {
            var keys = new List<int>(Keys);
            keys.Sort();
            return keys;
        }
    }

}