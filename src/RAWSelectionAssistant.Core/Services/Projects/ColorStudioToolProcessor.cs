using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Versioned tool node mathematics. Inputs/outputs are encoded sRGB float;
/// each operation explicitly enters linear RGB or OKLab. Legacy Develop remains unchanged.</summary>
public static class ColorStudioToolProcessor
{
    public static HighBitDepthImageBuffer Apply(HighBitDepthImageBuffer source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        node = node.Normalize();
        var parameters = ColorStudioToolCatalog.GetParameters(node.Type).ToDictionary(p => p.Key, p => ColorStudioToolCatalog.Value(node, p.Key));
        double P(string key) => parameters.GetValueOrDefault(key);
        if (!ColorStudioToolCatalog.IsTool(node.Type)) throw new ArgumentException("Unsupported photographic tool.");
        Validate(node);
        token.ThrowIfCancellationRequested();
        for (var i = 0; i < source.Rgb32.Length; i++)
        { if ((i & 8191) == 0) token.ThrowIfCancellationRequested(); if (!float.IsFinite(source.Rgb32.Span[i])) throw new ArgumentException("图像含无效浮点像素。"); }
        if (IsIdentity(node)) return source.Clone();
        if (node.Type == ColorStudioNodeType.Details) return ApplyDetails(source, node, token);
        var input = source.Rgb32.Span; var output = new float[input.Length];
        var curves = node.Type == ColorStudioNodeType.Curve ? new[] { "rgb", "r", "g", "b" }.Select(c => ReadCurve(node, c)).ToArray() : [];
        const int tableSize = 4096;
        double[][]? channelTables = null;
        if (node.Type == ColorStudioNodeType.Curve)
        {
            channelTables = new double[3][];
            for (var channel = 0; channel < 3; channel++)
            {
                token.ThrowIfCancellationRequested(); var current = channel;
                var table = channelTables[channel] = new double[tableSize + 1];
                for (var i = 0; i <= tableSize; i++)
                { var value = i / (double)tableSize; table[i] = EvaluateCurve(curves[current + 1], EvaluateCurve(curves[0], value)); }
            }
        }
        var levels = node.Type == ColorStudioNodeType.Levels ? new[] { "rgb", "r", "g", "b" }.Select(channel => new LevelTransform(P(channel + "_black"), P(channel + "_white"), P(channel + "_gamma"), P(channel + "_out_black"), P(channel + "_out_white"))).ToArray() : [];
        var warm = P("temperature") / 100 * .35; var tint = P("tint") / 100 * .25;
        var whiteGains = (R: Math.Exp(warm + tint * .5), G: Math.Exp(-tint), B: Math.Exp(-warm + tint * .5));
        var exposure = Math.Pow(2, P("exposure"));
        var balance = new[] { "master", "shadows", "midtones", "highlights" }.Select(name =>
        { var angle = P(name + "_hue") * Math.PI / 180; var amount = P(name + "_amount") / 100 * .12; return (A: Math.Cos(angle) * amount, B: Math.Sin(angle) * amount); }).ToArray();
        for (var offset = 0; offset < input.Length; offset += 3)
        {
            if ((offset & 2047) == 0) token.ThrowIfCancellationRequested();
            var r = (double)input[offset]; var g = (double)input[offset + 1]; var b = (double)input[offset + 2];
            if (!double.IsFinite(r) || !double.IsFinite(g) || !double.IsFinite(b)) throw new ArgumentException("图像含无效浮点像素。");
            if (node.Type == ColorStudioNodeType.WhiteBalance)
            {
                // Relative adaptation of an already decoded sRGB image; not camera Kelvin.
                r = Decode(r) * whiteGains.R; g = Decode(g) * whiteGains.G; b = Decode(b) * whiteGains.B;
                (r, g, b) = EncodeGamut(r, g, b);
            }
            else if (node.Type == ColorStudioNodeType.BasicTone)
            {
                r = Decode(r); g = Decode(g); b = Decode(b);
                var y = Luma(r, g, b);
                // Rational shoulder supplies continuous display-referred headroom. At EV=0 it is identity.
                var adjusted = y * exposure / (1 + y * Math.Max(0, exposure - 1));
                adjusted = Bend(adjusted, P("brightness") / 100 * .65);
                var shadow = 1 - Smooth(.02, .5, y); var highlight = Smooth(.35, .95, y);
                adjusted = Bend(adjusted, (P("shadows") * shadow + P("highlights") * highlight) / 100 * .65);
                var contrast = P("contrast") / 100;
                adjusted = Bend(adjusted, contrast * (adjusted - .5) * 1.5);
                adjusted = Bend(adjusted, (P("whites") * Smooth(.6, 1, y) + P("blacks") * (1 - Smooth(0, .2, y))) / 100 * .5);
                if (y > 1e-12) { var ratio = adjusted / y; r *= ratio; g *= ratio; b *= ratio; }
                (r, g, b) = EncodeGamut(r, g, b);
                if (P("saturation") != 0 || P("vibrance") != 0)
                {
                    var lab = OklabColorSpace.FromSrgb((float)r, (float)g, (float)b);
                    var saturation = P("saturation") / 100;
                    var vibrance = P("vibrance") / 100 * (1 - Smooth(.03, .24, lab.Chroma));
                    var scale = Math.Max(0, 1 + saturation + vibrance);
                    (r, g, b) = OklabColorSpace.ToSrgbGamutMappedFloat(lab with { A = lab.A * scale, B = lab.B * scale });
                }
            }
            else if (node.Type == ColorStudioNodeType.ColorBalance)
            {
                var lab = OklabColorSpace.FromSrgb((float)r, (float)g, (float)b);
                var sh = 1 - Smooth(.1, .6, lab.L); var hi = Smooth(.4, .95, lab.L); var mid = Math.Max(0, 1 - sh - hi);
                var a = lab.A; var bb = lab.B;
                var endpointFade = Math.Sin(Math.PI * Math.Clamp(lab.L, 0, 1));
                a += (balance[0].A + balance[1].A * sh + balance[2].A * mid + balance[3].A * hi) * endpointFade;
                bb += (balance[0].B + balance[1].B * sh + balance[2].B * mid + balance[3].B * hi) * endpointFade;
                (r, g, b) = OklabColorSpace.ToSrgbGamutMappedFloat(lab with { A = a, B = bb });
            }
            else if (node.Type == ColorStudioNodeType.Levels)
            {
                r = levels[1].Apply(levels[0].Apply(r)); g = levels[2].Apply(levels[0].Apply(g)); b = levels[3].Apply(levels[0].Apply(b));
            }
            else if (node.Type == ColorStudioNodeType.Curve)
            { r = Lookup(channelTables![0], r); g = Lookup(channelTables[1], g); b = Lookup(channelTables[2], b); }
            else if (node.Type == ColorStudioNodeType.SkinTone)
            {
                var lab = OklabColorSpace.FromSrgb((float)r, (float)g, (float)b); var hue = Math.Atan2(lab.B, lab.A) * 180 / Math.PI;
                var distance = Math.Abs(WrapDegrees(hue - P("center_hue")));
                var weight = (1 - Smooth(P("hue_width"), P("hue_width") + P("feather"), distance)) * Smooth(.015, .05, lab.Chroma) * Smooth(.08, .3, lab.L) * (1 - Smooth(.9, 1, lab.L));
                if (weight == 0) { output[offset] = (float)r; output[offset + 1] = (float)g; output[offset + 2] = (float)b; continue; }
                var h = hue + WrapDegrees(P("target_hue") - hue) * P("hue_uniformity") / 100 * weight;
                var c = lab.Chroma + (P("target_chroma") - lab.Chroma) * P("chroma_uniformity") / 100 * weight;
                var l = lab.L + (P("target_lightness") - lab.L) * P("lightness_uniformity") / 100 * weight;
                (r, g, b) = OklabColorSpace.ToSrgbGamutMappedFloat(new(l, Math.Cos(h * Math.PI / 180) * c, Math.Sin(h * Math.PI / 180) * c));
            }
            output[offset] = (float)r; output[offset + 1] = (float)g; output[offset + 2] = (float)b;
        }
        return Copy(source, output);
    }

    public static void Validate(ColorAdjustmentStackNode node)
    {
        foreach (var parameter in ColorStudioToolCatalog.GetParameters(node.Type)) _ = ColorStudioToolCatalog.Value(node, parameter.Key);
        if (node.Type == ColorStudioNodeType.Levels)
            foreach (var channel in new[] { "rgb", "r", "g", "b" })
            { if (ColorStudioToolCatalog.Value(node, channel + "_black") >= ColorStudioToolCatalog.Value(node, channel + "_white")) throw new ArgumentException("输入黑点必须小于输入白点。"); if (ColorStudioToolCatalog.Value(node, channel + "_out_black") > ColorStudioToolCatalog.Value(node, channel + "_out_white")) throw new ArgumentException("输出黑点不能大于输出白点。"); }
        if (node.Type == ColorStudioNodeType.Curve) foreach (var channel in new[] { "rgb", "r", "g", "b" }) _ = ReadCurve(node, channel);
    }

    public static bool IsIdentity(ColorAdjustmentStackNode node)
    {
        double P(string key) => ColorStudioToolCatalog.Value(node, key);
        if (node.Type == ColorStudioNodeType.Curve) return new[] { "rgb", "r", "g", "b" }.All(c => ReadCurve(node, c).All(p => p.X == p.Y));
        if (node.Type == ColorStudioNodeType.ColorBalance) return new[] { "master", "shadows", "midtones", "highlights" }.All(k => P(k + "_amount") == 0);
        if (node.Type == ColorStudioNodeType.SkinTone) return new[] { "hue_uniformity", "chroma_uniformity", "lightness_uniformity" }.All(k => P(k) == 0);
        if (node.Type == ColorStudioNodeType.Details) return new[] { "clarity", "structure", "sharpen", "luma_noise", "chroma_noise" }.All(k => P(k) == 0);
        return ColorStudioToolCatalog.GetParameters(node.Type).All(p => P(p.Key) == p.DefaultValue);
    }

    public readonly record struct CurvePoint(double X, double Y);
    public static IReadOnlyList<CurvePoint> ReadCurve(ColorAdjustmentStackNode node, string channel)
    {
        var rawCount = node.NumericParameters.GetValueOrDefault(channel + "_count", 5);
        if (!double.IsFinite(rawCount) || rawCount != Math.Round(rawCount) || rawCount is < 2 or > 16) throw new ArgumentException("曲线需包含 2 至 16 个控制点。");
        var count = (int)rawCount; var points = new CurvePoint[count];
        for (var i = 0; i < count; i++)
        {
            var x = node.NumericParameters.GetValueOrDefault($"{channel}_x{i}", i / (double)(count - 1)); var y = node.NumericParameters.GetValueOrDefault($"{channel}_y{i}", i / (double)(count - 1));
            if (!double.IsFinite(x) || !double.IsFinite(y) || x is < 0 or > 1 || y is < 0 or > 1 || i > 0 && x <= points[i - 1].X) throw new ArgumentException("曲线控制点必须按输入值递增，且处于 0 至 1。");
            points[i] = new(x, y);
        }
        if (points[0].X != 0 || points[^1].X != 1) throw new ArgumentException("曲线必须保留输入 0 和 1 的端点。");
        return points;
    }

    /// <summary>Shape-preserving cubic Hermite interpolation, including deliberately nonmonotonic curves.</summary>
    public static double EvaluateCurve(IReadOnlyList<CurvePoint> points, double x)
    {
        x = Math.Clamp(x, 0, 1); var i = 0; while (i < points.Count - 2 && x > points[i + 1].X) i++;
        double Slope(int at) => (points[at + 1].Y - points[at].Y) / (points[at + 1].X - points[at].X);
        double Tangent(int at)
        {
            if (at == 0) return Slope(0); if (at == points.Count - 1) return Slope(at - 1);
            var before = Slope(at - 1); var after = Slope(at); if (before * after <= 0) return 0;
            var h0 = points[at].X - points[at - 1].X; var h1 = points[at + 1].X - points[at].X;
            var w1 = 2 * h1 + h0; var w2 = h1 + 2 * h0; return (w1 + w2) / (w1 / before + w2 / after);
        }
        var h = points[i + 1].X - points[i].X; var t = (x - points[i].X) / h; var t2 = t * t; var t3 = t2 * t;
        var result = (2 * t3 - 3 * t2 + 1) * points[i].Y + (t3 - 2 * t2 + t) * h * Tangent(i) + (-2 * t3 + 3 * t2) * points[i + 1].Y + (t3 - t2) * h * Tangent(i + 1);
        return Math.Clamp(result, Math.Min(points[i].Y, points[i + 1].Y), Math.Max(points[i].Y, points[i + 1].Y));
    }

    private static HighBitDepthImageBuffer ApplyDetails(HighBitDepthImageBuffer source, ColorAdjustmentStackNode node, CancellationToken token)
    {
        var parameters = ColorStudioToolCatalog.GetParameters(node.Type).ToDictionary(p => p.Key, p => ColorStudioToolCatalog.Value(node, p.Key));
        double P(string key) => parameters.GetValueOrDefault(key);
        var count = source.PixelCount; var input = source.Rgb32.Span; var labs = new OklabColor[count];
        for (var i = 0; i < count; i++) { if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); labs[i] = OklabColorSpace.FromSrgb(input[i * 3], input[i * 3 + 1], input[i * 3 + 2]); }
        var denoised = new OklabColor[count]; var scale = Math.Max(source.Width, source.Height) / 1600d;
        var denoiseRadius = Math.Max(.1, scale);
        OklabColor Sample(double x, double y)
        {
            x = Math.Clamp(x, 0, source.Width - 1); y = Math.Clamp(y, 0, source.Height - 1);
            var x0 = (int)x; var y0 = (int)y; var x1 = Math.Min(source.Width - 1, x0 + 1); var y1 = Math.Min(source.Height - 1, y0 + 1);
            var dx = x - x0; var dy = y - y0;
            var a = labs[y0 * source.Width + x0]; var b = labs[y0 * source.Width + x1]; var c = labs[y1 * source.Width + x0]; var d = labs[y1 * source.Width + x1];
            double Mix(double aa, double bb, double cc, double dd) => (aa + (bb-aa)*dx)*(1-dy) + (cc+(dd-cc)*dx)*dy;
            return new(Mix(a.L,b.L,c.L,d.L),Mix(a.A,b.A,c.A,d.A),Mix(a.B,b.B,c.B,d.B));
        }
        for (var y = 0; y < source.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < source.Width; x++)
            {
                var index = y * source.Width + x; var center = labs[index];
                if (P("luma_noise") == 0 && P("chroma_noise") == 0) { denoised[index] = center; continue; }
                double total = 0, l = 0, a = 0, b = 0;
                for (var yy = -2; yy <= 2; yy++)
                    for (var xx = -2; xx <= 2; xx++)
                    {
                        var near = Sample(x + xx * denoiseRadius, y + yy * denoiseRadius); var delta = near.L - center.L;
                        var weight = Math.Exp(-delta * delta / .004 - (xx * xx + yy * yy) / 4.5);
                        total += weight; l += near.L * weight; a += near.A * weight; b += near.B * weight;
                    }
                denoised[index] = new(center.L + (l / total - center.L) * P("luma_noise") / 100, center.A + (a / total - center.A) * P("chroma_noise") / 100, center.B + (b / total - center.B) * P("chroma_noise") / 100);
            }
        }
        var lightness = denoised.Select(p => p.L).ToArray();
        var sharp = BlurFractional(lightness, source.Width, source.Height, (P("radius") + .5) * scale, token);
        var structure = BlurFractional(lightness, source.Width, source.Height, 5.5 * scale, token);
        var clarity = BlurFractional(lightness, source.Width, source.Height, 20.5 * scale, token);
        var output = new float[input.Length];
        for (var i = 0; i < count; i++)
        {
            if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); var lab = denoised[i]; var edge = lab.L - sharp[i];
            var threshold = P("threshold");
            var edgeWeight = threshold == 0 ? 1 : Smooth(threshold * .5, threshold * 1.5, Math.Abs(edge));
            var sharpening = edge * edgeWeight * P("sharpen") / 100 * 1.5;
            var delta = sharpening + (lab.L - structure[i]) * P("structure") / 100 + (lab.L - clarity[i]) * P("clarity") / 100 * .75;
            // Limit unsharp halos without changing chroma. Endpoint gamut mapping is explicit.
            var rgb = OklabColorSpace.ToSrgbGamutMappedFloat(lab with { L = Math.Clamp(lab.L + Math.Clamp(delta, -.15, .15), 0, 1) });
            output[i * 3] = (float)rgb.R; output[i * 3 + 1] = (float)rgb.G; output[i * 3 + 2] = (float)rgb.B;
        }
        return Copy(source, output);
    }
    // Integrate a pixel-area box with fractional radius instead of rounding every
    // proxy to a different integer kernel. At 1600 this retains the old box extent.
    private static double[] BlurFractional(double[] source, int width, int height, double radius, CancellationToken token)
    {
        radius = Math.Max(.0001, radius); var horizontal = new double[source.Length]; var result = new double[source.Length];
        void Line(double[] input, double[] output, int start, int stride, int length)
        {
            var integral = new double[length + 1];
            for (var i = 0; i < length; i++) integral[i + 1] = integral[i] + input[start + i * stride];
            double At(double p)
            {
                if (p <= 0) return p * input[start];
                if (p >= length) return integral[length] + (p - length) * input[start + (length - 1) * stride];
                var whole = (int)p; return integral[whole] + (p - whole) * input[start + whole * stride];
            }
            for (var i = 0; i < length; i++) output[start + i * stride] = (At(i + .5 + radius) - At(i + .5 - radius)) / (2 * radius);
        }
        for (var y = 0; y < height; y++) { token.ThrowIfCancellationRequested(); Line(source, horizontal, y * width, 1, width); }
        for (var x = 0; x < width; x++) { token.ThrowIfCancellationRequested(); Line(horizontal, result, x, width, height); }
        return result;
    }
    private readonly record struct LevelTransform(double Black, double White, double Gamma, double OutputBlack, double OutputWhite)
    {
        public double Apply(double value)
        {
            var normalized = Math.Clamp((value - Black) / (White - Black), 0, 1);
            return OutputBlack + (Gamma == 1 ? normalized : Math.Pow(normalized, 1 / Gamma)) * (OutputWhite - OutputBlack);
        }
    }
    private static double Lookup(double[] table, double value)
    { var position = Math.Clamp(value, 0, 1) * (table.Length - 1); var index = Math.Min(table.Length - 2, (int)position); var fraction = position - index; return table[index] + (table[index + 1] - table[index]) * fraction; }
    private static double Bend(double x, double amount) => x + Math.Clamp(amount, -.95, .95) * x * (1 - x);
    private static double Smooth(double min, double max, double x) { var t = Math.Clamp((x - min) / (max - min), 0, 1); return t * t * (3 - 2 * t); }
    private static double WrapDegrees(double angle) => (angle % 360 + 540) % 360 - 180;
    private static double Luma(double r, double g, double b) => .2126 * r + .7152 * g + .0722 * b;
    private static double Decode(double v) => v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
    private static double Encode(double v) => v <= .0031308 ? 12.92 * v : 1.055 * Math.Pow(Math.Max(0, v), 1 / 2.4) - .055;
    private static (double R, double G, double B) EncodeGamut(double r, double g, double b)
    { var maximum = Math.Max(1, Math.Max(r, Math.Max(g, b))); return (Encode(Math.Max(0, r) / maximum), Encode(Math.Max(0, g) / maximum), Encode(Math.Max(0, b) / maximum)); }
    private static HighBitDepthImageBuffer Copy(HighBitDepthImageBuffer source, float[] values) => new(source.Width, source.Height, values, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
}
