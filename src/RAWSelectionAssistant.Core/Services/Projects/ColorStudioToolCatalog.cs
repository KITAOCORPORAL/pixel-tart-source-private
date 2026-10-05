namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record ColorStudioToolParameter(ColorStudioNodeType NodeType, string Group, string Key,
    string Label, double Minimum, double Maximum, double DefaultValue = 0, string Unit = "", double Step = 1);

/// <summary>Shared product contract. The Core validates independently of slider limits.</summary>
public static class ColorStudioToolCatalog
{
    public static IReadOnlyList<ColorStudioToolParameter> Parameters { get; } = Build();
    public static IEnumerable<ColorStudioToolParameter> GetParameters(ColorStudioNodeType type) => Parameters.Where(p => p.NodeType == type);
    public static double DefaultValue(ColorStudioNodeType type, string key) => Parameters.FirstOrDefault(p => p.NodeType == type && p.Key == key)?.DefaultValue ?? 0;
    public static double Value(ColorAdjustmentStackNode node, string key)
    {
        var parameter = Parameters.FirstOrDefault(p => p.NodeType == node.Type && p.Key == key);
        var value = node.NumericParameters.GetValueOrDefault(key, parameter?.DefaultValue ?? 0);
        if (!double.IsFinite(value)) throw new ArgumentException($"参数 {key} 必须为有限数值。");
        return parameter is null ? value : Math.Clamp(value, parameter.Minimum, parameter.Maximum);
    }
    public static bool IsTool(ColorStudioNodeType type) => type is >= ColorStudioNodeType.WhiteBalance and <= ColorStudioNodeType.SkinTone;
    private static IReadOnlyList<ColorStudioToolParameter> Build()
    {
        var p = new List<ColorStudioToolParameter>();
        void Add(ColorStudioNodeType type, string group, string key, string label, double min, double max, double value = 0, string unit = "", double step = 1) => p.Add(new(type, group, key, label, min, max, value, unit, step));
        var t = ColorStudioNodeType.WhiteBalance;
        Add(t, "白平衡", "temperature", "冷暖偏移", -100, 100); Add(t, "白平衡", "tint", "绿－洋红", -100, 100);
        t = ColorStudioNodeType.BasicTone;
        Add(t, "基础曝光", "exposure", "曝光", -5, 5, 0, "EV", .1);
        Add(t, "基础曝光", "brightness", "亮度", -100, 100); Add(t, "基础曝光", "contrast", "对比度", -100, 100);
        foreach (var (key, label) in new[] { ("highlights", "高光"), ("shadows", "阴影"), ("whites", "白场"), ("blacks", "黑场") }) Add(t, "HDR 影调", key, label, -100, 100);
        Add(t, "全局颜色", "saturation", "饱和度", -100, 100); Add(t, "全局颜色", "vibrance", "自然饱和度", -100, 100);
        t = ColorStudioNodeType.ColorBalance;
        foreach (var (key, label) in new[] { ("master", "全局"), ("shadows", "阴影"), ("midtones", "中间调"), ("highlights", "高光") })
        { Add(t, "色彩平衡 · " + label, key + "_hue", "色相", 0, 360, 0, "°"); Add(t, "色彩平衡 · " + label, key + "_amount", "强度", 0, 100, 0, "%"); }
        t = ColorStudioNodeType.Levels;
        foreach (var (key, label) in new[] { ("rgb", "RGB"), ("r", "红"), ("g", "绿"), ("b", "蓝") })
        { Add(t, "色阶 · " + label, key + "_black", "输入黑点", 0, .99, 0, "", .01); Add(t, "色阶 · " + label, key + "_white", "输入白点", .01, 1, 1, "", .01); Add(t, "色阶 · " + label, key + "_gamma", "中间调", .1, 5, 1, "", .01); Add(t, "色阶 · " + label, key + "_out_black", "输出黑点", 0, 1, 0, "", .01); Add(t, "色阶 · " + label, key + "_out_white", "输出白点", 0, 1, 1, "", .01); }
        t = ColorStudioNodeType.Curve;
        foreach (var (key, label) in new[] { ("rgb", "RGB"), ("r", "红"), ("g", "绿"), ("b", "蓝") })
            for (var i = 0; i < 5; i++) Add(t, "曲线 · " + label, $"{key}_y{i}", $"{i * 25}% 控制点", 0, 1, i / 4d, "", .01);
        t = ColorStudioNodeType.Details;
        Add(t, "清晰度", "clarity", "清晰度", -100, 100); Add(t, "清晰度", "structure", "结构", -100, 100);
        Add(t, "锐化", "sharpen", "锐化强度", 0, 100); Add(t, "锐化", "radius", "参考半径", .5, 4, 1, "px / 1600", .1); Add(t, "锐化", "threshold", "边缘阈值", 0, .2, .01, "", .005);
        Add(t, "降噪", "luma_noise", "亮度降噪", 0, 100); Add(t, "降噪", "chroma_noise", "颜色降噪", 0, 100);
        t = ColorStudioNodeType.SkinTone;
        Add(t, "肤色范围", "center_hue", "范围中心", 0, 360, 45, "°"); Add(t, "肤色范围", "hue_width", "色相范围", 1, 90, 35, "°"); Add(t, "肤色范围", "feather", "羽化", 1, 60, 20, "°");
        Add(t, "肤色均匀化", "hue_uniformity", "色相均匀度", 0, 100); Add(t, "肤色均匀化", "chroma_uniformity", "色度均匀度", 0, 100); Add(t, "肤色均匀化", "lightness_uniformity", "明度均匀度", 0, 100);
        Add(t, "肤色目标", "target_hue", "目标色相", 0, 360, 45, "°"); Add(t, "肤色目标", "target_chroma", "目标色度", 0, .3, .10, "", .005); Add(t, "肤色目标", "target_lightness", "目标明度", 0, 1, .65, "", .01);
        return p.AsReadOnly();
    }
}
