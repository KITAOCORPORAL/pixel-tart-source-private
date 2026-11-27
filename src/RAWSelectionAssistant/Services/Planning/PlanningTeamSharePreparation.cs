#if PLANNING_REVIEW_PILOT
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services.Planning;

public sealed record PlanningTeamShareFile(string RelativePath, string LocalPath, long Length, string Sha256);
public sealed record PlanningTeamShareIssue(string Section, string Item, string Message, bool Blocking = false);

public sealed class PlanningTeamSharePrepared : IDisposable
{
    private readonly string directory;
    internal PlanningTeamSharePrepared(Guid projectId, Guid documentId, string fingerprint, string directory,
        IReadOnlyList<PlanningTeamShareFile> files, IReadOnlyList<PlanningTeamShareIssue> issues)
    { ProjectId = projectId; DocumentId = documentId; Fingerprint = fingerprint; this.directory = directory; Files = files; Issues = issues; }
    public Guid ProjectId { get; }
    public Guid DocumentId { get; }
    public string Fingerprint { get; }
    public IReadOnlyList<PlanningTeamShareFile> Files { get; }
    public IReadOnlyList<PlanningTeamShareIssue> Issues { get; }
    public void Dispose() => PlanningTeamSharePreparation.DeleteStage(directory);
}

/// <summary>Reads saved bytes without opening the planning store, which can perform copy recovery.</summary>
public static class PlanningTeamSharePreparation
{
    private const long MaxJson = 64L * 1024 * 1024;
    private static string StageParent => Path.GetFullPath(Path.Combine(AppDataPaths.Root, "PlanningTeamShare", "Staging"));
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Leaf(string? value) => Path.GetFileName((value ?? "").Replace('\\', '/'));
    private static string PrivatePath(Guid id, string extension) => Path.Combine(Path.GetPathRoot(StageParent)!, "PlanningShare", "assets", id.ToString("N") + extension);
    private static string SourceLabel(string value) => value.IndexOfAny(['/', '\\']) >= 0 ? Leaf(value) : value;
    private static string ImageSection(PlanningDocumentImage image) => PlanningReadableSummaryBuilder.SectionOrder[PlanningReadableSummaryBuilder.PlacementSection(image.Placement)];

    internal static void DeleteStage(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!string.Equals(Path.GetDirectoryName(full), StageParent, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(full), "N", out _)) return;
        try { PlanningImageStorage.RejectLinks(StageParent, full); if (Directory.Exists(full)) Directory.Delete(full, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static async Task<PlanningTeamSharePrepared> PrepareAsync(Guid projectId, Guid documentId, string planningRoot, string shotRoot, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty || documentId == Guid.Empty) throw new InvalidDataException("请先保存当前策划案。");
        planningRoot = Path.GetFullPath(planningRoot);
        shotRoot = Path.GetFullPath(shotRoot);
        string planName = $"{projectId:N}.planning.json", shotName = $"{projectId:N}.shots.json";
        string planPath = Path.Combine(planningRoot, planName), shotPath = Path.Combine(shotRoot, shotName);
        PlanningImageStorage.RejectLinks(planningRoot, planPath);
        PlanningImageStorage.RejectLinks(shotRoot, shotPath);
        if (File.Exists(planPath + ".draft")) throw new InvalidDataException("当前策划案还有未恢复的本地草稿，请先处理草稿。");
        if (new FileInfo(planPath).Length > MaxJson || File.Exists(shotPath) && new FileInfo(shotPath).Length > MaxJson)
            throw new InvalidDataException("本地已保存的策划或镜头文件超过 64 MiB。");
        var initialFingerprint = await PlanningTeamShareClient.ReadLocalFingerprintAsync(projectId, planningRoot, shotRoot, ct).ConfigureAwait(false);
        var planBytes = await ReadBoundedAsync(planPath, ct).ConfigureAwait(false);
        var state = JsonSerializer.Deserialize<PlanningProjectState>(planBytes) ?? throw new InvalidDataException("已保存的策划文件为空。");
        var doc = state.Document?.EnsureIdentity(projectId) ?? throw new InvalidDataException("已保存的策划正文不存在。");
        var package = doc.Package!;
        if (state.ProjectId != projectId || package.DocumentId != documentId) throw new InvalidDataException("策划文件不属于当前项目和文档。");
        var hasShots = File.Exists(shotPath);
        var shotBytes = hasShots ? await ReadBoundedAsync(shotPath, ct).ConfigureAwait(false) : null;
        var catalog = shotBytes is not null ? JsonSerializer.Deserialize<ProjectShotCatalog>(shotBytes)
            ?? throw new InvalidDataException("镜头文件为空。") : new ProjectShotCatalog(projectId, []);
        if (catalog.ProjectId != projectId || catalog.Shots.Any(s => s.ProjectId != projectId) || catalog.Shots.Select(s => s.ShotId).Distinct().Count() != catalog.Shots.Count)
            throw new InvalidDataException("镜头身份与当前项目不符。");
        var activeShots = catalog.Shots.Where(s => !s.IsArchived).ToArray();
        var issues = new List<PlanningTeamShareIssue>();
        var files = new List<PlanningTeamShareFile>();
        var stage = Path.Combine(StageParent, Guid.NewGuid().ToString("N"));
        PlanningImageStorage.RejectLinks(StageParent, stage);
        Directory.CreateDirectory(stage);
        try
        {
            if (doc.References.Select(r => r.Source.ReferenceId).Distinct().Count() != doc.References.Count ||
                doc.ShotExternalLinks.Select(r => r.ReferenceId).Distinct().Count() != doc.ShotExternalLinks.Count ||
                (package.RichLayout?.Images ?? []).Select(i => i.DocumentImageId).Distinct().Count() != (package.RichLayout?.Images.Count ?? 0))
                throw new InvalidDataException("策划中有重复的参考资料身份，请先修复。");
            // Stable library identities can be represented by an already-owned document PNG.
            // Merely naming an external path or another shot's unrelated image proves no ownership.
            bool HasOwnedLibraryImage(ProjectShotReference r) => r.LibraryId is Guid library && r.AssetId is Guid asset &&
                (package.RichLayout?.Images ?? []).Any(i=>i.Source is { } src && src.LibraryId==library && src.AssetId==asset);
            var refs = new List<PlanningDocumentReference>();
            foreach (var reference in doc.References)
            {
                var id = reference.Source.ReferenceId;
                var title = SourceLabel(reference.Source.Title ?? id.ToString("D"));
                string[] sections = ["参考图", .. (reference.IsMoodboard ? new[] { "情绪板" } : Array.Empty<string>()), .. (reference.IsHero ? new[] { "文字" } : Array.Empty<string>()), .. (reference.Category == "灯光" ? new[] { "灯位图" } : Array.Empty<string>()), .. (reference.Category == "造型" ? new[] { "服化道与道具" } : Array.Empty<string>())];
                var usedByShots = activeShots.Where(s => s.References.Any(r => r.ReferenceId == id)).ToArray();
                if (usedByShots.Length > 0) sections = [.. sections, "镜头清单与分镜" ];
                var copy = reference.ProjectCopy is null && HasOwnedLibraryImage(reference.Source) ? null : await StageCopyAsync(reference.ProjectCopy, id, title, sections, reference.Source.ExternalReference, required: reference.IsHero || usedByShots.Length > 0).ConfigureAwait(false);
                refs.Add(reference with { Source = RedactReference(reference.Source, copy), ProjectCopy = copy, ReconnectedFile = null, Extensions = null });
            }
            var shotLinks = new List<PlanningShotExternalLink>();
            foreach (var link in doc.ShotExternalLinks.Where(l => l.ShotIds.Any(id => activeShots.Any(s => s.ShotId == id))))
            {
                link.Validate();
                var assigned = activeShots.Where(s => lMatches(s, link)).ToArray();
                if (assigned.Length != activeShots.Count(s => link.ShotIds.Contains(s.ShotId))) throw new InvalidDataException("镜头参考收据与保存的镜头身份不匹配。");
                var title = string.Join("、", assigned.Select(s => s.Name));
                var copy = await StageCopyAsync(link.ProjectCopy, link.ReferenceId, title, ["镜头清单与分镜"], link.OriginalPath, required: true).ConfigureAwait(false);
                if (copy is not null) shotLinks.Add(link with { OriginalPath = copy.OriginalPath, ShotIds = assigned.Select(s => s.ShotId).ToArray(), ProjectCopy = copy, ReconnectedFile = null });
            }
            var shareCopies = refs.Where(r => r.ProjectCopy is not null).Select(r => r.ProjectCopy!).Concat(shotLinks.Select(r => r.ProjectCopy!)).ToDictionary(r => r.ReferenceId);
            var stagedShots = catalog.Shots.Select(s => s.Normalize() with
            {
                Extensions = null,
                References = s.References.Select(r =>
                {
                    if (!s.IsArchived && !shareCopies.ContainsKey(r.ReferenceId) && !doc.ShotExternalLinks.Any(l => l.ReferenceId == r.ReferenceId) && !doc.References.Any(l => PlanningReferenceLinks.SameSource(l.Source,r)) && !HasOwnedLibraryImage(r))
                        issues.Add(new("镜头清单与分镜", s.Name, $"参考图“{SourceLabel(r.Title ?? r.ReferenceId.ToString("D"))}”没有可分享的项目自有副本。", true));
                    return RedactReference(r, shareCopies.GetValueOrDefault(r.ReferenceId));
                }).ToArray()
            }).ToArray();
            var richImages = new List<PlanningDocumentImage>();
            foreach (var image in package.RichLayout?.Images ?? [])
            {
                var relative = PlanningImageStorage.RelativePath(documentId, image.DocumentImageId);
                if (image.DocumentId != documentId || !image.CopyPath.Replace('\\','/').Equals(relative, StringComparison.OrdinalIgnoreCase))
                { issues.Add(new(ImageSection(image), image.Title, "图片不在其项目自有的规范目录中。", true)); continue; }
                var source = Path.Combine(planningRoot, relative);
                try
                {
                    PlanningImageStorage.RejectLinks(planningRoot, source);
                    await using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await StageImageAsync(stream, relative, image.CopyHash, mime: "png").ConfigureAwait(false);
                    richImages.Add(image with { CopyPath = relative, StorageRoot = null });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                { issues.Add(new(ImageSection(image), image.Title, "项目自有图片缺失、已变化或不可读取。", true)); }
            }
            var ownedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var image in richImages) ownedPaths.Add(Path.GetFullPath(Path.Combine(planningRoot, image.CopyPath)));
            var resources = doc.Resources.Items.Select(r => r switch
            {
                PlanningProp prop => prop with { Source = SourceLabel(prop.Source), Extensions = null },
                PlanningGearItem gear => gear with { SourceCatalog = SourceLabel(gear.SourceCatalog), SourceId = SourceLabel(gear.SourceId), Extensions = null },
                PlanningLight light => RedactLight(light),
                _ => r with { Extensions = null }
            }).ToArray();
            var revisions = new List<PlanningCanvasRevision>();
            foreach (var rev in package.CanvasRevisions)
            {
                var section = resources.OfType<PlanningLight>().Any(l => l.CanvasRevisionId == rev.RevisionId) ? "灯位图" : "文档内容";
                if (string.IsNullOrEmpty(rev.PreviewPngBase64)) { issues.Add(new(section, rev.EditableSnapshot.Name, "没有已保存的固定画布快照。未读取当前画布或原图。")); continue; }
                var png = Convert.FromBase64String(rev.PreviewPngBase64);
                if (!IsPng(png)) { issues.Add(new(section, rev.EditableSnapshot.Name, "固定画布快照格式无效。", true)); continue; }
                revisions.Add(rev with { EditableSnapshot = rev.EditableSnapshot with { Objects = [], StorageRoot = null }, Extensions = null });
            }
            var attachments = doc.Attachments.Select(a =>
            {
                var name = Leaf(a.Name);
                issues.Add(new("文件", name, "仅保留附件名称；不包含附件内容或本机路径。"));
                return a with { Path = "", Name = name, ProjectCopy = null, ReconnectedFile = null, Extensions = null };
            }).ToArray();
            if (doc.Extensions?.ContainsKey("PixelTartReviewV1") != true && doc.Extensions?.ContainsKey("PixelTartWorkbenchV1") != true && doc.Extensions?.Keys.Any(k => k.Contains("Comment", StringComparison.OrdinalIgnoreCase) || k.Contains("Annotation", StringComparison.OrdinalIgnoreCase)) == true)
                issues.Add(new("文字", "历史批注", "检测到没有受支持锚点结构的扩展；原始扩展留在桌面策划中。"));
            if (state.BookingId is not null) issues.Add(new("档期", "关联档期", "保留已保存的关联 ID 与日期；未读取独立的档期数据库。"));
            foreach (var link in state.VisualLinks ?? []) if (!revisions.Any(r => r.CanvasId == link.TargetId)) issues.Add(new("情绪板", link.Title, "外部版面未包含已保存的固定快照，保留引用身份。"));
            if (package.Assets.Count > 0) issues.Add(new("文档内容", "库素材", "仅保留库素材引用身份；未读取原件。"));
            var sharedDocument = doc with
            {
                Package = package with { Assets = package.Assets.Select(a => a with { Extensions = null }).ToArray(), Extensions = null,
                    CanvasRevisions = revisions, RichLayout = package.RichLayout is { } layout ? layout with
                    { Extensions = null, Blocks = layout.Blocks.Select(b => b with { Extensions = null }).ToArray(), Page = layout.Page with { Extensions = null }, Images = richImages } : null },
                References = refs, Attachments = attachments, ShotExternalLinks = shotLinks, ShotColumns = doc.ShotColumns.Select(c => c with { Extensions = null }).ToArray(),
                Resources = doc.Resources with { Items = resources, Extensions = null },
                Presentation = doc.Presentation with { Blocks = doc.Presentation.Blocks.Select(b=>b with { Extensions=null }).ToArray(), Extensions=null },
                UiLayout = new(),
                Extensions = doc.Extensions?.Where(e => e.Key is "PixelTartReviewV1" or "PixelTartWorkbenchV1").ToDictionary()
            };
            if (issues.Any(i => i.Blocking)) return new(projectId, documentId, initialFingerprint, stage, [], issues.AsReadOnly());
            var shareState = state with { Document = sharedDocument, Extensions = null, CapturedAssets = [], ReferenceOutcomes = [],
                Summary = state.Summary is { } summary ? summary with { Extensions = null } : null,
                VisualLinks = state.VisualLinks?.Select(l => l with { Extensions = null }).ToArray() };
            if (sharedDocument.Package!.RichTextPackageBase64 is { } rich)
            {
                var sanitized = await OnStaAsync(() => PlanningShareRichProjection.SanitizeRtf(rich), ct).ConfigureAwait(false);
                if (sanitized.RemovedObjects > 0) issues.Add(new("文字", "内嵌对象", "仅迁移带项目自有身份的图片和固定画布快照；其他内嵌对象留在桌面原稿中。"));
                shareState = shareState with { Document = sharedDocument with { Package = sharedDocument.Package with { RichTextPackageBase64 = sanitized.Rtf } } };
            }
            var sharedBytes = JsonSerializer.SerializeToUtf8Bytes(shareState);
            PlanningShareRichSnapshot? projected = null;
            if (sharedDocument.Package.RichTextPackageBase64 is not null)
            {
                var snapshot = await OnStaAsync(() => PlanningShareRichProjection.Capture(sharedBytes, projectId), ct).ConfigureAwait(false);
                if (snapshot is null) throw new InvalidDataException("富文本投影未生成。");
                foreach (var loss in snapshot.Losses) issues.Add(new("文字", "排版", loss));
                projected = snapshot;
            }
            if (issues.Count > 0)
            {
                var final = shareState.Document!;
                var extensions = final.Extensions ?? new Dictionary<string, JsonElement>();
                extensions[PlanningShareSectionNotice.Extension] = JsonSerializer.SerializeToElement(issues.Where(i => !i.Blocking)
                    .Select(i => new PlanningShareSectionNotice(i.Section, i.Item, i.Message)).Distinct(), PlanningShareRichSnapshot.Json);
                sharedBytes = JsonSerializer.SerializeToUtf8Bytes(shareState with { Document = final with { Extensions = extensions } });
            }
            if (projected is not null)
                await StageBytesAsync(PlanningShareRichSnapshot.FileName, PlanningShareRichProjection.Serialize(projected with { PlanSha256 = Digest(sharedBytes) })).ConfigureAwait(false);
            await StageBytesAsync("ProjectPlanning/" + planName, sharedBytes).ConfigureAwait(false);
            if (hasShots) await StageBytesAsync("ProjectShots/" + shotName, JsonSerializer.SerializeToUtf8Bytes(catalog with { Shots = stagedShots, Extensions = null })).ConfigureAwait(false);
            if (File.Exists(planPath + ".draft") || initialFingerprint != await PlanningTeamShareClient.ReadLocalFingerprintAsync(projectId, planningRoot, shotRoot, ct).ConfigureAwait(false) ||
                !await MatchesSavedBytesAsync(planPath, planBytes, ct).ConfigureAwait(false) || !await MatchesSavedBytesAsync(shotPath, shotBytes, ct).ConfigureAwait(false))
                throw new InvalidDataException("策划或镜头清单在准备期间已变化，请重新预检。");
            if (files.Count > 2048 || files.Sum(f => f.Length) > 1024L * 1024 * 1024) throw new InvalidDataException("策划分享资料超过文件数或空间限制。");
            return new(projectId, documentId, initialFingerprint, stage, files.AsReadOnly(), issues.AsReadOnly());

            PlanningLight RedactLight(PlanningLight light)
            {
                if (!string.IsNullOrWhiteSpace(light.ReferencePath) && (!Path.IsPathFullyQualified(light.ReferencePath) || !ownedPaths.Contains(Path.GetFullPath(light.ReferencePath))))
                    issues.Add(new("灯位图", light.Name, "灯位外部参考文件未包含在分享中。"));
                return light with { ReferencePath = "", Extensions = null };
            }
            static bool lMatches(ProjectShot s, PlanningShotExternalLink link) => link.ShotIds.Contains(s.ShotId) && s.References.Any(r => r.ReferenceId == link.ReferenceId && r.LibraryId is null && r.ExternalReference == link.OriginalPath);
            static ProjectShotReference RedactReference(ProjectShotReference source, PlanningProjectCopy? copy) => source with
            {
                ExternalReference = source.LibraryId is not null ? null : copy?.OriginalPath ?? PrivatePath(source.ReferenceId, ".unavailable"),
                Title = source.Title is null ? null : SourceLabel(source.Title), Details = null, Extensions = null
            };
            async Task<PlanningProjectCopy?> StageCopyAsync(PlanningProjectCopy? item, Guid id, string title, string[] sectionNames, string? original, bool required)
            {
                void Issue(string message, bool block = false) { foreach (var section in sectionNames) issues.Add(new(section, title, message, block)); }
                if (item is null) { Issue("没有可分享的项目自有副本；未读取或上传原来源。", required); return null; }
                try
                {
                    item.Validate();
                    if (item.ProjectId != projectId || item.DocumentId != documentId || item.ReferenceId != id || item.Kind != "reference" ||
                        original is null || !item.OriginalPath.Equals(original, StringComparison.OrdinalIgnoreCase) || PlanningReferenceLinks.HasConflict(doc, catalog.Shots, id))
                        throw new InvalidDataException("参考副本身份或来源冲突。");
                    var extension = Path.GetExtension(item.OriginalPath).ToLowerInvariant();
                    if (extension is not (".jpg" or ".jpeg" or ".png")) { Issue("此项目自有副本不是 JPEG/PNG。", required); return null; }
                    var relative = item.RelativePath;
                    using var stream = PlanningProjectCopyStorage.OpenVerifiedRead(item with { StorageRoot = planningRoot }, ct);
                    await StageImageAsync(stream, relative, item.Sha256, extension == ".png" ? "png" : "jpeg").ConfigureAwait(false);
                    return item with { OriginalPath = PrivatePath(id, extension), StorageRoot = null };
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
                { Issue("项目自有副本缺失、已变化或归属无效；未读取原来源。", true); return null; }
            }
            async Task StageImageAsync(Stream stream, string relative, string expectedHash, string mime)
            {
                const int maxImage = 64 * 1024 * 1024;
                if (stream.Length is < 24 or > maxImage) throw new InvalidDataException("图片大小超出分享范围。");
                var header = new byte[24];
                await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
                if (mime == "png" ? !IsPng(header) : !(header[0] == 255 && header[1] == 216 && header[2] == 255))
                    throw new InvalidDataException("图片文件类型与已保存的引用不一致。");
                stream.Position = 0;
                var key = "ProjectPlanning/" + relative;
                var output = StagePath(key);
                await using (var copy = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    await stream.CopyToAsync(copy, ct).ConfigureAwait(false);
                await using var verify = File.OpenRead(output);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(verify, ct).ConfigureAwait(false));
                if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("图片副本 SHA-256 不符。");
                files.Add(new(key, output, verify.Length, hash));
            }
            async Task StageBytesAsync(string relative, byte[] bytes)
            {
                var path = StagePath(relative);
                if (bytes.LongLength > MaxJson) throw new InvalidDataException("分享资料中的单个文件超过 64 MiB。");
                await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
                files.Add(new(relative, path, bytes.LongLength, Digest(bytes)));
            }
            string StagePath(string relative)
            {
                var path = Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar));
                PlanningImageStorage.RejectLinks(stage, path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                return path;
            }
        }
        catch { DeleteStage(stage); throw; }
    }

    private static bool IsPng(byte[] bytes) => bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) &&
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) is > 0 and <= 20000 && BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) is > 0 and <= 20000;

    private static async Task<bool> MatchesSavedBytesAsync(string path, byte[]? expected, CancellationToken ct)
    {
        if (expected is null) return !File.Exists(path);
        if (!File.Exists(path)) return false;
        using var saved = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return saved.Length == expected.LongLength && (await SHA256.HashDataAsync(saved, ct).ConfigureAwait(false)).SequenceEqual(SHA256.HashData(expected));
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is < 1 or > MaxJson) throw new InvalidDataException("本地策划快照大小超出分享范围。");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, ct).ConfigureAwait(false);
        return bytes;
    }

    private static Task<T> OnStaAsync<T>(Func<T> run, CancellationToken ct)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { ct.ThrowIfCancellationRequested(); var value = run(); ct.ThrowIfCancellationRequested(); result.SetResult(value); }
            catch (OperationCanceledException) { result.SetCanceled(ct); }
            catch (Exception error) { result.SetException(error); }
        }) { IsBackground = true, Name = "PlanningTeamShareRich" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
}
#endif
