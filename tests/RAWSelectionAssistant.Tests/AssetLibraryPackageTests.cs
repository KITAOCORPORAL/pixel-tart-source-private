using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryPackageTests
{
    [TestMethod]
    public async Task ExportImport_MixedLibrarySurvivesSourceGoingOffline()
    {
        using var temp = new TempDirectory();
        var service = new AssetLibraryContainerService();
        var source = await service.CreateAsync(temp.Combine("A", "混合 库.ptlibrary"), "混合 库");
        var external = temp.Combine("A", "external", "reference.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(external)!);
        await File.WriteAllBytesAsync(external, [1, 2, 3, 4, 5]);
        await using (var repository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(source.DatabasePath)))
        {
            await repository.InitializeAsync();
            await repository.ImportAsync([new AssetImportRequest(external, ComputeContentHash: true)]);
            await repository.ImportAsync([new AssetImportRequest(external, AssetImportMode.ManagedCopy, source.ManagedAssetsPath, true, AssetDuplicateBehavior.ImportIndependentRecord)]);
        }
        var sourceHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(external)));
        var package = await new AssetLibraryPackageService().ExportAsync(source, temp.Combine("transfer.ptpack"), AssetLibraryPackageScope.CollectAvailableReferences, includePreviews: true);
        Directory.Move(temp.Combine("A"), temp.Combine("A-offline"));

        var imported = await new AssetLibraryPackageService().ImportAsync(package.PackagePath, temp.Combine("B", "导入库.ptlibrary"), "导入库");
        await using var target = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(imported.DatabasePath));
        await target.InitializeAsync();
        var page = await target.QueryAsync(new AssetLibraryQuery(PageSize: 10));

        Assert.AreEqual(2, page.TotalCount);
        Assert.IsTrue(page.Items.All(item => item.ImportMode == AssetImportMode.ManagedCopy));
        Assert.IsTrue(page.Items.All(item => File.Exists(item.ManagedCopyPath)));
        Assert.IsTrue(page.Items.All(item => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.ManagedCopyPath!))) == sourceHash));
        Assert.AreEqual(2, package.IncludedOriginalCount);
    }

    [TestMethod]
    public async Task Inspect_RejectsTraversalFutureVersionAndHashMismatch()
    {
        using var temp = new TempDirectory();
        var traversal = temp.Combine("traversal.ptpack");
        using (var archive = ZipFile.Open(traversal, ZipArchiveMode.Create)) archive.CreateEntry("../escape.txt");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new AssetLibraryPackageService().InspectAsync(traversal));
        Assert.IsFalse(File.Exists(temp.Combine("escape.txt")));

        var container = await new AssetLibraryContainerService().CreateAsync(temp.Combine("safe.ptlibrary"), "safe");
        var exported = await new AssetLibraryPackageService().ExportAsync(container, temp.Combine("safe.ptpack"), AssetLibraryPackageScope.DatabaseOnly);
        var corrupt = temp.Combine("corrupt.ptpack");
        File.Copy(exported.PackagePath, corrupt);
        using (var archive = ZipFile.Open(corrupt, ZipArchiveMode.Update))
        {
            var db = archive.GetEntry("payload/database/asset-library-v16.db")!;
            db.Delete();
            await using var output = archive.CreateEntry("payload/database/asset-library-v16.db").Open();
            await output.WriteAsync(new byte[] { 9, 8, 7 });
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new AssetLibraryPackageService().InspectAsync(corrupt));

        var future = temp.Combine("future.ptpack");
        File.Copy(exported.PackagePath, future);
        using (var archive = ZipFile.Open(future, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("package.manifest.json")!;
            string json;
            using (var reader = new StreamReader(entry.Open())) json = await reader.ReadToEndAsync();
            entry.Delete();
            await using var output = archive.CreateEntry("package.manifest.json").Open();
            await using var writer = new StreamWriter(output);
            await writer.WriteAsync(json.Replace("\"package_format_version\": 1", "\"package_format_version\": 99", StringComparison.Ordinal));
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new AssetLibraryPackageService().InspectAsync(future));
        // The normal package remains valid; malformed variants are rejected without publishing an import target.
        var inspected = await new AssetLibraryPackageService().InspectAsync(exported.PackagePath);
        Assert.AreEqual(AssetLibraryPackageScope.DatabaseOnly, inspected.Scope);
    }

    [TestMethod]
    public async Task Export_ExistingNameGetsSafeSuffixAndDoesNotOverwrite()
    {
        using var temp = new TempDirectory();
        var source = await new AssetLibraryContainerService().CreateAsync(temp.Combine("library.ptlibrary"), "library");
        var requested = temp.Combine("portable.ptpack");
        await File.WriteAllTextAsync(requested, "keep");

        var result = await new AssetLibraryPackageService().ExportAsync(source, requested, AssetLibraryPackageScope.DatabaseOnly);

        Assert.AreEqual("keep", await File.ReadAllTextAsync(requested));
        Assert.AreEqual(temp.Combine("portable (2).ptpack"), result.PackagePath);
        Assert.IsFalse(Directory.EnumerateFiles(temp.Path, "*.partial-*").Any());
    }
}
