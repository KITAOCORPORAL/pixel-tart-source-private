using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

internal static class StageIIISta
{
    public static Task Run(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var operation = action();
                _ = operation.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                Dispatcher.Run(); operation.GetAwaiter().GetResult(); completion.SetResult();
            }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
[TestClass]
public sealed class PublishingNinePositionPickerTests
{
    [TestMethod]
    public Task AllNineCellsUpdateTheExistingLayerBinding() => StageIIISta.Run(() =>
    {
        var layer = new PublishingWatermarkLayerViewModel(); var picker = new WatermarkPositionPicker { DataContext = layer };
        picker.SetBinding(WatermarkPositionPicker.PositionProperty, new Binding(nameof(layer.Position)) { Mode = BindingMode.TwoWay });
        Assert.AreEqual(3, picker.Rows); Assert.AreEqual(3, picker.Columns); Assert.HasCount(9, picker.Children);
        var positions = new HashSet<WatermarkPosition>();
        foreach (Button button in picker.Children)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); positions.Add(layer.Position);
            Assert.AreEqual("●", button.Content); Assert.IsNotNull(picker.GetBindingExpression(WatermarkPositionPicker.PositionProperty));
        }
        Assert.HasCount(9, positions); return Task.CompletedTask;
    });
}
[TestClass]
public sealed class DuplicateImportDecisionTests
{
    [TestMethod]
    public Task ExactImportOffersChoicesAndPreservesSource() => StageIIISta.Run(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-StageIII-Import", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "photo.jpg"); await File.WriteAllBytesAsync(source, [1,2,3,4,5]);
            var before = SHA256.HashData(await File.ReadAllBytesAsync(source));
            var database = Path.Combine(root, "library.db"); var repository = new SqliteAssetLibraryRepository(database);
            await repository.ImportAsync([new(source, ComputeContentHash: true)]);
            await using var vm = new AssetLibraryViewModel(database, new TaskOperationBridge(), productDatabasePath: Path.Combine(root,"product.db"), inspirationTrayDatabasePath: Path.Combine(root,"tray.db"));
            var prompts = 0;
            vm.DuplicateImportDecision = (prompt, _) => { prompts++; Assert.AreEqual(source, prompt.SourcePath); return Task.FromResult(DuplicateImportChoice.Skip); };
            await vm.ImportDroppedFilesAsync([source]); Assert.AreEqual(1, prompts); Assert.AreEqual(1L, (await repository.QueryAsync(new())).TotalCount);
            vm.DuplicateImportDecision = (_, _) => Task.FromResult(DuplicateImportChoice.ImportAnyway);
            await vm.ImportDroppedFilesAsync([source]); Assert.AreEqual(2L, (await repository.QueryAsync(new())).TotalCount);
            vm.DuplicateImportDecision = (_, _) => throw new AssertFailedException("Different content must not be blocked.");
            var different = Path.Combine(root, "different.jpg"); await File.WriteAllBytesAsync(different, [9,8,7]);
            await vm.ImportDroppedFilesAsync([different]); Assert.AreEqual(3L, (await repository.QueryAsync(new())).TotalCount);
            CollectionAssert.AreEqual(before, SHA256.HashData(await File.ReadAllBytesAsync(source)));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    });
}
