using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryWorkflowLinkTests
{
    [TestMethod]
    public async Task ProjectBookingAndWorkflowLinksSupportManyToManyAndReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-WorkflowLinks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var database = Path.Combine(root, "library.db"); var source = Path.Combine(root, "source.jpg"); await File.WriteAllBytesAsync(source, [1]);
        var projects = new[] { Guid.NewGuid(), Guid.NewGuid() }; var bookings = new[] { Guid.NewGuid(), Guid.NewGuid() }; Guid assetId;
        try
        {
            await using (var repository = new SqliteAssetLibraryRepository(database))
            {
                await repository.InitializeAsync(); await repository.ImportAsync([new AssetImportRequest(source)]); assetId = (await repository.QueryAsync(new())).Items.Single().AssetId;
                foreach (var project in projects) await repository.SaveProjectAssetLinkAsync(new(project, assetId, "Original", DateTimeOffset.UtcNow));
                foreach (var booking in bookings) await repository.SaveBookingAssetLinkAsync(new(booking, assetId, DateTimeOffset.UtcNow));
                await repository.SaveAssetWorkflowMetadataAsync(new(assetId, "CameraImport", AssetWorkflowStatus.PendingRetouch));
            }
            await using var reopened = new SqliteAssetLibraryRepository(database); await reopened.InitializeAsync();
            Assert.HasCount(2, await reopened.ListProjectAssetLinksAsync(assetId: assetId));
            Assert.HasCount(2, await reopened.ListBookingAssetLinksAsync(assetId: assetId));
            var metadata = await reopened.GetAssetWorkflowMetadataAsync(assetId);
            Assert.AreEqual("CameraImport", metadata!.AssetOrigin); Assert.AreEqual(AssetWorkflowStatus.PendingRetouch, metadata.WorkflowStatus);
            Assert.AreEqual(1, await reopened.RemoveProjectAssetLinkAsync(projects[0], assetId));
            Assert.AreEqual(1, await reopened.RemoveBookingAssetLinkAsync(bookings[0], assetId));
            Assert.HasCount(1, await reopened.ListProjectAssetLinksAsync(assetId: assetId));
            Assert.HasCount(1, await reopened.ListBookingAssetLinksAsync(assetId: assetId));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
