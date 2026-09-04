using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3AutomatedAcceptanceDriverDisposalContractTests
{
    [TestMethod]
    public void DriverExposesIdempotentAsyncDisposalWithoutSynchronousWaiting()
    {
        var source = ReadDriver();

        StringAssert.Contains(source,
            "public sealed class AssetLibraryP3AutomatedAcceptanceDriver : IAsyncDisposable");
        StringAssert.Contains(source, "public ValueTask DisposeAsync()");
        StringAssert.Contains(source, "lock (_disposeGate)");
        StringAssert.Contains(source, "_disposeTask ??= DisposeCoreAsync();");
        StringAssert.Contains(source, "return new ValueTask(_disposeTask);");
        StringAssert.Contains(source, "Interlocked.Exchange(ref _disposeStarted, 1);");
        Assert.IsFalse(source.Contains("_acceptanceRepository.DisposeAsync().AsTask().GetAwaiter().GetResult()",
            StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("public void Dispose()", StringComparison.Ordinal),
            "A synchronous compatibility method cannot safely guarantee asynchronous repository cleanup.");
    }

    [TestMethod]
    public void DriverAsyncDisposalAwaitsRepositoryBeforeCompleting()
    {
        var source = ReadDriver();
        var coreStart = source.IndexOf("private async Task DisposeCoreAsync()", StringComparison.Ordinal);
        var repositoryAwait = source.IndexOf("await _acceptanceRepository.DisposeAsync();", coreStart,
            StringComparison.Ordinal);
        var completed = source.IndexOf("GC.SuppressFinalize(this);", repositoryAwait, StringComparison.Ordinal);
        var coreEnd = source.IndexOf("private async Task ExecuteBoundButtonCommandAsync", coreStart,
            StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, coreStart);
        Assert.IsGreaterThan(coreStart, repositoryAwait);
        Assert.IsGreaterThan(repositoryAwait, completed,
            "DisposeAsync must not complete until repository asynchronous cleanup has completed.");
        Assert.IsGreaterThan(completed, coreEnd);
        Assert.IsFalse(source[coreStart..coreEnd].Contains("catch", StringComparison.Ordinal),
            "Repository disposal failure must propagate through the shared disposal task.");
    }

    private static string ReadDriver() => File.ReadAllText(Path.Combine(
        RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary",
        "AssetLibraryP3AutomatedAcceptanceDriver.cs"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pixel Tart repository root.");
    }
}
