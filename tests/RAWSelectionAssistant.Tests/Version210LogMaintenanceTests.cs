using System.IO.Compression;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210LogMaintenanceTests
{
    [TestMethod]
    public void FileLog_RotatesWhenMaximumFileSizeIsReached()
    {
        using var temp=new TempDirectory();var log=new FileLogService(temp.Path,new LogRetentionOptions(30,1024*1024,1));log.Info("first");log.Info("second");Assert.IsGreaterThanOrEqualTo(Directory.GetFiles(temp.Path,"*.log").Length,2);
    }

    [TestMethod]
    public async Task Cleanup_RemovesExpiredLogs()
    {
        using var temp=new TempDirectory();var old=temp.CreateFile("app-old.log",new byte[10]);File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-31));await new LogMaintenanceService(temp.Path,new LogRetentionOptions(30,1024*1024,1024)).CleanupAsync();Assert.IsFalse(File.Exists(old));
    }

    [TestMethod]
    public async Task Cleanup_EnforcesTotalCapacity()
    {
        using var temp=new TempDirectory();var newest=temp.CreateFile("new.log",new byte[60]);var oldest=temp.CreateFile("old.log",new byte[60]);File.SetLastWriteTimeUtc(newest,DateTime.UtcNow);File.SetLastWriteTimeUtc(oldest,DateTime.UtcNow.AddMinutes(-1));await new LogMaintenanceService(temp.Path,new LogRetentionOptions(30,80,1024)).CleanupAsync();Assert.IsTrue(File.Exists(newest));Assert.IsFalse(File.Exists(oldest));
    }

    [TestMethod]
    public async Task DiagnosticPackage_RedactsSecretsAndContainsNoPhotos()
    {
        using var temp=new TempDirectory();await File.WriteAllTextAsync(temp.Combine("app.log"),@"C:\Customers\Alice\portrait.jpg token=abcdef");var zip=await new LogMaintenanceService(temp.Path).ExportDiagnosticsAsync(temp.Combine("diagnostic.zip"));using var archive=ZipFile.OpenRead(zip);Assert.IsFalse(archive.Entries.Any(x=>new[]{".jpg",".jpeg",".raw",".arw"}.Contains(Path.GetExtension(x.FullName),StringComparer.OrdinalIgnoreCase)));var logEntry=archive.Entries.Single(x=>x.FullName.EndsWith("app.log",StringComparison.Ordinal));using var reader=new StreamReader(logEntry.Open());var text=await reader.ReadToEndAsync();Assert.IsFalse(text.Contains("Customers",StringComparison.OrdinalIgnoreCase));Assert.IsFalse(text.Contains("abcdef",StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Clear_RemovesUserLogs()
    {
        using var temp=new TempDirectory();temp.CreateFile("one.log",[1]);temp.CreateFile("two.log",[2]);await new LogMaintenanceService(temp.Path).ClearAsync();Assert.IsEmpty(Directory.GetFiles(temp.Path,"*.log"));
    }
}

