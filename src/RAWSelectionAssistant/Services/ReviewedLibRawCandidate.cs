using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;

public sealed record ReviewedRawCandidateConfiguration(string Executable,string ExecutableSha256,string NativeLibrarySha256,string Version="0.22.2",int TimeoutSeconds=180);
public sealed record ReviewedRawCandidateResult(HighBitDepthImageBuffer Pixels,string SourceSha256,string DecoderVersion,string ExecutableSha256,string NativeLibrarySha256,string NumericDomain);
/// <summary>Opt-in isolated official executable adapter, never a silent fallback or ABI replacement.</summary>
public static class ReviewedLibRawCandidate
{
    public const string Contract="OfficialLibRaw0222DcrawRgb16SrgbV1";
    public const string ReviewedExecutableSha256="8DF090A80C784FA03182B91AB1679FD08E5FC139D54B0BFB8B003024EF741129";
    public const string ReviewedNativeSha256="6A459C22039ABF0EAC4D263673337C8ED5F223ACBD372FCF77610DEBF80AC8CD";
    public static async Task<ReviewedRawCandidateResult> DecodeAsync(string source,ReviewedRawCandidateConfiguration configuration,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(configuration.Version!="0.22.2"||configuration.TimeoutSeconds is <1 or >600||!configuration.ExecutableSha256.Equals(ReviewedExecutableSha256,StringComparison.OrdinalIgnoreCase)||!configuration.NativeLibrarySha256.Equals(ReviewedNativeSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unreviewed candidate version/configuration.");
        var executable=Path.GetFullPath(configuration.Executable);var native=Path.Combine(Path.GetDirectoryName(executable)!,"libraw.dll");
        static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input));}
        if(!string.Equals(Hash(executable),configuration.ExecutableSha256,StringComparison.OrdinalIgnoreCase)||!string.Equals(Hash(native),configuration.NativeLibrarySha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Candidate binary identity mismatch; original decoder unchanged.");
        var sourceHash=Hash(source);var directory=Path.Combine(Path.GetTempPath(),"Kitao-reviewed-raw-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var output=Path.Combine(directory,"decoded.tif");var acquired=false;
        try
        {
            await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);acquired=true;
            using var work=StudioGlobalResources.ReserveWork(StudioGlobalResources.MasterLimit);
            using var process=new Process{StartInfo=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory,RedirectStandardError=true,RedirectStandardOutput=true}};
            foreach(var value in new[]{"-w","-6","-T","-o","1","-g","2.4","12.92","-Z",output,Path.GetFullPath(source)})process.StartInfo.ArgumentList.Add(value);
            process.Start();var stderr=process.StandardError.ReadToEndAsync(token);var stdout=process.StandardOutput.ReadToEndAsync(token);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
            try{await process.WaitForExitAsync(deadline.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();if(token.IsCancellationRequested)throw;throw new IOException("Reviewed candidate decode timed out; prior result retained.");}
            await stderr;await stdout;token.ThrowIfCancellationRequested();if(process.ExitCode!=0||!File.Exists(output))throw new InvalidDataException("Reviewed candidate could not decode this input; native stage failed.");
            using var file=File.OpenRead(output);var decoded=TiffReadBack.Read(file);
            if(decoded.BitsPerSample!=16)throw new InvalidDataException("Candidate did not return RGB16; no precision fallback.");
            if(!string.Equals(sourceHash,Hash(source),StringComparison.OrdinalIgnoreCase))throw StudioFailureException.SourceChanged("Input changed during candidate decode.");
            return new(new HighBitDepthImageBuffer(decoded.Width,decoded.Height,decoded.Rgb48Samples,"16","LibRaw0222-sRGBPrimariesGamma2p4Toe12p92",(ushort)decoded.Orientation),sourceHash,Contract,configuration.ExecutableSha256,configuration.NativeLibrarySha256,"LibRaw0222 sRGB primaries, explicit dcraw gamma2.4/toe12.92 and camera WB baked; isolated candidate domain, not admitted as exact ICC sRGB/PV4");
        }
        finally{if(acquired)ProfessionalRawResourcePolicy.HeavyWork.Release();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
