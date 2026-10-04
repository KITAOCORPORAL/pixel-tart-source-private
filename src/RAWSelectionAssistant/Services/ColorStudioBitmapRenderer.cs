using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

/// <summary>WPF pixel adapter for the shared headless stack; both preview and existing export call this.</summary>
public static class ColorStudioBitmapRenderer
{
    public static BitmapSource Render(BitmapSource source, ColorAdjustmentStack stack, ReferenceLook? reference, CancellationToken token = default)
    {
        if(source.Format.BitsPerPixel>32) return RenderHighPrecision(source,stack,reference,token);
        var input = HistogramService.EnsureBgra32(source);
        var bgra = new byte[input.PixelWidth * input.PixelHeight * 4];
        input.CopyPixels(bgra, input.PixelWidth * 4, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < rgb.Length / 3; pixel++)
        {
            rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4];
        }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(buffer), buffer));
        var result = new ColorStudioRenderPipeline().Render(buffer, analysis, reference, stack, token).Pixels;
        for (var pixel = 0; pixel < result.PixelCount; pixel++)
        {
            bgra[pixel * 4] = result.Rgb24.Span[pixel * 3 + 2];
            bgra[pixel * 4 + 1] = result.Rgb24.Span[pixel * 3 + 1];
            bgra[pixel * 4 + 2] = result.Rgb24.Span[pixel * 3];
        }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, input.PixelWidth * 4);
        output.Freeze();
        return output;
    }

    private static BitmapSource RenderHighPrecision(BitmapSource source,ColorAdjustmentStack stack,ReferenceLook? reference,CancellationToken token)
    {
        var rgba=new FormatConvertedBitmap(source,PixelFormats.Rgba64,null,0);var bytes=new byte[rgba.PixelWidth*rgba.PixelHeight*8];rgba.CopyPixels(bytes,rgba.PixelWidth*8,0);
        var samples=new ushort[bytes.Length/2];Buffer.BlockCopy(bytes,0,samples,0,bytes.Length);var rgb=new ushort[samples.Length/4*3];
        for(var p=0;p<rgb.Length/3;p++)for(var c=0;c<3;c++)rgb[p*3+c]=samples[p*4+c];
        var master=new RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer(source.PixelWidth,source.PixelHeight,rgb,"16","sRGB");
        var display=master.ToVisualRgb24();var analysis=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),VisualAnalysisFingerprint.Compute(display),display),token);
        var result=new ColorStudioRenderPipeline().Render(master,analysis,reference,stack,token).ProcessingPixels!.ToRgb48();
        for(var p=0;p<result.Length/3;p++)for(var c=0;c<3;c++)samples[p*4+c]=result[p*3+c];
        Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);var output=BitmapSource.Create(source.PixelWidth,source.PixelHeight,source.DpiX,source.DpiY,PixelFormats.Rgba64,null,bytes,source.PixelWidth*8);output.Freeze();return output;
    }
    public static BitmapSource ShowSelection(BitmapSource source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        var input = HistogramService.EnsureBgra32(source);
        var bgra = new byte[input.PixelWidth * input.PixelHeight * 4];
        input.CopyPixels(bgra, input.PixelWidth * 4, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < rgb.Length / 3; pixel++)
        {
            rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4];
        }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        var result = new ColorStudioRenderPipeline().ShowSelection(buffer, node, token);
        for (var pixel = 0; pixel < result.PixelCount; pixel++)
        {
            bgra[pixel * 4] = result.Rgb24.Span[pixel * 3 + 2];
            bgra[pixel * 4 + 1] = result.Rgb24.Span[pixel * 3 + 1];
            bgra[pixel * 4 + 2] = result.Rgb24.Span[pixel * 3];
        }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, input.PixelWidth * 4);
        output.Freeze();
        return output;
    }

    public static BitmapSource RenderSelection(BitmapSource source, ColorAdjustmentStack stack, ReferenceLook? reference, ColorAdjustmentStackNode selected, CancellationToken token = default)
    {
        var input = HistogramService.EnsureBgra32(source);
        var bgra = new byte[input.PixelWidth * input.PixelHeight * 4]; input.CopyPixels(bgra, input.PixelWidth * 4, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        for (var pixel = 0; pixel < rgb.Length / 3; pixel++)
        { rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4]; }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(buffer), buffer));
        var rendered = new ColorStudioRenderPipeline().Render(buffer, analysis, reference, stack, token);
        if (rendered.NodeInputs is null || !rendered.NodeInputs.TryGetValue(selected.Id, out var nodeInput)) return Render(source, stack, reference, token);
        var selection = new ColorStudioRenderPipeline().ShowSelection(nodeInput, selected, token);
        for (var pixel = 0; pixel < selection.PixelCount; pixel++)
        { bgra[pixel * 4] = selection.Rgb24.Span[pixel * 3 + 2]; bgra[pixel * 4 + 1] = selection.Rgb24.Span[pixel * 3 + 1]; bgra[pixel * 4 + 2] = selection.Rgb24.Span[pixel * 3]; }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, input.PixelWidth * 4);
        output.Freeze(); return output;
    }
}
