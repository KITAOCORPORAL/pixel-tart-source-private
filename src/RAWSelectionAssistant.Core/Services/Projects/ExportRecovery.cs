using System.Text.Json;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record ExportRecipeSnapshot(int Version,string Format,int? BitDepth,string SizeMode,int? Width,int? Height,int? Edge,
    int? Quality,string Compression,string ColorProfile,string MetadataPolicy,string Sharpening,IReadOnlyList<WatermarkLayer>? Watermarks=null,int? OutputWidth=null,int? OutputHeight=null,string? RecipeName=null,string? FilenameTemplate=null,string? AdjustmentFingerprint=null,int? Dpi=null,ProfessionalRawDefinition? RawDefinition=null,ProfessionalRawContract? RawInput=null)
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
    public static async Task<IReadOnlyList<StudioExportReceipt>> FindRecoverableAsync(string root,CancellationToken token=default)
    {
        var results=new Dictionary<Guid,StudioExportReceipt>();
        async Task Visit(string directory){ManagedExportFiles.RejectLinks(directory);foreach(var file in Directory.EnumerateFiles(directory)){if(!file.EndsWith(".kitao-export.pending.json",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(file)!="task.json")continue;ManagedExportFiles.RejectLinks(file);if(Path.GetFileName(file)=="task.json" && Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file)))!=".kitao-export-tasks")continue;var value=await ReadAsync(file,token);if(value.ManagedTaskId is not null)ManagedExportFiles.Validate(value,value.OutputPath);if(value.CompletionState is not ("Completed" or "Cancelled"))results[value.ExportId]=value;}foreach(var child in Directory.EnumerateDirectories(directory)){token.ThrowIfCancellationRequested();await Visit(child);}}
        if(Directory.Exists(root))await Visit(Path.GetFullPath(root));return results.Values.ToArray();
    }
    public static async Task<string> HashAsync(string path,CancellationToken token=default)
    {await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexString(await SHA256.HashDataAsync(stream,token));}
    public static async Task<ExportRecoveryResult> RecoverAsync(string output,Guid versionId,string? sourceHash,CancellationToken token=default)
    {
        output=Path.GetFullPath(output);ManagedExportFiles.RejectLinks(output);
        using var gate=await AcquireRecoveryLockAsync(output,token);
        if(gate is null)return new("Running",null,"另一执行者正在恢复");
        var pending=output+".kitao-export.pending.json";
        if(!File.Exists(pending))
        {
            var completedPath=output+".kitao-export.json";
            ManagedExportFiles.RejectLinks(completedPath);
            if(File.Exists(completedPath)){var done=await ReadAsync(completedPath,token);if(done.EditVersionId==versionId&&string.Equals(done.SourceHash,sourceHash,StringComparison.OrdinalIgnoreCase)&&File.Exists(output)&&await HashAsync(output,token)==done.OutputHash)return new("Completed",done);return new("Conflict",done,"已有输出或清单身份不一致");}
            var root=Path.Combine(Path.GetDirectoryName(output)!,".kitao-export-tasks");
            if(Directory.Exists(root))foreach(var directory in Directory.EnumerateDirectories(root))
            {ManagedExportFiles.RejectLinks(directory);var task=Path.Combine(directory,"task.json");if(!File.Exists(task))continue;ManagedExportFiles.RejectLinks(task);var candidate=await ReadAsync(task,token);if(candidate.OutputPath==output&&candidate.EditVersionId==versionId&&candidate.SourceHash==sourceHash&&candidate.CompletionState=="Encoded"){ManagedExportFiles.Validate(candidate,output);await AtomicWriteAsync(pending,candidate,false,token);break;}}
            if(!File.Exists(pending))return new("Missing",null);
        }
        ManagedExportFiles.RejectLinks(pending);ManagedExportFiles.RejectLinks(output+".kitao-export.json");
        var receipt=await ReadAsync(pending,token);
        if(receipt.CompletionState=="Cancelled")return new("Cancelled",receipt);
        if(!string.Equals(Path.GetFullPath(receipt.OutputPath),output,StringComparison.OrdinalIgnoreCase)||receipt.EditVersionId!=versionId||!string.Equals(receipt.SourceHash,sourceHash,StringComparison.OrdinalIgnoreCase))return new("Conflict",receipt,"路径、源或编辑版本不同");
        FileStream? taskLock=null;
        try
        {
            if(receipt.ManagedTaskId is Guid taskId){var directory=ManagedExportFiles.DirectoryFor(output,taskId);ManagedExportFiles.RejectLinks(directory);ManagedExportFiles.RejectLinks(Path.Combine(directory,"task.lock"));try{taskLock=new FileStream(Path.Combine(directory,"task.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){return new("Running",receipt);}}
            if(receipt.ManagedTaskId is not null){try{ManagedExportFiles.Validate(receipt,output);}catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException){return new("Failed",receipt,e.Message);}}
            if(!File.Exists(output))
            {
                if(receipt.ManagedTaskId is null)return new("Missing",receipt,"旧清单未记录临时产物");
                string artifact;try{artifact=ManagedExportFiles.Validate(receipt,output);}catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException){return new("Failed",receipt,e.Message);}
                if(!File.Exists(artifact))return new("Missing",receipt,"任务临时产物缺失");
                if(await HashAsync(artifact,token)!=receipt.OutputHash)return new("Failed",receipt,"临时产物 hash 不一致");
                token.ThrowIfCancellationRequested();File.Move(artifact,output,false);
            }
            if(!string.Equals(await HashAsync(output,token),receipt.OutputHash,StringComparison.OrdinalIgnoreCase))return new("Conflict",receipt,"输出 hash 不匹配");
            var sidecar=output+".kitao-export.json";
            if(File.Exists(sidecar)){var current=await ReadAsync(sidecar,token);if(current.ExportId!=receipt.ExportId||current.OutputHash!=receipt.OutputHash)return new("Conflict",receipt,"清单冲突");}
            else await AtomicWriteAsync(sidecar,receipt with{CompletionState="Completed"},false,token);
            File.Delete(pending);
            if(receipt.ManagedTaskId is Guid id)await AtomicWriteAsync(Path.Combine(ManagedExportFiles.DirectoryFor(output,id),"task.json"),receipt with{CompletionState="Completed"},true,token);
            return new("Completed",receipt with{CompletionState="Completed"});
        }
        finally{taskLock?.Dispose();}
    }
    public static async Task<ExportRecoveryResult> CancelAsync(string output,Guid taskId,bool removeManagedArtifact,CancellationToken token=default)
    {
        output=Path.GetFullPath(output);ManagedExportFiles.RejectLinks(output);using var gate=await AcquireRecoveryLockAsync(output,token);if(gate is null)return new("Running",null);
        var directory=ManagedExportFiles.DirectoryFor(output,taskId);ManagedExportFiles.RejectLinks(directory);
        var taskPath=Path.Combine(directory,"task.json");if(!File.Exists(taskPath))return new("Missing",null);ManagedExportFiles.RejectLinks(taskPath);
        var receipt=await ReadAsync(taskPath,token);var artifact=ManagedExportFiles.Validate(receipt,output);if(receipt.ExportId!=taskId)return new("Conflict",receipt);
        ManagedExportFiles.RejectLinks(Path.Combine(directory,"task.lock"));using var taskLock=new FileStream(Path.Combine(directory,"task.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(File.Exists(output))return new("Conflict",receipt,"成片已存在，请恢复清单；取消不会删除成片。");
        var pendingPath=output+".kitao-export.pending.json";if(File.Exists(pendingPath)){ManagedExportFiles.RejectLinks(pendingPath);if((await ReadAsync(pendingPath,token)).ExportId!=taskId)return new("Conflict",receipt,"pending 属于其他任务");}
        if(removeManagedArtifact&&File.Exists(artifact)){if(await HashAsync(artifact,token)!=receipt.OutputHash)return new("Conflict",receipt,"临时产物已改变，拒绝删除。");File.Delete(artifact);}
        var cancelled=receipt with{CompletionState="Cancelled"};await AtomicWriteAsync(taskPath,cancelled,true,token);
        var pending=output+".kitao-export.pending.json";if(File.Exists(pending)){ManagedExportFiles.RejectLinks(pending);var current=await ReadAsync(pending,token);if(current.ExportId!=taskId)return new("Conflict",receipt);await AtomicWriteAsync(pending,cancelled,true,token);}
        return new("Cancelled",cancelled);
    }
    private static Task<FileStream?> AcquireRecoveryLockAsync(string output,CancellationToken token)
    {token.ThrowIfCancellationRequested();ManagedExportFiles.RejectLinks(output+".recovery.lock");try{return Task.FromResult<FileStream?>(new FileStream(output+".recovery.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None));}catch(IOException){return Task.FromResult<FileStream?>(null);}}
    internal static async Task AtomicWriteAsync(string path,StudioExportReceipt receipt,bool overwrite,CancellationToken token)
    {
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await JsonSerializer.SerializeAsync(stream,receipt,new JsonSerializerOptions{WriteIndented=true},token);await stream.FlushAsync(token);stream.Flush(true);}token.ThrowIfCancellationRequested();File.Move(temporary,path,overwrite);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
