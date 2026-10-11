using System.Runtime.InteropServices;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

/// <summary>Version-gated ABI bridge for fields missing from RawContext's public getters.</summary>
internal static class ProfessionalLibRawAdapter
{
    private static void VerifyLayout()
    {
        if (!Environment.Is64BitProcess || (typeof(RawContext).Assembly.GetName().Version != new Version(0,21,1,7) || RawContext.VersionNumber.Major != 0 || RawContext.VersionNumber.Minor != 21 || RawContext.VersionNumber.Build != 1))
            throw new NotSupportedException("专业 RAW 适配器仅验证了 LibRaw 0.21.1 x64；未回退旧路径。");
        // Sdcb.LibRaw 0.21.1.7's full managed layout and its explicit native output layout must agree.
        if (Marshal.OffsetOf<LibRawData>(nameof(LibRawData.OutputParams)) != Marshal.OffsetOf(typeof(RawContext).Assembly.GetType("Sdcb.LibRaw.Natives.LibRawDataX64", throwOnError: true)!, "OutputParams"))
            throw new NotSupportedException("LibRaw ABI 不匹配。");
    }
    internal static string ConfigureUnpack(RawContext context)
    {
        VerifyLayout();
        var pointer=IntPtr.Add(context.UnsafeGetHandle(),checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.RawUnpackParams))));
        var parameters=Marshal.PtrToStructure<RawUnpackParams>(pointer);
        parameters.UseRawSpeed=0;parameters.UseDngSdk=0;parameters.Options=1 << 1; // LIBRAW_RAWOPTIONS_CONVERTFLOAT_TO_INT (0.21.1)
        parameters.ShotSelect=0;parameters.Specials=0;parameters.MaxRawMemoryMb=2048;
        parameters.SonyArw2PosterizationThr=0;parameters.CoolscanNefGamma=1;parameters.CustomCameraStrings=IntPtr.Zero;
        Marshal.StructureToPtr(parameters,pointer,false);
        return System.Text.Json.JsonSerializer.Serialize(new{parameters.UseRawSpeed,parameters.UseDngSdk,parameters.Options,parameters.ShotSelect,parameters.Specials,parameters.MaxRawMemoryMb,parameters.SonyArw2PosterizationThr,parameters.CoolscanNefGamma});
    }
    internal static bool CameraMatrixWillBeApplied(RawContext context)
    {
        VerifyLayout();
        var offset=checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.RawData))+(int)Marshal.OffsetOf<LibRawRawData>(nameof(LibRawRawData.IOParams)));
        return Marshal.PtrToStructure<LibRawInternalOutputParams>(IntPtr.Add(context.UnsafeGetHandle(),offset)).RawColor==0;
    }
    internal static LibRawColorData ReadColor(RawContext context)
    {
        VerifyLayout();
        var offset = checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.Color)));
        var color = Marshal.PtrToStructure<LibRawColorData>(IntPtr.Add(context.UnsafeGetHandle(), offset));
        if (color.Maximum != context.ColorMaximum || color.CamMul.Length != 4 || color.CBlack.Length < 6 ||
            Enumerable.Range(0, 4).Any(c => color.CamMul[c] != context.CameraMultipler[c]) ||
            Enumerable.Range(0, 12).Any(i => color.RgbCam[i] != context.RgbCamera[i / 4, i % 4]))
            throw new InvalidDataException("LibRaw 颜色结构与官方 getter 不一致，拒绝解码。");
        return color;
    }
}
