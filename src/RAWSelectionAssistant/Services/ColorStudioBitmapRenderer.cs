using System.Windows.Media;
using System.Runtime.InteropServices;
using RAWSelectionAssistant.Core.Services.Color;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

/// <summary>WPF pixel adapter for the shared headless stack; both preview and existing export call this.</summary>
public static class ColorStudioBitmapRenderer
{
    public static BitmapSource Render(BitmapSource source, ColorAdjustmentStack stack, ReferenceLook? reference, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (source.Format == PixelFormats.Rgb128Float || source.Format == PixelFormats.Rgba128Float || source.Format == PixelFormats.Prgba128Float)
            return RenderFloat(source, stack, reference, token);
        if(source.Format.BitsPerPixel>32)
        {
            if((source.Format==PixelFormats.Rgb48 || source.Format==PixelFormats.Rgba64) &&
                (long)source.PixelWidth*source.PixelHeight>4L*1024*1024 && CanRenderBoundedExposure(stack,reference))
                return RenderBoundedExposure(source,stack,token);
            return RenderHighPrecision(source,stack,reference,token);
        }
        var input = HistogramService.EnsureBgra32(source);
        var bgra = new byte[input.PixelWidth * input.PixelHeight * 4];
        input.CopyPixels(bgra, input.PixelWidth * 4, 0);
        var rgb = new byte[input.PixelWidth * input.PixelHeight * 3];
        var alpha = new byte[input.PixelWidth * input.PixelHeight];
        for (var pixel = 0; pixel < rgb.Length / 3; pixel++)
        {
            rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4];
            alpha[pixel] = bgra[pixel * 4 + 3];
        }
        var buffer = new VisualPixelBuffer(input.PixelWidth, input.PixelHeight, rgb, alpha);
        var analysis = ColorStudioProcessingAnalysis.Create(buffer, stack, reference, token);
        var rendered = new ColorStudioRenderPipeline().Render(buffer, analysis, reference, stack, token, captureNodeDiagnostics: false);using var renderedOwner=rendered.ProcessingPixels;var result=rendered.Pixels;
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

    private static BitmapSource RenderFloat(BitmapSource source, ColorAdjustmentStack stack, ReferenceLook? reference, CancellationToken token)
    {
        var rgba = new FormatConvertedBitmap(source, PixelFormats.Rgba128Float, null, 0);
        var samples = new float[checked(source.PixelWidth * source.PixelHeight * 4)]; rgba.CopyPixels(samples, source.PixelWidth * 16, 0);
        var rgb = new float[samples.Length / 4 * 3];
        // WPF float formats are linear scRGB. The processing contract is encoded
        // sRGB float, so explicitly convert in both directions (alpha is linear).
        static float EncodeScRgb(float value) => (float)(value <= .0031308f ? value * 12.92 : 1.055 * Math.Pow(value, 1 / 2.4) - .055);
        static float DecodeSrgb(float value) => (float)(value <= .04045f ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4));
        for (var p = 0; p < rgb.Length / 3; p++) for (var c = 0; c < 3; c++) rgb[p * 3 + c] = EncodeScRgb(samples[p * 4 + c]);
        using var master = new RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer(source.PixelWidth, source.PixelHeight, rgb);
        var displayRgb = master.ToVisualRgb24(); var alpha = Enumerable.Range(0, source.PixelWidth * source.PixelHeight).Select(p => (byte)Math.Clamp(Math.Round(samples[p * 4 + 3] * 255), 0, 255)).ToArray();
        var display = new VisualPixelBuffer(source.PixelWidth, source.PixelHeight, displayRgb.Rgb24, alpha); var analysis = ColorStudioProcessingAnalysis.Create(display, stack, reference, token);
        using var processed = new ColorStudioRenderPipeline().Render(master, analysis, reference, stack, token, captureNodeDiagnostics: false, alpha: alpha).ProcessingPixels!;var result=processed.Rgb32;
        for (var p = 0; p < rgb.Length / 3; p++) for (var c = 0; c < 3; c++)
        {
            var index = p * 3 + c;
            // Avoid even a rounding-only round trip for neutral/untouched channels.
            if (result.Span[index] != rgb[index]) samples[p * 4 + c] = DecodeSrgb(result.Span[index]);
        }
        var output = BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Rgba128Float, null, samples, source.PixelWidth * 16); output.Freeze(); return output;
    }

    // Storage/ownership adaptation only. The existing point-effect implementation and
    // its RGB16 -> float -> RGB16 rounding are unchanged. Never tile a graph, mask,
    // reference-dependent operation or spatial tool by assuming pixel independence.
    private static bool CanRenderBoundedExposure(ColorAdjustmentStack stack, ReferenceLook? reference)
    {
        if(reference is not null || stack.Graph is not null || stack.ProcessingVersion!=2)return false;
        var enabled=stack.Normalize().Nodes.Where(node=>node.Enabled).ToArray();
        return enabled.Length==1 && enabled[0].Type==ColorStudioNodeType.BasicTone &&
            enabled[0].WideToolVersion==0 && enabled[0].FilmSettings is null &&
            enabled[0].NumericParameters.Keys.All(key=>key=="exposure") &&
            StudioLayerComposite.Identity(enabled[0]);
    }
    private static BitmapSource RenderBoundedExposure(BitmapSource source,ColorAdjustmentStack stack,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var node=stack.Normalize().Nodes.Single(item=>item.Enabled);
        var width=source.PixelWidth;var height=source.PixelHeight;var stride=checked(width*8);var length=checked(stride*height);
        var rowsPerBlock=Math.Max(1,Math.Min(height,1024*1024/stride));
        var rowBytes=new byte[checked(stride*rowsPerBlock)];
        var rgba=new FormatConvertedBitmap(source,PixelFormats.Rgba64,null,0);
        // Account temporary native output plus the detached WIC copy; the caller
        // retains the published bitmap after this operation releases its scratch.
        using var reservation=StudioGlobalResources.ReserveWork(checked((long)length*2+rowBytes.Length));
        using var pixels=GraphPixelAllocation.Enter();
        var memory=Marshal.AllocHGlobal(length);
        try
        {
            for(var y=0;y<height;y+=rowsPerBlock)
            {
                token.ThrowIfCancellationRequested();var rows=Math.Min(rowsPerBlock,height-y);var count=checked(width*rows);
                rgba.CopyPixels(new System.Windows.Int32Rect(0,y,width,rows),rowBytes,stride,0);
                var samples=MemoryMarshal.Cast<byte,ushort>(rowBytes.AsSpan(0,checked(count*8)));
                var input=GraphPixelAllocation.Allocate(checked(count*3));
                for(var p=0;p<count;p++)for(var c=0;c<3;c++)input[p*3+c]=samples[p*4+c]/65535f;
                using var master=new HighBitDepthImageBuffer(width,rows,input,"16","sRGB");
                using var processed=ColorStudioRenderPipeline.ApplyPointEffect(master,node,token);
                var result=processed.Rgb32.Span;
                for(var p=0;p<count;p++)for(var c=0;c<3;c++)
                    samples[p*4+c]=(ushort)Math.Clamp(Math.Round(result[p*3+c]*65535),0,65535);
                // Preserve the original 16-bit alpha samples exactly.
                Marshal.Copy(rowBytes,0,IntPtr.Add(memory,checked(y*stride)),checked(rows*stride));
            }
            token.ThrowIfCancellationRequested();
            var output=BitmapSource.Create(width,height,source.DpiX,source.DpiY,PixelFormats.Rgba64,null,memory,length,stride);
            output.Freeze();return output;
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static BitmapSource RenderHighPrecision(BitmapSource source,ColorAdjustmentStack stack,ReferenceLook? reference,CancellationToken token)
    {
        var rgba=new FormatConvertedBitmap(source,PixelFormats.Rgba64,null,0);var bytes=new byte[rgba.PixelWidth*rgba.PixelHeight*8];rgba.CopyPixels(bytes,rgba.PixelWidth*8,0);
        var samples=new ushort[bytes.Length/2];Buffer.BlockCopy(bytes,0,samples,0,bytes.Length);var rgb=new ushort[samples.Length/4*3];
        for(var p=0;p<rgb.Length/3;p++)for(var c=0;c<3;c++)rgb[p*3+c]=samples[p*4+c];
        using var master=new RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer(source.PixelWidth,source.PixelHeight,rgb,"16","sRGB");
        var displayRgb=master.ToVisualRgb24();var alpha=Enumerable.Range(0,source.PixelWidth*source.PixelHeight).Select(p=>(byte)(samples[p*4+3]/257)).ToArray();
        var display=new VisualPixelBuffer(source.PixelWidth,source.PixelHeight,displayRgb.Rgb24,alpha);var analysis=ColorStudioProcessingAnalysis.Create(display,stack,reference,token);
        using var processed=new ColorStudioRenderPipeline().Render(master,analysis,reference,stack,token,captureNodeDiagnostics:false,alpha:alpha).ProcessingPixels!;var result=processed.ToRgb48();
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
        var analysis = ColorStudioProcessingAnalysis.Create(buffer, stack, reference, token);
        var rendered = new ColorStudioRenderPipeline().Render(buffer, analysis, reference, stack, token);
        if (rendered.NodeInputs is null || !rendered.NodeInputs.TryGetValue(selected.Id, out var nodeInput)) return Render(source, stack, reference, token);
        var selection = new ColorStudioRenderPipeline().ShowSelection(nodeInput, selected, token);
        for (var pixel = 0; pixel < selection.PixelCount; pixel++)
        { bgra[pixel * 4] = selection.Rgb24.Span[pixel * 3 + 2]; bgra[pixel * 4 + 1] = selection.Rgb24.Span[pixel * 3 + 1]; bgra[pixel * 4 + 2] = selection.Rgb24.Span[pixel * 3]; }
        var output = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, bgra, input.PixelWidth * 4);
        output.Freeze(); return output;
    }
}
