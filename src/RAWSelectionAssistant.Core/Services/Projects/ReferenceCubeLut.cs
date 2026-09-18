using System.Globalization;
using System.Text;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed class ReferenceCubeLut
{
    public ReferenceCubeLut(int size, IReadOnlyList<(double R, double G, double B)> values, ColorPipelineDescriptor descriptor)
    {
        if (size is < 2 or > 65 || values.Count != size * size * size) throw new ArgumentException("Cube LUT dimensions are invalid.");
        Size = size; Values = values; Descriptor = descriptor;
    }
    public int Size { get; }
    public IReadOnlyList<(double R, double G, double B)> Values { get; }
    public ColorPipelineDescriptor Descriptor { get; }
    public (double R, double G, double B) Sample(double r, double g, double b)
    {
        r = Math.Clamp(r, 0, 1); g = Math.Clamp(g, 0, 1); b = Math.Clamp(b, 0, 1);
        var x = r * (Size - 1); var y = g * (Size - 1); var z = b * (Size - 1);
        var x0 = (int)Math.Floor(x); var y0 = (int)Math.Floor(y); var z0 = (int)Math.Floor(z);
        var x1 = Math.Min(Size - 1, x0 + 1); var y1 = Math.Min(Size - 1, y0 + 1); var z1 = Math.Min(Size - 1, z0 + 1);
        var tx = x - x0; var ty = y - y0; var tz = z - z0;
        (double R,double G,double B) At(int red, int green, int blue) => Values[blue * Size * Size + green * Size + red];
        static (double R,double G,double B) Mix((double R,double G,double B) a,(double R,double G,double B) c,double t) => (a.R+(c.R-a.R)*t,a.G+(c.G-a.G)*t,a.B+(c.B-a.B)*t);
        var c00=Mix(At(x0,y0,z0),At(x1,y0,z0),tx); var c10=Mix(At(x0,y1,z0),At(x1,y1,z0),tx);
        var c01=Mix(At(x0,y0,z1),At(x1,y0,z1),tx); var c11=Mix(At(x0,y1,z1),At(x1,y1,z1),tx);
        return Mix(Mix(c00,c10,ty),Mix(c01,c11,ty),tz);
    }
}

public static class ReferenceCubeLutBuilder
{
    public static ReferenceCubeLut Build(int size, Func<VisualRgb24, VisualRgb24> transform, ColorPipelineDescriptor? descriptor = null)
    {
        if (size is not (33 or 65)) throw new ArgumentOutOfRangeException(nameof(size));
        var values = new List<(double,double,double)>(size*size*size);
        for(var b=0;b<size;b++) for(var g=0;g<size;g++) for(var r=0;r<size;r++)
        {
            var input = new VisualRgb24((byte)Math.Round(r*255d/(size-1)),(byte)Math.Round(g*255d/(size-1)),(byte)Math.Round(b*255d/(size-1)));
            var output=transform(input); values.Add((output.R/255d,output.G/255d,output.B/255d));
        }
        return new(size, values, descriptor ?? new());
    }

    public static async Task ExportAsync(ReferenceCubeLut lut, string path, string title, CancellationToken token = default)
    {
        var builder = new StringBuilder(); builder.AppendLine($"TITLE \"{title.Replace("\"", "'")}\"");
        builder.AppendLine("# Pixel Tart 快速仿色；适用于已转换到 sRGB 的显示参照图像。");
        builder.AppendLine($"LUT_3D_SIZE {lut.Size}"); builder.AppendLine("DOMAIN_MIN 0.0 0.0 0.0"); builder.AppendLine("DOMAIN_MAX 1.0 1.0 1.0");
        foreach(var value in lut.Values) builder.AppendLine(string.Create(CultureInfo.InvariantCulture,$"{value.R:F7} {value.G:F7} {value.B:F7}"));
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { await File.WriteAllTextAsync(temporary,builder.ToString(),new UTF8Encoding(false),token); File.Move(temporary,full,true); }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
}
