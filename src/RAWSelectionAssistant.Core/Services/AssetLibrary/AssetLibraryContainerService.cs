using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record AssetLibraryContainerDescriptor(
    Guid LibraryId,
    string DisplayName,
    string ContainerPath,
    string DatabasePath,
    DateTimeOffset CreatedAt);

public sealed class AssetLibraryContainerService
{
    public const int ContainerFormatVersion = 1;
    public const string ContainerExtension = ".ptlibrary";
    public const string ManifestFileName = "library.manifest.json";
    public const string DatabaseRelativePath = "database/asset-library-v16.db";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<AssetLibraryContainerDescriptor> CreateAsync(
        string containerPath,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var target = NormalizeContainerPath(containerPath);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException($"目标素材库已存在：{target}");
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidDataException("素材库必须有父目录。");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(target)}.staging-{Guid.NewGuid():N}");
        var now = DateTimeOffset.UtcNow;
        var manifest = new ContainerManifest
        {
            ContainerFormatVersion = ContainerFormatVersion,
            LibraryId = Guid.NewGuid(),
            DisplayName = NormalizeDisplayName(displayName, target),
            CreatedAt = now,
            DatabaseRelativePath = DatabaseRelativePath
        };

        try
        {
            CreateLayout(staging);
            var databasePath = ResolveContainedPath(staging, DatabaseRelativePath);
            var repository = new SqliteAssetLibraryRepository(databasePath);
            await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await repository.DisposeAsync().ConfigureAwait(false);
            await WriteManifestAsync(staging, manifest, cancellationToken).ConfigureAwait(false);
            await ValidateDatabaseAsync(databasePath, cancellationToken).ConfigureAwait(false);
            Directory.Move(staging, target);
            return ToDescriptor(target, manifest);
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    public async Task<AssetLibraryContainerDescriptor> OpenAsync(
        string containerPath,
        CancellationToken cancellationToken = default)
    {
        var root = NormalizeContainerPath(containerPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"素材库不存在：{root}");
        var manifestPath = Path.Combine(root, ManifestFileName);
        if (!File.Exists(manifestPath)) throw new InvalidDataException("素材库 manifest 缺失。");
        ContainerManifest manifest;
        try
        {
            await using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true);
            manifest = await JsonSerializer.DeserializeAsync<ContainerManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("素材库 manifest 为空。");
        }
        catch (JsonException ex) { throw new InvalidDataException("素材库 manifest 无法解析。", ex); }

        ValidateManifest(manifest);
        var databasePath = ResolveContainedPath(root, manifest.DatabaseRelativePath);
        if (!File.Exists(databasePath)) throw new InvalidDataException("素材库数据库缺失。");
        await ValidateDatabaseAsync(databasePath, cancellationToken).ConfigureAwait(false);
        return ToDescriptor(root, manifest);
    }

    public static bool TryResolveStartupDatabasePath(
        AssetLibraryPortableSettings? settings,
        out string databasePath)
    {
        databasePath = string.Empty;
        var root = AssetLibraryPortableSettings.NormalizePath(settings?.CurrentContainerPath);
        if (root.Length == 0 || !Directory.Exists(root) ||
            !string.Equals(Path.GetExtension(root), ContainerExtension, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var manifestPath = Path.Combine(root, ManifestFileName);
            if (!File.Exists(manifestPath)) return false;
            var manifest = JsonSerializer.Deserialize<ContainerManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is null) return false;
            ValidateManifest(manifest);
            var candidate = ResolveContainedPath(root, manifest.DatabaseRelativePath);
            if (!File.Exists(candidate)) return false;
            databasePath = candidate;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            return false;
        }
    }

    private static string NormalizeContainerPath(string path)
    {
        var full = AssetLibraryPortableSettings.NormalizePath(path);
        if (full.Length == 0) throw new ArgumentException("素材库路径不能为空。", nameof(path));
        if (!string.Equals(Path.GetExtension(full), ContainerExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"素材库目录必须使用 {ContainerExtension} 扩展名。");
        return full;
    }

    private static void CreateLayout(string root)
    {
        foreach (var relative in new[] { "database", "assets/managed", "previews", "analysis", "journals", "backups", "locks", "logs" })
            Directory.CreateDirectory(ResolveContainedPath(root, relative));
    }

    private static async Task WriteManifestAsync(string root, ContainerManifest manifest, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, ManifestFileName);
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384, true))
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path);
    }

    private static void ValidateManifest(ContainerManifest manifest)
    {
        if (manifest.ContainerFormatVersion != ContainerFormatVersion)
            throw new InvalidDataException($"不支持的素材库容器版本：{manifest.ContainerFormatVersion}");
        if (manifest.LibraryId == Guid.Empty) throw new InvalidDataException("素材库身份无效。");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName)) throw new InvalidDataException("素材库名称为空。");
        if (!string.Equals(manifest.DatabaseRelativePath.Replace('\\', '/'), DatabaseRelativePath, StringComparison.Ordinal))
            throw new InvalidDataException("素材库数据库路径不受支持。");
    }

    private static async Task ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("素材库数据库未通过完整性检查。");
        await using var version = connection.CreateCommand();
        version.CommandText = "SELECT COALESCE(MAX(Version),0) FROM AssetLibrarySchemaInfo;";
        if (Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != AssetLibrarySchema.Version)
            throw new InvalidDataException("素材库数据库 schema 版本不受支持。");
    }

    private static string ResolveContainedPath(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException("容器内部路径必须为相对路径。");
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("容器内部路径越界。");
        return candidate;
    }

    private static string NormalizeDisplayName(string displayName, string path) =>
        string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(path) : displayName.Trim();

    private static AssetLibraryContainerDescriptor ToDescriptor(string root, ContainerManifest manifest) => new(
        manifest.LibraryId,
        manifest.DisplayName,
        root,
        ResolveContainedPath(root, manifest.DatabaseRelativePath),
        manifest.CreatedAt);

    private sealed class ContainerManifest
    {
        [JsonPropertyName("container_format_version")]
        public int ContainerFormatVersion { get; set; }
        [JsonPropertyName("library_id")]
        public Guid LibraryId { get; set; }
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;
        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
        [JsonPropertyName("database_relative_path")]
        public string DatabaseRelativePath { get; set; } = string.Empty;
    }
}
