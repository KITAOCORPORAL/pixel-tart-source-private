namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record StudioEditVersion(Guid Id,string Name,DateTimeOffset CreatedAt,Guid? LibraryId,Guid? AssetId,string? SourceHash,
    ReferenceLook? Look,ColorAdjustmentStack? Stack,PixelTartFilmSettings? Film,ColorStudioMatchEngine Engine,MatchV4ExecutionMode Mode)
{
    public ProfessionalRawDefinition? RawDefinition {get;init;}
    public ProfessionalRawContract? RawInput {get;init;}
    public Guid? OriginVersionId {get;init;}
    public string? OriginIdentity {get;init;}
    public bool Archived { get; init; }
    public bool IsOriginal {get;init;}
    public bool IsMain {get;init;}
    public bool MainExplicit {get;init;}
    public int Rating {get;init;}
    public string? ColorLabel {get;init;}

    public int ProcessingVersion => RawDefinition?.ProcessingVersion ?? Stack?.ProcessingVersion ?? 1;
}
public sealed record StudioExportReceipt(Guid ExportId,Guid? LibraryId,Guid? AssetId,string? SourceHash,Guid EditVersionId,int ProcessingVersion,
    string Recipe,string OutputPath,string OutputHash,DateTimeOffset CompletedAt, ExportRecipeSnapshot? RecipeSnapshot=null,int ManifestVersion=2,string CompletionState="Completed",Guid? ManagedTaskId=null,string? ManagedArtifactPath=null);
public static class StudioExportReceiptStore
{
    public static async Task CommitOutputAsync(string temporary,string output,StudioExportReceipt receipt,CancellationToken token=default,Action<string>? afterOutputCommit=null,Action<string>? checkpoint=null)
    {
        output=Path.GetFullPath(output);ManagedExportFiles.RejectLinks(output);
        if(File.Exists(output)||File.Exists(output+".kitao-export.json")||File.Exists(output+".kitao-export.pending.json"))throw new IOException("输出或清单已存在，未覆盖。");
        if(receipt.ExportId==Guid.Empty)throw new InvalidDataException("任务 ID 不能为空。");
        var directory=ManagedExportFiles.DirectoryFor(output,receipt.ExportId);Directory.CreateDirectory(directory);ManagedExportFiles.RejectLinks(directory);
        using var execution=new FileStream(Path.Combine(directory,"task.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var artifact=Path.Combine(directory,"encoded.bin");File.Move(temporary,artifact,false);
        var completed=receipt with{OutputPath=output,OutputHash=await ExportRecovery.HashAsync(artifact,token),ManagedTaskId=receipt.ExportId,ManagedArtifactPath=artifact,ManifestVersion=3};
        await using(var owner=new FileStream(Path.Combine(directory,"owner.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {await System.Text.Json.JsonSerializer.SerializeAsync(owner,new ManagedExportArtifact(receipt.ExportId,output,artifact,completed.OutputHash),cancellationToken:token);await owner.FlushAsync(token);owner.Flush(true);}
        await ExportRecovery.AtomicWriteAsync(Path.Combine(directory,"task.json"),completed with{CompletionState="Encoded"},false,token);checkpoint?.Invoke("Encoded");
        var pending=output+".kitao-export.pending.json";
        await ExportRecovery.AtomicWriteAsync(pending,completed with{CompletionState="OutputCommitPending"},false,token);checkpoint?.Invoke("Pending");
        token.ThrowIfCancellationRequested();File.Move(artifact,output,false);checkpoint?.Invoke("Moved");afterOutputCommit?.Invoke(output);
        await ExportRecovery.AtomicWriteAsync(output+".kitao-export.json",completed with{CompletionState="Completed"},false,token);checkpoint?.Invoke("Receipt");
        File.Delete(pending);await ExportRecovery.AtomicWriteAsync(Path.Combine(directory,"task.json"),completed with{CompletionState="Completed"},true,CancellationToken.None);
    }
    public static async Task WriteAsync(string output,StudioExportReceipt receipt,CancellationToken token=default)
    {
        if(!File.Exists(output)) throw new IOException("输出未完成，不能记录成功。");
        if(receipt.EditVersionId==Guid.Empty)throw new InvalidDataException("导出缺少编辑版本身份。");
        await using var input=new FileStream(output,FileMode.Open,FileAccess.Read,FileShare.Read);
        var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(input,token));
        var completed=receipt with{OutputPath=Path.GetFullPath(output),OutputHash=hash};
        var pending=output+".kitao-export.pending.json";
        await ExportRecovery.AtomicWriteAsync(pending,completed with{CompletionState="OutputCompleteReceiptPending"},false,token);
        await ExportRecovery.AtomicWriteAsync(output+".kitao-export.json",completed,false,token);
        File.Delete(pending);
    }
}
