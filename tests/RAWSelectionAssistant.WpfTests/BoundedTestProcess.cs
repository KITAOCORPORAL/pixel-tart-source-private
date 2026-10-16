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
        using var samplingStop=new CancellationTokenSource();
        var sampleTask=Task.Run(async()=>{while(!samplingStop.IsCancellationRequested){
            try{process.Refresh();Log("sample",new{pid=process.Id,elapsedMs=watch.Elapsed.TotalMilliseconds,hasExited=process.HasExited,cpuMs=process.TotalProcessorTime.TotalMilliseconds,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,threads=process.Threads.Cast<ProcessThread>().Select(t=>new{t.Id,state=t.ThreadState.ToString(),wait=t.ThreadState==System.Diagnostics.ThreadState.Wait?t.WaitReason.ToString():null}).ToArray(),outputTask=outputTask.Status.ToString(),exitTask=exitTask.Status.ToString(),errorTask=errorTask.Status.ToString()});}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            try{await Task.Delay(250,samplingStop.Token);}catch(OperationCanceledException){}
        }});
        Log("start",new{pid=process.Id,file=start.FileName,arguments=start.ArgumentList.ToArray(),cwd=start.WorkingDirectory,cwdLength=start.WorkingDirectory.Length,temp=Environment.GetEnvironmentVariable("TEMP"),limitMs=timeout.TotalMilliseconds});
        try
        {
            await Task.WhenAll(outputTask, errorTask, exitTask).WaitAsync(deadline.Token).ConfigureAwait(false);
            var result = new Result(process.Id, process.ExitCode, stdout.ToArray(), await errorTask.ConfigureAwait(false), watch.Elapsed);
            Console.WriteLine($"SUBPROCESS_END pid={result.ProcessId} exit={result.ExitCode} elapsed_ms={result.Elapsed.TotalMilliseconds:F0} stdout_bytes={result.Stdout.Length} stderr_chars={result.Stderr.Length}");
            Log("end",new{pid=result.ProcessId,exitCode=result.ExitCode,elapsedMs=result.Elapsed.TotalMilliseconds,stdoutBytes=result.Stdout.Length,stderrChars=result.Stderr.Length});
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
            Log("timeout",new{pid=process.Id,elapsedMs=watch.Elapsed.TotalMilliseconds,outputTask=outputTask.Status.ToString(),exitTask=exitTask.Status.ToString(),errorTask=errorTask.Status.ToString(),stderr});
            throw new TimeoutException($"Owned subprocess timed out: pid={process.Id}; file={start.FileName}; limit={timeout}; elapsed={watch.Elapsed}; output/exit/EOF completion={outputTask.Status}/{exitTask.Status}/{errorTask.Status}; stderr={stderr}");
        }
        finally { samplingStop.Cancel();await sampleTask.ConfigureAwait(false); }
    }
    private static readonly object LogGate=new();
    private static void Log(string phase,object detail)
    {
        var target=Environment.GetEnvironmentVariable("KITAO_TEST_PROCESS_EVENTS");if(string.IsNullOrWhiteSpace(target))return;
        lock(LogGate)File.AppendAllText(target,System.Text.Json.JsonSerializer.Serialize(new{atUtc=DateTimeOffset.UtcNow,phase,ownerPid=Environment.ProcessId,detail})+Environment.NewLine);
    }
}
