using MetadataExtractor;
using System.Drawing;
using System.Security.Cryptography;
using Sdcb.LibRaw;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

public interface IProfessionalRawDecoder
{
    Task<ProfessionalRawMaster> DecodeAsync(string path, ProfessionalRawDefinition definition, CancellationToken token = default);
}

public sealed class ProfessionalRawDecoder(Action<string>? stage = null) : IProfessionalRawDecoder
{
    public async Task<ProfessionalRawMaster> DecodeAsync(string path, ProfessionalRawDefinition definition, CancellationToken token = default)
    {
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
        try{return await Task.Run(()=>Decode(path,definition.Validate(),token),token);}
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }

    public static double[] ResolveWhiteBalance(double[] camera, bool alreadyApplied, ProfessionalRawDefinition definition)
    {
        definition.Validate();
        if (alreadyApplied) throw new NotSupportedException("此 RAW 已应用白平衡，首期专业路径拒绝再次应用相机通道增益。");
        if (camera.Length != 4 || camera.Take(3).Any(x => !double.IsFinite(x) || x <= 0))
            throw new InvalidDataException("缺少可用相机白平衡，未回退旧处理。");
        var applied = (double[])camera.Clone();
        if (!double.IsFinite(applied[3]) || applied[3] <= 0) applied[3] = applied[1];
        if (definition.WhiteBalance == "CameraGains")
        {
            applied[0] *= definition.RedGain; applied[1] *= definition.GreenGain;
            applied[2] *= definition.BlueGain; applied[3] *= definition.GreenGain;
        }
        return applied;
    }

    internal static void Configure(OutputParams output, double[] applied)
    {
        // Both flags are false: LibRaw's camera-WB branch otherwise overwrites user_mul.
        output.UseAutoWb = false; output.UseCameraWb = false;
        output.UserMultipliers = applied.Select(x => (float)x).ToArray();
        output.Greybox = new Rectangle(0, 0, -1, -1); output.Cropbox = new Rectangle(0, 0, -1, -1);
        output.OutputProfile = null; output.CameraProfile = null; output.BadPixels = null; output.DarkFrame = null;
        output.HalfSize = false; output.FourColorRgb = false; output.Aber = [1, 1, 1, 1];
        output.OutputFlags = 0; output.UseCameraMatrix = true; output.OutputColor = LibRawColorSpace.ProPhotoRgb;
        output.OutputBps = 16; output.Gamma = [1, 1, 0, 0, 0, 0]; output.Brightness = 1;
        output.NoAutoBright = true; output.AutoBrightThr = .01f; output.AdjustMaximumThr = 0;
        output.HighlightMode = 0; output.UserBlack = -1; output.UserCBlack = [-1000001, -1000001, -1000001, -1000001];
        output.UserSaturation = -1; output.UserQual = DemosaicAlgorithm.AdaptiveHomogeneityDirected;
        output.Interpolation = true; output.AutoScale = true; output.UserFlip = 0; output.OutputTiff = false;
        output.Threshold = 0; output.MedianPasses = 0; output.ExpCorrec = false; output.ExpShift = 1;
        output.ExpPreser = 0; output.GreenMatching = 0; output.FbddNoiserd = 0; output.DcbIterations = 0;
        output.DcbEnhanceFl = 0; output.UseFujiRotate = true;
    }

    private ProfessionalRawMaster Decode(string path, ProfessionalRawDefinition definition, CancellationToken token)
    {
        if (!RawMatchTiff16ProductPipeline.IsRaw(path)) throw new NotSupportedException("新 RAW 版本不接受已渲染 JPG/PNG。");
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = Convert.ToHexString(SHA256.HashData(source)); token.ThrowIfCancellationRequested();
        try
        {
            using var context = RawContext.OpenFile(path); var unpackParameters=ProfessionalLibRawAdapter.ConfigureUnpack(context); context.Unpack();stage?.Invoke("Unpacked"); token.ThrowIfCancellationRequested();
            var color = ProfessionalLibRawAdapter.ReadColor(context);
            var identity = context.ImageParams;
            // First production contract is three-color Bayer; non-mosaic/sRAW and X-Trans require separate validation.
            if (identity.Colors != 3 || identity.Filters <= 1000 || identity.IsFoveon != 0)
                throw new NotSupportedException("首期专业 RAW 仅支持三通道 Bayer，相机类型未适配；未回退旧路径。");
            var camera = context.CameraMultipler.Select(x => (double)x).ToArray();
            var applied = ResolveWhiteBalance(camera, color.AsShotWbApplied != 0, definition);
            if(!ProfessionalLibRawAdapter.CameraMatrixWillBeApplied(context))throw new InvalidDataException("LibRaw 未解析可应用的相机颜色配置，拒绝把相机 RGB 冒充工作空间。");
            var matrix = color.RgbCam.Select(x => (double)x).ToArray();
            if (matrix.Any(x => !double.IsFinite(x)) || matrix.Sum(Math.Abs) < .01 || color.Maximum <= color.Black)
                throw new InvalidDataException("缺少可用相机颜色矩阵或黑白电平，未回退旧处理。");
            context.DcrawProcess(output => Configure(output, applied));stage?.Invoke("DemosaicWBMatrix"); token.ThrowIfCancellationRequested();
            var actual = OutputParams.FromLibRaw(context.UnsafeGetHandle());
            if (actual.UseCameraWb || actual.UseAutoWb || !actual.NoAutoBright || actual.OutputColor != LibRawColorSpace.ProPhotoRgb ||
                actual.OutputBps != 16 || actual.Gamma[0] != 1 || actual.Gamma[1] != 1 || actual.HighlightMode != 0 ||
                actual.UserMultipliers.Where((v, i) => v != (float)applied[i]).Any())
                throw new InvalidDataException("LibRaw 输出参数与专业配置不一致。");
            var warnings = MarshalWarnings(context);
            if(warnings.Contains("BadCameraWb") || warnings.Contains("NoMetadata") || warnings.Contains("NoInputProfile"))throw new InvalidDataException("LibRaw 缺少可信相机配置："+warnings);
            var after = ProfessionalLibRawAdapter.ReadColor(context);
            var minimum=applied.Min();
            if(Enumerable.Range(0,4).Any(i=>Math.Abs(after.PreMul[i]-applied[i]/minimum)>1e-5*Math.Max(1,applied[i]/minimum)))
                throw new InvalidDataException("实际白平衡与已选增益不一致，拒绝重复或隐式变换。");
            using var processed = context.MakeDcrawMemoryImage();stage?.Invoke("NativeRGB16");
            if (processed.Bits != 16 || processed.Channels != 3 || processed.ImageType != ProcessedImageType.Bitmap)
                throw new InvalidDataException("LibRaw 未返回线性 RGB16。");
            var count = checked(processed.Width * processed.Height * 3);
            if (processed.DataSize < checked(count * 2)) throw new InvalidDataException("RAW 输出不完整。");
            if((long)count*sizeof(float)>ProfessionalRawResourcePolicy.MasterBudgetBytes)throw new NotSupportedException("RAW master超过512MiB缓存预算；未降低尺寸或位深，当前专业版本拒绝此文件。");
            var samples = processed.AsSpan<ushort>()[..count]; var pixels = new float[count]; var clipped = 0; var zeros = 0;
            for (var i = 0; i < count; i++) { pixels[i] = samples[i] / 65535f; if (samples[i] == 65535) clipped++; if (samples[i] == 0) zeros++; }
            stage?.Invoke("FloatMaster");
            ushort orientation = 1;
            try
            {
                var metadata = ImageMetadataReader.ReadMetadata(path).OfType<MetadataExtractor.Formats.Exif.ExifIfd0Directory>().FirstOrDefault();
                if (metadata?.TryGetInt32(MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagOrientation, out var value) == true && value is >= 1 and <= 8) orientation = (ushort)value;
            }
            catch (ImageProcessingException) { }
            var contract = new ProfessionalRawContract(RawContext.Version, identity.Make.Trim(), identity.Model.Trim(), hash, definition.DecodeKey,
                camera, applied, matrix, checked((int)color.Maximum), SaturatedChannelSamples: clipped)
            {
                OutputParametersJson = System.Text.Json.JsonSerializer.Serialize(actual), UnpackParametersJson=unpackParameters,
                BlackLevel = color.Black, ChannelBlackLevels = color.CBlack.Take(4).ToArray(), BlackPatternRows = color.CBlack[4],
                BlackPatternColumns = color.CBlack[5], BlackPatternSha256 = Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(color.CBlack.AsSpan()))),
                NormalizedWhiteBalance = after.PreMul.Select(x => (double)x).ToArray(), ZeroChannelSamples = zeros,
                DecoderWarnings = warnings
            };
            return new(new HighBitDepthImageBuffer(processed.Width, processed.Height, pixels, "16", ProfessionalRawPipeline.WorkingSpace, orientation), contract);
        }
        catch (LibRawException ex) { throw new InvalidDataException("LibRaw 不支持或无法解码此相机文件，未回退旧处理。", ex); }
    }

    private static string MarshalWarnings(RawContext context)
    {
        var offset = checked((int)System.Runtime.InteropServices.Marshal.OffsetOf<Sdcb.LibRaw.Natives.LibRawData>(nameof(Sdcb.LibRaw.Natives.LibRawData.ProcessWarnings)));
        return ((LibRawWarning)System.Runtime.InteropServices.Marshal.ReadInt32(context.UnsafeGetHandle(), offset)).ToString();
    }
}
