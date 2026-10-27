using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services.Publishing;

public sealed partial class WpfPublishingRenderer : IPublishingRenderer, IPublishingOutputInspector, IPublishingPixelVerifier
{
    public Task<string> VerifyPixelsAsync(string source,string output,PublishingOptions options,CancellationToken token)=>Task.Run(()=>
    {
        if(IsManagedDocumentSource(source))return VerifyManagedDocument(source,output,options,token);
        if(options.OutputFormat==PublishingOutputFormat.Tiff && (!options.EffectiveDimensions.Enabled||options.EffectiveDimensions.Mode==PublishingSizeMode.Original) && (!options.WatermarksEnabled||!options.EffectiveWatermarkLayers.Any(x=>x.Enabled)))
        {using var a=File.OpenRead(source);using var b=File.OpenRead(output);var original=TiffStreamValidator.Validate(a,token);var actual=TiffStreamValidator.Validate(b,token);if(original.Width!=actual.Width||original.Height!=actual.Height||original.Orientation!=actual.Orientation||original.PixelSha256!=actual.PixelSha256||!original.IccProfile.AsSpan().SequenceEqual(actual.IccProfile))throw new InvalidDataException("Publishing TIFF16 contract mismatch.");return System.Text.Json.JsonSerializer.Serialize(new{mode="StreamingPixelsExact",actual.Width,actual.Height,actual.BitsPerSample,actual.Orientation,actual.PixelSha256});}
        using var input=File.OpenRead(source);using var result=File.OpenRead(output);
        var aFrame=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];var bFrame=BitmapDecoder.Create(result,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
        if(bFrame.ColorContexts is not {Count:>0})throw new InvalidDataException("Publishing output has no sRGB profile.");
        using(var icc=bFrame.ColorContexts[0].OpenProfileStream()){using var memory=new MemoryStream();icc.CopyTo(memory);if(!memory.ToArray().AsSpan().SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Publishing output profile is not sRGB.");}
        if((options.EffectiveDimensions.Enabled&&options.EffectiveDimensions.Mode!=PublishingSizeMode.Original)||(options.WatermarksEnabled&&options.EffectiveWatermarkLayers.Any(x=>x.Enabled)))
        {
            // Production verification rerenders the frozen recipe losslessly and reads BOTH encoded files.
            // Independent algorithm/font oracles belong to tests, not this same-renderer check.
            var expected=Path.Combine(Path.GetTempPath(),"kitao-pixel-reference-"+Guid.NewGuid()+".tif");
            try
            {
                var referenceOptions=options.OutputFormat==PublishingOutputFormat.Tiff?options:options with{OutputFormat=PublishingOutputFormat.Png};
                Render(source,expected,referenceOptions,token);
                using var referenceStream=File.OpenRead(expected);
                var reference=BitmapDecoder.Create(referenceStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                if(reference.PixelWidth!=bFrame.PixelWidth||reference.PixelHeight!=bFrame.PixelHeight)throw new InvalidDataException("Publishing recipe dimensions mismatch.");
                var high=options.OutputFormat==PublishingOutputFormat.Tiff&&options.OutputBitDepth==PublishingOutputBitDepth.Sixteen;
                var pixelFormat=high?PixelFormats.Rgb48:PixelFormats.Rgb24;
                var expectedPixels=new FormatConvertedBitmap(reference,pixelFormat,null,0);var actualPixels=new FormatConvertedBitmap(bFrame,pixelFormat,null,0);
                var rowBytes=reference.PixelWidth*(high?6:3);var er=new byte[rowBytes];var ar=new byte[rowBytes];double total=0,squared=0;var maxError=0;
                for(var y=0;y<reference.PixelHeight;y++)
                {
                    token.ThrowIfCancellationRequested();var rect=new Int32Rect(0,y,reference.PixelWidth,1);expectedPixels.CopyPixels(rect,er,rowBytes,0);actualPixels.CopyPixels(rect,ar,rowBytes,0);
                    for(var i=0;i<rowBytes;i+=high?2:1)
                    {
                        var e=high?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(er.AsSpan(i)):er[i];var a=high?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(ar.AsSpan(i)):ar[i];
                        var delta=Math.Abs(e-a);total+=delta;squared+=(double)delta*delta;maxError=Math.Max(maxError,delta);
                    }
                }
                var count=(double)reference.PixelWidth*reference.PixelHeight*3;var mean=total/count;var rms=Math.Sqrt(squared/count);
                if(options.OutputFormat==PublishingOutputFormat.Jpeg?(mean>5||rms>15):maxError!=0)throw new InvalidDataException("Publishing encoded recipe pixels mismatch.");
                return System.Text.Json.JsonSerializer.Serialize(new{mode="EncodedRecipePixelComparison",independentAlgorithmOracle=false,bitDepth=high?16:8,mean,rms,maxError,reference.PixelWidth,reference.PixelHeight});
            }
            finally{if(File.Exists(expected))File.Delete(expected);}
        }
        var oriented=StudioQuickExport.ApplyOrientation(aFrame,ReadOrientation(aFrame));var before=new FormatConvertedBitmap(oriented,PixelFormats.Rgb24,null,0);var after=new FormatConvertedBitmap(bFrame,PixelFormats.Rgb24,null,0);
        if(before.PixelWidth!=after.PixelWidth||before.PixelHeight!=after.PixelHeight)throw new InvalidDataException("Publishing orientation/size mismatch.");
        var stride=before.PixelWidth*3;var left=new byte[stride];var right=new byte[stride];double sum=0,squares=0;var max=0;
        for(var y=0;y<before.PixelHeight;y++){token.ThrowIfCancellationRequested();var rectangle=new Int32Rect(0,y,before.PixelWidth,1);before.CopyPixels(rectangle,left,stride,0);after.CopyPixels(rectangle,right,stride,0);for(var i=0;i<stride;i++){var error=Math.Abs(left[i]-right[i]);sum+=error;squares+=(double)error*error;max=Math.Max(max,error);}}
        var n=(double)stride*before.PixelHeight;var mae=sum/n;var rmse=Math.Sqrt(squares/n);
        if(options.OutputFormat==PublishingOutputFormat.Png&&max!=0)throw new InvalidDataException("Publishing PNG8 lossless mismatch.");
        if(options.OutputFormat==PublishingOutputFormat.Jpeg&&(mae>5||rmse>15))throw new InvalidDataException("Publishing JPEG exceeds MAE5/RMSE15 budget.");
        return System.Text.Json.JsonSerializer.Serialize(new{mode=options.OutputFormat==PublishingOutputFormat.Png?"RGB8Exact":"JPEG444Lossy",quality=options.OutputFormat==PublishingOutputFormat.Jpeg?options.EffectiveDimensions.JpegQuality:(int?)null,before.PixelWidth,before.PixelHeight,mae,rmse,max});
    },token);
    public Task<(int Width,int Height,int BitDepth)> InspectAsync(string path,CancellationToken token)=>Task.Run(()=>
    {
        token.ThrowIfCancellationRequested();using var stream=File.OpenRead(path);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];return(frame.PixelWidth,frame.PixelHeight,frame.Format.BitsPerPixel>32?16:8);
    },token);
    public async Task RenderAsync(string sourcePath, string destinationPath, PublishingOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); options.Validate();
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(cancellationToken);
        try{
            using var input=File.OpenRead(sourcePath);
            var header=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];
            var size=OutputSize(header.PixelWidth,header.PixelHeight,options.EffectiveDimensions);
            using var reservation=StudioGlobalResources.ReserveWork(checked((long)header.PixelWidth*header.PixelHeight*24+(long)size.Width*size.Height*16+32L*1024*1024));
            if(File.Exists(destinationPath))throw new IOException("出版目的已存在，未覆盖。");
            var ownStage=destinationPath+".render-"+Guid.NewGuid().ToString("N");
            try { await Task.Run(() => Render(sourcePath, ownStage, options, cancellationToken), cancellationToken); cancellationToken.ThrowIfCancellationRequested(); File.Move(ownStage,destinationPath,false); }
            finally { if(File.Exists(ownStage))File.Delete(ownStage); }
        }
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }

    public Task VerifyAsync(string imagePath, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        if (decoder.Frames.Count == 0 || decoder.Frames[0].PixelWidth <= 0 || decoder.Frames[0].PixelHeight <= 0) throw new InvalidDataException("发布文件无法读取。");
    }, cancellationToken);

    private static void Render(string sourcePath, string destinationPath, PublishingOptions options, CancellationToken cancellationToken)
    {
        if(IsManagedDocumentSource(sourcePath)){RenderManagedDocument(sourcePath,destinationPath,options,cancellationToken);return;}
        var isProfessional=false;
        if(File.Exists(sourcePath+".kitao-export.json"))
        {
            var upstream=System.Text.Json.JsonSerializer.Deserialize<StudioExportReceipt>(File.ReadAllText(sourcePath+".kitao-export.json"));
            if(upstream?.RecipeSnapshot?.RawDefinition is not null)
            {
                isProfessional=true;

                if(!string.Equals(options.ColorSpaceProfile,"sRGB",StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("PV4 Publishing仅支持sRGB。");
                if(options.OutputFormat==PublishingOutputFormat.Jpeg&&options.OutputBitDepth!=PublishingOutputBitDepth.Eight)throw new NotSupportedException("JPEG仅支持8位。");
                if(options.OutputFormat==PublishingOutputFormat.Png&&options.OutputBitDepth!=PublishingOutputBitDepth.Eight)throw new NotSupportedException("Publishing PNG16未接线，未伪装8位输出。");
                if(options.OutputFormat==PublishingOutputFormat.Tiff && (!options.EffectiveDimensions.Enabled||options.EffectiveDimensions.Mode==PublishingSizeMode.Original) && (!options.WatermarksEnabled||!options.EffectiveWatermarkLayers.Any(x=>x.Enabled)))
                {
                    using var input=File.OpenRead(sourcePath);var info=TiffStreamValidator.Validate(input,cancellationToken);input.Position=0;
                    using var reader=new BinaryReader(input,System.Text.Encoding.ASCII,true);input.Position=4;input.Position=reader.ReadUInt32();var count=reader.ReadUInt16();uint pixelOffset=0;
                    for(var i=0;i<count;i++){var tag=reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt32();var value=reader.ReadUInt32();if(tag==273)pixelOffset=value;}
                    input.Position=pixelOffset;using var streamedOutput=new FileStream(destinationPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                    TiffExport.WriteRgb48Rows(streamedOutput,info.Width,info.Height,(y,row)=>{cancellationToken.ThrowIfCancellationRequested();input.ReadExactly(row.Span);},new(IccProfile:info.IccProfile,Dpi:options.EffectiveDimensions.Dpi,Orientation:(ushort)info.Orientation),cancellationToken);streamedOutput.Flush(true);return;
                }
            }
        }
        var decoder = BitmapDecoder.Create(new Uri(Path.GetFullPath(sourcePath)), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var originalFrame = decoder.Frames[0];
        BitmapSource frame = originalFrame;
        if (options.OutputFormat == PublishingOutputFormat.Tiff && options.OutputBitDepth == PublishingOutputBitDepth.Sixteen)
        {
            WriteTiff16(sourcePath, originalFrame, destinationPath, options, cancellationToken);
            return;
        }
        if(isProfessional && (!options.EffectiveDimensions.Enabled||options.EffectiveDimensions.Mode==PublishingSizeMode.Original) && (!options.WatermarksEnabled||!options.EffectiveWatermarkLayers.Any(x=>x.Enabled)))
        {
            frame=StudioQuickExport.ApplyOrientation(frame,ReadOrientation(originalFrame));
            if(options.OutputFormat==PublishingOutputFormat.Jpeg){ProfessionalWicRowEncoder.WriteBitmap(destinationPath,frame,options.EffectiveDimensions.JpegQuality,cancellationToken,options.EffectiveDimensions.Dpi);return;}
            ProfessionalWicRowEncoder.WriteBitmap(destinationPath,frame,95,cancellationToken,options.EffectiveDimensions.Dpi,png:true);return;
        }
        frame = StudioQuickExport.ApplyOrientation(frame,ReadOrientation(originalFrame));
        frame = ApplyRequestedProfile(frame, options.ColorSpaceProfile, PixelFormats.Rgb24);
        var (width, height) = OutputSize(frame.PixelWidth, frame.PixelHeight, options.EffectiveDimensions);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            // Drawing coordinates are output pixels. WPF drawing units are 1/96 inch;
            // compensate for output metadata DPI so the entire bitmap is covered.
            var pixelToDip = 96d / options.EffectiveDimensions.Dpi;
            context.PushTransform(new ScaleTransform(pixelToDip, pixelToDip));
            context.DrawImage(frame, new Rect(0, 0, width, height));
            if (options.WatermarksEnabled)
                foreach (var layer in options.EffectiveWatermarkLayers.Where(layer => layer.Enabled)) DrawLayer(context, layer, width, height, cancellationToken);
            context.Pop();
        }
        var bitmap = new RenderTargetBitmap(width, height, options.EffectiveDimensions.Dpi, options.EffectiveDimensions.Dpi, PixelFormats.Pbgra32); bitmap.Render(drawing); bitmap.Freeze();
        var metadata = CreateOutputMetadata(originalFrame, options);
        BitmapEncoder encoder = options.OutputFormat switch
        {
            PublishingOutputFormat.Png => new PngBitmapEncoder(),
            PublishingOutputFormat.Tiff => new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw },
            _ => new JpegBitmapEncoder { QualityLevel = options.EffectiveDimensions.JpegQuality }
        };
        var profilePath = ResolveProfilePath(options.ColorSpaceProfile)
            ?? throw new InvalidDataException($"未找到请求的输出 ICC 配置：{options.ColorSpaceProfile}");
        encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, [new System.Windows.Media.ColorContext(new Uri(profilePath))]));
        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None); encoder.Save(output); output.Flush(true);
    }

    private static void WriteTiff16(string sourcePath, BitmapFrame frame, string destinationPath, PublishingOptions options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var sourceIsHighPrecision = frame.Format == PixelFormats.Rgb48 || frame.Format == PixelFormats.Rgba64 || frame.Format == PixelFormats.Prgba64 || frame.Format == PixelFormats.Bgr101010;
        var orientation = ReadOrientation(frame);
        var convertedFrame = ApplyRequestedProfile(frame, options.ColorSpaceProfile, PixelFormats.Rgb48);
        var dimensions = options.EffectiveDimensions;
        if (options.WatermarksEnabled && options.EffectiveWatermarkLayers.Any(layer => layer.Enabled))
            throw new NotSupportedException("TIFF16 水印的高精度合成尚未接入；已停止输出以避免 8-bit 量化。");
        var pixels = ToHighBitDepth(convertedFrame, token);
        if (!sourceIsHighPrecision)
            pixels = new HighBitDepthImageBuffer(pixels.Width, pixels.Height, pixels.Rgb32, "8-bit source promoted", pixels.WorkingColorSpace, orientation);
        var (width, height) = OutputSize(convertedFrame.PixelWidth, convertedFrame.PixelHeight, dimensions);
        if (width != pixels.Width || height != pixels.Height) pixels = ResizeRgb48(pixels, width, height, token);

        // ICC describes the output pixels, not personal metadata. Strip must not remove it.
        var icc = ProfileBytes(options.ColorSpaceProfile);
        var tiffOptions = new TiffExportOptions(
            TiffBitDepth.Sixteen,
            icc,
            Dpi: dimensions.Dpi,
            Orientation: orientation,
            RequireHighBitDepthSource: sourceIsHighPrecision);
        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        TiffExport.WriteRgb48(output, pixels, tiffOptions, token);
        output.Flush(true);
    }

    private static HighBitDepthImageBuffer ResizeRgb48(HighBitDepthImageBuffer source, int width, int height, CancellationToken token)
    {
        var input = source.Rgb32.Span;
        var output = new float[checked(width * height * 3)];
        for (var y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            var sy = Math.Clamp((y + .5) * source.Height / height - .5, 0, source.Height - 1);
            var y0 = (int)sy; var y1 = Math.Min(y0 + 1, source.Height - 1); var fy = (float)(sy - y0);
            for (var x = 0; x < width; x++)
            {
                var sx = Math.Clamp((x + .5) * source.Width / width - .5, 0, source.Width - 1);
                var x0 = (int)sx; var x1 = Math.Min(x0 + 1, source.Width - 1); var fx = (float)(sx - x0);
                for (var channel = 0; channel < 3; channel++)
                {
                    var top = input[(y0 * source.Width + x0) * 3 + channel] * (1 - fx) + input[(y0 * source.Width + x1) * 3 + channel] * fx;
                    var bottom = input[(y1 * source.Width + x0) * 3 + channel] * (1 - fx) + input[(y1 * source.Width + x1) * 3 + channel] * fx;
                    output[(y * width + x) * 3 + channel] = top * (1 - fy) + bottom * fy;
                }
            }
        }
        return new(width, height, output, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
    }

    private static HighBitDepthImageBuffer ToHighBitDepth(BitmapSource frame, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (frame.Format == PixelFormats.Rgb48 || frame.Format == PixelFormats.Rgba64 || frame.Format == PixelFormats.Prgba64 || frame.Format == PixelFormats.Bgr101010)
        {
            var converted = frame.Format == PixelFormats.Rgb48 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Rgb48, null, 0);
            converted.Freeze();
            var stride = converted.PixelWidth * 6; var bytes = new byte[stride * converted.PixelHeight]; converted.CopyPixels(bytes, stride, 0);
            var values = new ushort[converted.PixelWidth * converted.PixelHeight * 3]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return new HighBitDepthImageBuffer(converted.PixelWidth, converted.PixelHeight, values, "16", "sRGB");
        }
        throw new NotSupportedException("TIFF16 需要高精度源像素；8-bit 源不能冒充高精度 TIFF16。");
    }

    private static VisualPixelBuffer ToVisualBuffer(BitmapSource source, CancellationToken token)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0); converted.Freeze();
        var stride = converted.PixelWidth * 3; var bgr = new byte[stride * converted.PixelHeight]; converted.CopyPixels(bgr, stride, 0);
        var rgb = new byte[bgr.Length];
        for (var i = 0; i < bgr.Length; i += 3) { if ((i & 0x7fff) == 0) token.ThrowIfCancellationRequested(); rgb[i] = bgr[i + 2]; rgb[i + 1] = bgr[i + 1]; rgb[i + 2] = bgr[i]; }
        return new VisualPixelBuffer(converted.PixelWidth, converted.PixelHeight, rgb);
    }

    private static BitmapSource ApplyRequestedProfile(BitmapSource frame, string requestedProfile, PixelFormat? outputFormat = null)
    {
        var target = ResolveProfilePath(requestedProfile);
        if (target is null) throw new InvalidDataException($"未找到请求的输出 ICC 配置：{requestedProfile}");
        var targetFormat = outputFormat ?? PixelFormats.Pbgra32;
        if (frame.Format == PixelFormats.Rgb48 && targetFormat != PixelFormats.Rgb48)
        {
            // WIC does not accept RGB48 -> RGB24/Pbgra32 as one ColorConvertedBitmap operation.
            // Convert in RGB48, then quantize only for the explicitly 8-bit JPEG/PNG output.
            var highPrecision = ApplyRequestedProfile(frame, requestedProfile, PixelFormats.Rgb48);
            var display = new FormatConvertedBitmap(highPrecision, targetFormat, null, 0);
            display.Freeze();
            return display;
        }
        var source = frame is BitmapFrame bitmapFrame && bitmapFrame.ColorContexts is { Count: > 0 } contexts
            ? contexts[0]
            : new System.Windows.Media.ColorContext(new Uri(ProfilePath("sRGB")));
        var converted = new ColorConvertedBitmap(frame, source, new System.Windows.Media.ColorContext(new Uri(target)), targetFormat);
        converted.Freeze();
        return converted;
    }

    private static BitmapMetadata? CreateOutputMetadata(BitmapSource frame, PublishingOptions options)
    {
        if (options.MetadataPolicy == ExportRecipeMetadataPolicy.Strip || frame.Metadata is not BitmapMetadata original) return null;
        try
        {
            if (options.MetadataPolicy == ExportRecipeMetadataPolicy.Preserve) return original.Clone() as BitmapMetadata;
            var format = frame is BitmapFrame bitmapFrame ? bitmapFrame.Decoder.CodecInfo.FileExtensions.TrimStart('.') : "png";
            var metadata = new BitmapMetadata(format.Equals("jpg", StringComparison.OrdinalIgnoreCase) || format.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png");
            if (!string.IsNullOrWhiteSpace(original.Copyright)) metadata.Copyright = original.Copyright;
            if (original.Author is { Count: > 0 }) metadata.Author = new System.Collections.ObjectModel.ReadOnlyCollection<string>(original.Author.ToArray());
            if (!string.IsNullOrWhiteSpace(original.Comment)) metadata.Comment = original.Comment;
            return metadata;
        }
        catch (Exception error) when (error is NotSupportedException or InvalidOperationException or ArgumentException) { return null; }
    }

    private static ReadOnlyMemory<byte> ProfileBytes(string requestedProfile)
    {
        var path = ResolveProfilePath(requestedProfile);
        return path is null ? ReadOnlyMemory<byte>.Empty : File.ReadAllBytes(path);
    }

    public static IReadOnlyList<string> AvailableOutputProfiles => new[] { "sRGB", "Adobe RGB (1998)" }
        .Where(profile => ResolveProfilePath(profile) is { } path && File.Exists(path)).ToArray();

    private static string? ResolveProfilePath(string requested) => requested switch
    {
        "sRGB" or "sRGB IEC61966-2.1" => ProfilePath("sRGB"),
        "Adobe RGB (1998)" or "AdobeRGB" => new[] { @"C:\Program Files (x86)\Common Files\Adobe\Color\Profiles\Recommended\AdobeRGB1998.icc", @"C:\Program Files\Capture One\Capture One\Color Profiles\Common\Adobe RGB (1998).icm" }.FirstOrDefault(File.Exists),
        _ => null
    };

    private static string ProfilePath(string name) => name == "sRGB" ? @"C:\Windows\System32\spool\drivers\color\sRGB Color Space Profile.icm" : name;

    private static ushort ReadOrientation(BitmapFrame frame)
    {
        if(frame.Metadata is not BitmapMetadata metadata)return 1;
        foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})
        {try{if(metadata.ContainsQuery(query)){var value=Convert.ToInt32(metadata.GetQuery(query),CultureInfo.InvariantCulture);if(value is >=1 and <=8)return(ushort)value;}}catch(Exception error)when(error is NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException){} }
        return 1;
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
        if (options.Mode == PublishingSizeMode.ShortestEdge)
        {
            var shortEdgeScale = Math.Min(1, options.LongestEdge / (double)Math.Min(sourceWidth, sourceHeight));
            return (Math.Max(1, (int)Math.Round(sourceWidth * shortEdgeScale)), Math.Max(1, (int)Math.Round(sourceHeight * shortEdgeScale)));
        }
        var scale = Math.Min(1, options.LongestEdge / (double)Math.Max(sourceWidth, sourceHeight));
        return (Math.Max(1, (int)Math.Round(sourceWidth * scale)), Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }

    private static void DrawLayer(DrawingContext context, WatermarkLayer layer, int outputWidth, int outputHeight, CancellationToken cancellationToken,bool managed=false)
    {
        layer.Validate(); cancellationToken.ThrowIfCancellationRequested();
        if (layer.Type == WatermarkLayerType.Image)
        {
            var source = new BitmapImage(); source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.UriSource = new Uri(Path.GetFullPath(layer.ImagePath!)); source.EndInit(); source.Freeze();
            BitmapSource interpreted=source;
            if(managed)
            {
                using var cms=new RAWSelectionAssistant.Core.Services.Color.ControlledIcc.LcmsRgbEngine();
                var inputProfile=DocumentColorWorkflowV1.InspectInput(layer.ImagePath!,cms).Profile;
                using var document=DocumentColorWorkflowV1.ReadIndependentAlphaAsync(layer.ImagePath!,inputProfile,cms.StandardProfile("sRGB"),null,cancellationToken).GetAwaiter().GetResult();
                using var upright=document.NormalizeOrientation(cancellationToken);var values=upright.Pixels.Rgb32.Span;var alpha=upright.Alpha.Span;var b=new byte[upright.Pixels.PixelCount*4];for(int i=0;i<upright.Pixels.PixelCount;i++){float a=alpha.IsEmpty?1:alpha[i];for(int c=0;c<3;c++)b[i*4+c]=(byte)Math.Clamp(Math.Round(values[i*3+2-c]*a*255),0,255);b[i*4+3]=(byte)Math.Round(a*255);}
                interpreted=BitmapSource.Create(upright.Pixels.Width,upright.Pixels.Height,96,96,PixelFormats.Pbgra32,null,b,upright.Pixels.Width*4);interpreted.Freeze();
            }
            var adjusted = Adjust(interpreted, layer.EffectiveColorAdjustments, cancellationToken);
            var width = outputWidth * layer.WidthPercent / 100d; var height = width * adjusted.PixelHeight / adjusted.PixelWidth;
            var rect = Position(layer, outputWidth, outputHeight, width, height);
            context.PushOpacity(layer.Opacity); context.DrawImage(adjusted, rect); context.Pop();
            return;
        }

        var typeface = new Typeface(new FontFamily(layer.FontFamily), FontStyles.Normal, ParseWeight(layer.FontWeight), FontStretches.Normal);
        var brush = new SolidColorBrush(Adjust(ParseColor(layer.Color), layer.EffectiveColorAdjustments)); brush.Freeze();
        var fontSize = layer.FontSize * outputWidth / 1920d;
        var text = new FormattedText(layer.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0);
        var elements = new List<FormattedText>();
        var iterator = StringInfo.GetTextElementEnumerator(layer.Text);
        while (iterator.MoveNext()) elements.Add(new FormattedText(iterator.GetTextElement(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0));
        var advance = layer.LetterSpacing * outputWidth / 1920d;
        var textWidth = Math.Max(1, elements.Sum(item => item.WidthIncludingTrailingWhitespace) + Math.Max(0, elements.Count - 1) * advance);
        var location = Position(layer, outputWidth, outputHeight, textWidth, text.Height).Location;
        context.PushOpacity(layer.Opacity);
        if (Math.Abs(layer.LetterSpacing) < .001) context.DrawText(text, location);
        else
        {
            foreach (var element in elements)
            {
                context.DrawText(element, location);
                location.X += element.WidthIncludingTrailingWhitespace + advance;
            }
        }
        context.Pop();
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
