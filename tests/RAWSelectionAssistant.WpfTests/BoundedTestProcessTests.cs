using System.Diagnostics;
using System.IO;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BoundedTestProcessTests
{
    private static ProcessStartInfo Start(string script) => new("powershell.exe")
    { ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script } };
    [TestMethod]
    public void ConcurrentLargeStdoutAndStderrFinishWithoutPipeDeadlock()
    {
        var result=BoundedTestProcess.Run(Start("$s='x'*8192;1..32|%{[Console]::Error.Write($s);[Console]::Out.Write($s)};exit 7"));
        Assert.AreEqual(7,result.ExitCode);Assert.AreEqual(8192*32,result.Stdout.Length);Assert.AreEqual(8192*32,result.Stderr.Length);
    }
    [TestMethod]
    public void TimeoutKillsOwnedProcessAndIsNotReportedAsPassOrPartialExit()
    {
        var path=Path.Combine(Path.GetTempPath(),"owned-subprocess-"+Guid.NewGuid()+".pid");
        try
        {
            var escaped=path.Replace("'","''");
            var error=Assert.Throws<TimeoutException>(()=>BoundedTestProcess.Run(Start($"[IO.File]::WriteAllText('{escaped}',[string]$PID);Start-Sleep -Seconds 30"),TimeSpan.FromSeconds(3)));
            StringAssert.Contains(error.Message,"Owned subprocess timed out");Assert.IsTrue(File.Exists(path));
            var pid=int.Parse(File.ReadAllText(path));Assert.IsFalse(Process.GetProcesses().Any(p=>p.Id==pid),"Timed-out owned subprocess must not remain alive.");
        }
        finally{File.Delete(path);}
    }
}
