using System.Diagnostics;
using System.IO;
using System.Text;

namespace RAWSelectionAssistant.WpfTests;

/// <summary>Test-only subprocess supervision. Drains both pipes concurrently and bounds exit AND pipe EOF.</summary>
internal static class BoundedTestProcess
{
    internal sealed record Result(int ProcessId, int ExitCode, byte[] Stdout, string Stderr, TimeSpan Elapsed)
    {
        public string Output => Encoding.UTF8.GetString(Stdout);
    }

    public static Result Run(ProcessStartInfo start, TimeSpan? timeout = null)
    {
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        return RunAsync(start, timeout ?? TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
    }

    private static async Task<Result> RunAsync(ProcessStartInfo start, TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Cannot start {start.FileName}.");
        using var stdout = new MemoryStream();
        using var deadline = new CancellationTokenSource(timeout);
        var watch = Stopwatch.StartNew();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var errorTask = process.StandardError.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();
        Console.WriteLine($"SUBPROCESS_START pid={process.Id} file={start.FileName} timeout_ms={timeout.TotalMilliseconds:F0}");
        try
        {
            await Task.WhenAll(outputTask, errorTask, exitTask).WaitAsync(deadline.Token).ConfigureAwait(false);
            var result = new Result(process.Id, process.ExitCode, stdout.ToArray(), await errorTask.ConfigureAwait(false), watch.Elapsed);
            Console.WriteLine($"SUBPROCESS_END pid={result.ProcessId} exit={result.ExitCode} elapsed_ms={result.Elapsed.TotalMilliseconds:F0} stdout_bytes={result.Stdout.Length} stderr_chars={result.Stderr.Length}");
            return result;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            // Kill only this owned subprocess tree, never a name-based/global process set.
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            try { await Task.WhenAll(outputTask, errorTask, exitTask).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception cleanup) { Console.WriteLine($"SUBPROCESS_CLEANUP pid={process.Id} {cleanup.GetType().Name}"); }
            var stderr = errorTask.IsCompletedSuccessfully ? errorTask.Result : "<stderr EOF not reached>";
            throw new TimeoutException($"Owned subprocess timed out: pid={process.Id}; file={start.FileName}; limit={timeout}; elapsed={watch.Elapsed}; output/exit/EOF completion={outputTask.Status}/{exitTask.Status}/{errorTask.Status}; stderr={stderr}");
        }
    }
}
