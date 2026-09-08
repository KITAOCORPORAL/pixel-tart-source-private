using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public enum AssetLibraryPackageScope { DatabaseOnly, ManagedAssets, CollectAvailableReferences }

public sealed record AssetLibraryPackageSummary(
    Guid PackageId, Guid SourceLibraryId, string PackagePath, AssetLibraryPackageScope Scope,
    int AssetCount, int IncludedOriginalCount, int ReferenceOnlyCount, int MissingCount, long PackageBytes);

public sealed record AssetLibraryPackageInspection(
    Guid PackageId, Guid SourceLibraryId, AssetLibraryPackageScope Scope, int AssetCount,
    int IncludedOriginalCount, int ReferenceOnlyCount, int MissingCount, IReadOnlyList<AssetLibraryPackageFile> Files);

public sealed record AssetLibraryPackageFile(string Path, long Size, string Sha256, bool Rebuildable = false);
public sealed record AssetLibraryPackageReference(Guid AssetId, string State, string SourcePath);

public sealed class AssetLibraryPackageLimits
{
    public int MaximumFileCount { get; init; } = 100_000;
    public long MaximumSingleFileBytes { get; init; } = 20L * 1024 * 1024 * 1024;
    public long MaximumExpandedBytes { get; init; } = 200L * 1024 * 1024 * 1024;
    public double MaximumCompressionRatio { get; init; } = 250d;
}

/// <summary>Streaming, fail-closed transport for portable asset libraries.</summary>
public sealed class AssetLibraryPackageService
{
    public const int PackageFormatVersion = 1;
    public const string PackageExtension = ".ptpack";
    private const string ManifestEntryName = "package.manifest.json";
    private const string DatabaseEntryName = "payload/database/asset-library-v16.db";
    private const string PortableRootToken = "__PTPACK_ROOT__";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public async Task<AssetLibraryPackageSummary> ExportAsync(
        AssetLibraryContainerDescriptor descriptor,
        string packagePath,
        AssetLibraryPackageScope scope,
        bool includePreviews = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var target = GetAvailablePackagePath(packagePath);
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidDataException("素材包必须有父目录。");
        Directory.CreateDirectory(parent);
        EnsureAvailableSpace(parent, EstimateExportBytes(descriptor, scope, includePreviews));
        var operationId = Guid.NewGuid();
        var partial = target + $".partial-{operationId:N}";
        var snapshot = Path.Combine(parent, $".{Path.GetFileName(target)}.snapshot-{operationId:N}.db");
        var packageId = Guid.NewGuid();
        try
        {
            await CreateDatabaseSnapshotAsync(descriptor.DatabasePath, snapshot, cancellationToken).ConfigureAwait(false);
            var inventory = await ReadInventoryAsync(snapshot, cancellationToken).ConfigureAwait(false);
            var sources = new List<SourceFile> { new(snapshot, DatabaseEntryName, false) };
            var includedAssetIds = new HashSet<Guid>();

            if (scope is AssetLibraryPackageScope.ManagedAssets or AssetLibraryPackageScope.CollectAvailableReferences)
                AddDirectoryFiles(descriptor.ManagedAssetsPath, "payload/assets/managed", sources);
            if (includePreviews) AddDirectoryFiles(descriptor.PreviewCachePath, "payload/previews", sources, rebuildable: true);

            if (scope == AssetLibraryPackageScope.CollectAvailableReferences)
            {
                foreach (var asset in inventory.Where(item => !item.IsManaged && !item.IsMissing && File.Exists(item.SourcePath)))
                {
                    var extension = Path.GetExtension(asset.SourcePath);
                    var entry = $"payload/assets/managed/collected/{asset.AssetId:N}{extension}";
                    sources.Add(new SourceFile(asset.SourcePath, entry, false));
                    includedAssetIds.Add(asset.AssetId);
                    await MakeCollectedReferencePortableAsync(snapshot, asset.AssetId, entry["payload/".Length..], cancellationToken).ConfigureAwait(false);
                }
            }
            await MakeManagedPathsPortableAsync(snapshot, descriptor.ManagedAssetsPath, cancellationToken).ConfigureAwait(false);

            var fileEntries = new List<AssetLibraryPackageFile>(sources.Count);
            await using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, true))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var source in sources.OrderBy(item => item.EntryPath, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var info = new FileInfo(source.SourcePath);
                    var zipEntry = archive.CreateEntry(source.EntryPath, CompressionLevel.Optimal);
                    await using var input = new FileStream(source.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
                    await using var output = zipEntry.Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[1024 * 1024];
                    long copied = 0;
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                        if (read == 0) break;
                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        copied += read;
                    }
                    if (copied != info.Length) throw new IOException($"打包期间文件发生变化：{source.SourcePath}");
                    fileEntries.Add(new(source.EntryPath, copied, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), source.Rebuildable));
                }

                var manifest = new PackageManifest
                {
                    PackageFormatVersion = PackageFormatVersion,
                    PackageId = packageId,
                    SourceLibraryId = descriptor.LibraryId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Scope = scope.ToString(),
                    DatabaseSchemaVersion = AssetLibrarySchema.Version,
                    AssetCount = inventory.Count,
                    IncludedOriginalCount = inventory.Count(item => item.IsManaged) + includedAssetIds.Count,
                    ReferenceOnlyCount = inventory.Count(item => !item.IsManaged && !includedAssetIds.Contains(item.AssetId)),
                    MissingCount = inventory.Count(item => item.IsMissing || (!item.IsManaged && !File.Exists(item.SourcePath))),
                    Files = fileEntries,
                    References = inventory.Where(item => !item.IsManaged && !includedAssetIds.Contains(item.AssetId))
                        .Select(item => new AssetLibraryPackageReference(item.AssetId, item.IsMissing || !File.Exists(item.SourcePath) ? "missing" : "external", item.SourcePath)).ToList()
                };
                var entry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await JsonSerializer.SerializeAsync(entryStream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            await InspectAsync(partial, cancellationToken: cancellationToken).ConfigureAwait(false);
            File.Move(partial, target);
            var finalSize = new FileInfo(target).Length;
            var inspect = await InspectAsync(target, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new(packageId, descriptor.LibraryId, target, scope, inspect.AssetCount, inspect.IncludedOriginalCount, inspect.ReferenceOnlyCount, inspect.MissingCount, finalSize);
        }
        catch
        {
            TryDeleteFile(partial);
            throw;
        }
        finally { TryDeleteFile(snapshot); }
    }

    public async Task<AssetLibraryPackageInspection> InspectAsync(
        string packagePath,
        AssetLibraryPackageLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= new AssetLibraryPackageLimits();
        await using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count > limits.MaximumFileCount) throw new InvalidDataException("素材包文件数量超过安全限制。");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeEntryPath(entry.FullName);
            if (!paths.Add(normalized)) throw new InvalidDataException($"素材包包含重复或大小写冲突路径：{normalized}");
            if (entry.Length > limits.MaximumSingleFileBytes) throw new InvalidDataException("素材包单文件超过安全限制。");
            var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixMode == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("素材包不能包含符号链接或重解析点。");
            expanded = checked(expanded + entry.Length);
            if (expanded > limits.MaximumExpandedBytes) throw new InvalidDataException("素材包展开大小超过安全限制。");
            if (entry.Length > 1024 * 1024 && entry.CompressedLength > 0 && (double)entry.Length / entry.CompressedLength > limits.MaximumCompressionRatio)
                throw new InvalidDataException("素材包压缩比超过安全限制。");
        }
        var manifestEntry = archive.GetEntry(ManifestEntryName) ?? throw new InvalidDataException("素材包 manifest 缺失。");
        PackageManifest manifest;
        await using (var manifestStream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<PackageManifest>(manifestStream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("素材包 manifest 无法解析。");
        ValidateManifest(manifest);
        if (manifest.Files.Count + 1 != archive.Entries.Count) throw new InvalidDataException("素材包文件清单与实际内容不一致。");
        foreach (var file in manifest.Files)
        {
            var entry = archive.GetEntry(NormalizeEntryPath(file.Path)) ?? throw new InvalidDataException($"素材包文件缺失：{file.Path}");
            if (entry.Length != file.Size) throw new InvalidDataException($"素材包文件大小校验失败：{file.Path}");
            await using var input = entry.Open();
            var hash = await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(Convert.ToHexString(hash), file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"素材包文件哈希校验失败：{file.Path}");
        }
        return new(manifest.PackageId, manifest.SourceLibraryId, Enum.Parse<AssetLibraryPackageScope>(manifest.Scope), manifest.AssetCount,
            manifest.IncludedOriginalCount, manifest.ReferenceOnlyCount, manifest.MissingCount, manifest.Files);
    }

    public async Task<AssetLibraryContainerDescriptor> ImportAsync(
        string packagePath,
        string targetContainerPath,
        string displayName,
        AssetLibraryPackageLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        var inspection = await InspectAsync(packagePath, limits, cancellationToken).ConfigureAwait(false);
        var target = Path.GetFullPath(targetContainerPath);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException($"目标素材库已存在：{target}");
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidDataException("目标素材库必须有父目录。");
        Directory.CreateDirectory(parent);
        EnsureAvailableSpace(parent, inspection.Files.Sum(file => file.Size));
        var operationId = Guid.NewGuid();
        var extraction = Path.Combine(parent, $".{Path.GetFileName(target)}.import-{operationId:N}");
        AssetLibraryContainerDescriptor? created = null;
        try
        {
            Directory.CreateDirectory(extraction);
            await ExtractValidatedAsync(packagePath, extraction, limits ?? new(), cancellationToken).ConfigureAwait(false);
            var databaseSnapshot = Path.Combine(extraction, DatabaseEntryName.Replace('/', Path.DirectorySeparatorChar));
            await ValidateSnapshotAsync(databaseSnapshot, cancellationToken).ConfigureAwait(false);
            created = await new AssetLibraryContainerService().CreateAsync(target, displayName, cancellationToken).ConfigureAwait(false);
            new AssetLibraryDatabase(created.DatabasePath).ClearConnectionPool();
            File.Copy(databaseSnapshot, created.DatabasePath, overwrite: true);
            var assetsRoot = Path.Combine(extraction, "payload", "assets", "managed");
            if (Directory.Exists(assetsRoot)) CopyDirectory(assetsRoot, created.ManagedAssetsPath, cancellationToken);
            var previewsRoot = Path.Combine(extraction, "payload", "previews");
            if (Directory.Exists(previewsRoot)) CopyDirectory(previewsRoot, created.PreviewCachePath, cancellationToken);
            await ResolvePortablePathsAsync(created.DatabasePath, created.ContainerPath, cancellationToken).ConfigureAwait(false);
            await ValidateSnapshotAsync(created.DatabasePath, cancellationToken).ConfigureAwait(false);
            return await new AssetLibraryContainerService().OpenAsync(created.ContainerPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (created is not null) TryDeleteDirectory(created.ContainerPath);
            throw;
        }
        finally { TryDeleteDirectory(extraction); }
    }

    private static async Task ExtractValidatedAsync(string packagePath, string extraction, AssetLibraryPackageLimits limits, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var root = Path.GetFullPath(extraction).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = NormalizeEntryPath(entry.FullName).Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(extraction, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("素材包路径越界。");
            var directory = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(directory);
            if (entry.FullName.EndsWith('/')) continue;
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            await input.CopyToAsync(output, 1024 * 1024, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string NormalizeEntryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || normalized.Contains(':') || normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"素材包包含不安全路径：{path}");
        return normalized;
    }

    private static void ValidateManifest(PackageManifest manifest)
    {
        if (manifest.PackageFormatVersion != PackageFormatVersion) throw new InvalidDataException($"不支持的素材包版本：{manifest.PackageFormatVersion}");
        if (manifest.PackageId == Guid.Empty || manifest.SourceLibraryId == Guid.Empty) throw new InvalidDataException("素材包身份无效。");
        if (!Enum.TryParse<AssetLibraryPackageScope>(manifest.Scope, out _)) throw new InvalidDataException("素材包导出范围无效。");
        if (manifest.DatabaseSchemaVersion != AssetLibrarySchema.Version) throw new InvalidDataException("素材包数据库版本不受支持。");
        if (manifest.Files.Count == 0 || !manifest.Files.Any(file => file.Path == DatabaseEntryName)) throw new InvalidDataException("素材包数据库清单缺失。");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            var path = NormalizeEntryPath(file.Path);
            if (!path.StartsWith("payload/", StringComparison.Ordinal) || !paths.Add(path) || file.Size < 0 || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("素材包文件清单无效。");
        }
    }

    private static async Task CreateDatabaseSnapshotAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var sourceBuilder = new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        var destinationBuilder = new SqliteConnectionStringBuilder { DataSource = destinationPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
        await using var source = new SqliteConnection(sourceBuilder.ToString());
        await using var destination = new SqliteConnection(destinationBuilder.ToString());
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(destination);
    }

    private static async Task<List<InventoryItem>> ReadInventoryAsync(string databasePath, CancellationToken cancellationToken)
    {
        var result = new List<InventoryItem>();
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString()); await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT AssetId,SourcePath,ImportMode,ManagedCopyPath,IsMissing FROM AssetItems;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), string.Equals(reader.GetString(2), "ManagedCopy", StringComparison.OrdinalIgnoreCase), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt32(4) != 0));
        return result;
    }

    private static async Task MakeCollectedReferencePortableAsync(string databasePath, Guid assetId, string relativePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenWriteAsync(databasePath, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AssetItems SET SourcePath=$path,NormalizedSourcePath=$path,ImportMode='ManagedCopy',ManagedCopyPath=$path,IsMissing=0 WHERE AssetId=$id;";
        command.Parameters.AddWithValue("$path", $"{PortableRootToken}/{relativePath}"); command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task MakeManagedPathsPortableAsync(string databasePath, string managedRoot, CancellationToken cancellationToken)
    {
        var items = await ReadInventoryAsync(databasePath, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenWriteAsync(databasePath, cancellationToken).ConfigureAwait(false);
        foreach (var item in items.Where(item => item.IsManaged && !string.IsNullOrWhiteSpace(item.ManagedCopyPath)))
        {
            var relative = Path.GetRelativePath(managedRoot, item.ManagedCopyPath!);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) continue;
            var portable = $"{PortableRootToken}/assets/managed/{relative.Replace('\\', '/')}";
            await using var command = connection.CreateCommand(); command.CommandText = "UPDATE AssetItems SET SourcePath=$path,NormalizedSourcePath=$path,ManagedCopyPath=$path WHERE AssetId=$id;";
            command.Parameters.AddWithValue("$path", portable); command.Parameters.AddWithValue("$id", item.AssetId.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ResolvePortablePathsAsync(string databasePath, string containerRoot, CancellationToken cancellationToken)
    {
        await using var connection = await OpenWriteAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var prefix = PortableRootToken + "/";
        await using var read = connection.CreateCommand(); read.CommandText = "SELECT AssetId,SourcePath FROM AssetItems WHERE SourcePath LIKE $prefix;"; read.Parameters.AddWithValue("$prefix", prefix + "%");
        var updates = new List<(string Id, string Path)>();
        await using (var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var relative = reader.GetString(1)[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                updates.Add((reader.GetString(0), Path.GetFullPath(Path.Combine(containerRoot, relative))));
            }
        foreach (var update in updates)
        {
            await using var command = connection.CreateCommand(); command.CommandText = "UPDATE AssetItems SET SourcePath=$path,NormalizedSourcePath=$path,ManagedCopyPath=$path WHERE AssetId=$id;";
            command.Parameters.AddWithValue("$path", update.Path); command.Parameters.AddWithValue("$id", update.Id); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<SqliteConnection> OpenWriteAsync(string path, CancellationToken cancellationToken)
    { var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()); await connection.OpenAsync(cancellationToken).ConfigureAwait(false); return connection; }

    private static async Task ValidateSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[16]; await using (var file = File.OpenRead(path)) if (await file.ReadAsync(header, cancellationToken).ConfigureAwait(false) != header.Length || !System.Text.Encoding.ASCII.GetString(header).StartsWith("SQLite format 3", StringComparison.Ordinal)) throw new InvalidDataException("素材包数据库头无效。");
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString()); await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var check = connection.CreateCommand(); check.CommandText = "PRAGMA quick_check;"; if (!string.Equals(Convert.ToString(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)), "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("素材包数据库完整性失败。");
        await using var version = connection.CreateCommand(); version.CommandText = "SELECT COALESCE(MAX(Version),0) FROM AssetLibrarySchemaInfo;"; if (Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != AssetLibrarySchema.Version) throw new InvalidDataException("素材包数据库 schema 不受支持。");
    }

    private static void AddDirectoryFiles(string root, string entryRoot, List<SourceFile> sources, bool rebuildable = false)
    {
        if (!Directory.Exists(root)) return;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"拒绝打包重解析文件：{path}");
            sources.Add(new(path, $"{entryRoot}/{Path.GetRelativePath(root, path).Replace('\\', '/')}", rebuildable));
        }
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, overwrite: false);
        }
    }

    private static long EstimateExportBytes(AssetLibraryContainerDescriptor descriptor, AssetLibraryPackageScope scope, bool previews)
    { long size = new FileInfo(descriptor.DatabasePath).Length; if (scope != AssetLibraryPackageScope.DatabaseOnly) size += DirectoryBytes(descriptor.ManagedAssetsPath); if (previews) size += DirectoryBytes(descriptor.PreviewCachePath); return Math.Max(size * 2, 32 * 1024 * 1024); }
    private static long DirectoryBytes(string path) => Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) : 0;
    private static void EnsureAvailableSpace(string directory, long required) { var root = Path.GetPathRoot(Path.GetFullPath(directory))!; if (new DriveInfo(root).AvailableFreeSpace < required) throw new IOException("目标磁盘可用空间不足。"); }
    private static string GetAvailablePackagePath(string path) { var full = Path.GetFullPath(path); if (!string.Equals(Path.GetExtension(full), PackageExtension, StringComparison.OrdinalIgnoreCase)) full += PackageExtension; if (!File.Exists(full) && !Directory.Exists(full)) return full; var parent = Path.GetDirectoryName(full)!; var stem = Path.GetFileNameWithoutExtension(full); for (var i = 2; i < 10_000; i++) { var candidate = Path.Combine(parent, $"{stem} ({i}){PackageExtension}"); if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate; } throw new IOException("无法生成安全的素材包文件名。"); }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private sealed record SourceFile(string SourcePath, string EntryPath, bool Rebuildable);
    private sealed record InventoryItem(Guid AssetId, string SourcePath, bool IsManaged, string? ManagedCopyPath, bool IsMissing);
    private sealed class PackageManifest
    {
        public int PackageFormatVersion { get; set; }
        public Guid PackageId { get; set; }
        public Guid SourceLibraryId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string Scope { get; set; } = string.Empty;
        public int DatabaseSchemaVersion { get; set; }
        public int AssetCount { get; set; }
        public int IncludedOriginalCount { get; set; }
        public int ReferenceOnlyCount { get; set; }
        public int MissingCount { get; set; }
        public List<AssetLibraryPackageFile> Files { get; set; } = [];
        public List<AssetLibraryPackageReference> References { get; set; } = [];
    }
}
