using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record ManagedExportArtifact(Guid TaskId,string OutputPath,string ArtifactPath,string Hash);
internal static class ManagedExportFiles
{
    internal static string DirectoryFor(string output,Guid id)=>Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,".kitao-export-tasks",id.ToString("N"));
    internal static void RejectLinks(string path)
    {
        var full=Path.GetFullPath(path);
        for(var current=full;!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
        {
            if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("导出恢复不接受链接路径。");
        }    }
    internal static string Validate(StudioExportReceipt receipt,string output)
    {
        if(receipt.ManagedTaskId is not Guid id||id!=receipt.ExportId)throw new InvalidDataException("缺少任务身份。");
        var directory=DirectoryFor(output,id);var artifact=Path.Combine(directory,"encoded.bin");RejectLinks(directory);RejectLinks(artifact);
        if(!string.Equals(receipt.ManagedArtifactPath,artifact,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("临时产物不属于此任务。");
        RejectLinks(Path.Combine(directory,"owner.json"));
        var owner=JsonSerializer.Deserialize<ManagedExportArtifact>(File.ReadAllText(Path.Combine(directory,"owner.json")))??throw new InvalidDataException("任务凭据缺失。");
        if(owner.TaskId!=id||owner.OutputPath!=Path.GetFullPath(output)||owner.ArtifactPath!=artifact||owner.Hash!=receipt.OutputHash)throw new InvalidDataException("任务凭据不匹配。");return artifact;
    }
}
