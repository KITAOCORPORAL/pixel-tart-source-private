using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record AssetLibraryContainerDescriptor(
    Guid LibraryId, string DisplayName, string ContainerPath, string DatabasePath,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string ManagedAssetsPath,
    string PreviewCachePath, string ContentMode);

public sealed class AssetLibraryContainerService
{
    public const int ContainerFormatVersion = 1;
    public const string ContainerExtension = ".ptlibrary";
    public const string ManifestFileName = "library.manifest.json";
    public const string DatabaseRelativePath = "database/asset-library-v16.db";
    public const string ManagedAssetsRelativePath = "assets/managed";
    public const string PreviewCacheRelativePath = "previews";
    private const string StagingOwnerFileName = ".pixel-tart-staging-owner.json";
    private const int MaximumDisplayNameLength = 160;

    private static readonly HashSet<string> KnownManifestProperties = new(StringComparer.Ordinal)
    {
        "container_format_version", "library_id", "display_name", "created_at", "updated_at",
        "database_relative_path", "database_schema_version", "managed_assets_relative_path",
        "preview_cache_relative_path", "content_mode", "app_minimum_version", "payload_sha256", "extensions"
    };

    public async Task<AssetLibraryContainerDescriptor> CreateAsync(string containerPath, string displayName, CancellationToken cancellationToken = default)
    {
        var target = NormalizeContainerPath(containerPath);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException($"目标素材库已存在：{target}");
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidDataException("素材库必须有父目录。");
        Directory.CreateDirectory(parent);
        var operationId = Guid.NewGuid();
        var expectedStagingName = $".{Path.GetFileName(target)}.staging-{operationId:N}";
        var staging = Path.Combine(parent, expectedStagingName);
        var now = DateTimeOffset.UtcNow;
        var manifest = new ContainerManifest
        {
            ContainerFormatVersion = ContainerFormatVersion,
            LibraryId = Guid.NewGuid(),
            DisplayName = NormalizeDisplayName(displayName, target),
            CreatedAt = now,
            UpdatedAt = now,
            DatabaseRelativePath = DatabaseRelativePath,
            DatabaseSchemaVersion = AssetLibrarySchema.Version,
            ManagedAssetsRelativePath = ManagedAssetsRelativePath,
            PreviewCacheRelativePath = PreviewCacheRelativePath,
            ContentMode = "reference",
            AppMinimumVersion = Branding.ProductVersion
        };

        try
        {
            Directory.CreateDirectory(staging);
            await WriteStagingOwnerAsync(staging, operationId, target, cancellationToken).ConfigureAwait(false);
            CreateLayout(staging);
            EnsureCriticalPathsArePhysical(staging, manifest);
            var databasePath = ResolveContainedPath(staging, manifest.DatabaseRelativePath);
            var repository = new SqliteAssetLibraryRepository(databasePath);
            await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await repository.DisposeAsync().ConfigureAwait(false);
            await ValidateDatabaseAsync(databasePath, cancellationToken).ConfigureAwait(false);
            await WriteManifestAsync(staging, manifest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(target) || File.Exists(target)) throw new IOException($"目标素材库已存在：{target}");
            Directory.Move(staging, target);
            return ToDescriptor(target, manifest);
        }
        catch
        {
            TryDeleteOwnedStaging(parent, staging, expectedStagingName, operationId, target);
            throw;
        }
    }

    public async Task<AssetLibraryContainerDescriptor> OpenAsync(string containerPath, CancellationToken cancellationToken = default)
    {
        var root = NormalizeContainerPath(containerPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"素材库不存在：{root}");
        EnsureNotReparsePoint(root, "素材库根目录");
        var manifest = await ReadManifestAsync(root, cancellationToken).ConfigureAwait(false);
        EnsureCriticalPathsArePhysical(root, manifest);
        var databasePath = ResolveContainedPath(root, manifest.DatabaseRelativePath);
        if (!File.Exists(databasePath)) throw new InvalidDataException("素材库数据库缺失。");
        await ValidateDatabaseAsync(databasePath, cancellationToken).ConfigureAwait(false);
        return ToDescriptor(root, manifest);
    }

    /// <summary>
    /// Copies a legacy fixed-path SQLite database into a new portable container.
    /// The source is opened read-only and is never moved, deleted, or vacuumed.
    /// </summary>
    public async Task<AssetLibraryContainerDescriptor> MigrateLegacyDatabaseAsync(
        string legacyDatabasePath,
        string containerPath,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var source = AssetLibraryPortableSettings.NormalizePath(legacyDatabasePath);
        if (source.Length == 0 || !File.Exists(source)) throw new FileNotFoundException("旧素材库数据库不存在。", source);
        var descriptor = await CreateAsync(containerPath, displayName, cancellationToken).ConfigureAwait(false);
        try
        {
            var sourceBuilder = new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
            var destinationBuilder = new SqliteConnectionStringBuilder { DataSource = descriptor.DatabasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
            await using var sourceConnection = new SqliteConnection(sourceBuilder.ToString());
            await using var destinationConnection = new SqliteConnection(destinationBuilder.ToString());
            await sourceConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await destinationConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            sourceConnection.BackupDatabase(destinationConnection);
            await ValidateDatabaseAsync(descriptor.DatabasePath, cancellationToken).ConfigureAwait(false);
            return await OpenAsync(descriptor.ContainerPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try { if (Directory.Exists(descriptor.ContainerPath)) Directory.Delete(descriptor.ContainerPath, recursive: true); } catch { }
            throw;
        }
    }

    public async Task<AssetLibraryWriteLease> AcquireWriteLeaseAsync(AssetLibraryContainerDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        EnsureNotReparsePoint(descriptor.ContainerPath, "素材库根目录");
        var lockDirectory = ResolveContainedPath(descriptor.ContainerPath, "locks");
        Directory.CreateDirectory(lockDirectory);
        EnsureNoReparseComponents(descriptor.ContainerPath, lockDirectory);
        return await AssetLibraryWriteLease.AcquireAsync(lockDirectory, descriptor.LibraryId, cancellationToken).ConfigureAwait(false);
    }

    public static bool TryResolveStartupDatabasePath(AssetLibraryPortableSettings? settings, out string databasePath)
    {
        databasePath = string.Empty;
        var root = AssetLibraryPortableSettings.NormalizePath(settings?.CurrentContainerPath);
        if (root.Length == 0 || !Directory.Exists(root) || !string.Equals(Path.GetExtension(root), ContainerExtension, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            EnsureNotReparsePoint(root, "素材库根目录");
            var manifest = ReadManifest(root);
            EnsureCriticalPathsArePhysical(root, manifest);
            var candidate = ResolveContainedPath(root, manifest.DatabaseRelativePath);
            if (!File.Exists(candidate)) return false;
            databasePath = candidate;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException) { return false; }
    }

    private static string NormalizeContainerPath(string path)
    {
        var full = AssetLibraryPortableSettings.NormalizePath(path);
        if (full.Length == 0) throw new ArgumentException("素材库路径不能为空。", nameof(path));
        if (!string.Equals(Path.GetExtension(full), ContainerExtension, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"素材库目录必须使用 {ContainerExtension} 扩展名。");
        ValidatePathComponent(Path.GetFileName(full), "素材库目录名");
        return full;
    }

    private static void CreateLayout(string root)
    {
        foreach (var relative in new[] { "database", ManagedAssetsRelativePath, PreviewCacheRelativePath, "analysis", "journals", "backups", "locks", "logs" })
            Directory.CreateDirectory(ResolveContainedPath(root, relative));
    }

    private static async Task WriteManifestAsync(string root, ContainerManifest manifest, CancellationToken cancellationToken)
    {
        manifest.PayloadSha256 = ComputePayloadHash(manifest);
        var path = Path.Combine(root, ManifestFileName);
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        await File.WriteAllBytesAsync(temporary, SerializeManifest(manifest), cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path);
    }

    private static async Task<ContainerManifest> ReadManifestAsync(string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, ManifestFileName);
        if (!File.Exists(path)) throw new InvalidDataException("素材库 manifest 缺失。");
        return ParseManifest(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
    }

    private static ContainerManifest ReadManifest(string root)
    {
        var path = Path.Combine(root, ManifestFileName);
        if (!File.Exists(path)) throw new InvalidDataException("素材库 manifest 缺失。");
        return ParseManifest(File.ReadAllBytes(path));
    }

    private static ContainerManifest ParseManifest(byte[] utf8)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("素材库 manifest 根节点必须为对象。");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new InvalidDataException($"素材库 manifest 包含重复属性：{property.Name}");
                if (!KnownManifestProperties.Contains(property.Name)) throw new InvalidDataException($"素材库 manifest 包含未知关键属性：{property.Name}");
                if (property.Name == "extensions" && property.Value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("manifest extensions 必须为对象。");
            }
            var root = document.RootElement;
            var manifest = new ContainerManifest
            {
                ContainerFormatVersion = ReadInt(root, "container_format_version"),
                LibraryId = ReadGuid(root, "library_id"),
                DisplayName = ReadString(root, "display_name"),
                CreatedAt = ReadDate(root, "created_at"),
                UpdatedAt = ReadDate(root, "updated_at"),
                DatabaseRelativePath = ReadString(root, "database_relative_path"),
                DatabaseSchemaVersion = ReadInt(root, "database_schema_version"),
                ManagedAssetsRelativePath = ReadString(root, "managed_assets_relative_path"),
                PreviewCacheRelativePath = ReadString(root, "preview_cache_relative_path"),
                ContentMode = ReadString(root, "content_mode"),
                AppMinimumVersion = ReadString(root, "app_minimum_version"),
                PayloadSha256 = ReadString(root, "payload_sha256")
            };
            ValidateManifest(manifest);
            return manifest;
        }
        catch (JsonException ex) { throw new InvalidDataException("素材库 manifest 无法解析。", ex); }
    }

    private static byte[] SerializeManifest(ContainerManifest manifest)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("container_format_version", manifest.ContainerFormatVersion); writer.WriteString("library_id", manifest.LibraryId);
            writer.WriteString("display_name", manifest.DisplayName); writer.WriteString("created_at", manifest.CreatedAt.ToUniversalTime()); writer.WriteString("updated_at", manifest.UpdatedAt.ToUniversalTime());
            writer.WriteString("database_relative_path", manifest.DatabaseRelativePath); writer.WriteNumber("database_schema_version", manifest.DatabaseSchemaVersion);
            writer.WriteString("managed_assets_relative_path", manifest.ManagedAssetsRelativePath); writer.WriteString("preview_cache_relative_path", manifest.PreviewCacheRelativePath);
            writer.WriteString("content_mode", manifest.ContentMode); writer.WriteString("app_minimum_version", manifest.AppMinimumVersion); writer.WriteString("payload_sha256", manifest.PayloadSha256);
            writer.WriteStartObject("extensions"); writer.WriteEndObject(); writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static string ComputePayloadHash(ContainerManifest manifest)
    {
        var payload = string.Join("\n", new[]
        {
            manifest.ContainerFormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), manifest.LibraryId.ToString("D"), manifest.DisplayName,
            manifest.CreatedAt.ToUniversalTime().ToString("O"), manifest.UpdatedAt.ToUniversalTime().ToString("O"), manifest.DatabaseRelativePath,
            manifest.DatabaseSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), manifest.ManagedAssetsRelativePath,
            manifest.PreviewCacheRelativePath, manifest.ContentMode, manifest.AppMinimumVersion
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static void ValidateManifest(ContainerManifest manifest)
    {
        if (manifest.ContainerFormatVersion != ContainerFormatVersion) throw new InvalidDataException($"不支持的素材库容器版本：{manifest.ContainerFormatVersion}");
        if (manifest.LibraryId == Guid.Empty) throw new InvalidDataException("素材库身份无效。");
        ValidateDisplayName(manifest.DisplayName);
        if (manifest.CreatedAt == default || manifest.UpdatedAt < manifest.CreatedAt) throw new InvalidDataException("素材库时间字段无效。");
        if (manifest.DatabaseSchemaVersion != AssetLibrarySchema.Version) throw new InvalidDataException("素材库声明的数据库 schema 版本不受支持。");
        ValidateExactRelativePath(manifest.DatabaseRelativePath, DatabaseRelativePath, "数据库");
        ValidateExactRelativePath(manifest.ManagedAssetsRelativePath, ManagedAssetsRelativePath, "托管素材");
        ValidateExactRelativePath(manifest.PreviewCacheRelativePath, PreviewCacheRelativePath, "预览缓存");
        if (manifest.ContentMode is not ("reference" or "managed" or "mixed")) throw new InvalidDataException("素材库 content_mode 无效。");
        if (string.IsNullOrWhiteSpace(manifest.AppMinimumVersion) || manifest.AppMinimumVersion.Any(char.IsControl)) throw new InvalidDataException("素材库最低应用版本无效。");
        if (!string.Equals(manifest.PayloadSha256, ComputePayloadHash(manifest), StringComparison.Ordinal)) throw new InvalidDataException("素材库 manifest 完整性校验失败。");
    }

    private static void ValidateExactRelativePath(string actual, string expected, string field)
    {
        if (!string.Equals(actual?.Replace('\\', '/'), expected, StringComparison.Ordinal)) throw new InvalidDataException($"素材库{field}相对路径不受支持。");
        foreach (var component in expected.Split('/')) ValidatePathComponent(component, $"{field}路径");
    }

    private static void ValidateDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumDisplayNameLength || value.Any(char.IsControl)) throw new InvalidDataException("素材库名称为空、过长或包含控制字符。");
    }

    private static async Task ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString()); await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var integrity = connection.CreateCommand(); integrity.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)), "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("素材库数据库未通过完整性检查。");
        await using var version = connection.CreateCommand(); version.CommandText = "SELECT COALESCE(MAX(Version),0) FROM AssetLibrarySchemaInfo;";
        if (Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != AssetLibrarySchema.Version) throw new InvalidDataException("素材库数据库 schema 版本不受支持。");
    }

    private static void EnsureCriticalPathsArePhysical(string root, ContainerManifest manifest)
    {
        EnsureNotReparsePoint(root, "素材库根目录");
        foreach (var relative in new[] { manifest.DatabaseRelativePath, manifest.ManagedAssetsRelativePath, manifest.PreviewCacheRelativePath, "backups", "locks" }) EnsureNoReparseComponents(root, ResolveContainedPath(root, relative));
    }

    private static void EnsureNoReparseComponents(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar); var relative = Path.GetRelativePath(normalizedRoot, candidate); var current = normalizedRoot;
        EnsureNotReparsePoint(current, "素材库根目录");
        foreach (var component in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        { current = Path.Combine(current, component); if (File.Exists(current) || Directory.Exists(current)) EnsureNotReparsePoint(current, "素材库内部路径"); }
    }

    private static void EnsureNotReparsePoint(string path, string field)
    { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"{field}不能是符号链接或重解析点：{path}"); }

    private static string ResolveContainedPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("容器内部路径必须是安全相对路径。");
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("容器内部路径越界。");
        return candidate;
    }

    private static void ValidatePathComponent(string component, string field)
    {
        var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        var stem = component.Split('.')[0];
        if (string.IsNullOrWhiteSpace(component) || component.EndsWith('.') || component.EndsWith(' ') || component.Any(character => char.IsControl(character) || Path.GetInvalidFileNameChars().Contains(character)) || reserved.Contains(stem, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException($"{field}包含不安全的路径组件：{component}");
    }

    private static string NormalizeDisplayName(string displayName, string path)
    { var value = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(path) : displayName.Trim(); ValidateDisplayName(value); return value; }

    private static AssetLibraryContainerDescriptor ToDescriptor(string root, ContainerManifest manifest) => new(
        manifest.LibraryId, manifest.DisplayName, root, ResolveContainedPath(root, manifest.DatabaseRelativePath), manifest.CreatedAt, manifest.UpdatedAt,
        ResolveContainedPath(root, manifest.ManagedAssetsRelativePath), ResolveContainedPath(root, manifest.PreviewCacheRelativePath), manifest.ContentMode);

    private static async Task WriteStagingOwnerAsync(string staging, Guid operationId, string target, CancellationToken cancellationToken)
    { await File.WriteAllBytesAsync(Path.Combine(staging, StagingOwnerFileName), JsonSerializer.SerializeToUtf8Bytes(new StagingOwner(operationId, target)), cancellationToken).ConfigureAwait(false); }

    private static void TryDeleteOwnedStaging(string parent, string staging, string expectedName, Guid operationId, string target)
    {
        try
        {
            if (!Directory.Exists(staging) || !string.Equals(Path.GetDirectoryName(staging), parent, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFileName(staging), expectedName, StringComparison.Ordinal)) return;
            var markerPath = Path.Combine(staging, StagingOwnerFileName);
            if (!File.Exists(markerPath) || (File.GetAttributes(staging) & FileAttributes.ReparsePoint) != 0) return;
            var owner = JsonSerializer.Deserialize<StagingOwner>(File.ReadAllBytes(markerPath));
            if (owner?.OperationId != operationId || !string.Equals(owner.TargetPath, target, StringComparison.OrdinalIgnoreCase)) return;
            Directory.Delete(staging, recursive: true);
        }
        catch { }
    }

    private static int ReadInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : throw new InvalidDataException($"manifest 缺少有效字段：{name}");
    private static string ReadString(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : throw new InvalidDataException($"manifest 缺少有效字段：{name}");
    private static Guid ReadGuid(JsonElement root, string name) => Guid.TryParse(ReadString(root, name), out var value) ? value : throw new InvalidDataException($"manifest GUID 无效：{name}");
    private static DateTimeOffset ReadDate(JsonElement root, string name) => DateTimeOffset.TryParse(ReadString(root, name), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var value) ? value : throw new InvalidDataException($"manifest 时间无效：{name}");

    private sealed class ContainerManifest
    {
        public int ContainerFormatVersion { get; set; }
        public Guid LibraryId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string DatabaseRelativePath { get; set; } = string.Empty;
        public int DatabaseSchemaVersion { get; set; }
        public string ManagedAssetsRelativePath { get; set; } = string.Empty; public string PreviewCacheRelativePath { get; set; } = string.Empty;
        public string ContentMode { get; set; } = string.Empty; public string AppMinimumVersion { get; set; } = string.Empty; public string PayloadSha256 { get; set; } = string.Empty;
    }

    private sealed record StagingOwner(Guid OperationId, string TargetPath);
}

public sealed class AssetLibraryWriteLease : IAsyncDisposable
{
    private readonly FileStream _stream; private int _disposed;
    private AssetLibraryWriteLease(FileStream stream, AssetLibraryLockOwner owner) { _stream = stream; Owner = owner; }
    public AssetLibraryLockOwner Owner { get; }

    internal static async Task<AssetLibraryWriteLease> AcquireAsync(string lockDirectory, Guid libraryId, CancellationToken cancellationToken)
    {
        var lockPath = Path.Combine(lockDirectory, "writer.lock"); FileStream stream;
        try { stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, true); }
        catch (IOException ex) { throw new InvalidOperationException("此素材库正在被另一个像素蛋挞实例使用。", ex); }
        try
        {
            var process = Process.GetCurrentProcess();
            var owner = new AssetLibraryLockOwner(1, libraryId, process.Id, process.StartTime.ToUniversalTime(), Environment.MachineName, process.SessionId, Branding.ProductVersion, Guid.NewGuid(), DateTimeOffset.UtcNow);
            var payload = JsonSerializer.SerializeToUtf8Bytes(owner, new JsonSerializerOptions { WriteIndented = true });
            stream.SetLength(0); await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false); await stream.FlushAsync(cancellationToken).ConfigureAwait(false); stream.Position = 0;
            return new AssetLibraryWriteLease(stream, owner);
        }
        catch { await stream.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public async ValueTask DisposeAsync() { if (Interlocked.Exchange(ref _disposed, 1) == 0) await _stream.DisposeAsync().ConfigureAwait(false); }
}

public sealed record AssetLibraryLockOwner(int Version, Guid LibraryId, int ProcessId, DateTime ProcessStartedAt, string MachineName, int SessionId, string AppVersion, Guid LeaseId, DateTimeOffset AcquiredAt);
