using System.Text.Json;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record ExportRecipeSnapshot(int Version,string Format,int? BitDepth,string SizeMode,int? Width,int? Height,int? Edge,
    int? Quality,string Compression,string ColorProfile,string MetadataPolicy,string Sharpening,IReadOnlyList<WatermarkLayer>? Watermarks=null,int? OutputWidth=null,int? OutputHeight=null,string? RecipeName=null,string? FilenameTemplate=null,string? AdjustmentFingerprint=null,int? Dpi=null)
{
    public static ExportRecipeSnapshot Publishing(PublishingOptions options)=>new(1,options.OutputFormat.ToString(),options.OutputFormat==PublishingOutputFormat.Tiff?16:8,
        options.EffectiveDimensions.Enabled?options.EffectiveDimensions.Mode.ToString():"Original",options.EffectiveDimensions.Enabled&&options.EffectiveDimensions.Mode==PublishingSizeMode.Exact?options.EffectiveDimensions.Width:null,options.EffectiveDimensions.Enabled&&options.EffectiveDimensions.Mode==PublishingSizeMode.Exact?options.EffectiveDimensions.Height:null,options.EffectiveDimensions.Enabled&&options.EffectiveDimensions.Mode is PublishingSizeMode.LongestEdge or PublishingSizeMode.ShortestEdge?options.EffectiveDimensions.LongestEdge:null,
        options.OutputFormat==PublishingOutputFormat.Jpeg?options.EffectiveDimensions.JpegQuality:null,options.OutputFormat==PublishingOutputFormat.Jpeg?"JPEG":options.OutputFormat==PublishingOutputFormat.Tiff?"None":"PNG codec default",options.ColorSpaceProfile,options.MetadataPolicy.ToString(),"Not implemented",options.WatermarksEnabled?options.EffectiveWatermarkLayers.Where(x=>x.Enabled).ToArray():[],Dpi:options.EffectiveDimensions.Dpi);
}
public sealed record ExportRecoveryResult(string State,StudioExportReceipt? Receipt,string? Error=null);
public static class ExportRecovery
{
    public static async Task<StudioExportReceipt> ReadAsync(string path,CancellationToken token=default)
    {await using var stream=File.OpenRead(path);return await JsonSerializer.DeserializeAsync<StudioExportReceipt>(stream,cancellationToken:token)??throw new InvalidDataException("导出清单为空。");}
    public static async Task<string> HashAsync(string path,CancellationToken token=default)
    {await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexString(await SHA256.HashDataAsync(stream,token));}
    public static async Task<ExportRecoveryResult> RecoverAsync(string output,Guid versionId,string? sourceHash,CancellationToken token=default)
    {
        var pending=output+".kitao-export.pending.json";if(!File.Exists(pending))return new("Missing",null);
        var receipt=await ReadAsync(pending,token);
        if(!string.Equals(Path.GetFullPath(receipt.OutputPath),Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase))return new("Conflict",receipt,"清单输出路径不一致");
        if(receipt.EditVersionId!=versionId||!string.Equals(receipt.SourceHash,sourceHash,StringComparison.OrdinalIgnoreCase))return new("Conflict",receipt,"源或编辑版本不同");
        if(!File.Exists(output)||!string.Equals(await HashAsync(output,token),receipt.OutputHash,StringComparison.OrdinalIgnoreCase))return new("Conflict",receipt,"输出 hash 不匹配，未修改文件");
        var sidecar=output+".kitao-export.json";
        if(File.Exists(sidecar)){var current=await ReadAsync(sidecar,token);if(current.ExportId!=receipt.ExportId||current.OutputHash!=receipt.OutputHash)return new("Conflict",receipt,"清单冲突");}
        else await AtomicWriteAsync(sidecar,receipt with{CompletionState="Completed"},false,token);
        File.Delete(pending);return new("Completed",receipt with{CompletionState="Completed"});
    }
    internal static async Task AtomicWriteAsync(string path,StudioExportReceipt receipt,bool overwrite,CancellationToken token)
    {
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await JsonSerializer.SerializeAsync(stream,receipt,new JsonSerializerOptions{WriteIndented=true},token);await stream.FlushAsync(token);stream.Flush(true);}token.ThrowIfCancellationRequested();File.Move(temporary,path,overwrite);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
