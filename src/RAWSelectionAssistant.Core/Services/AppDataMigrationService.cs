using System.Text;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class AppDataMigrationService(ILogService? logService = null)
{
    public bool MigrateLegacyData(string? legacyRoot = null, string? targetRoot = null)
    {
        legacyRoot ??= AppDataPaths.LegacyRoot;
        targetRoot ??= AppDataPaths.Root;
        if (!Directory.Exists(legacyRoot) || PathsEqual(legacyRoot, targetRoot))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(targetRoot);
            CopyFileIfMissing(Path.Combine(legacyRoot, "settings.json"), Path.Combine(targetRoot, "settings.json"));
            CopyDirectoryIfMissing(Path.Combine(legacyRoot, "Indexes"), Path.Combine(targetRoot, "Indexes"));
            CopyDirectoryIfMissing(Path.Combine(legacyRoot, "Logs"), Path.Combine(targetRoot, "Logs"));
            WriteMigrationLog(targetRoot, $"已从旧版应用数据目录兼容复制设置、索引和必要日志：{legacyRoot}");
            logService?.Info($"旧版应用数据已兼容复制到新目录：{targetRoot}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            try { WriteMigrationLog(targetRoot, $"旧版应用数据迁移失败，将继续尝试使用可用配置：{ex.Message}"); } catch { }
            logService?.Error("旧版应用数据迁移失败。", ex);
            return false;
        }
    }

    private static void CopyFileIfMissing(string source, string target)
    {
        if (!File.Exists(source) || File.Exists(target)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, false);
    }

    private static void CopyDirectoryIfMissing(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            if (File.Exists(destination)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, false);
        }
    }

    private static void WriteMigrationLog(string targetRoot, string message)
    {
        Directory.CreateDirectory(Path.Combine(targetRoot, "Logs"));
        File.AppendAllText(
            Path.Combine(targetRoot, "Logs", "migration.log"),
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}",
            new UTF8Encoding(false));
    }

    private static bool PathsEqual(string first, string second) => string.Equals(
        Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);
}
