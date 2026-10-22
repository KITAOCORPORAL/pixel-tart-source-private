using System.Security.Cryptography;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>A user-selected local file. The original path and reference ID are never rewritten.</summary>
public sealed record PlanningExternalFile(Guid ReferenceId, string Kind, string OriginalPath, string SelectedPath,
    string FileType, long Bytes, string Sha256, DateTimeOffset VerifiedAt)
{
    public void Validate(Guid id, string kind, string original)
    {
        if (ReferenceId != id || Kind != kind || OriginalPath is null || !OriginalPath.Equals(original, StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(SelectedPath) || Bytes is < 1 or > PlanningProjectCopyStorage.MaximumBytes ||
            Sha256 is null || Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(FileType))
            throw new InvalidDataException("重连文件身份与引用不符。");
    }
    public static async Task<PlanningExternalFile> InspectAsync(Guid id, string kind, string original, string selected, CancellationToken token = default)
    {
        if (id == Guid.Empty || kind is not ("reference" or "attachment") || !Path.IsPathFullyQualified(original))
            throw new InvalidDataException("仅支持有明确原路径的外部参考图或普通附件。");
        selected = Path.GetFullPath(selected);
        if (selected.StartsWith(@"\\?\", StringComparison.Ordinal) || selected.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            selected.AsSpan(Path.GetPathRoot(selected)!.Length).Contains(':')) throw new InvalidDataException("不支持设备或备用数据流。");
        PlanningImageStorage.RejectLinks(Path.GetPathRoot(selected)!, selected);
        await using var stream = new FileStream(selected, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (stream.Length is < 1 or > PlanningProjectCopyStorage.MaximumBytes) throw new InvalidDataException("文件须为1字节至64MB。");
        var bytes = stream.Length; var header = new byte[Math.Min(16, (int)Math.Min(bytes, 16))]; await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        var ext = Path.GetExtension(selected).ToLowerInvariant();
        bool Image() => ext switch
        {
            ".png" => header.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}),
            ".jpg" or ".jpeg" => header.AsSpan().StartsWith(new byte[]{255,216,255}),
            ".gif" => header.AsSpan().StartsWith("GIF8"u8),
            ".bmp" => header.AsSpan().StartsWith("BM"u8),
            ".tif" or ".tiff" => header.AsSpan().StartsWith(new byte[]{73,73,42,0}) || header.AsSpan().StartsWith(new byte[]{77,77,0,42}),
            _ => false
        };
        var originalExtension=Path.GetExtension(original).ToLowerInvariant();
        if(kind=="reference" && ext!=originalExtension && !((ext is ".jpg" or ".jpeg") && (originalExtension is ".jpg" or ".jpeg")) && !((ext is ".tif" or ".tiff") && (originalExtension is ".tif" or ".tiff")))
            throw new InvalidDataException("替代参考图须与原文件格式相同；请先转换为原格式再定位。");
        var image = Image();
        if (kind == "reference" && !image) throw new InvalidDataException("参考图须为签名与扩展名匹配的PNG/JPEG/GIF/BMP/TIFF。");
        if (kind == "attachment")
        {
            if (!ext.Equals(Path.GetExtension(original), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("附件类型须与原附件扩展名相同。");
            var valid = image || ext switch
            {
                ".pdf" => header.AsSpan().StartsWith("%PDF-"u8),
                ".docx" or ".xlsx" or ".pptx" => header.AsSpan().StartsWith(new byte[]{80,75,3,4}),
                ".txt" or ".md" or ".csv" or ".json" => !header.Contains((byte)0),
                ".rtf" => header.AsSpan().StartsWith("{\\rtf"u8),
                _ => false
            };
            if (!valid) throw new InvalidDataException("不支持此普通附件类型或文件签名不符；不会执行文件。");
        }
        if (kind=="attachment" && ext is ".docx" or ".xlsx" or ".pptx")
        {
            stream.Position=0;
            using var zip=new System.IO.Compression.ZipArchive(stream,System.IO.Compression.ZipArchiveMode.Read,true);
            var expected=ext==".docx"?"word/document.xml":ext==".xlsx"?"xl/workbook.xml":"ppt/presentation.xml";
            if(zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry(expected) is null)throw new InvalidDataException("Office附件包结构与扩展名不符。");
        }
        stream.Position = 0; var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        if (stream.Length != bytes) throw new IOException("核对期间文件大小变化。");
        return new(id, kind, original, selected, ext, bytes, hash, DateTimeOffset.UtcNow);
    }
    public static string ResolveVerified(PlanningExternalFile file, Guid id, string kind, string original)
    {
        file.Validate(id, kind, original);
        var actual = InspectAsync(id, kind, original, file.SelectedPath).GetAwaiter().GetResult();
        if (!file.SameContent(actual)) throw new InvalidDataException("重连文件大小、类型或SHA-256已变化；未回退原位置。");
        return file.SelectedPath;
    }
    public bool SameContent(PlanningExternalFile other) => SelectedPath.Equals(other.SelectedPath, StringComparison.OrdinalIgnoreCase) &&
        FileType == other.FileType && Bytes == other.Bytes && Sha256.Equals(other.Sha256, StringComparison.OrdinalIgnoreCase);
}

public sealed partial class PlanningProjectStore
{
    public Task ReconnectExternalAsync(Guid projectId, PlanningExternalFile selected, bool importCopy, CancellationToken token = default) =>
        ReconnectExternalCoreAsync(projectId,selected,importCopy,null,null,token);

    public Task ReconnectExternalAsync(Guid projectId,PlanningExternalFile selected,bool importCopy,ProjectShotStore shots,
        IReadOnlyList<Guid>? expectedShotIds=null,CancellationToken token=default) =>
        shots.WithCatalogLockAsync(projectId,catalog=>ReconnectExternalCoreAsync(projectId,selected,importCopy,catalog,expectedShotIds,token),token);

    private async Task ReconnectExternalCoreAsync(Guid projectId,PlanningExternalFile selected,bool importCopy,
        ProjectShotCatalog? catalog,IReadOnlyList<Guid>? expectedShotIds,CancellationToken token)
    {
        var created = new List<string>();
        await UpdateAsync(projectId, async state =>
        {
            var document = (state.Document ?? throw new InvalidDataException("无策划文档")).EnsureIdentity(projectId);
            var refs = document.References.Where(x => x.Source.ReferenceId == selected.ReferenceId).ToArray();
            var files = document.Attachments.Where(x => x.Id == selected.ReferenceId).ToArray();
            var occurrences=catalog?.Shots.Where(s=>s.References.Any(r=>r.ReferenceId==selected.ReferenceId)).ToArray()??[];
            if(expectedShotIds is not null && !expectedShotIds.Order().SequenceEqual(occurrences.Select(s=>s.ShotId).Order()))
                throw new InvalidDataException("确认期间镜头引用归属变化；请重新选择。");
            var shotOnly=selected.Kind=="reference" && refs.Length==0 && occurrences.Length>0;
            if(refs.Length+files.Length!=(shotOnly?0:1) || (selected.Kind=="reference" && files.Length>0) ||
                (selected.Kind=="attachment" && refs.Length>0) || (selected.Kind=="attachment" && occurrences.Length>0))
                throw new InvalidDataException("同ID冲突或引用已移除；未改写现有数据。");
            if(selected.Kind=="reference" && catalog is not null && PlanningReferenceLinks.HasConflict(document,catalog.Shots,selected.ReferenceId))
                throw new InvalidDataException("同ID文档/镜头来源冲突；未改写现有数据。");
            var shotSource=occurrences.FirstOrDefault()?.References.Single(r=>r.ReferenceId==selected.ReferenceId);
            var original=refs.Length==1?refs[0].Source.ExternalReference:files.Length==1?files[0].Path:shotSource?.ExternalReference;
            var previousShot=document.ShotExternalLinks.SingleOrDefault(r=>r.ReferenceId==selected.ReferenceId);
            if(original is null || refs.Any(r=>r.Source.LibraryId is not null || r.ProjectCopy is not null) ||
                files.Any(f=>f.ProjectCopy is not null) || occurrences.Any(s=>s.References.Any(r=>r.ReferenceId==selected.ReferenceId && r.LibraryId is not null)) ||
                previousShot?.ProjectCopy is not null || (previousShot is not null && previousShot.OriginalPath!=original))
                throw new InvalidDataException("素材库、冲突来源或已有自有副本不可被重连覆盖。");
            selected.Validate(selected.ReferenceId,selected.Kind,original);
            var verified=await PlanningExternalFile.InspectAsync(selected.ReferenceId,selected.Kind,original,selected.SelectedPath,token).ConfigureAwait(false);
            if(!selected.SameContent(verified))throw new InvalidDataException("确认后文件类型、大小或SHA-256变化；请重新选择。");
            // A repeated approval is idempotent and never demotes/overwrites an owned copy.
            PlanningProjectCopy? copy=null;
            if(importCopy)
            {
                copy=await PlanningProjectCopyStorage.CopyAsync(StorageRoot,projectId,document.Package!.DocumentId,
                    selected.ReferenceId,selected.Kind,selected.SelectedPath,created,token,recordedOriginal:original).ConfigureAwait(false);
                if(copy.Bytes!=selected.Bytes || !copy.Sha256.Equals(selected.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("复制期间文件内容变化；策划未提交。");
            }
            token.ThrowIfCancellationRequested();
            var shotLinks=document.ShotExternalLinks.Where(r=>r.ReferenceId!=selected.ReferenceId).ToList();
            if(shotOnly)shotLinks.Add(new(selected.ReferenceId,original,occurrences.Select(s=>s.ShotId).ToArray(),verified,copy));
            return state with{Document=document with
            {
                References=document.References.Select(x=>x.Source.ReferenceId==selected.ReferenceId?x with{ReconnectedFile=verified,ProjectCopy=copy}:x).ToArray(),
                Attachments=document.Attachments.Select(x=>x.Id==selected.ReferenceId?x with{ReconnectedFile=verified,ProjectCopy=copy}:x).ToArray(),
                ShotExternalLinks=shotLinks
            }};
        },token,()=>{foreach(var path in created){PlanningImageStorage.RejectLinks(StorageRoot,path);if(File.Exists(path))File.Delete(path);}}).ConfigureAwait(false);
    }
}
