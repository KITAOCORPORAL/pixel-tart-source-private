using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2DragDropHardeningTests
{
    [TestMethod]
    public void DragDropSourceContainsFailClosedTargetAndPayloadGates()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryDragDropBehavior.cs"));
        StringAssert.Contains(source, "IsWritableTargetShape");
        StringAssert.Contains(source, "TryReadPayload");
        StringAssert.Contains(source, "DragDropEffects.None");
        StringAssert.Contains(source, "ReportDropRejected");
        StringAssert.Contains(source, "CanDropPayload");
        StringAssert.Contains(source, "finally { StartPoints.Remove(element); }");
    }

    [TestMethod]
    public void BrowserCommandSourceValidatesArchivedAndMissingTargetsBeforeMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryBrowserCommandService.cs"));
        StringAssert.Contains(source, "includeArchived: true");
        StringAssert.Contains(source, "archived-target");
        StringAssert.Contains(source, "missing-target");
        StringAssert.Contains(source, "PreviewDropAsync(ids, target, cancellationToken)");
        StringAssert.Contains(source, "if (!preview.IsAllowed)");
    }

    [TestMethod]
    public async Task DropPreviewRejectsArchivedAndUnknownTargetsWithoutMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P2Drop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "library.db"));
            await repository.InitializeAsync();
            var path = Path.Combine(root, "asset.jpg");
            await File.WriteAllTextAsync(path, "fixture");
            await repository.ImportAsync([new AssetImportRequest(path)]);
            var asset = (await repository.QueryAsync(new AssetLibraryQuery(PageSize: 10))).Items.Single();
            var active = await repository.SaveFolderAsync(new(Guid.NewGuid(), null, "有效目标"));
            var archived = await repository.SaveFolderAsync(new(Guid.NewGuid(), null, "已归档目标", IsArchived: true));

            var assembly = typeof(AssetLibraryViewModel).Assembly;
            var targetType = assembly.GetType("PixelTart.Modules.AssetLibrary.AssetLibraryDropTarget", throwOnError: true)!;
            var kindType = assembly.GetType("PixelTart.Modules.AssetLibrary.AssetLibraryDropTargetKind", throwOnError: true)!;
            var serviceType = assembly.GetType("PixelTart.Modules.AssetLibrary.AssetLibraryBrowserCommandService", throwOnError: true)!;
            var service = Activator.CreateInstance(serviceType, repository)!;
            var previewMethod = serviceType.GetMethod("PreviewDropAsync")!;
            var executeMethod = serviceType.GetMethod("ExecuteDropAsync")!;

            var archivedKind = Enum.Parse(kindType, "Folder");
            var activeTarget = Activator.CreateInstance(targetType, archivedKind, active.FolderId, active.Name, false)!;
            var activePreview = await AwaitResult(previewMethod.Invoke(service, [new[] { asset.AssetId }, activeTarget, CancellationToken.None])!);
            Assert.IsTrue((bool)Read(activePreview, "IsAllowed")!);
            Assert.AreEqual(1, (int)Read(activePreview, "ChangeCount")!);
            var activeResult = await AwaitResult(executeMethod.Invoke(service, [new[] { asset.AssetId }, activeTarget, CancellationToken.None])!);
            Assert.AreEqual(1, (int)Read(activeResult, "ChangedCount")!);
            Assert.HasCount(1, await repository.ListFolderMembershipsAsync(folderId: active.FolderId));

            var archivedTarget = Activator.CreateInstance(targetType, archivedKind, archived.FolderId, archived.Name, true)!;
            var archivedPreview = await AwaitResult(previewMethod.Invoke(service, [new[] { asset.AssetId }, archivedTarget, CancellationToken.None])!);
            Assert.IsFalse((bool)Read(archivedPreview, "IsAllowed")!);
            Assert.AreEqual("archived-target", Read(archivedPreview, "FailureCode"));

            var unknownTarget = Activator.CreateInstance(targetType, archivedKind, Guid.NewGuid(), "不存在目标", false)!;
            var unknownPreview = await AwaitResult(previewMethod.Invoke(service, [new[] { asset.AssetId }, unknownTarget, CancellationToken.None])!);
            Assert.IsFalse((bool)Read(unknownPreview, "IsAllowed")!);
            Assert.AreEqual("missing-target", Read(unknownPreview, "FailureCode"));

            var executeResult = await AwaitResult(executeMethod.Invoke(service, [new[] { asset.AssetId }, archivedTarget, CancellationToken.None])!);
            Assert.AreEqual(0, (int)Read(executeResult, "ChangedCount")!);
            Assert.IsEmpty(await repository.ListFolderMembershipsAsync(folderId: archived.FolderId));
            await repository.DisposeAsync();
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task VisibleUndoRedoStateRestoresFromDurableJournalAcrossServiceRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3UndoRestart", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "library.db"));
            await repository.InitializeAsync();
            var path = Path.Combine(root, "asset.jpg");
            await File.WriteAllTextAsync(path, "fixture");
            await repository.ImportAsync([new AssetImportRequest(path)]);
            var asset = (await repository.QueryAsync(new AssetLibraryQuery(PageSize: 10))).Items.Single();

            var serviceType = typeof(AssetLibraryViewModel).Assembly.GetType(
                "PixelTart.Modules.AssetLibrary.AssetLibraryBrowserCommandService", throwOnError: true)!;
            var first = Activator.CreateInstance(serviceType, repository)!;
            await AwaitResult(serviceType.GetMethod("RateAsync")!.Invoke(
                first, [new[] { asset.AssetId }, 4, CancellationToken.None])!);

            var restarted = Activator.CreateInstance(serviceType, repository)!;
            await (Task)serviceType.GetMethod("RestoreFromJournalAsync")!.Invoke(
                restarted, [CancellationToken.None])!;
            Assert.IsTrue((bool)serviceType.GetProperty("CanUndo")!.GetValue(restarted)!);
            Assert.IsFalse((bool)serviceType.GetProperty("CanRedo")!.GetValue(restarted)!);
            Assert.IsTrue((bool)await AwaitResult(serviceType.GetMethod("UndoAsync")!.Invoke(
                restarted, [CancellationToken.None])!));

            var restartedAfterUndo = Activator.CreateInstance(serviceType, repository)!;
            await (Task)serviceType.GetMethod("RestoreFromJournalAsync")!.Invoke(
                restartedAfterUndo, [CancellationToken.None])!;
            Assert.IsFalse((bool)serviceType.GetProperty("CanUndo")!.GetValue(restartedAfterUndo)!);
            Assert.IsTrue((bool)serviceType.GetProperty("CanRedo")!.GetValue(restartedAfterUndo)!);
            Assert.IsTrue((bool)await AwaitResult(serviceType.GetMethod("RedoAsync")!.Invoke(
                restartedAfterUndo, [CancellationToken.None])!));
            Assert.AreEqual(4, (await repository.QueryAsync(new AssetLibraryQuery(PageSize: 10))).Items.Single().Rating);
            await repository.DisposeAsync();
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task ExternalMetadataResultsJoinTheSameLifoUndoServiceAcrossRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3UnifiedUndo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "library.db"));
            await repository.InitializeAsync();
            var path = Path.Combine(root, "asset.jpg");
            await File.WriteAllTextAsync(path, "fixture");
            await repository.ImportAsync([new AssetImportRequest(path)]);
            var asset = (await repository.QueryAsync(new AssetLibraryQuery(PageSize: 10))).Items.Single();
            var serviceType = typeof(AssetLibraryViewModel).Assembly.GetType(
                "PixelTart.Modules.AssetLibrary.AssetLibraryBrowserCommandService", throwOnError: true)!;
            var service = Activator.CreateInstance(serviceType, repository)!;
            await AwaitResult(serviceType.GetMethod("RateAsync")!.Invoke(
                service, [new[] { asset.AssetId }, 4, CancellationToken.None])!);

            var external = await repository.SetAssetsMissingAsync([asset.AssetId], true);
            serviceType.GetMethod("RememberExternalResult", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(service, [external]);
            Assert.IsTrue((bool)serviceType.GetProperty("CanUndo")!.GetValue(service)!);

            var restarted = Activator.CreateInstance(serviceType, repository)!;
            await (Task)serviceType.GetMethod("RestoreFromJournalAsync")!.Invoke(
                restarted, [CancellationToken.None])!;
            Assert.IsTrue((bool)await AwaitResult(serviceType.GetMethod("UndoAsync")!.Invoke(
                restarted, [CancellationToken.None])!));
            var afterMissingUndo = await repository.GetAssetAsync(asset.AssetId);
            Assert.IsNotNull(afterMissingUndo);
            Assert.IsFalse(afterMissingUndo.IsMissing);
            Assert.AreEqual(4, afterMissingUndo.Rating);
            Assert.IsTrue((bool)serviceType.GetProperty("CanUndo")!.GetValue(restarted)!,
                "Undoing the newest mixed command must expose the next durable operation.");

            Assert.IsTrue((bool)await AwaitResult(serviceType.GetMethod("UndoAsync")!.Invoke(
                restarted, [CancellationToken.None])!));
            Assert.AreEqual(0, (await repository.GetAssetAsync(asset.AssetId))!.Rating);
            Assert.IsTrue((bool)serviceType.GetProperty("CanRedo")!.GetValue(restarted)!);
            await repository.DisposeAsync();
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task MissingThumbnailPublishesVisibleFailureStateAndClearsOnReset()
    {
        await RunSta(() =>
        {
            var image = new Image();
            AsyncThumbnail.SetDecodeWidth(image, 120);
            AsyncThumbnail.SetSourcePath(image, Path.Combine(Path.GetTempPath(), "PixelTart-missing", Guid.NewGuid().ToString("N"), "missing.png"));
            Assert.IsTrue(AsyncThumbnail.GetHasFailure(image));
            StringAssert.Contains(AsyncThumbnail.GetFailureMessage(image) ?? string.Empty, "文件不存在");

            AsyncThumbnail.SetSourcePath(image, null);
            Assert.IsFalse(AsyncThumbnail.GetHasFailure(image));
            Assert.IsNull(AsyncThumbnail.GetFailureMessage(image));
        });
    }

    [TestMethod]
    public void ThumbnailFailureOverlayIsDeclaredForAllP2Views()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        StringAssert.Contains(xaml, "AsyncThumbnail.HasFailure");
        StringAssert.Contains(xaml, "AssetThumbnailFailure");
        StringAssert.Contains(xaml, "SelectedAssetThumbnailFailure");
        Assert.IsGreaterThanOrEqualTo(4, CountOccurrences(xaml, "AsyncThumbnail.HasFailure"));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var offset = 0; (offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0; offset += value.Length)
            count++;
        return count;
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static async Task<object> AwaitResult(object taskObject)
    {
        var task = (Task)taskObject;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static object? Read(object value, string propertyName) => value.GetType().GetProperty(propertyName)!.GetValue(value);
}
