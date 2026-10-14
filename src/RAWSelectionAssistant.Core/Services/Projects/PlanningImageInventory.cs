namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningImageInventoryItem(string RelativePath,string? Sha256,string Disposition);
public sealed record PlanningImageInventory(IReadOnlyList<PlanningImageInventoryItem> Files,IReadOnlyList<string> OpenIssues,bool DeletionAllowed=false);
public static class PlanningImageInventoryScanner
{
    public static PlanningImageInventory Scan(string root,Guid documentId,IEnumerable<Guid>? inMemoryUndoRedoIds=null)
    {
        var referenced=new HashSet<Guid>(inMemoryUndoRedoIds??[]);var issues=new List<string>();
        foreach(var file in Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.TopDirectoryOnly):[])
        {
            var name=Path.GetFileName(file);
            if(!(name.EndsWith(".planning.json",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".draft",StringComparison.OrdinalIgnoreCase)||name.Contains(".docx-recovery-",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".foundation-v0.bak",StringComparison.OrdinalIgnoreCase)))continue;
            try
            {
                PlanningImageStorage.RejectLinks(root,file);
                using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file));
                void Walk(System.Text.Json.JsonElement value)
                {
                    if(value.ValueKind==System.Text.Json.JsonValueKind.Object)
                    {
                        if(value.TryGetProperty("DocumentId",out var doc)&&doc.ValueKind==System.Text.Json.JsonValueKind.String&&Guid.TryParse(doc.GetString(),out var d)&&d==documentId&&value.TryGetProperty("DocumentImageId",out var image)&&Guid.TryParse(image.GetString(),out var id))referenced.Add(id);
                        foreach(var property in value.EnumerateObject())Walk(property.Value);
                    }
                    else if(value.ValueKind==System.Text.Json.JsonValueKind.Array)foreach(var child in value.EnumerateArray())Walk(child);
                }
                Walk(json.RootElement);
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){issues.Add($"{name} 无法审计：{e.Message}");}
        }
        var rows=new List<PlanningImageInventoryItem>();var directory=PlanningImageStorage.DocumentDirectory(root,documentId);
        foreach(var path in Directory.Exists(directory)?Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly):[])
        {
            var relative=Path.GetRelativePath(root,path).Replace('\\','/');string? hash=null;var disposition="UNKNOWN_KEEP";
            try
            {
                PlanningImageStorage.RejectLinks(root,path);
                using var stream=File.OpenRead(path);hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
                if(Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var id))disposition=referenced.Contains(id)?"REFERENCED_OR_RECOVERY_KEEP":issues.Count==0?"UNREFERENCED_CANDIDATE_KEEP":"AUDIT_INCOMPLETE_KEEP";
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException){issues.Add(relative+"："+e.Message);}
            rows.Add(new(relative,hash,disposition));
        }
        if(issues.Count>0)rows=rows.Select(row=>row.Disposition=="UNREFERENCED_CANDIDATE_KEEP"?row with{Disposition="AUDIT_INCOMPLETE_KEEP"}:row).ToList();
        return new(rows,issues,false);
    }
}
