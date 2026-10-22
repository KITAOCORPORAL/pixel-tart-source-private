using System.Security.Cryptography;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>A user-selected local file. The original path and reference ID are never rewritten.</summary>
public sealed record PlanningExternalFile(Guid ReferenceId, string Kind, string OriginalPath, string SelectedPath,
    string FileType, long Bytes, string Sha256, DateTimeOffset VerifiedAt)
{
    public void Validate(Guid id, string kind, string original)
    {
        if (ReferenceId != id || Kind != kind || !OriginalPath.Equals(original, StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(SelectedPath) || Bytes is < 1 or > PlanningProjectCopyStorage.MaximumBytes ||
            Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(FileType))
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
    public async Task ReconnectExternalAsync(Guid projectId, PlanningExternalFile selected, bool importCopy, CancellationToken token = default)
    {
        var created = new List<string>();
        await UpdateAsync(projectId, async state =>
        {
            var document = (state.Document ?? throw new InvalidDataException("无策划文档")).EnsureIdentity(projectId);
            var refs = document.References.Where(x => x.Source.ReferenceId == selected.ReferenceId).ToArray();
            var files = document.Attachments.Where(x => x.Id == selected.ReferenceId).ToArray();
            if (refs.Length + files.Length != 1 || (selected.Kind == "reference") != (refs.Length == 1))
                throw new InvalidDataException("同ID冲突或引用已移除；未改写现有数据。");
            var original = refs.Length == 1 ? refs[0].Source.ExternalReference : files[0].Path;
            if (original is null || (refs.Length == 1 && (refs[0].Source.LibraryId is not null || refs[0].ProjectCopy is not null)) ||
                (files.Length == 1 && files[0].ProjectCopy is not null)) throw new InvalidDataException("素材库或已有自有副本不可被重连覆盖。");
            selected.Validate(selected.ReferenceId, selected.Kind, original);
            var verified = await PlanningExternalFile.InspectAsync(selected.ReferenceId, selected.Kind, original, selected.SelectedPath, token);
            if (!selected.SameContent(verified)) throw new InvalidDataException("确认后文件类型、大小或SHA-256变化；请重新选择。");
            PlanningProjectCopy? copy = null;
            if (importCopy)
            {
                copy = await PlanningProjectCopyStorage.CopyAsync(StorageRoot, projectId, document.Package!.DocumentId,
                    selected.ReferenceId, selected.Kind, selected.SelectedPath, created, token, recordedOriginal: original);
                if (copy.Bytes != selected.Bytes || !copy.Sha256.Equals(selected.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("复制期间文件内容变化；策划未提交。");
            }
            token.ThrowIfCancellationRequested();
            return state with { Document = document with
            {
                References = document.References.Select(x => x.Source.ReferenceId == selected.ReferenceId ? x with { ReconnectedFile = verified, ProjectCopy = copy } : x).ToArray(),
                Attachments = document.Attachments.Select(x => x.Id == selected.ReferenceId ? x with { ReconnectedFile = verified, ProjectCopy = copy } : x).ToArray()
            }};
        }, token, () => { foreach (var path in created) { PlanningImageStorage.RejectLinks(StorageRoot,path); if(File.Exists(path)) File.Delete(path); } });
    }
}
