using CAMAPI.DotnetHelper;
using CAMAPI.EventHandler;
using CAMAPI.ModelFormerTypes;
using CAMAPI.ResultStatus;
using CAMAPI.TechOperation;

namespace DirectCladdingOperationExtension;

public class OperationEventHandler : ICamApiEventHandler,
    ICamApiHandlerTechOperationInitModelFormers
{
    private class ModelFormerMakeSupportedItems : ICamApiModelFormerMakeSupportedItems
    {
        public void MakeSupportedItems(ICamApiModelFormerSupportedItems itemsObj)
        {
            using var itemsCom = new ComWrapper<ICamApiModelFormerSupportedItems>(itemsObj);
            itemsCom.Invoke(items =>
            {
                items.AddItem("Curve",
                    InterfaceInfo.IID<ICamApiCurvesArrayModelItem>(),
                    "", "Curve", "", "", false, null);
            });
        }
    }
    
    /// <summary>
    /// We always return false, because only one event is supported
    /// </summary>
    public bool GetAsyncMode(string interfaceUid)
    {
        return false;
    }
    
    /// <summary>
    /// Initialize model items, which can be created in job assignment
    /// </summary>
    public void InitModelFormers(ICamApiModelFormer modelFormersObj)
    {
        using var modelFormersCom = new ComWrapper<ICamApiModelFormer>(modelFormersObj);
        using var supportedItemsCom = modelFormersCom.InvokeAndWrap(modelFormers => modelFormers.SupportedItems);
        if (!supportedItemsCom.IsNull)
            return;

        modelFormersCom.Invoke(modelFormers =>
        {
            modelFormers.MakeSupportedItems(new ModelFormerMakeSupportedItems(), out var resultStatus);
            if (resultStatus.Code == TResultStatusCode.rsError)
                throw new Exception(resultStatus.Description);
        });
    }
}