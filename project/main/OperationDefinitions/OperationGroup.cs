namespace DirectCladdingOperationExtension;

public class OperationGroup
{
    public List<OperationCurve> Group = [];
    public int[] OrderIndex = [];
    public void InitOrder()
    {
        if (Group.Count > 0)
        {
            OrderIndex = new int[Group.Count];
            for (int i = 0; i < Group.Count; i++)
                OrderIndex[i] = i;
        }
        else
            OrderIndex = [];
    }

    public OperationCurve GetOrderedItem(int index)
    {
        if ((index >= 0) && (index < OrderIndex.Length))
            return Group[OrderIndex[index]];
        else
            throw new IndexOutOfRangeException($"Index {index} is out of range");
    }

    public void SetOrderedItem(int index, OperationCurve value)
    {
        if ((index >= 0) && (index < OrderIndex.Length))
            Group[OrderIndex[index]] = value;
    }

    public OperationGroup()
    {
    }
}