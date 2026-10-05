using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class TetherReferenceModeViewModel
{
    // Promote the effective legacy look once when the user begins node editing.
    // The promoted snapshot is also the undo baseline, so adding a tool cannot
    // silently discard an existing reference match or standalone film effect.
    private ColorAdjustmentStack EffectiveEditingStack()
    {
        if (AdjustmentStack.Nodes.Count > 0) return AdjustmentStack;
        if (SelectedLook is { } look)
            return ColorStudioLegacyMigration.Migrate(look with { Film = FilmSettings }).Stack;
        return FilmSettings.Enabled
            ? new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片", FilmSettings: FilmSettings)])
            : AdjustmentStack;
    }

    public ColorAdjustmentStackNode? ToolNode(ColorStudioNodeType type) =>
        SelectedAdjustmentNode?.Type == type ? SelectedAdjustmentNode : AdjustmentStack.Nodes.FirstOrDefault(n => n.Type == type);

    public double ToolValue(ColorStudioToolParameter parameter) => ToolNode(parameter.NodeType) is { } node
        ? ColorStudioToolCatalog.Value(node, parameter.Key) : parameter.DefaultValue;

    public void SetToolParameter(ColorStudioToolParameter parameter, double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentException("请输入有限数值。");
        value = Math.Clamp(value, parameter.Minimum, parameter.Maximum);
        if (Math.Abs(ToolValue(parameter) - value) < 1e-10) return;
        var node = ToolNode(parameter.NodeType) ?? new ColorAdjustmentStackNode(Guid.NewGuid(), parameter.NodeType, ToolName(parameter.NodeType));
        var changed = node with { NumericParameters = new Dictionary<string, double>(node.NumericParameters) { [parameter.Key] = value } };
        ColorStudioToolProcessor.Validate(changed);
        Enabled = true;
        ChangeStack(nodes => nodes.Any(n => n.Id == node.Id)
            ? nodes.Select(n => n.Id == node.Id ? changed : n).ToArray()
            : InsertToolInDefaultOrder(nodes, changed), processingVersion: 2);
    }

    public void ResetToolGroup(ColorStudioNodeType type, string group)
    {
        if (ToolNode(type) is not { } node) return;
        var keys = ColorStudioToolCatalog.GetParameters(type).Where(p => p.Group == group).Select(p => p.Key).ToHashSet();
        var changed = node with { NumericParameters = node.NumericParameters.Where(p => !keys.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value) };
        ChangeStack(nodes => nodes.Select(n => n.Id == node.Id ? changed : n).ToArray());
    }

    public void SetToolEnabled(ColorStudioNodeType type, bool enabled)
    {
        if (ToolNode(type) is not { } node)
        {
            if (!enabled)
            {
                var disabled = new ColorAdjustmentStackNode(Guid.NewGuid(), type, ToolName(type), Enabled: false);
                ChangeStack(nodes => InsertToolInDefaultOrder(nodes, disabled), processingVersion: 2);
            }
            return;
        }
        if (node.Enabled == enabled) return;
        ChangeStack(nodes => nodes.Select(n => n.Id == node.Id ? n with { Enabled = enabled } : n).ToArray());
    }

    public void SetCurvePoints(string channel, IReadOnlyList<ColorStudioToolProcessor.CurvePoint> points)
    {
        if (channel is not ("rgb" or "r" or "g" or "b")) throw new ArgumentException("未知曲线通道。");
        var node = ToolNode(ColorStudioNodeType.Curve) ?? new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.Curve, "曲线");
        var values = node.NumericParameters.Where(p => !p.Key.StartsWith(channel + "_", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);
        values[channel + "_count"] = points.Count;
        for (var i = 0; i < points.Count; i++) { values[$"{channel}_x{i}"] = points[i].X; values[$"{channel}_y{i}"] = points[i].Y; }
        var changed = node with { NumericParameters = values };
        _ = ColorStudioToolProcessor.ReadCurve(changed, channel);
        ColorStudioToolProcessor.Validate(changed);
        Enabled = true;
        ChangeStack(nodes => nodes.Any(n => n.Id == node.Id) ? nodes.Select(n => n.Id == node.Id ? changed : n).ToArray() : InsertToolInDefaultOrder(nodes, changed), processingVersion: 2);
    }

    public static string ToolName(ColorStudioNodeType type) => type switch
    {
        ColorStudioNodeType.WhiteBalance => "白平衡", ColorStudioNodeType.BasicTone => "基础与 HDR 影调",
        ColorStudioNodeType.ColorBalance => "色彩平衡", ColorStudioNodeType.Levels => "色阶",
        ColorStudioNodeType.Curve => "曲线", ColorStudioNodeType.Details => "细节",
        ColorStudioNodeType.SkinTone => "肤色均匀化", _ => type.ToString()
    };

    private static IReadOnlyList<ColorAdjustmentStackNode> InsertToolInDefaultOrder(IReadOnlyList<ColorAdjustmentStackNode> nodes, ColorAdjustmentStackNode node)
    {
        static int Stage(ColorStudioNodeType type) => type switch
        {
            ColorStudioNodeType.WhiteBalance => 0, ColorStudioNodeType.BasicTone or ColorStudioNodeType.Develop => 1,
            ColorStudioNodeType.ReferenceMatch => 2, ColorStudioNodeType.ColorRange or ColorStudioNodeType.SkinTone or ColorStudioNodeType.ColorBalance => 3,
            ColorStudioNodeType.Levels or ColorStudioNodeType.Curve => 4, ColorStudioNodeType.Details => 5,
            ColorStudioNodeType.Film => 6, _ => 3
        };
        var result = nodes.ToList();
        var index = result.FindIndex(n => Stage(n.Type) > Stage(node.Type));
        result.Insert(index < 0 ? result.Count : index, node);
        return result;
    }
}

