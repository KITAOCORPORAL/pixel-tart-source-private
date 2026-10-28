using System.Security.Cryptography;
using System.Text.Json.Serialization;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Additive ownership receipt. Original reference identity/path remain unchanged.</summary>
public sealed record PlanningProjectCopy(Guid ProjectId, Guid DocumentId, Guid ReferenceId,
    string Kind, string OriginalPath, string RelativePath, string Sha256, long Bytes, DateTimeOffset ImportedAt)
{
    [JsonIgnore] public string? StorageRoot { get; init; }
    public void Validate()
    {
        if (ProjectId == Guid.Empty || DocumentId == Guid.Empty || ReferenceId == Guid.Empty ||
            Kind is not ("reference" or "attachment") || !Path.IsPathFullyQualified(OriginalPath) ||
            Bytes < 1 || Bytes > PlanningProjectCopyStorage.MaximumBytes || Sha256 is null || Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit) ||
            RelativePath is null || !RelativePath.Equals(PlanningProjectCopyStorage.Relative(this), StringComparison.Ordinal))
            throw new InvalidDataException("项目副本身份、路径或大小无效。");
    }
}

public static class PlanningProjectCopyStorage
{
    public const long MaximumBytes = 64L * 1024 * 1024;
    private static string Extension(string source) { var e=Path.GetExtension(source).ToLowerInvariant(); return e.Length is > 1 and <= 13 && e.Skip(1).All(char.IsAsciiLetterOrDigit) ? e : ".bin"; }
    public static string Relative(PlanningProjectCopy copy) => $"project-copies/{copy.ProjectId:N}/{copy.DocumentId:N}/{copy.Kind}/{copy.ReferenceId:N}/{copy.Sha256.ToLowerInvariant()}{Extension(copy.OriginalPath)}";
    public static PlanningDocument Bind(PlanningDocument document, string root) => document with
    {
        References = document.References.Select(r => r with { ReconnectedFile = BindExternal(r.ReconnectedFile,r.Source.ReferenceId,"reference",r.Source.ExternalReference), ProjectCopy = BindCopy(r.ProjectCopy, document, r.Source.ReferenceId, "reference", r.Source.ExternalReference, root) }).ToArray(),
        ShotExternalLinks = BindShotLinks(document,root),
        Attachments = document.Attachments.Select(a => a with { ReconnectedFile = BindExternal(a.ReconnectedFile,a.Id,"attachment",a.Path), ProjectCopy = BindCopy(a.ProjectCopy, document, a.Id, "attachment", a.Path, root) }).ToArray()
    };
    private static IReadOnlyList<PlanningShotExternalLink> BindShotLinks(PlanningDocument document,string root)
    {
        if(document.ShotExternalLinks.Select(s=>s.ReferenceId).Distinct().Count()!=document.ShotExternalLinks.Count)
            throw new InvalidDataException("重复镜头外链收据ID。");
        return document.ShotExternalLinks.Select(link=>
        {
            link.Validate();
            // Legacy identity conflicts remain visible for explicit repair; package validation still rejects them.
            return link with{ProjectCopy=BindCopy(link.ProjectCopy,document,link.ReferenceId,"reference",link.OriginalPath,root)};
        }).ToArray();
    }
    private static PlanningExternalFile? BindExternal(PlanningExternalFile? file,Guid id,string kind,string? original)
    {
        if(file is not null){if(original is null)throw new InvalidDataException("重连无原路径");file.Validate(id,kind,original);}return file;
    }
    private static PlanningProjectCopy? BindCopy(PlanningProjectCopy? copy, PlanningDocument document, Guid reference, string kind, string? original, string root)
    {
        if (copy is null) return null;
        copy.Validate();
        if (document.Package is not {} package || copy.ProjectId != package.ProjectId || copy.DocumentId != package.DocumentId || copy.ReferenceId != reference || copy.Kind != kind || original is null || !Path.IsPathFullyQualified(original) || !Path.GetFullPath(copy.OriginalPath).Equals(Path.GetFullPath(original),StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("项目副本与当前文档/引用身份不匹配。");
        return copy with { StorageRoot = Path.GetFullPath(root) };
    }
    public static string Resolve(PlanningProjectCopy copy)
    {
        copy.Validate();
        if (copy.StorageRoot is null) throw new InvalidDataException("项目副本未绑定当前存储根。");
        var path = Path.GetFullPath(Path.Combine(copy.StorageRoot, copy.RelativePath));
        PlanningImageStorage.RejectLinks(copy.StorageRoot, path);
        return path;
    }
    /// <summary>Same seekable read lease is held through hash verification and decode; no full encoded-file allocation.</summary>
    public static FileStream OpenVerifiedRead(PlanningProjectCopy copy,CancellationToken token=default)
    {
        var path=Resolve(copy);var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.SequentialScan);
        try
        {
            if(stream.Length!=copy.Bytes||stream.Length<1||stream.Length>MaximumBytes)throw new InvalidDataException("项目自有副本大小变化或超限；未读取原来源。");
            using var hash=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);var buffer=new byte[81920];int count;
            while((count=stream.Read(buffer))>0){token.ThrowIfCancellationRequested();hash.AppendData(buffer,0,count);}token.ThrowIfCancellationRequested();PlanningImageStorage.RejectLinks(copy.StorageRoot!,path);
            if(!Convert.ToHexString(hash.GetHashAndReset()).Equals(copy.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("项目自有副本已变化（SHA-256不符）；未回退原来源。");
            stream.Position=0;return stream;
        }
        catch{stream.Dispose();throw;}
    }
    public static byte[] ReadVerified(PlanningProjectCopy copy)
    {
        var path = Resolve(copy);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != copy.Bytes || stream.Length < 1 || stream.Length > MaximumBytes) throw new InvalidDataException("项目自有副本大小变化或超限；未读取原来源。");
        var data = new byte[checked((int)stream.Length)]; stream.ReadExactly(data);
        PlanningImageStorage.RejectLinks(copy.StorageRoot!, path);
        if (!Convert.ToHexString(SHA256.HashData(data)).Equals(copy.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("项目自有副本已变化（SHA-256不符）；未回退原来源。");
        return data;
    }
    public static string Status(PlanningProjectCopy copy)
    {
        try { ReadVerified(copy); return "项目自有副本 · SHA-256已核对"; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return "项目自有副本不可用：" + error.Message + "（未回退外链）"; }
    }
    public static Task<PlanningProjectCopy> ImportCanvasAsync(string root,Guid canvas,Guid reference,string source,CancellationToken token=default)=>CopyAsync(root,canvas,canvas,reference,"reference",source,new List<string>(),token);
    internal static async Task<PlanningProjectCopy> CopyAsync(string root, Guid project, Guid document, Guid reference, string kind, string source,
        ICollection<string> created, CancellationToken token, IProgress<long>? progress = null, string? recordedOriginal = null)
    {
        source = Path.GetFullPath(source);
        // Reject streams/devices and all existing reparse ancestors before opening the source.
        if (source.AsSpan(Path.GetPathRoot(source)!.Length).Contains(':') || source.StartsWith(@"\\?\", StringComparison.Ordinal) || source.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new InvalidDataException("不支持设备或备用数据流作为导入来源。");
        PlanningImageStorage.RejectLinks(Path.GetPathRoot(source)!, source);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length < 1 || input.Length > MaximumBytes) throw new InvalidDataException("导入仅支持1字节至64MB的普通文件。");
        var length = input.Length; var modified = File.GetLastWriteTimeUtc(source);
        var staging = Path.Combine(root, "project-copies", project.ToString("N"), document.ToString("N"), ".staging");
        PlanningImageStorage.RejectLinks(root, staging); Directory.CreateDirectory(staging); PlanningImageStorage.RejectLinks(root, staging);
        var temporary = Path.Combine(staging, Guid.NewGuid().ToString("N") + ".part");
        var journal=temporary+".owner.json";var ownership=PlanningCopyRecovery.New(project,document,temporary);PlanningCopyRecovery.Save(journal,ownership);var completed=false;
        try
        {
            string digest;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    var buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                    {
                        token.ThrowIfCancellationRequested(); total += count;
                        if (total > MaximumBytes || total > length) throw new InvalidDataException("复制期间来源大小变化。");
                        hash.AppendData(buffer, 0, count); await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false); progress?.Report(total);
                    }
                    if (total != length) throw new InvalidDataException("复制期间来源被截断。");
                    await output.FlushAsync(token).ConfigureAwait(false); output.Flush(true);
                }
                digest = Convert.ToHexString(hash.GetHashAndReset());
            }
            token.ThrowIfCancellationRequested(); PlanningImageStorage.RejectLinks(Path.GetPathRoot(source)!, source);
            if (input.Length != length || File.GetLastWriteTimeUtc(source) != modified) throw new InvalidDataException("复制期间来源已变化。");
            var copy = new PlanningProjectCopy(project, document, reference, kind, recordedOriginal ?? source, "", digest, length, DateTimeOffset.UtcNow) { StorageRoot = Path.GetFullPath(root) };
            copy = copy with { RelativePath = Relative(copy) }; copy.Validate();
            PlanningCopyRecovery.Save(journal,ownership with{Copy=copy});
            var target = Resolve(copy); Directory.CreateDirectory(Path.GetDirectoryName(target)!); PlanningImageStorage.RejectLinks(root, target);
            PlanningImageStorage.RejectLinks(root, temporary);
            if (File.Exists(target)) ReadVerified(copy);
            else { File.Move(temporary, target, false); created.Add(target); }
            ReadVerified(copy); completed=true; return copy;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary);if(!completed&&File.Exists(journal))File.Delete(journal); }
    }
}
