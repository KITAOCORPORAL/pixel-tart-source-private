using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Services;

public sealed record CollageExportResult(string OutputPath, int PixelWidth, int PixelHeight, long FileSizeBytes);

public sealed class CollageExportService
{
    public async Task<CollageExportResult> ExportAsync(CollageProject project, string requestedPath, CancellationToken cancellationToken = default)
    {
        if (project.Images.Count == 0) throw new InvalidOperationException("请先导入照片。");
        var outputPath = ResolveAutoNumberedPath(requestedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        try
        {
            var bitmap = Application.Current is null
                ? Render(project, cancellationToken)
                : await Application.Current.Dispatcher.InvokeAsync(() => Render(project, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true);
            BitmapEncoder encoder = string.Equals(project.Export.Format, "PNG", StringComparison.OrdinalIgnoreCase)
                ? new PngBitmapEncoder()
                : new JpegBitmapEncoder { QualityLevel = Math.Clamp(project.Export.JpegQuality, 1, 100) };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
            return new CollageExportResult(outputPath, bitmap.PixelWidth, bitmap.PixelHeight, stream.Length);
        }
        catch
        {
            if (File.Exists(outputPath)) try { File.Delete(outputPath); } catch { }
            throw;
        }
    }

    public RenderTargetBitmap Render(CollageProject project, CancellationToken cancellationToken = default)
    {
        var options = project.Export;
        var width = Math.Clamp(options.PixelWidth, 320, 12000);
        var height = Math.Clamp(options.PixelHeight, 320, 12000);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var background = options.TransparentBackground ? Brushes.Transparent : BrushFrom(options.BackgroundColor, Brushes.Black);
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
            var slots = ResolveSlots(project);
            var margin = Math.Clamp(options.OuterMargin, 0, Math.Min(width, height) / 3d);
            var spacing = Math.Clamp(options.Spacing, 0, Math.Min(width, height) / 4d);
            var availableWidth = Math.Max(1, width - margin * 2);
            var availableHeight = Math.Max(1, height - margin * 2);
            for (var index = 0; index < slots.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var slot = slots[index];
                var state = project.Images.FirstOrDefault(x => x.SlotId == slot.Id) ?? project.Images.ElementAtOrDefault(index);
                if (state is null || !File.Exists(state.SourcePath)) continue;
                var rect = new Rect(
                    margin + slot.X * availableWidth + spacing / 2,
                    margin + slot.Y * availableHeight + spacing / 2,
                    Math.Max(1, slot.Width * availableWidth - spacing),
                    Math.Max(1, slot.Height * availableHeight - spacing));
                var bitmap = LoadBitmap(state.SourcePath, 0);
                var brush = new ImageBrush(bitmap)
                {
                    Stretch = state.FitMode == CollageFitMode.Fit ? Stretch.Uniform : Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
                };
                var zoom = Math.Clamp(state.Zoom, .2, 8);
                brush.Viewbox = new Rect(
                    Math.Clamp(.5 - .5 / zoom + state.OffsetX, 0, Math.Max(0, 1 - 1 / zoom)),
                    Math.Clamp(.5 - .5 / zoom + state.OffsetY, 0, Math.Max(0, 1 - 1 / zoom)),
                    1 / zoom,
                    1 / zoom);
                var transforms = new TransformGroup();
                transforms.Children.Add(new ScaleTransform(state.FlipHorizontal ? -1 : 1, state.FlipVertical ? -1 : 1, .5, .5));
                transforms.Children.Add(new RotateTransform(state.Rotation, .5, .5));
                brush.RelativeTransform = transforms;
                var pen = options.BorderWidth > 0 ? new Pen(BrushFrom(options.BorderColor, Brushes.Black), options.BorderWidth) : null;
                if (options.Shadow)
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(55, 0, 0, 0)), null, new Rect(rect.X + 6, rect.Y + 8, rect.Width, rect.Height), options.CornerRadius, options.CornerRadius);
                dc.DrawRoundedRectangle(brush, pen, rect, options.CornerRadius, options.CornerRadius);
            }
        }
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    public static BitmapImage LoadBitmap(string path, int decodePixelWidth = 2048)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0) image.DecodePixelWidth = decodePixelWidth;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static IReadOnlyList<CollageSlot> ResolveSlots(CollageProject project)
    {
        if (project.Mode == CollageMode.Template) return CollageTemplateCatalog.Get(project.TemplateId).Slots;
        var dimensions = project.Images.Select(image =>
        {
            try { var bitmap = LoadBitmap(image.SourcePath, 512); return (Width: (double)bitmap.PixelWidth, Height: (double)bitmap.PixelHeight); }
            catch { return (Width: 1d, Height: 1d); }
        }).ToArray();
        var weights = dimensions.Select(x => project.Mode == CollageMode.VerticalStrip ? x.Height / Math.Max(1, x.Width) : x.Width / Math.Max(1, x.Height)).ToArray();
        var total = Math.Max(.001, weights.Sum());
        var cursor = 0d;
        var slots = new List<CollageSlot>();
        for (var index = 0; index < weights.Length; index++)
        {
            var share = weights[index] / total;
            slots.Add(project.Mode == CollageMode.VerticalStrip
                ? new CollageSlot((index + 1).ToString(), 0, cursor, 1, share)
                : new CollageSlot((index + 1).ToString(), cursor, 0, share, 1));
            cursor += share;
        }
        return slots;
    }

    private static Brush BrushFrom(string value, Brush fallback)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
        catch { return fallback; }
    }

    private static string ResolveAutoNumberedPath(string requestedPath)
    {
        var full = Path.GetFullPath(requestedPath);
        if (!File.Exists(full)) return full;
        var directory = Path.GetDirectoryName(full)!;
        var stem = Path.GetFileNameWithoutExtension(full);
        var extension = Path.GetExtension(full);
        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{index}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("无法生成不冲突的拼图文件名。");
    }
}
