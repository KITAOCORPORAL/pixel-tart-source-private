using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class OrganizeService(ILogService? logService = null)
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".JPG", ".JPEG", ".PNG", ".TIFF", ".TIF", ".WEBP", ".HEIC",
        ".ARW", ".CR2", ".CR3", ".NEF", ".NRW", ".RAF", ".DNG", ".RW2",
        ".ORF", ".ORI", ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<IReadOnlyList<OrganizePhotoItem>> ScanAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default,
        IProgress<OrganizeExecutionProgress>? progress = null)
    {
        var files = ExpandInputs(inputs).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var result = new List<OrganizePhotoItem>(files.Length);
        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await Task.Run(() => ReadPhoto(files[index]), cancellationToken).ConfigureAwait(false));
            progress?.Report(new(index + 1, files.Length, files[index]));
        }
        return result;
    }

    public IReadOnlyList<PhotoGroupDefinition> Group(IReadOnlyList<OrganizePhotoItem> photos, OrganizeRule rule)
    {
        var active = photos.Where(x => !x.Excluded).ToArray();
        var groups = new List<PhotoGroupDefinition>();
        if (rule.Type == OrganizeRuleType.FixedCount)
        {
            var size = Math.Max(1, rule.FixedCount);
            for (var index = 0; index < active.Length; index += size)
            {
                groups.Add(new PhotoGroupDefinition
                {
                    Name = $"第 {index / size + 1:000} 组",
                    SourcePaths = active.Skip(index).Take(size).Select(x => x.SourcePath).ToList()
                });
            }
        }
        else if (rule.Type == OrganizeRuleType.Manual)
        {
            groups.AddRange(active.GroupBy(x => SafeGroupName(x.GroupName)).Select(g => NewGroup(g.Key, g)));
        }
        else
        {
            groups.AddRange(active.Select((photo, index) => (photo, index))
                .GroupBy(x => GroupKey(x.photo, rule, x.index))
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => NewGroup(g.Key, g.Select(x => x.photo))));
        }

        foreach (var group in groups)
        {
            foreach (var path in group.SourcePaths)
            {
                var photo = active.First(x => string.Equals(x.SourcePath, path, StringComparison.OrdinalIgnoreCase));
                photo.GroupName = group.Name;
            }
        }
        return groups;
    }

    public OrganizePlan BuildPlan(
        IReadOnlyList<OrganizePhotoItem> photos,
        IReadOnlyList<PhotoGroupDefinition> groups,
        IEnumerable<string> sourceRoots,
        string outputRoot,
        OrganizeRule rule,
        OrganizeOperationType operationType = OrganizeOperationType.Copy,
        OrganizeConflictPolicy conflictPolicy = OrganizeConflictPolicy.AutoNumber,
        bool verifySha256 = false)
    {
        var normalizedSourceRoots = sourceRoots.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var normalizedOutput = string.IsNullOrWhiteSpace(outputRoot) ? string.Empty : Path.GetFullPath(outputRoot);
        if (operationType != OrganizeOperationType.SavePlan && string.IsNullOrWhiteSpace(normalizedOutput))
            throw new ArgumentException("复制或移动整理必须选择输出目录。", nameof(outputRoot));
        if (operationType != OrganizeOperationType.SavePlan)
        {
            var outputPrefix = normalizedOutput.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var sourceRoot in normalizedSourceRoots)
            {
                var sourcePrefix = sourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (string.Equals(sourceRoot, normalizedOutput, StringComparison.OrdinalIgnoreCase) ||
                    outputPrefix.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("输出目录不能与来源目录相同，也不能位于来源目录内部。");
            }
        }

        var operationId = Guid.NewGuid();
        var items = new List<OrganizeManifestItem>();
        foreach (var group in groups)
        {
            foreach (var sourcePath in group.SourcePaths)
            {
                var photo = photos.First(x => string.Equals(x.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
                var destination = operationType == OrganizeOperationType.SavePlan
                    ? string.Empty
                    : SafeDestination(normalizedOutput, group.Name, photo, conflictPolicy);
                items.Add(new OrganizeManifestItem
                {
                    OperationId = operationId,
                    SourcePath = photo.SourcePath,
                    DestinationPath = destination,
                    OperationType = operationType,
                    ConflictPolicy = conflictPolicy,
                    ExpectedSourceSize = photo.FileSizeBytes,
                    ExpectedSourceModifiedAt = photo.ModifiedAt
                });
            }
        }

        if (operationType != OrganizeOperationType.SavePlan && Path.GetPathRoot(normalizedOutput) is { Length: > 0 } driveRoot)
        {
            var drive = new DriveInfo(driveRoot);
            var required = items.Sum(x => x.ExpectedSourceSize);
            if (drive.IsReady && required > Math.Max(0, drive.AvailableFreeSpace - 64L * 1024 * 1024))
                throw new IOException("目标磁盘可用空间不足，无法安全执行整理清单。");
        }

        return new OrganizePlan
        {
            OperationId = operationId,
            SourceRoots = normalizedSourceRoots,
            OutputRoot = normalizedOutput,
            Rule = rule,
            OperationType = operationType,
            ConflictPolicy = conflictPolicy,
            VerifySha256 = verifySha256,
            Groups = groups.ToList(),
            Items = items,
            MetadataMissingCount = photos.Count(x => x.MetadataMissing),
            ConflictRiskCount = items.Count(x => x.DestinationPath.Length > 0 && File.Exists(x.DestinationPath))
        };
    }

    public async Task<OrganizeExecutionResult> ExecuteAsync(
        OrganizePlan plan,
        bool moveConfirmed = false,
        bool overwriteConfirmed = false,
        CancellationToken cancellationToken = default,
        IProgress<OrganizeExecutionProgress>? progress = null)
    {
        if (plan.OperationType == OrganizeOperationType.Move && !moveConfirmed)
            throw new InvalidOperationException("移动整理需要二次确认。源文件只会在目标校验成功后删除。");
        if (plan.ConflictPolicy == OrganizeConflictPolicy.Overwrite && !overwriteConfirmed)
            throw new InvalidOperationException("覆盖策略需要额外确认。");

        var manifest = new OrganizeManifest
        {
            OperationId = plan.OperationId,
            OperationType = plan.OperationType,
            ConflictPolicy = plan.ConflictPolicy,
            Items = plan.Items.Select(CloneItem).ToList()
        };

        if (plan.OperationType == OrganizeOperationType.SavePlan)
        {
            manifest.CompletedAt = DateTimeOffset.UtcNow;
            return new OrganizeExecutionResult { Manifest = manifest };
        }

        System.IO.Directory.CreateDirectory(plan.OutputRoot);
        var createdByTask = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for (var index = 0; index < manifest.Items.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = manifest.Items[index];
                string? overwriteBackupPath = null;
                progress?.Report(new(index, manifest.Items.Count, item.SourcePath));
                try
                {
                    ValidateSource(item);
                    item.DestinationPath = ResolveDestination(item, plan.OutputRoot, overwriteConfirmed);
                    if (item.DestinationPath.Length == 0)
                    {
                        item.State = OrganizeItemState.Skipped;
                        continue;
                    }

                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(item.DestinationPath)!);
                    var destinationMode = item.ConflictPolicy == OrganizeConflictPolicy.Overwrite && overwriteConfirmed
                        ? FileMode.Create
                        : FileMode.CreateNew;
                    if (destinationMode == FileMode.Create && File.Exists(item.DestinationPath))
                    {
                        overwriteBackupPath = item.DestinationPath + $".pixeltart-backup-{plan.OperationId:N}";
                        File.Copy(item.DestinationPath, overwriteBackupPath, false);
                    }
                    createdByTask.Add(item.DestinationPath);
                    await CopyOneAsync(item.SourcePath, item.DestinationPath, destinationMode, cancellationToken).ConfigureAwait(false);
                    var destination = new FileInfo(item.DestinationPath);
                    if (destination.Length != item.ExpectedSourceSize) throw new IOException("目标文件长度校验失败。");
                    if (plan.VerifySha256)
                    {
                        item.OptionalSourceHash = await HashAsync(item.SourcePath, cancellationToken).ConfigureAwait(false);
                        var destinationHash = await HashAsync(item.DestinationPath, cancellationToken).ConfigureAwait(false);
                        if (!string.Equals(item.OptionalSourceHash, destinationHash, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("目标文件 SHA-256 校验失败。");
                    }
                    if (plan.OperationType == OrganizeOperationType.Move)
                    {
                        File.Delete(item.SourcePath);
                        item.State = OrganizeItemState.Moved;
                    }
                    else item.State = OrganizeItemState.Copied;
                    if (overwriteBackupPath is not null && File.Exists(overwriteBackupPath)) File.Delete(overwriteBackupPath);
                }
                catch (OperationCanceledException)
                {
                    if (createdByTask.Contains(item.DestinationPath) && File.Exists(item.DestinationPath))
                    {
                        try { File.Delete(item.DestinationPath); } catch { }
                    }
                    if (overwriteBackupPath is not null && File.Exists(overwriteBackupPath))
                    {
                        try { File.Move(overwriteBackupPath, item.DestinationPath, true); createdByTask.Remove(item.DestinationPath); } catch { }
                    }
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    if (item.State != OrganizeItemState.Moved && createdByTask.Contains(item.DestinationPath) && File.Exists(item.DestinationPath))
                    {
                        try { File.Delete(item.DestinationPath); } catch { }
                    }
                    if (overwriteBackupPath is not null && File.Exists(overwriteBackupPath))
                    {
                        try { File.Move(overwriteBackupPath, item.DestinationPath, true); } catch { }
                    }
                    item.State = OrganizeItemState.Failed;
                    item.ErrorCode = ErrorCode(ex);
                    item.ErrorMessage = ex.Message;
                    logService?.Error($"整理图片失败：{item.SourcePath}", ex);
                }
                await SaveManifestAsync(plan.OutputRoot, manifest, cancellationToken).ConfigureAwait(false);
            }
            manifest.CompletedAt = DateTimeOffset.UtcNow;
            await SaveManifestAsync(plan.OutputRoot, manifest, cancellationToken).ConfigureAwait(false);
            progress?.Report(new(manifest.Items.Count, manifest.Items.Count, string.Empty));
            return new OrganizeExecutionResult { Manifest = manifest };
        }
        catch (OperationCanceledException)
        {
            var removable = plan.OperationType == OrganizeOperationType.Copy
                ? manifest.Items.Where(x => x.State == OrganizeItemState.Copied).Select(x => x.DestinationPath)
                : manifest.Items.Where(x => x.State is not OrganizeItemState.Moved).Select(x => x.DestinationPath);
            foreach (var path in removable.Where(createdByTask.Contains).Where(File.Exists))
            {
                try { File.Delete(path); } catch { }
            }
            foreach (var item in manifest.Items.Where(x => x.State == OrganizeItemState.Pending)) item.State = OrganizeItemState.Cancelled;
            await SaveManifestAsync(plan.OutputRoot, manifest, CancellationToken.None).ConfigureAwait(false);
            return new OrganizeExecutionResult { Manifest = manifest };
        }
    }

    public async Task ExportReportsAsync(OrganizeManifest manifest, string outputDirectory, CancellationToken cancellationToken = default)
    {
        System.IO.Directory.CreateDirectory(outputDirectory);
        var prefix = $"整理报告_{manifest.OperationId:N}";
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, prefix + ".json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
        var csv = new StringBuilder("SourcePath,DestinationPath,OperationType,ConflictPolicy,State,ErrorCode,ErrorMessage\r\n");
        foreach (var item in manifest.Items)
            csv.AppendLine(string.Join(',', Csv(item.SourcePath), Csv(item.DestinationPath), item.OperationType, item.ConflictPolicy, item.State, Csv(item.ErrorCode), Csv(item.ErrorMessage)));
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, prefix + ".csv"), csv.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var text = string.Join(Environment.NewLine, manifest.Items.Select(x => $"[{x.State}] {x.SourcePath} -> {x.DestinationPath} {x.ErrorCode} {x.ErrorMessage}"));
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, prefix + ".txt"), text, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> SavePlanAsync(OrganizePlan plan, string requestedPath, CancellationToken cancellationToken = default)
    {
        var outputPath = File.Exists(requestedPath) ? AutoNumber(Path.GetFullPath(requestedPath)) : Path.GetFullPath(requestedPath);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384, true);
        await JsonSerializer.SerializeAsync(stream, plan, JsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(true);
        return outputPath;
    }

    public async Task<bool> UndoMoveAsync(OrganizeManifest manifest, CancellationToken cancellationToken = default)
    {
        if (manifest.OperationType != OrganizeOperationType.Move) return false;
        foreach (var item in manifest.Items.Where(x => x.State == OrganizeItemState.Moved))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(item.DestinationPath) || File.Exists(item.SourcePath)) return false;
            if (new FileInfo(item.DestinationPath).Length != item.ExpectedSourceSize) return false;
            if (!string.IsNullOrWhiteSpace(item.OptionalSourceHash) &&
                !string.Equals(item.OptionalSourceHash, await HashAsync(item.DestinationPath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase)) return false;
        }
        foreach (var item in manifest.Items.Where(x => x.State == OrganizeItemState.Moved).Reverse())
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(item.SourcePath)!);
            await CopyOneAsync(item.DestinationPath, item.SourcePath, FileMode.CreateNew, cancellationToken).ConfigureAwait(false);
            if (new FileInfo(item.SourcePath).Length != item.ExpectedSourceSize) return false;
            File.Delete(item.DestinationPath);
            item.State = OrganizeItemState.Undone;
        }
        return true;
    }

    private static IEnumerable<string> ExpandInputs(IEnumerable<string> inputs)
    {
        foreach (var input in inputs.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (File.Exists(input) && SupportedExtensions.Contains(Path.GetExtension(input))) yield return Path.GetFullPath(input);
            else if (System.IO.Directory.Exists(input))
                foreach (var file in System.IO.Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories).Where(x => SupportedExtensions.Contains(Path.GetExtension(x)))) yield return Path.GetFullPath(file);
        }
    }

    private static OrganizePhotoItem ReadPhoto(string path)
    {
        var info = new FileInfo(path);
        DateTimeOffset? capture = null;
        string make = "", model = "", lens = "";
        var width = 0; var height = 0;
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var sub = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (sub?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) == true) capture = original;
            make = ifd0?.GetString(ExifDirectoryBase.TagMake)?.Trim() ?? "";
            model = ifd0?.GetString(ExifDirectoryBase.TagModel)?.Trim() ?? "";
            lens = sub?.GetString(ExifDirectoryBase.TagLensModel)?.Trim() ?? "";
            width = ReadDimension(directories, "width");
            height = ReadDimension(directories, "height");
        }
        catch { }
        return new OrganizePhotoItem
        {
            SourcePath = info.FullName,
            SourceRoot = info.DirectoryName ?? string.Empty,
            FileSizeBytes = info.Length,
            ModifiedAt = info.LastWriteTimeUtc,
            CaptureTime = capture,
            CameraMake = make,
            CameraModel = model,
            LensModel = lens,
            PixelWidth = width,
            PixelHeight = height,
            MetadataMissing = capture is null && make.Length == 0 && model.Length == 0 && lens.Length == 0
        };
    }

    private static int ReadDimension(IReadOnlyList<MetadataExtractor.Directory> directories, string name) =>
        directories.SelectMany(d => d.Tags.Select(tag => (Tag: tag, Directory: d)))
            .Where(x => x.Tag.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Directory.GetObject(x.Tag.Type))
            .Select(value => value switch { int i => i, uint u => (int)u, long l when l <= int.MaxValue => (int)l, _ => 0 })
            .FirstOrDefault(x => x > 0);

    private static string GroupKey(OrganizePhotoItem photo, OrganizeRule rule, int index) => rule.Type switch
    {
        OrganizeRuleType.OriginalFolder => Path.GetFileName(Path.GetDirectoryName(photo.SourcePath)) ?? "根目录",
        OrganizeRuleType.CaptureDate => photo.CaptureTime?.ToString("yyyy-MM-dd") ?? "元数据缺失",
        OrganizeRuleType.CaptureYear => photo.CaptureTime?.ToString("yyyy") ?? "元数据缺失",
        OrganizeRuleType.CaptureYearMonth => photo.CaptureTime?.ToString("yyyy-MM") ?? "元数据缺失",
        OrganizeRuleType.CaptureDateHour => photo.CaptureTime?.ToString("yyyy-MM-dd_HH时") ?? "元数据缺失",
        OrganizeRuleType.CameraMake => ValueOrMissing(photo.CameraMake),
        OrganizeRuleType.CameraModel => ValueOrMissing(photo.CameraModel),
        OrganizeRuleType.LensModel => ValueOrMissing(photo.LensModel),
        OrganizeRuleType.FileFormat => photo.Extension,
        OrganizeRuleType.Landscape => photo.PixelWidth > photo.PixelHeight ? "横图" : "其他方向",
        OrganizeRuleType.Portrait => photo.PixelHeight > photo.PixelWidth ? "竖图" : "其他方向",
        OrganizeRuleType.Square => photo.PixelHeight > 0 && Math.Abs(photo.PixelWidth - photo.PixelHeight) <= Math.Max(photo.PixelWidth, photo.PixelHeight) * .03 ? "方图" : "其他方向",
        OrganizeRuleType.FileNamePrefix => Prefix(photo.FileName),
        OrganizeRuleType.FileNameNumber => Digits(photo.FileName),
        OrganizeRuleType.FileSizeRange => SizeRange(photo.FileSizeBytes),
        OrganizeRuleType.CustomKeyword => photo.FileName.Contains(rule.Parameter, StringComparison.OrdinalIgnoreCase) ? SafeGroupName(rule.Parameter) : "其他",
        _ => "未分组"
    };

    private static PhotoGroupDefinition NewGroup(string name, IEnumerable<OrganizePhotoItem> items) => new() { Name = SafeGroupName(name), SourcePaths = items.Select(x => x.SourcePath).ToList() };
    private static string ValueOrMissing(string value) => string.IsNullOrWhiteSpace(value) ? "元数据缺失" : value.Trim();
    private static string Prefix(string name) { var stem=Path.GetFileNameWithoutExtension(name); var index=stem.IndexOfAny("0123456789".ToCharArray()); return SafeGroupName(index > 0 ? stem[..index] : stem); }
    private static string Digits(string name) { var digits=new string(Path.GetFileNameWithoutExtension(name).Where(char.IsDigit).ToArray()); return digits.Length == 0 ? "无数字段" : digits; }
    private static string SizeRange(long bytes) => bytes switch { < 1024*1024 => "小于 1 MB", < 5L*1024*1024 => "1-5 MB", < 20L*1024*1024 => "5-20 MB", _ => "大于 20 MB" };
    private static string SafeGroupName(string value) { var cleaned=string.Concat((value ?? "").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim().Trim('.'); return string.IsNullOrWhiteSpace(cleaned) ? "未分组" : cleaned; }
    private static string SafeDestination(string root, string group, OrganizePhotoItem photo, OrganizeConflictPolicy policy) => Path.Combine(root, SafeGroupName(group), FileNameForPolicy(photo, policy));
    private static string FileNameForPolicy(OrganizePhotoItem photo, OrganizeConflictPolicy policy) => policy switch
    {
        OrganizeConflictPolicy.AddSourceFolder => $"{SafeGroupName(Path.GetFileName(photo.SourceRoot))}_{photo.FileName}",
        OrganizeConflictPolicy.AddCaptureDate => $"{photo.CaptureTime?.ToString("yyyyMMdd") ?? "未知日期"}_{photo.FileName}",
        OrganizeConflictPolicy.AddShortHash => $"{Path.GetFileNameWithoutExtension(photo.FileName)}_{ShortHash(photo.SourcePath)}{Path.GetExtension(photo.FileName)}",
        _ => photo.FileName
    };

    private static string ResolveDestination(OrganizeManifestItem item, string root, bool overwriteConfirmed)
    {
        var full = Path.GetFullPath(item.DestinationPath);
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new IOException("目标路径超出输出根目录。");
        if (!File.Exists(full)) return full;
        return item.ConflictPolicy switch
        {
            OrganizeConflictPolicy.Skip => string.Empty,
            OrganizeConflictPolicy.Overwrite when overwriteConfirmed => full,
            _ => AutoNumber(full)
        };
    }

    private static string AutoNumber(string path)
    {
        var directory = Path.GetDirectoryName(path)!; var stem = Path.GetFileNameWithoutExtension(path); var extension = Path.GetExtension(path);
        for (var index=2; index<int.MaxValue; index++) { var candidate=Path.Combine(directory,$"{stem}_{index}{extension}"); if(!File.Exists(candidate)) return candidate; }
        throw new IOException("无法生成不冲突的目标文件名。");
    }

    private static void ValidateSource(OrganizeManifestItem item)
    {
        var info = new FileInfo(item.SourcePath);
        if (!info.Exists) throw new FileNotFoundException("源文件不存在。", item.SourcePath);
        if (info.Length != item.ExpectedSourceSize || info.LastWriteTimeUtc != item.ExpectedSourceModifiedAt.UtcDateTime) throw new IOException("源文件在计划生成后发生变化，请重新预览清单。");
        if (Path.GetFullPath(item.SourcePath).Equals(Path.GetFullPath(item.DestinationPath), StringComparison.OrdinalIgnoreCase)) throw new IOException("源文件与目标文件不能相同。");
    }

    private static async Task CopyOneAsync(string source, string destination, FileMode destinationMode, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024*128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(destination, destinationMode, FileAccess.Write, FileShare.None, 1024*128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Flush(true);
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024*128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static string ShortHash(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..8];
    private static string ErrorCode(Exception ex) => ex switch { UnauthorizedAccessException => "ACCESS_DENIED", FileNotFoundException => "SOURCE_NOT_FOUND", OperationCanceledException => "CANCELLED", _ => "IO_ERROR" };
    private static string Csv(string value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    private static OrganizeManifestItem CloneItem(OrganizeManifestItem item) => new() { SchemaVersion=item.SchemaVersion, OperationId=item.OperationId, SourcePath=item.SourcePath, DestinationPath=item.DestinationPath, OperationType=item.OperationType, ConflictPolicy=item.ConflictPolicy, ExpectedSourceSize=item.ExpectedSourceSize, ExpectedSourceModifiedAt=item.ExpectedSourceModifiedAt, OptionalSourceHash=item.OptionalSourceHash, State=item.State, ErrorCode=item.ErrorCode, ErrorMessage=item.ErrorMessage };
    private static Task SaveManifestAsync(string root, OrganizeManifest manifest, CancellationToken cancellationToken) => File.WriteAllTextAsync(Path.Combine(root, $"organize-manifest-{manifest.OperationId:N}.json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
}
