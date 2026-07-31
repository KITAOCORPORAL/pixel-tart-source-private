using System.Text;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class FileLogService : ILogService
{
    private readonly object _sync = new();

    public FileLogService() => AppDataPaths.EnsureCreated();

    public void Info(string message) => Write("INFO", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var path = Path.Combine(AppDataPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            if (exception is not null)
            {
                text += exception + Environment.NewLine;
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
}
