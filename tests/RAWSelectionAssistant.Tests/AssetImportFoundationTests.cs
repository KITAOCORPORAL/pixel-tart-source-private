using System.Buffers.Binary;
using System.Text;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetImportFoundationTests
{
    [TestMethod]
    public async Task MixedImportReportsUnsupportedMissingAndMetadataFailureWithoutRejectingIndexableAsset()
    {
        using var temp = new TempDirectory();
        var png = temp.CreateFile("正常 图片.png", MinimalPng(41, 29));
        var corrupt = temp.CreateFile("损坏.jpg", [1, 2, 3]);
        var unsupported = temp.CreateFile("说明.xyz", [4, 5]);
        var missing = Path.Combine(temp.Path, "缺失.jpg");
        await using var repository = new SqliteAssetLibraryRepository(Path.Combine(temp.Path, "library.db"));

        var result = await repository.ImportAsync(new[] { png, corrupt, unsupported, missing }.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));

        Assert.AreEqual(2, result.ImportedCount);
        Assert.AreEqual(1, result.UnsupportedCount);
        Assert.AreEqual(1, result.MissingCount);
        Assert.AreEqual(1, result.MetadataFailedCount);
        Assert.HasCount(3, result.Issues);
        var assets = (await repository.QueryAsync(new(PageSize: 10))).Items;
        Assert.HasCount(2, assets);
        Assert.AreEqual(41, assets.Single(asset => asset.SourcePath == png).Width);
        Assert.AreEqual(AssetMetadataStatus.Failed, assets.Single(asset => asset.SourcePath == corrupt).MetadataStatus);
    }

    [TestMethod]
    public async Task PerFileIoFailureIsReportedAndDoesNotAbortRemainingImports()
    {
        using var temp = new TempDirectory();
        var first = temp.CreateFile("first.png", MinimalPng(11, 7));
        var failing = temp.CreateFile("locked.png", MinimalPng(13, 9));
        var last = temp.CreateFile("last.png", MinimalPng(17, 5));
        await using var repository = new SqliteAssetLibraryRepository(
            new AssetLibraryDatabase(Path.Combine(temp.Path, "library.db")),
            metadataExtractor: new IoFailingExtractor(failing));

        var result = await repository.ImportAsync(
            new[] { first, failing, last }.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));

        Assert.AreEqual(2, result.ImportedCount);
        Assert.AreEqual(1, result.FailedCount);
        Assert.IsTrue(result.Issues.Any(issue => issue.FileName == "locked.png" && issue.ErrorCode == ErrorCodeCatalog.FileLocked));
        var assets = (await repository.QueryAsync(new(PageSize: 10))).Items;
        CollectionAssert.AreEquivalent(new[] { first, last }, assets.Select(asset => asset.SourcePath).ToArray());
    }

    [TestMethod]
    public async Task ReferenceAndManagedImportsPreserveSourceAndManagedCancellationLeavesNoOrphan()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source.png", MinimalPng(13, 7));
        var before = File.ReadAllBytes(source);
        var managed = Path.Combine(temp.Path, "managed");
        await using var repository = new SqliteAssetLibraryRepository(Path.Combine(temp.Path, "library.db"));

        var reference = await repository.ImportAsync([new(source, ComputeContentHash: true)]);
        Assert.AreEqual(1, reference.ImportedCount);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(source));

        var copy = await repository.ImportAsync([new(source, AssetImportMode.ManagedCopy, managed, true, AssetDuplicateBehavior.ImportIndependentRecord)]);
        Assert.AreEqual(1, copy.ImportedCount);
        Assert.HasCount(1, Directory.GetFiles(managed));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(source));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await repository.ImportAsync([new(source, AssetImportMode.ManagedCopy, Path.Combine(temp.Path, "cancelled"), true)], cancellation.Token);
        Assert.IsTrue(cancelled.Cancelled);
        Assert.IsFalse(Directory.Exists(Path.Combine(temp.Path, "cancelled")) && Directory.EnumerateFiles(Path.Combine(temp.Path, "cancelled")).Any());
    }

    [TestMethod]
    public async Task MetadataBackfillOnlyUpdatesMissingMetadataAndPersistsAcrossRestart()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("backfill.png", MinimalPng(88, 55));
        var database = Path.Combine(temp.Path, "library.db");
        await using (var repository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(database), metadataExtractor: new FailedExtractor()))
        {
            await repository.ImportAsync([new(source, ComputeContentHash: true)]);
        }
        await using (var repository = new SqliteAssetLibraryRepository(database))
        {
            var result = await repository.BackfillTechnicalMetadataAsync();
            Assert.AreEqual(1, result.Updated);
            var asset = (await repository.QueryAsync(new(PageSize: 10))).Items.Single();
            Assert.AreEqual(88, asset.Width);
            Assert.AreEqual(55, asset.Height);
        }
        await using (var restarted = new SqliteAssetLibraryRepository(database))
        {
            var asset = (await restarted.QueryAsync(new(PageSize: 10))).Items.Single();
            Assert.AreEqual(88, asset.Width);
            Assert.AreEqual(AssetMetadataStatus.Complete, asset.MetadataStatus);
        }
    }

    [TestMethod]
    public async Task ConcurrentImportsStayBoundToTheirOriginalLibraryDatabases()
    {
        using var temp = new TempDirectory();
        var sourceA = temp.CreateFile("sources/A.png", MinimalPng(101, 51));
        var sourceB = temp.CreateFile("sources/B.png", MinimalPng(202, 102));
        var databaseA = Path.Combine(temp.Path, "A", "library.db");
        var databaseB = Path.Combine(temp.Path, "B", "library.db");
        await using var repositoryA = new SqliteAssetLibraryRepository(databaseA);
        await using var repositoryB = new SqliteAssetLibraryRepository(databaseB);

        await Task.WhenAll(
            repositoryA.ImportAsync([new(sourceA, ComputeContentHash: true)]),
            repositoryB.ImportAsync([new(sourceB, ComputeContentHash: true)]));

        var assetsA = (await repositoryA.QueryAsync(new(PageSize: 10))).Items;
        var assetsB = (await repositoryB.QueryAsync(new(PageSize: 10))).Items;
        Assert.AreEqual(Path.GetFullPath(sourceA), assetsA.Single().SourcePath);
        Assert.AreEqual(Path.GetFullPath(sourceB), assetsB.Single().SourcePath);
        Assert.AreEqual(101, assetsA.Single().Width);
        Assert.AreEqual(202, assetsB.Single().Width);
    }

    [TestMethod]
    public async Task V7DatabaseMigratesToV8WithValidatedExternalBackup()
    {
        using var temp = new TempDirectory();
        var containers = new AssetLibraryContainerService();
        var descriptor = await containers.CreateAsync(Path.Combine(temp.Path, "迁移.ptlibrary"), "迁移库");
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={descriptor.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM AssetLibrarySchemaInfo WHERE Version=8;";
            await command.ExecuteNonQueryAsync();
        }
        Assert.IsFalse(File.Exists(descriptor.DatabasePath + ".schema-v7-backup.sqlite"));
        await using var repository = new SqliteAssetLibraryRepository(descriptor.DatabasePath);
        await repository.InitializeAsync();
        Assert.IsTrue(File.Exists(descriptor.DatabasePath + ".schema-v7-backup.sqlite"));
    }

    [TestMethod]
    public async Task HundredsOfMixedAssetsRemainConsistentAcrossCancellationAndRestart()
    {
        using var temp = new TempDirectory();
        var sources = Enumerable.Range(0, 300)
            .Select(index => temp.CreateFile($"bulk/素材-{index:000}.png", MinimalPng(20 + index % 31, 15 + index % 17)))
            .ToArray();
        var database = Path.Combine(temp.Path, "bulk-library.db");
        await using (var repository = new SqliteAssetLibraryRepository(database))
        {
            using var cancellation = new CancellationTokenSource();
            var progress = new CancelAfterProgress(cancellation, 120);
            var result = await repository.ImportAsync(sources.Select(path => new AssetImportRequest(path, ComputeContentHash: true)), cancellation.Token, progress);
            Assert.IsTrue(result.Cancelled);
            Assert.IsGreaterThanOrEqualTo(100, result.ImportedCount);
            Assert.IsLessThan(300, result.ImportedCount);
        }
        await using (var restarted = new SqliteAssetLibraryRepository(database))
        {
            var firstPage = await restarted.QueryAsync(new(PageSize: 500));
            Assert.IsGreaterThanOrEqualTo(100, firstPage.TotalCount);
            Assert.IsTrue(firstPage.Items.All(item => item.Width is > 0 && item.Height is > 0));
        }
    }

    private sealed class FailedExtractor : IAssetMetadataExtractor
    {
        public Task<AssetTechnicalMetadata> ExtractAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssetTechnicalMetadata(null, null, null, null, AssetMetadataStatus.Failed, [], "test", "injected"));
    }

    private sealed class IoFailingExtractor(string failingPath) : IAssetMetadataExtractor
    {
        public Task<AssetTechnicalMetadata> ExtractAsync(string path, CancellationToken cancellationToken = default) =>
            string.Equals(path, failingPath, StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<AssetTechnicalMetadata>(new IOException("injected per-file read failure"))
                : new MetadataExtractorAssetMetadataExtractor().ExtractAsync(path, cancellationToken);
    }

    private sealed class CancelAfterProgress(CancellationTokenSource source, int threshold) : IProgress<int>
    {
        public void Report(int value)
        {
            if (value >= threshold) source.Cancel();
        }
    }

    private static byte[] MinimalPng(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; header[9] = 2;
        WriteChunk(stream, "IHDR", header); WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> integer = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(integer, data.Length); stream.Write(integer);
        var typeBytes = Encoding.ASCII.GetBytes(type); stream.Write(typeBytes); stream.Write(data); BinaryPrimitives.WriteUInt32BigEndian(integer, 0); stream.Write(integer);
    }
}
