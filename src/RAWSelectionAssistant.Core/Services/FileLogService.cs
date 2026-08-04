using System.Text;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class FileLogService : ILogService
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly LogRetentionOptions _retention;

    public FileLogService(string? logDirectory = null, LogRetentionOptions? retention = null)
    {
        AppDataPaths.EnsureCreated();
        _logDirectory = logDirectory ?? AppDataPaths.LogDirectory;
        _retention = retention ?? new LogRetentionOptions();
        Directory.CreateDirectory(_logDirectory);
        new LogMaintenanceService(_logDirectory, _retention).CleanupAsync().GetAwaiter().GetResult();
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var path = ResolveCurrentPath();
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {AuditLogService.Sanitize(message)}{Environment.NewLine}";
            if (exception is not null)
            {
                text += AuditLogService.Sanitize(exception.ToString()) + Environment.NewLine;
            }

            lock (_sync)
            {
                File.AppendAllText(path, text, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging must never terminate the desktop application.
        }
    }

    private string ResolveCurrentPath()
    {
        var prefix = $"app-{DateTime.Now:yyyyMMdd}";
        for (var index = 0; index < 1000; index++)
        {
            var path = Path.Combine(_logDirectory, index == 0 ? prefix + ".log" : $"{prefix}-{index:D3}.log");
            if (!File.Exists(path) || new FileInfo(path).Length < _retention.MaximumFileBytes) return path;
        }
        return Path.Combine(_logDirectory, prefix + "-overflow.log");
    }
}

public sealed record LogRetentionOptions(int MaximumDays = 30, long MaximumTotalBytes = 200L * 1024 * 1024, long MaximumFileBytes = 10L * 1024 * 1024);
