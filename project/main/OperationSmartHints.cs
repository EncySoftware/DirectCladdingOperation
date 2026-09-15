using CAMAPI.Application;
using CAMAPI.DotnetHelper;
using CAMAPI.ResultStatus;
using CAMAPI.TechOperation;

namespace DirectCladdingOperationExtension;

/// <summary>
/// Builds English context help from the operation's actual XML properties.
/// </summary>
internal static class OperationSmartHints
{
    private static string? _loadedHintsFile;

    internal static void Load(ICamApiTechOperation operation)
    {
        // Solver initialization takes place in the CAM application. Generate once per
        // extension session, after the operation's XML properties are available.
        if (_loadedHintsFile != null)
            return;

        using var application = SystemExtensionFactory.GetApplication();
        using var manager = application.InvokeAndWrap(app => app.SmartHintManager);
        var hintsFolder = Path.GetDirectoryName(typeof(OperationSmartHints).Assembly.Location)!;
#if DEBUG
        hintsFolder = GetSourceFolder();
#endif
        var hintsFile = Path.Combine(hintsFolder, "DirectCladdingOperation.smarthint");
        var imagesFolder = Path.Combine(hintsFolder, "SmartHintImages");

#if DEBUG
        // Author hints with real operation properties during development.
        // Release installations only read the file shipped in the extension package.
        using (var builder = manager.InvokeAndWrap(hints =>
        {
            var result = hints.CreateHintBuilder(hintsFile, imagesFolder, "", 1033,
                TCamApiSmartHintBuildMode.hbmRewrite, out var status);
            CheckStatus(status, "create smart hint builder");
            return result;
        }))
        using (var properties = new ComWrapper<STXMLPropTypes.IST_XMLPropPointer>(operation.XMLProp))
        {
            foreach (var (path, value, text) in Definitions)
            {
                using var property = properties.InvokeAndWrap(props => props.Ptr[path]);
                if (property.IsNull)
                    throw new InvalidOperationException($"Smart hint property not found: {path}");
                property.Invoke(prop => builder.Invoke(hints =>
                {
                    var image = ImageFor(path, value);
                    var legend = LegendFor(image);
                    hints.AddHint(prop, value, image, text + legend, out var status);
                    CheckStatus(status, $"build smart hint for {path} {value}");
                }));
            }
        }

#endif
        manager.Invoke(hints =>
        {
            hints.LoadSmartHints(hintsFile, imagesFolder, "", out var status);
            CheckStatus(status, "load smart hints");
        });
        _loadedHintsFile = hintsFile;
    }

    private static void CheckStatus(TResultStatus status, string action)
    {
        if (status.Code == TResultStatusCode.rsError)
            throw new InvalidOperationException($"Cannot {action}: {status.Description}");
    }

#if DEBUG
    // The compiler supplies this source file's path; independent of the DLL output
    // folder and the working directory used to start CAM.
    private static string GetSourceFolder(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        var folder = Path.GetDirectoryName(sourceFile);
        if (folder == null || !File.Exists(Path.Combine(folder, "DirectCladdingOperationExtension.csproj")))
            throw new InvalidOperationException("The Direct Cladding source project is required to generate Debug smart hints.");
        return folder;
    }
#endif

    private static string ImageFor(string path, string value) => path switch
    {
        "PrintingStrategy" => "printing-strategy.png",
        "Sort.SortBy" => value == "ByLayer" ? "sort-layer.png" : "sort-feature.png",
        "Sort.SplitByLayer.Enabled" when value == "False" => "sort-feature.png",
        "Sort.SplitByLayer.Enabled" or "Sort.SplitByLayer.Count" => "split-layers.png",
        "Sort.AllowReverse" when value == "True" => "reverse-direction-true.png",
        "Sort.AllowReverse" when value == "False" => "reverse-direction-false.png",
        "Sort.AllowReverse" => "reverse-direction.png",
        "Sort.StartPoint.Mode" when value == "" => "start-point.png",
        "Sort.StartPoint.Mode" when value == "Manually" => "start-point.png",
        "Sort.StartPoint.Point" => "start-point.png",
        "Sort.OptimizeOrder" when value == "False" => "optimize-order-false.png",
        "Sort.OptimizeOrder" => "optimize-order.png",
        "RapidDistance" or "LinksPlaceHolder.FirstLinkType" or "LinksPlaceHolder.LastLinkType"
            or "LinksPlaceHolder.ShortLinkType" or "LinksPlaceHolder.LongLinkType" => "link-clearance.png",
        _ => ""
    };

    // Keep image legends in the hint text so translations can use the same images.
    private static string LegendFor(string image) => image switch
    {
        "printing-strategy.png" => "\n\n1 - Planar strategy.\n2 - Helical strategy.",
        "sort-feature.png" or "sort-layer.png" =>
            "\n\nThe two stacks represent separate features. Numbers 1-8 indicate the processing sequence.",
        "split-layers.png" =>
            "\n\nThe two stacks represent separate features with Count = 2. Numbers 1-8 indicate the processing sequence.",
        "start-point.png" => "\n\n1 - Reference point.\n2 - Selected start point.\n3 - Closed curve.",
        "reverse-direction.png" => "\n\n1 - Original direction.\n2 - Reversed direction.",
        "reverse-direction-true.png" or "reverse-direction-false.png" =>
            "\n\n1 - Input direction.\n2 - Output direction for the closed curve.",
        "link-clearance.png" => "\n\n1 - Direct link.\n2 - Clearance level.\n3 - Vertical clearance above the endpoint.",
        "optimize-order.png" or "optimize-order-false.png" =>
            "\n\nNumbers 1-4 indicate the processing sequence in this example.",
        _ => ""
    };

    private static readonly (string Path, string Value, string Text)[] Definitions =
    [
        ("PrintingStrategy", "", "Choose how the input curves are processed. Planar groups curves into layers or features. Helical follows the supplied continuous curves. Prepare the deposition curves in CAD and add them to the job assignment before calculating the toolpath."),
        ("PrintingStrategy", "Helical", "Follow the supplied continuous curves. Curves whose start is above their end are reversed to run upward. Enable Optimize order to sort the curves by proximity. This strategy does not generate a helix from planar contours."),
        ("PrintingStrategy", "Planar", "Process the supplied layer curves by feature or by layer. Use Sorting to control the processing order, split features into batches, and adjust the start points of closed curves."),
        ("Sort.SortBy", "", "Choose the grouping used for planar printing. ByFeature processes curves belonging to the same feature together. ByLayer processes curves at the same Z level together."),
        ("Sort.SortBy", "ByFeature", "Group curves into features using their bounding boxes. Process each feature together, or enable Split by layers to alternate between features in batches."),
        ("Sort.SortBy", "ByLayer", "Group curves by their Z level and process the layer groups. Enable Optimize order to optimize the order within the layer processing strategy."),
        ("Sort.SplitByLayer.Enabled", "", "Enable batch processing across features. Set Count to a value greater than 1 to split feature curves into batches. Disable this option to process each feature together."),
        ("Sort.SplitByLayer.Enabled", "True", "Batch processing is enabled for planar printing sorted by feature. With Count greater than 1, process a batch of consecutive curves from each feature before continuing with the next batch. Count = 1 leaves features unsplit."),
        ("Sort.SplitByLayer.Enabled", "False", "Batch processing is disabled. Process each feature together without alternating between features in batches. Count has no effect while this option is disabled; the other sorting settings still apply."),
        ("Sort.SplitByLayer.Count", "", "Number of consecutive curves taken from each feature in one batch. For input with one curve per layer per feature, this is the number of layers per batch. Use a value greater than 1 to activate splitting; 1 leaves features unsplit."),
        ("Sort.AllowReverse", "", "Reverse the traversal direction of closed input curves. Open curves retain their direction. Turn this off when the direction of the supplied closed contours must be preserved."),
        ("Sort.AllowReverse", "True", "Reverse the traversal direction of closed input curves. The output contour is traversed in the opposite direction to the input contour. This option does not reverse open curves."),
        ("Sort.AllowReverse", "False", "Preserve the traversal direction of closed input curves. The output contour is traversed in the same direction as the input contour. Start-point selection and processing-order settings still apply."),
        ("Sort.StartPoint.Mode", "", "Choose how to place the start points of closed curves. Do not change keeps the input start points. Nearest uses the machine start position as the initial reference. Manually uses the specified Point as the initial reference. Subsequent curves use the preceding selected point."),
        ("Sort.StartPoint.Mode", "Default", "Keep the start points defined by the input curves."),
        ("Sort.StartPoint.Mode", "Automatically", "Choose a point on the first closed curve nearest to the machine start position, then use the preceding selected point as the reference for subsequent curves."),
        ("Sort.StartPoint.Mode", "Manually", "Use Point as the initial reference for selecting a start point on a closed curve. Subsequent curves use the preceding selected point as their reference."),
        ("Sort.StartPoint.Point", "", "Initial reference position for manual start-point selection. The start point is chosen on the curve near this position, so the reference does not have to lie exactly on the curve."),
        ("Sort.OptimizeOrder", "", "Optimize the processing order using proximity between curves or groups within the selected strategy. Disable this option to retain the order produced by grouping without proximity optimization."),
        ("Sort.OptimizeOrder", "True", "Proximity optimization is enabled. Reorder curves or groups within the selected strategy to reduce travel between them. The illustration shows an example of visiting nearby curves in sequence; the resulting order depends on the input geometry and strategy."),
        ("Sort.OptimizeOrder", "False", "Proximity optimization is disabled. Retain the order produced by the selected grouping strategy, or the input curve order for helical printing. This can produce longer links between curves, as illustrated. Grouping and other strategy settings still apply."),
        ("RapidDistance", "", "Vertical clearance above a curve endpoint used by links that retract by the rapid distance. This is an offset from the endpoint, not an absolute Z level."),
        ("LinksPlaceHolder.ShortLink", "", "Set the distance threshold for classifying links between curves. Distances below the threshold use Short link type; distances equal to or above it use Long link type. Specify an absolute distance or a percentage of the tool diameter."),
        ("LinksPlaceHolder.FirstLinkType", "", "Choose how the tool approaches the first curve: directly, via the safe level, or using the rapid-distance clearance. Check the approach against the workpiece and fixtures."),
        ("LinksPlaceHolder.LastLinkType", "", "Choose how the tool leaves the final curve: directly, via the safe level, or using the rapid-distance clearance. By default, this follows First link type."),
        ("LinksPlaceHolder.ShortLinkType", "", "Choose the transition used when the distance to the next curve is below the short-link threshold: directly, via the safe level, or using the rapid-distance clearance."),
        ("LinksPlaceHolder.LongLinkType", "", "Choose the transition used when the distance to the next curve reaches or exceeds the short-link threshold: directly, via the safe level, or using the rapid-distance clearance."),
    ];
}
