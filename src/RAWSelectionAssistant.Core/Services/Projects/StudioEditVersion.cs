namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record StudioEditVersion(Guid Id,string Name,DateTimeOffset CreatedAt,Guid? LibraryId,Guid? AssetId,string? SourceHash,
    ReferenceLook? Look,ColorAdjustmentStack? Stack,PixelTartFilmSettings? Film,ColorStudioMatchEngine Engine,MatchV4ExecutionMode Mode)
{
    public bool Archived { get; init; }
    public int ProcessingVersion => Stack?.ProcessingVersion ?? 1;
}
public sealed record StudioExportReceipt(Guid ExportId,Guid? LibraryId,Guid? AssetId,string? SourceHash,Guid EditVersionId,int ProcessingVersion,
    string Recipe,string OutputPath,string OutputHash,DateTimeOffset CompletedAt, ExportRecipeSnapshot? RecipeSnapshot=null,int ManifestVersion=2,string CompletionState="Completed");
public static class StudioExportReceiptStore
{
    public static async Task CommitOutputAsync(string temporary,string output,StudioExportReceipt receipt,CancellationToken token=default,Action<string>? afterOutputCommit=null)
    {
        if(File.Exists(output)||File.Exists(output+".kitao-export.json")||File.Exists(output+".kitao-export.pending.json"))throw new IOException("输出或清单已存在，未覆盖。");
        var completed=receipt with{OutputPath=Path.GetFullPath(output),OutputHash=await ExportRecovery.HashAsync(temporary,token)};
        var pending=output+".kitao-export.pending.json";
        await ExportRecovery.AtomicWriteAsync(pending,completed with{CompletionState="OutputCommitPending"},false,token);
        var moved=false;
        try
        {
            token.ThrowIfCancellationRequested();File.Move(temporary,output,false);moved=true;afterOutputCommit?.Invoke(output);
            await ExportRecovery.AtomicWriteAsync(output+".kitao-export.json",completed,false,token);File.Delete(pending);
        }
        catch{if(!moved&&File.Exists(pending))File.Delete(pending);throw;}
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
