using System.IO;
using System.Windows;
using System.Windows.Controls;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CanvasContextMenuContractTests
{
    [TestMethod]
    public Task ContextCopyUsesClipboardAndDuplicateIsExplicit() => AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async () =>
    {
        var editor = CanvasFixtures.Create();
        var view = new FreeCanvasView(editor, new WpfAssetThumbnailProvider(),
            new CanvasDocumentStore(Path.Combine(Path.GetTempPath(), "CanvasMenu-" + Guid.NewGuid())));
        var menu = await view.CreateSelectionMenuAsync();
        var items = menu.Items.OfType<MenuItem>().ToArray();
        Assert.IsFalse(items.Any(item => (string?)item.Header is "查看大图" or "在素材库中显示" or "灵感板"));
        var copy = items.Single(item => (string?)item.Header == "复制");
        copy.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.HasCount(1, editor.Document.Objects);
        Assert.IsTrue(editor.CanPaste);
        Assert.IsFalse(items.Single(item => (string?)item.Header == "组合").IsEnabled);
        items.Single(item => (string?)item.Header == "创建副本").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.HasCount(2, editor.Document.Objects);
        editor.Undo(); Assert.HasCount(1, editor.Document.Objects);
        Assert.IsGreaterThanOrEqualTo(6, items.Single(item => (string?)item.Header == "变换").Items.Count);
        Assert.IsGreaterThanOrEqualTo(4, items.Single(item => (string?)item.Header == "排列").Items.Count);
    });
}
