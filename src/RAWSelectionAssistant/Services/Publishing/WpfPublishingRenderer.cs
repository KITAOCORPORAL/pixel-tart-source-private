using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Publishing;

namespace RAWSelectionAssistant.Services.Publishing;

public sealed class WpfPublishingRenderer : IPublishingRenderer
{
    public Task RenderAsync(string sourcePath, string destinationPath, PublishingOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); options.Validate();
        return Task.Run(() => Render(sourcePath, destinationPath, options, cancellationToken), cancellationToken);
    }

    public Task VerifyAsync(string imagePath, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0 || decoder.Frames[0].PixelWidth <= 0 || decoder.Frames[0].PixelHeight <= 0) throw new InvalidDataException("发布文件无法读取。");
    }, cancellationToken);

    private static void Render(string sourcePath, string destinationPath, PublishingOptions options, CancellationToken cancellationToken)
    {
        var decoder = BitmapDecoder.Create(new Uri(Path.GetFullPath(sourcePath)), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var (width, height) = OutputSize(frame.PixelWidth, frame.PixelHeight, options.EffectiveDimensions);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawImage(frame, new Rect(0, 0, width, height));
            if (options.WatermarksEnabled)
                foreach (var layer in options.EffectiveWatermarkLayers.Where(layer => layer.Enabled)) DrawLayer(context, layer, width, height, cancellationToken);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); bitmap.Freeze();
        BitmapMetadata? metadata = null;
        if (options.EffectiveDimensions.PreserveMetadata && frame.Metadata is BitmapMetadata original)
        {
            try { metadata = original.Clone() as BitmapMetadata; } catch (Exception error) when (error is NotSupportedException or InvalidOperationException) { }
        }
        BitmapEncoder encoder = options.OutputFormat == PublishingOutputFormat.Png ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = options.EffectiveDimensions.JpegQuality };
        encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, frame.ColorContexts));
        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None); encoder.Save(output); output.Flush(true);
    }

    private static (int Width, int Height) OutputSize(int sourceWidth, int sourceHeight, PublishingDimensions options)
    {
        if (!options.Enabled || options.Mode == PublishingSizeMode.Original) return (sourceWidth, sourceHeight);
        if (options.Mode == PublishingSizeMode.Exact)
        {
            // The requested box is a maximum extent. Never stretch a photograph
            // into another aspect ratio merely to fill that box.
            var fit = Math.Min(options.Width / (double)sourceWidth, options.Height / (double)sourceHeight);
            return (Math.Max(1, (int)Math.Round(sourceWidth * fit)), Math.Max(1, (int)Math.Round(sourceHeight * fit)));
        }
        var scale = Math.Min(1, options.LongestEdge / (double)Math.Max(sourceWidth, sourceHeight));
        return (Math.Max(1, (int)Math.Round(sourceWidth * scale)), Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }

    private static void DrawLayer(DrawingContext context, WatermarkLayer layer, int outputWidth, int outputHeight, CancellationToken cancellationToken)
    {
        layer.Validate(); cancellationToken.ThrowIfCancellationRequested();
        if (layer.Type == WatermarkLayerType.Image)
        {
            var source = new BitmapImage(); source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.UriSource = new Uri(Path.GetFullPath(layer.ImagePath!)); source.EndInit(); source.Freeze();
            var adjusted = Adjust(source, layer.EffectiveColorAdjustments, cancellationToken);
            var width = outputWidth * layer.WidthPercent / 100d; var height = width * adjusted.PixelHeight / adjusted.PixelWidth;
            var rect = Position(layer, outputWidth, outputHeight, width, height);
            context.PushOpacity(layer.Opacity); context.DrawImage(adjusted, rect); context.Pop();
            return;
        }

        var typeface = new Typeface(new FontFamily(layer.FontFamily), FontStyles.Normal, ParseWeight(layer.FontWeight), FontStretches.Normal);
        var brush = new SolidColorBrush(Adjust(ParseColor(layer.Color), layer.EffectiveColorAdjustments)); brush.Freeze();
        var text = new FormattedText(layer.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, layer.FontSize * outputWidth / 1920d, brush, 1.0);
        var location = Position(layer, outputWidth, outputHeight, text.WidthIncludingTrailingWhitespace, text.Height).Location;
        context.PushOpacity(layer.Opacity); context.DrawText(text, location); context.Pop();
    }

    private static Rect Position(WatermarkLayer layer, double outputWidth, double outputHeight, double width, double height)
    {
        var marginX = outputWidth * layer.MarginPercent / 100; var marginY = outputHeight * layer.MarginPercent / 100;
        var x = layer.Position switch { WatermarkPosition.TopLeft or WatermarkPosition.MiddleLeft or WatermarkPosition.BottomLeft => marginX, WatermarkPosition.TopCenter or WatermarkPosition.Center or WatermarkPosition.BottomCenter => (outputWidth - width) / 2, _ => outputWidth - marginX - width };
        var y = layer.Position switch { WatermarkPosition.TopLeft or WatermarkPosition.TopCenter or WatermarkPosition.TopRight => marginY, WatermarkPosition.MiddleLeft or WatermarkPosition.Center or WatermarkPosition.MiddleRight => (outputHeight - height) / 2, _ => outputHeight - marginY - height };
        return new(x + layer.HorizontalOffset, y + layer.VerticalOffset, width, height);
    }

    private static BitmapSource Adjust(BitmapSource source, WatermarkColorAdjustments adjustments, CancellationToken cancellationToken)
    {
        if (adjustments == new WatermarkColorAdjustments()) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); converted.Freeze();
        var stride = converted.PixelWidth * 4; var pixels = new byte[stride * converted.PixelHeight]; converted.CopyPixels(pixels, stride, 0);
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if ((offset & 0xffff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var (hue, saturation, lightness) = RgbToHsl(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
            var rgb = HslToRgb(hue + adjustments.Hue, Math.Clamp(saturation + adjustments.Saturation, 0, 1), Math.Clamp(lightness + adjustments.Lightness, 0, 1));
            pixels[offset] = adjustments.Invert ? (byte)(255 - rgb.B) : rgb.B; pixels[offset + 1] = adjustments.Invert ? (byte)(255 - rgb.G) : rgb.G; pixels[offset + 2] = adjustments.Invert ? (byte)(255 - rgb.R) : rgb.R;
        }
        var output = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null, pixels, stride); output.Freeze(); return output;
    }

    private static Color Adjust(Color color, WatermarkColorAdjustments adjustments) { var hsl = RgbToHsl(color.R, color.G, color.B); var rgb = HslToRgb(hsl.H + adjustments.Hue, Math.Clamp(hsl.S + adjustments.Saturation, 0, 1), Math.Clamp(hsl.L + adjustments.Lightness, 0, 1)); return Color.FromArgb(color.A, adjustments.Invert ? (byte)(255 - rgb.R) : rgb.R, adjustments.Invert ? (byte)(255 - rgb.G) : rgb.G, adjustments.Invert ? (byte)(255 - rgb.B) : rgb.B); }
    private static Color ParseColor(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static FontWeight ParseWeight(string value) => string.Equals(value, "Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : string.Equals(value, "Normal", StringComparison.OrdinalIgnoreCase) ? FontWeights.Normal : FontWeights.SemiBold;
    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b) { var red=r/255d;var green=g/255d;var blue=b/255d;var max=Math.Max(red,Math.Max(green,blue));var min=Math.Min(red,Math.Min(green,blue));var d=max-min;var l=(max+min)/2;if(d==0)return(0,0,l);var s=d/(1-Math.Abs(2*l-1));var h=max==red?60*(((green-blue)/d)%6):max==green?60*((blue-red)/d+2):60*((red-green)/d+4);return((h+360)%360,s,l); }
    private static (byte R, byte G, byte B) HslToRgb(double h,double s,double l){h=(h%360+360)%360;var c=(1-Math.Abs(2*l-1))*s;var x=c*(1-Math.Abs((h/60)%2-1));var m=l-c/2;var (r,g,b)=h switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};return((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));}
}
