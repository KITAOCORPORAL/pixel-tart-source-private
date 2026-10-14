using System.Security.Cryptography;
using System.Text.Json.Serialization;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Only identity-derived document-owned PNGs are admitted. No basename search or GC.</summary>
public static class PlanningImageStorage
{
    public static string RelativePath(Guid documentId, Guid imageId) => $"images/{documentId:N}/{imageId:N}.png";
    private static InvalidDataException Problem(PlanningDocumentImage image, string reason)
    {
        var error=new InvalidDataException($"策划图片“{image.Title}” [文档 {image.DocumentId:D} / 图片 {image.DocumentImageId:D}]：{reason}");
        error.Data["PlanningImageIntegrity"]=true;return error;
    }
    public static bool IsImageIntegrityError(Exception error)=>error.Data["PlanningImageIntegrity"] is true;
    public static string DocumentDirectory(string root, Guid documentId)
    { var path=Path.GetFullPath(Path.Combine(root,"images",documentId.ToString("N")));RejectLinks(root,path);return path; }
    public static void RejectLinks(string root,string path)
    {
        var basePath=Path.GetFullPath(root);var current=Path.GetFullPath(path);
        if(!current.Equals(basePath,StringComparison.OrdinalIgnoreCase)&&!current.StartsWith(basePath.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("图片路径超出策划存储根。");
        while(true)
        {
            if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("图片路径包含符号链接/重解析点，停止读取。");
            var parent=Path.GetDirectoryName(current);if(parent is null||parent==current)break;current=parent;
        }
    }
    private static void Verify(PlanningDocumentImage image,string path)
    {
        if(!File.Exists(path))throw Problem(image,"副本缺失。请恢复该图片的原备份，未替换为其他图片。");
        using var stream=File.OpenRead(path);var hash=Convert.ToHexString(SHA256.HashData(stream));
        if(!hash.Equals(image.CopyHash,StringComparison.OrdinalIgnoreCase))throw Problem(image,"副本 SHA-256 不符。停止读取，不回退其他文件。");
    }
    public static PlanningDocumentImage Bind(PlanningDocumentImage image,string? root=null)
    {
        image.Validate();root??=image.StorageRoot;
        var canonicalRelative=RelativePath(image.DocumentId,image.DocumentImageId);
        var stored=image.CopyPath.Replace('\\','/');
        if(stored.Split('/').Any(p=>p is "." or "..")||stored.IndexOf('\0')>=0)throw Problem(image,"不允许 . 或 .. 路径段。");
        if(Path.IsPathRooted(image.CopyPath))
        {
            var legacy=Path.GetFullPath(image.CopyPath);
            if(!legacy.Replace('\\','/').EndsWith("/"+canonicalRelative,StringComparison.OrdinalIgnoreCase))throw Problem(image,"旧绝对路径不属于其文档/图片身份目录。");
            if(File.Exists(legacy))
            {
                RejectLinks(Path.GetPathRoot(legacy)!,legacy);Verify(image,legacy);
                // If it already belongs to this root, adopt portable metadata without file writes.
                if(root is not null&&legacy.Equals(Path.GetFullPath(Path.Combine(root,canonicalRelative)),StringComparison.OrdinalIgnoreCase))return image with{CopyPath=canonicalRelative,StorageRoot=Path.GetFullPath(root)};
                return image with{StorageRoot=root is null?null:Path.GetFullPath(root)};
            }
            if(root is null)throw Problem(image,"旧绝对副本缺失，且没有当前策划根目录。");
        }
        else
        {
            if(!stored.Equals(canonicalRelative,StringComparison.OrdinalIgnoreCase))throw Problem(image,"相对路径与文档/图片身份不一致。");
            if(root is null)throw Problem(image,"相对副本没有绑定策划存储根目录。");
        }
        var candidate=Path.GetFullPath(Path.Combine(root!,canonicalRelative));RejectLinks(root!,candidate);Verify(image,candidate);
        return image with{CopyPath=canonicalRelative,StorageRoot=Path.GetFullPath(root!)};
    }
    public static string Resolve(PlanningDocumentImage image)
    {var bound=Bind(image);return Path.IsPathRooted(bound.CopyPath)?Path.GetFullPath(bound.CopyPath):Path.GetFullPath(Path.Combine(bound.StorageRoot!,bound.CopyPath));}
    public static PlanningDocumentImage PrepareForSave(PlanningDocumentImage image,string root)
    {
        var bound=Bind(image,root);var source=Resolve(bound);var relative=RelativePath(image.DocumentId,image.DocumentImageId);var target=Path.GetFullPath(Path.Combine(root,relative));RejectLinks(root,target);
        if(!source.Equals(target,StringComparison.OrdinalIgnoreCase))
        {
            if(File.Exists(target))Verify(image,target);
            else
            {
                Directory.CreateDirectory(DocumentDirectory(root,image.DocumentId));var temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{using(var input=File.OpenRead(source))using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){input.CopyTo(output);output.Flush(true);}Verify(image,temporary);File.Move(temporary,target);}
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
        }
        return bound with{CopyPath=relative,StorageRoot=Path.GetFullPath(root)};
    }
    public static PlanningDocument BindDocument(PlanningDocument document,string root,bool saving=false)
    {
        if(document.Package?.RichLayout is not {} layout)return document;
        var images=layout.Images.Select(image=>
        {
            if(image.DocumentId!=document.Package.DocumentId)throw Problem(image,"图片与策划文档身份不符。");
            return saving?PrepareForSave(image,root):Bind(image,root);
        }).ToArray();
        return document with{Package=document.Package with{RichLayout=layout with{Images=images}}};
    }
}
