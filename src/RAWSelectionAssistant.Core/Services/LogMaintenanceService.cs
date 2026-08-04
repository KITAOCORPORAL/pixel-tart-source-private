using System.IO.Compression;
using System.Text;

namespace RAWSelectionAssistant.Core.Services;

public interface ILogMaintenanceService
{
    Task CleanupAsync(CancellationToken cancellationToken = default);
    Task<string> ExportDiagnosticsAsync(string destinationZip, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class LogMaintenanceService(string logDirectory, LogRetentionOptions? options = null) : ILogMaintenanceService
{
    private readonly LogRetentionOptions _options = options ?? new LogRetentionOptions();

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(logDirectory)) return Task.CompletedTask;
        var files = Directory.EnumerateFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly).Select(path => new FileInfo(path)).OrderByDescending(x => x.LastWriteTimeUtc).ToList();
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.MaximumDays));
        foreach (var file in files.Where(x => x.LastWriteTimeUtc < cutoff).ToArray()) { cancellationToken.ThrowIfCancellationRequested(); TryDelete(file.FullName); files.Remove(file); }
        long total = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += file.Exists ? file.Length : 0;
            if (total > _options.MaximumTotalBytes) TryDelete(file.FullName);
        }
        return Task.CompletedTask;
    }

    public async Task<string> ExportDiagnosticsAsync(string destinationZip, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(destinationZip);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if (File.Exists(full)) throw new IOException("Diagnostic package already exists.");
        await using var output = new FileStream(full, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, true);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
        if (Directory.Exists(logDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(logDirectory, "*.log").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.CreateEntry("Logs/" + Path.GetFileName(file), CompressionLevel.Fastest);
                await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                foreach (var line in await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false)) await writer.WriteLineAsync(AuditLogService.Sanitize(line)).ConfigureAwait(false);
            }
        }
        var diagnostics = archive.CreateEntry("diagnostics.txt", CompressionLevel.Fastest);
        await using (var writer = new StreamWriter(diagnostics.Open(), new UTF8Encoding(false)))
        {
            await writer.WriteLineAsync($"Product=像素蛋挞").ConfigureAwait(false);
            await writer.WriteLineAsync($"Version={RAWSelectionAssistant.Core.Models.Branding.ProductVersion}").ConfigureAwait(false);
            await writer.WriteLineAsync($"OS={Environment.OSVersion.VersionString}").ConfigureAwait(false);
            await writer.WriteLineAsync($"Runtime={Environment.Version}").ConfigureAwait(false);
            await writer.WriteLineAsync("PhotosIncluded=false").ConfigureAwait(false);
            await writer.WriteLineAsync("LicenseSecretsIncluded=false").ConfigureAwait(false);
        }
        return full;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(logDirectory)) return Task.CompletedTask;
        foreach (var file in Directory.EnumerateFiles(logDirectory, "*.log")) { cancellationToken.ThrowIfCancellationRequested(); TryDelete(file); }
        return Task.CompletedTask;
    }

    private static void TryDelete(string path) { try { File.Delete(path); } catch { } }
}
