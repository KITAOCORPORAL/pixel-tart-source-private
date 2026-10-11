namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record StudioEditVersion(Guid Id,string Name,DateTimeOffset CreatedAt,Guid? LibraryId,Guid? AssetId,string? SourceHash,
    ReferenceLook? Look,ColorAdjustmentStack? Stack,PixelTartFilmSettings? Film,ColorStudioMatchEngine Engine,MatchV4ExecutionMode Mode)
{
    public int ProcessingVersion => Stack?.ProcessingVersion ?? 1;
}
public sealed record StudioExportReceipt(Guid ExportId,Guid? LibraryId,Guid? AssetId,string? SourceHash,Guid EditVersionId,int ProcessingVersion,
    string Recipe,string OutputPath,string OutputHash,DateTimeOffset CompletedAt);
public static class StudioExportReceiptStore
{
    public static async Task WriteAsync(string output,StudioExportReceipt receipt,CancellationToken token=default)
    {
        if(!File.Exists(output)) throw new IOException("输出未完成，不能记录成功。");
        if(receipt.EditVersionId==Guid.Empty)throw new InvalidDataException("导出缺少编辑版本身份。");
        await using var input=new FileStream(output,FileMode.Open,FileAccess.Read,FileShare.Read);
        var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(input,token));
        var path=output+".kitao-export.json";var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {await System.Text.Json.JsonSerializer.SerializeAsync(stream,receipt with {OutputPath=Path.GetFullPath(output),OutputHash=hash},new System.Text.Json.JsonSerializerOptions{WriteIndented=true},token);await stream.FlushAsync(token);stream.Flush(true);}
            token.ThrowIfCancellationRequested();File.Move(temporary,path,false);
        }
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}
