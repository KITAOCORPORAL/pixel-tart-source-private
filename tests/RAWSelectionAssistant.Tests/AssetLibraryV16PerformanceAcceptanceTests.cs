using System.Diagnostics;
using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AssetLibraryV16PerformanceAcceptanceTests
{
    [TestMethod]
    [DataRow(100)]
    [DataRow(1000)]
    public async Task GeneratedJpegCorpusCompletesDecodeAnalysisCacheDatabaseAndInspector(int imageCount)
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The production WPF decoder acceptance is Windows-only.");
        await using var setup = await AcceptanceSetup.CreateAsync(imageCount);
        var result = await setup.RunAsync("pipeline", imageCount);
        Assert.AreEqual("pixel-tart-asset-library-v16-pipeline/v1", result.RootElement.GetProperty("schema").GetString());
        Assert.AreEqual(imageCount, result.RootElement.GetProperty("requestedCount").GetInt32());
        Assert.AreEqual(imageCount, result.RootElement.GetProperty("importedCount").GetInt32());

        var cold = result.RootElement.GetProperty("cold");
        Assert.AreEqual(0, cold.GetProperty("cacheHits").GetInt32());
        Assert.AreEqual(imageCount, cold.GetProperty("cacheMisses").GetInt32());
        Assert.AreEqual(imageCount, cold.GetProperty("inspectorValidCount").GetInt32());
        Assert.IsGreaterThan(0, cold.GetProperty("decodeMilliseconds").GetDouble());
        Assert.IsGreaterThan(0, cold.GetProperty("analysisCacheSqliteMilliseconds").GetDouble());
        Assert.IsGreaterThan(0, cold.GetProperty("inspectorMilliseconds").GetDouble());

        var warm = result.RootElement.GetProperty("warm");
        Assert.AreEqual(imageCount, warm.GetProperty("cacheHits").GetInt32());
        Assert.AreEqual(0, warm.GetProperty("cacheMisses").GetInt32());
        Assert.AreEqual(imageCount, warm.GetProperty("inspectorValidCount").GetInt32());
        Assert.IsGreaterThan(0, warm.GetProperty("decodeMilliseconds").GetDouble());
        Assert.IsGreaterThan(0, warm.GetProperty("analysisCacheSqliteMilliseconds").GetDouble());
        Assert.IsGreaterThan(0, warm.GetProperty("inspectorMilliseconds").GetDouble());

        var database = result.RootElement.GetProperty("database");
        Assert.AreEqual(imageCount, database.GetProperty("assetItems").GetInt32());
        Assert.AreEqual(imageCount, database.GetProperty("analysisCacheRows").GetInt32());
        Assert.AreEqual(imageCount, database.GetProperty("visualFeatureRows").GetInt32());
        Assert.IsGreaterThanOrEqualTo(imageCount, database.GetProperty("paletteRows").GetInt32());
        Assert.AreEqual(imageCount, result.RootElement.GetProperty("reopenedInspectorValidCount").GetInt32());
        Assert.IsTrue(result.RootElement.GetProperty("finalInspectorPublished").GetBoolean());
        Assert.AreEqual(
            result.RootElement.GetProperty("expectedFinalAssetId").GetGuid(),
            result.RootElement.GetProperty("finalInspectorAssetId").GetGuid());
        Assert.IsFalse(result.RootElement.GetProperty("colorManagementReferenceVerified").GetBoolean());
        Assert.IsFalse(result.RootElement.GetProperty("rawVisualProxyVerified").GetBoolean());
    }

    [TestMethod]
    public async Task RapidSelectionSwitchCancelsRealDecodesAndPublishesOnlyLatestInspectorResult()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The production WPF decoder acceptance is Windows-only.");
        await using var setup = await AcceptanceSetup.CreateAsync(3);
        var result = await setup.RunAsync("cancellation", 3);
        Assert.AreEqual("pixel-tart-asset-library-v16-cancellation/v1", result.RootElement.GetProperty("schema").GetString());
        Assert.IsGreaterThanOrEqualTo(1, result.RootElement.GetProperty("aDecoderCalls").GetInt32());
        Assert.IsTrue(result.RootElement.GetProperty("aDecoderStartedUncancelled").GetBoolean());
        Assert.IsTrue(result.RootElement.GetProperty("aDecoderCancelled").GetBoolean());
        Assert.IsGreaterThanOrEqualTo(1, result.RootElement.GetProperty("bDecoderCalls").GetInt32());
        Assert.IsTrue(result.RootElement.GetProperty("bDecoderStartedUncancelled").GetBoolean());
        Assert.IsTrue(result.RootElement.GetProperty("bDecoderCancelled").GetBoolean());
        Assert.IsFalse(result.RootElement.GetProperty("aPublished").GetBoolean());
        Assert.IsFalse(result.RootElement.GetProperty("bPublished").GetBoolean());
        Assert.IsTrue(result.RootElement.GetProperty("cPublished").GetBoolean());
        var published = result.RootElement.GetProperty("publishedAssetIds").EnumerateArray().Select(item => item.GetGuid()).ToArray();
        Assert.HasCount(1, published);
        Assert.AreEqual(result.RootElement.GetProperty("expectedPublishedAssetId").GetGuid(), published[0]);
        Assert.AreEqual("Valid", result.RootElement.GetProperty("cInspectorState").GetString());
    }

    [TestMethod]
    [Ignore("No certified non-sRGB generated ICC reference fixture exists. Embedded-profile detection without an independent converted-RGB oracle is insufficient.")]
    public void NonSrgbIccReferenceConvertsWithinIndependentTolerance()
    {
    }

    [TestMethod]
    [Ignore("No reliable program-generated RAW or DNG embedded-preview fixture exists, and the current Preview decoder rejects RAW. Do not fabricate this result.")]
    public void RawEmbeddedPreviewUsesPreviewFingerprintWithoutDemosaicOrSourceWrite()
    {
    }

    private sealed class AcceptanceSetup : IAsyncDisposable
    {
        private readonly string _root;
        private readonly string _fixtureRoot;
        private readonly string _databasePath;
        private int _run;

        private AcceptanceSetup(string root, string fixtureRoot, string databasePath)
        {
            _root = root;
            _fixtureRoot = fixtureRoot;
            _databasePath = databasePath;
        }

        public static async Task<AcceptanceSetup> CreateAsync(int fixtureCount)
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart_AssetLibrary_V16_Acceptance", "Performance", Guid.NewGuid().ToString("N"));
            var fixtureRoot = Path.Combine(root, "fixtures");
            var databasePath = Path.Combine(root, "data", "asset-library.db");
            var generator = Path.Combine(RepositoryRoot(), "tools", "AssetLibraryV16Acceptance", "New-AssetLibraryV16Fixtures.ps1");
            var generated = await RunProcessAsync(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", generator, "-OutputRoot", fixtureRoot, "-PerformanceCount", fixtureCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "-PerformanceWidth", "192", "-PerformanceHeight", "128"],
                RepositoryRoot());
            Assert.AreEqual(0, generated.ExitCode, generated.StandardError);
            return new(root, fixtureRoot, databasePath);
        }

        public async Task<JsonDocument> RunAsync(string mode, int count)
        {
            var resultPath = Path.Combine(_root, "results", $"{++_run:D2}-{mode}-{count}.json");
            var project = Path.Combine(RepositoryRoot(), "tools", "AssetLibraryV16Acceptance", "PixelTart.AssetLibrary.V16.AcceptanceRunner.csproj");
            var dotnet = ResolveDotnetHost();
            var execution = await RunProcessAsync(
                dotnet,
                ["run", "--project", project, "-c", "Debug", "--no-restore", "--", mode, "--fixture-root", _fixtureRoot, "--database", _databasePath, "--result", resultPath, "--count", count.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                RepositoryRoot());
            Assert.AreEqual(0, execution.ExitCode, execution.StandardError + Environment.NewLine + execution.StandardOutput);
            Assert.IsTrue(File.Exists(resultPath));
            return JsonDocument.Parse(await File.ReadAllBytesAsync(resultPath));
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Unable to start {fileName}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await standardOutput, await standardError);
    }

    private static string ResolveDotnetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var workspaceHost = Path.Combine(RepositoryRoot(), "..", "..", ".dotnet", "dotnet.exe");
        if (File.Exists(workspaceHost)) return Path.GetFullPath(workspaceHost);
        return "dotnet";
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
