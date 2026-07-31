using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RawIndexServiceTests
{
    [TestMethod]
    public async Task Scan_IndexesRawExtensionsWithoutCaseSensitivity()
    {
        using var temp = new TempDirectory();
        temp.CreateFile("中文目录/DSC00001.arw");
        temp.CreateFile("中文目录/IMG_0002.Cr3");
        temp.CreateFile("中文目录/not-raw.jpg");
        var service = CreateService(temp);
        var index = await service.ScanAsync([temp.Path], null, null, CancellationToken.None);
        Assert.HasCount(2, index.Files);
        Assert.IsTrue(index.ByNumericId.ContainsKey("1"));
        Assert.IsTrue(index.ByNumericId.ContainsKey("2"));
    }

    [TestMethod]
    public async Task Scan_CancellationReturnsCanceledTaskWithoutCrash()
    {
        using var temp = new TempDirectory();
        temp.CreateFile("DSC00001.ARW");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = CreateService(temp);
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ScanAsync([temp.Path], null, null, cts.Token));
    }

    [TestMethod]
    public async Task Scan_UnauthorizedDirectoryDoesNotStopOtherSources()
    {
        using var temp = new TempDirectory();
        var validRoot = temp.Combine("valid");
        var deniedRoot = temp.Combine("denied");
        Directory.CreateDirectory(validRoot);
        Directory.CreateDirectory(deniedRoot);
        temp.CreateFile("valid/DSC00007.ARW");
        var fileSystem = new DeniedDirectoryFileSystem(deniedRoot);
        var log = new TestLogService();
        var service = new RawIndexService(new FileNameNormalizer(), log, fileSystem, temp.Combine("index.json"));

        var index = await service.ScanAsync([deniedRoot, validRoot], null, null, CancellationToken.None);

        Assert.HasCount(1, index.Files);
        Assert.IsGreaterThanOrEqualTo(1, index.SkippedDirectoryCount);
        Assert.IsTrue(log.Messages.Any(x => x.Contains("UnauthorizedAccessException", StringComparison.Ordinal)));
    }

    private static RawIndexService CreateService(TempDirectory temp) =>
        new(new FileNameNormalizer(), new TestLogService(), cacheFilePath: temp.Combine("index.json"));

    private sealed class DeniedDirectoryFileSystem(string deniedRoot) : IRawFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public IEnumerable<string> EnumerateDirectories(string path)
        {
            if (path.Equals(deniedRoot, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("test");
            return Directory.EnumerateDirectories(path);
        }
        public IEnumerable<string> EnumerateFiles(string path)
        {
            if (path.Equals(deniedRoot, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("test");
            return Directory.EnumerateFiles(path);
        }
        public FileInfo GetFileInfo(string path) => new(path);
    }
}
