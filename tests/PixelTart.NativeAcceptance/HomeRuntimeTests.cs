using System.Windows.Automation;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace PixelTart.NativeAcceptance;

[TestClass]
public sealed class HomeRuntimeTests
{
    [TestMethod]
    public async Task RealReleaseAppStartsAndOpensLibrary()
    {
        if (Environment.GetEnvironmentVariable("PIXEL_TART_HOME_RUNTIME") != "1")
            Assert.Inconclusive("Opt in with PIXEL_TART_HOME_RUNTIME=1 to launch the isolated production EXE.");
        var repository = FindRepository();
        using var host = await PixelTartProcessHost.StartAsync(repository, CancellationToken.None, homeValidation: true, prepareRuntime: async runtime =>
        {
            var library = await new AssetLibraryContainerService().CreateAsync(Path.Combine(runtime, "Home.ptlibrary"), "Home runtime validation");
            var settings = new AppSettings();
            settings.AssetLibraryPortable.CurrentContainerPath = library.ContainerPath;
            await File.WriteAllTextAsync(Path.Combine(runtime, "settings.json"), JsonSerializer.Serialize(settings));
        });
        var output = Path.Combine(repository, "artifacts/runtime-validation/2026-09-30-home");
        Directory.CreateDirectory(output);
        var root = AutomationElement.FromHandle(host.Hwnd);
        var capture = new NativeWindowCapture(host);
        Win32.ShowWindow(host.Hwnd, 3);
        Win32.SetForegroundWindow(host.Hwnd);
        await Task.Delay(1500);
        Dump(root, Path.Combine(output, "startup-uia.json"));
        File.Copy(capture.Capture("01_app_start").Path, Path.Combine(output, "01_app_start.png"), true);
        var tutorialExit = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "TutorialExitButton"));
        if (tutorialExit is not null && tutorialExit.Current.IsEnabled)
        {
            ((InvokePattern)tutorialExit.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            await Task.Delay(500);
        }
        var navigation = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetLibraryNavigationButton"));
        Assert.IsNotNull(navigation);
        Assert.AreEqual(host.Pid, navigation.Current.ProcessId);
        ((InvokePattern)navigation.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(2500);
        File.Copy(capture.Capture("01_app_start_ready").Path, Path.Combine(output, "01_app_start.png"), true);
        Dump(root, Path.Combine(output, "library-uia.json"));
        File.Copy(capture.Capture("14_toolbar_library").Path, Path.Combine(output, "14_toolbar_library.png"), true);
        var import = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetLibraryImport"));
        Assert.IsNotNull(import, "The production library page must be loaded.");
        Assert.IsTrue(import.Current.IsEnabled);
        var fixtures = Path.Combine(host.RuntimeRoot, "import-fixtures");
        Directory.CreateDirectory(fixtures);
        foreach (var extension in new[] { "jpg", "png", "tiff" }) WritePhoto(Path.Combine(fixtures, "home." + extension), extension);
        foreach (var extension in new[] { "jpg", "png", "tiff" })
        {
        await NativeWait.UntilAsync(_ => Task.FromResult(import.Current.IsEnabled), enabled => enabled, TimeSpan.FromSeconds(20));
        ((InvokePattern)import.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(700);
        Dump(root, Path.Combine(output, "import-dialog-uia.json"));
        var filename = root.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.AutomationIdProperty, "1148")));
        Assert.IsNotNull(filename, "Import picker filename control is required.");
        Console.WriteLine("Setting file picker selection.");
        // The Windows common dialog provider rejects ValuePattern.SetValue on this host.
        // Address only its observed edit HWND, after verifying process ownership.
        var editHandle = new nint(filename.Current.NativeWindowHandle);
        Win32.GetWindowThreadProcessId(editHandle, out var editPid);
        Assert.AreEqual(host.Pid, (int)editPid);
        var selection = Path.GetFullPath(Path.Combine(fixtures, "home." + extension));
        Assert.AreNotEqual(nint.Zero, Win32.SendMessageTimeout(editHandle, 0x000C, 0, selection, 2, 3000, out _));
        var open = root.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
        Assert.IsNotNull(open);
        Console.WriteLine("Confirming file picker selection.");
        ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await using var check = new SqliteAssetLibraryRepository(Path.Combine(host.RuntimeRoot, "Home.ptlibrary", AssetLibraryContainerService.DatabaseRelativePath));
        await NativeWait.UntilAsync(async _ => (await check.QueryAsync(new())).Items, items => items.Any(asset => asset.SourcePath == selection), TimeSpan.FromSeconds(30));
        }
        Dump(root, Path.Combine(output, "imported-uia.json"));
        await using var importedRepository = new SqliteAssetLibraryRepository(Path.Combine(host.RuntimeRoot, "Home.ptlibrary", AssetLibraryContainerService.DatabaseRelativePath));
        var imported = (await importedRepository.QueryAsync(new())).Items;
        Assert.HasCount(3, imported);
        Assert.IsTrue(imported.All(asset => asset.Width == 960 && asset.Height == 640), "Import must persist dimensions before opening Inspector.");
        File.Copy(capture.Capture("02_dimensions").Path, Path.Combine(output, "02_dimensions.png"), true);
        var asset = imported.First(item => item.Extension.Equals(".PNG", StringComparison.OrdinalIgnoreCase));
        AutomationElement Find(string id) => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id))
            ?? throw new InvalidOperationException("Missing production control: " + id);
        var card = Find("AssetCard_" + asset.AssetId.ToString("N"));
        ((SelectionItemPattern)card.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        await Task.Delay(700);
        Dump(root, Path.Combine(output, "selected-uia.json"));
        using (var dpi = new PhysicalDpiScope())
        {
            var bounds = card.Current.BoundingRectangle;
            var x = (int)(bounds.X + bounds.Width / 2);
            var y = (int)(bounds.Y + Math.Min(40, bounds.Height / 2));
            host.Validate(captureOnly: true);
            var clientPoint = new Win32.POINT(x, y);
            Assert.IsTrue(Win32.ScreenToClient(host.Hwnd, ref clientPoint));
            var position = new nint((clientPoint.Y << 16) | (clientPoint.X & 65535));
            Win32.SendMessageValue(host.Hwnd, 0x0200, 0, position, 2, 3000, out _);
            Win32.SendMessageValue(host.Hwnd, 0x0204, 2, position, 2, 3000, out _);
            Win32.SendMessageValue(host.Hwnd, 0x0205, 0, position, 2, 3000, out _);
        }
        await Task.Delay(500);
        Dump(root, Path.Combine(output, "context-menu-uia.json"));
        var context = Find("AssetVisualContextMenu");
        var contextHwnd = OwnedPopupWindow(host, context);
        Console.WriteLine($"Context menu hwnd: {contextHwnd}; bounds {context.Current.BoundingRectangle}");
        File.Copy(capture.Capture("07_context_menu_normal", contextHwnd).Path, Path.Combine(output, "07_context_menu_normal.png"), true);
        ((ExpandCollapsePattern)Find("AssetContextRating").GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        await Task.Delay(300);
        Dump(root, Path.Combine(output, "context-submenu-uia.json"));
        AutomationElement MenuItem(string title) => root.FindAll(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.NameProperty, title),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem))).Cast<AutomationElement>().First(e => !e.Current.IsOffscreen);
        var parentBounds = Find("AssetContextRating").Current.BoundingRectangle;
        var childBounds = MenuItem("1 星").Current.BoundingRectangle;
        Assert.IsGreaterThanOrEqualTo(parentBounds.Right - 2, childBounds.Left, $"Submenu overlaps: {parentBounds} / {childBounds}");
        var placements = new List<object> { new { State = "center", Parent = parentBounds.ToString(), Child = childBounds.ToString() } };
        ((ExpandCollapsePattern)Find("AssetContextRating").GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
        using (var dpi = new PhysicalDpiScope())
        {
            Assert.IsTrue(Win32.GetWindowRect(contextHwnd, out var popupBounds));
            var window = host.Validate(captureOnly: true).Bounds;
            Assert.IsTrue(Win32.SetWindowPos(contextHwnd, 0, (int)(window.X + window.Width - popupBounds.Bounds.Width - 25), 1100, 0, 0, 0x15));
        }
        ((ExpandCollapsePattern)Find("AssetContextRating").GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        await Task.Delay(400);
        parentBounds = Find("AssetContextRating").Current.BoundingRectangle;
        childBounds = MenuItem("1 星").Current.BoundingRectangle;
        Assert.IsLessThanOrEqualTo(parentBounds.Left + 2, childBounds.Right, $"Edge submenu did not flip: {parentBounds} / {childBounds}");
        placements.Add(new { State = "right-bottom-edge", Parent = parentBounds.ToString(), Child = childBounds.ToString() });
        File.WriteAllText(Path.Combine(output, "context-placement.json"), JsonSerializer.Serialize(placements));
        File.Copy(capture.Capture("08_context_menu_edge", OwnedPopupWindow(host, MenuItem("1 星"))).Path, Path.Combine(output, "08_context_menu_edge.png"), true);
        Win32.SendMessageValue(contextHwnd, 0x001F, 0, 0, 2, 3000, out _);
        ((InvokePattern)Find("AssetLibraryRatingFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        ((InvokePattern)Find("AssetLibraryRatingFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var rating = Find("AssetInspectorRatingControl");
        ((RangeValuePattern)rating.GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(4);
        await NativeWait.UntilAsync(async _ => await importedRepository.GetAssetAsync(asset.AssetId), value => value?.Rating == 4, TimeSpan.FromSeconds(10));
        File.Copy(capture.Capture("03_rating").Path, Path.Combine(output, "03_rating.png"), true);
        ((InvokePattern)Find("AssetLibraryColorFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(500);
        Dump(root, Path.Combine(output, "color-popup-uia.json"));
        var colorMenu = Find("AssetPopupColor");
        var labels = colorMenu.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.NameProperty, "颜色标记"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)));
        ((ExpandCollapsePattern)labels.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        await Task.Delay(300);
        ((InvokePattern)Find("AssetSetColor红").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await NativeWait.UntilAsync(async _ => await importedRepository.GetAssetAsync(asset.AssetId), value => value?.ColorLabel == "红", TimeSpan.FromSeconds(10));
        File.Copy(capture.Capture("04_color").Path, Path.Combine(output, "04_color.png"), true);
        ((InvokePattern)Find("AssetLibraryRatingFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(200);
        Assert.IsNotNull(Find("AssetPopupRating"));
        ((InvokePattern)Find("AssetLibraryColorFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(200);
        Assert.IsNull(root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetPopupRating")));
        File.Copy(capture.Capture("05_popup_switch").Path, Path.Combine(output, "05_popup_switch.png"), true);
        File.Copy(capture.Capture("05_color_popup", OwnedPopupWindow(host, Find("AssetPopupColor"))).Path, Path.Combine(output, "05_color_popup.png"), true);
        ((InvokePattern)Find("AssetLibraryMore").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(200);
        ((InvokePattern)Find("AssetAdvancedFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(300);
        File.Copy(capture.Capture("06_advanced_filter").Path, Path.Combine(output, "06_advanced_filter.png"), true);
        // Click blank workspace through the real HWND; keep keyboard/mouse provenance explicit.
        SendClientClick(host, 1300, 650);
        await Task.Delay(300);
        var closedFilter = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetFilterPopover"));
        Assert.IsTrue(closedFilter is null || closedFilter.Current.IsOffscreen);
        ((InvokePattern)Find("AssetLibraryRatingFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(200);
        var popupHwnd = OwnedPopupWindow(host, Find("AssetPopupRating"));
        Win32.SendMessageValue(popupHwnd, 0x0100, 0x1B, 0, 2, 3000, out _);
        Win32.SendMessageValue(popupHwnd, 0x0101, 0x1B, 0, 2, 3000, out _);
        await Task.Delay(300);
        Assert.IsNull(root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetPopupRating")), "Esc must close rating popup.");
        ((InvokePattern)Find("AssetLibraryMore").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(200);
        ((InvokePattern)MenuItem("新建智能文件夹").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(400);
        ((ValuePattern)Find("P3SmartFolderName").GetCurrentPattern(ValuePattern.Pattern)).SetValue("Home guard draft");
        ((InvokePattern)Find("AssetLibraryColorFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(300);
        Assert.IsFalse(Find("SmartFolderGuardCancel").Current.IsOffscreen);
        ((InvokePattern)Find("SmartFolderGuardCancel").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Assert.AreEqual("Home guard draft", ((ValuePattern)Find("P3SmartFolderName").GetCurrentPattern(ValuePattern.Pattern)).Current.Value);
        ((InvokePattern)Find("AssetLibraryColorFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(200);
        ((InvokePattern)Find("SmartFolderGuardDiscard").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(300);
        Assert.IsNotNull(Find("AssetPopupColor"));
        ((InvokePattern)Find("AssetLibraryColorFilter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await host.CloseAsync();
        var before = (await importedRepository.GetAssetAsync(asset.AssetId))!;
        var sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(asset.SourcePath));
        var group = await importedRepository.SaveTagGroupAsync(new(Guid.NewGuid(), "Home tags"));
        var tag = await importedRepository.SaveTagAsync(new(Guid.NewGuid(), "Home retained tag", group.TagGroupId));
        await importedRepository.AddTagsAsync([asset.AssetId], [tag.TagId]);
        var projectId = Guid.NewGuid();
        await importedRepository.SaveProjectAssetLinkAsync(new(projectId, asset.AssetId, "reference", DateTimeOffset.UtcNow));
        var smart = await importedRepository.SaveSmartFolderQueryDocumentAsync(new(Guid.NewGuid(), "Home red labels"), new()
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, [AssetQueryNode.Rule(AssetQueryField.ColorLabel, AssetQueryOperator.Equals, ["红"])])
        });
        // Model an old imported record in the isolated library, then use the normal app lifecycle.
        await using (var connection = new SqliteConnection("Data Source=" + Path.Combine(host.RuntimeRoot, "Home.ptlibrary", AssetLibraryContainerService.DatabaseRelativePath)))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE AssetItems SET Width=NULL,Height=NULL WHERE AssetId=$id";
            command.Parameters.AddWithValue("$id", asset.AssetId.ToString("D"));
            Assert.AreEqual(1, await command.ExecuteNonQueryAsync());
        }
        using var restarted = await PixelTartProcessHost.StartAsync(repository, CancellationToken.None, homeValidation: true, existingRuntime: host.RuntimeRoot);
        root = AutomationElement.FromHandle(restarted.Hwnd);
        await PrepareRestart(root, output, "backfill");
        Console.WriteLine("Reopened app: navigating for backfill.");
        ((InvokePattern)Find("AssetLibraryNavigationButton").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await NativeWait.UntilAsync(_ => Task.FromResult(root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetCard_" + asset.AssetId.ToString("N")))), e => e is not null, TimeSpan.FromSeconds(30));
        await NativeWait.UntilAsync(async _ => await importedRepository.GetAssetAsync(asset.AssetId), value => value?.Width == 960 && value.Height == 640, TimeSpan.FromSeconds(30));
        var after = (await importedRepository.GetAssetAsync(asset.AssetId))!;
        Assert.AreEqual(before with { ModifiedAt = after.ModifiedAt }, after);
        Assert.AreEqual(asset.AssetId, (await importedRepository.QueryAsync(new(TagId: tag.TagId))).Items.Single().AssetId);
        Assert.HasCount(1, await importedRepository.ListProjectAssetLinksAsync(assetId: asset.AssetId, projectId: projectId));
        CollectionAssert.AreEqual(sourceHash, SHA256.HashData(await File.ReadAllBytesAsync(asset.SourcePath)));
        ((SelectionItemPattern)Find("AssetCard_" + asset.AssetId.ToString("N")).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        await Task.Delay(1000);
        Console.WriteLine("Verifying persisted rating, then clearing.");
        Assert.AreEqual(4d, ((RangeValuePattern)Find("AssetInspectorRatingControl").GetCurrentPattern(RangeValuePattern.Pattern)).Current.Value);
        ((RangeValuePattern)Find("AssetInspectorRatingControl").GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(0);
        await NativeWait.UntilAsync(async _ => await importedRepository.GetAssetAsync(asset.AssetId), value => value?.Rating == 0 && value.ColorLabel == "红", TimeSpan.FromSeconds(10));
        await restarted.CloseAsync();
        using var cleared = await PixelTartProcessHost.StartAsync(repository, CancellationToken.None, homeValidation: true, existingRuntime: host.RuntimeRoot);
        root = AutomationElement.FromHandle(cleared.Hwnd);
        await PrepareRestart(root, output, "cleared");
        ((InvokePattern)Find("AssetLibraryNavigationButton").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await NativeWait.UntilAsync(_ => Task.FromResult(root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetCard_" + asset.AssetId.ToString("N")))), e => e is not null, TimeSpan.FromSeconds(30));
        ((SelectionItemPattern)Find("AssetCard_" + asset.AssetId.ToString("N")).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        await Task.Delay(500);
        Assert.AreEqual(0d, ((RangeValuePattern)Find("AssetInspectorRatingControl").GetCurrentPattern(RangeValuePattern.Pattern)).Current.Value);
        Assert.AreEqual("红", (await importedRepository.GetAssetAsync(asset.AssetId))!.ColorLabel);
        Win32.ShowWindow(cleared.Hwnd, 3);
        await Task.Delay(500);
        var finalCapture = new NativeWindowCapture(cleared);
        ((InvokePattern)Find("AssetSmartFolderNode_" + smart.SmartFolderId.ToString("N")).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(1000);
        Assert.AreEqual(1, root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>().Count(e => !e.Current.IsOffscreen && e.Current.AutomationId.StartsWith("AssetCard_")));
        File.Copy(finalCapture.Capture("10_smart_folder").Path, Path.Combine(output, "10_smart_folder.png"), true);
        ((InvokePattern)Find("AssetLibraryAllAssets").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(500);
        ((InvokePattern)Find("AssetTagGroup_" + group.TagGroupId.ToString("N") + "Filter").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(1000);
        Assert.AreEqual(1, root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>().Count(e => !e.Current.IsOffscreen && e.Current.AutomationId.StartsWith("AssetCard_")));
        File.Copy(finalCapture.Capture("11_tag_group").Path, Path.Combine(output, "11_tag_group.png"), true);
        var slider = (RangeValuePattern)Find("AssetThumbnailSizeSlider").GetCurrentPattern(RangeValuePattern.Pattern);
        Console.WriteLine($"Thumbnail range: {slider.Current.Minimum}..{slider.Current.Maximum}; value {slider.Current.Value}");
        slider.SetValue(280);
        Console.WriteLine($"Thumbnail at 280: {slider.Current.Value}; max {slider.Current.Maximum}");
        await Task.Delay(700);
        slider = (RangeValuePattern)Find("AssetThumbnailSizeSlider").GetCurrentPattern(RangeValuePattern.Pattern);
        slider.SetValue(Math.Floor(slider.Current.Maximum) - 1);
        await Task.Delay(1000);
        File.Copy(finalCapture.Capture("13_thumbnail_max").Path, Path.Combine(output, "13_thumbnail_max.png"), true);
        ((SelectionItemPattern)Find("AssetCard_" + asset.AssetId.ToString("N")).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        await Task.Delay(500);
        ((InvokePattern)Find("AssetInspectorExport").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(700);
        Dump(root, Path.Combine(output, "export-dialog-uia.json"));
        Console.WriteLine("Export dialog open.");
        var exportDirectory = Path.Combine(cleared.RuntimeRoot, "exports");
        Directory.CreateDirectory(exportDirectory);
        SetDialogPath(root, cleared, exportDirectory);
        await NativeWait.UntilAsync(_ => Task.FromResult(Directory.GetFiles(exportDirectory)), files => files.Length == 1, TimeSpan.FromSeconds(20));
        CollectionAssert.AreEqual(sourceHash, SHA256.HashData(await File.ReadAllBytesAsync(Directory.GetFiles(exportDirectory).Single())));
        CollectionAssert.AreEqual(sourceHash, SHA256.HashData(await File.ReadAllBytesAsync(asset.SourcePath)));
        File.Copy(finalCapture.Capture("09_export").Path, Path.Combine(output, "09_export.png"), true);
        ((InvokePattern)Find("AssetInspectorExport").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(500);
        var cancel = root.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "2"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
        Assert.IsNotNull(cancel);
        ((InvokePattern)cancel.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Assert.HasCount(1, Directory.GetFiles(exportDirectory));
        // Deny reads only for this synthetic fixture and verify the existing error path.
        using (var lockedSource = new FileStream(asset.SourcePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            ((InvokePattern)Find("AssetInspectorExport").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            await Task.Delay(400);
            SetDialogPath(root, cleared, exportDirectory);
            await NativeWait.UntilAsync(_ => Task.FromResult(root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                .Cast<AutomationElement>().Any(e => e.Current.Name.StartsWith("导出失败"))), failed => failed, TimeSpan.FromSeconds(10));
        }
        Assert.HasCount(1, Directory.GetFiles(exportDirectory));
        // Enter the existing canvas through the real card context menu.
        slider.SetValue(280);
        await Task.Delay(500);
        using (var dpi = new PhysicalDpiScope())
        {
            var bounds = Find("AssetCard_" + asset.AssetId.ToString("N")).Current.BoundingRectangle;
            var point = new Win32.POINT((int)(bounds.X + 30), (int)(bounds.Y + 30));
            Win32.ScreenToClient(cleared.Hwnd, ref point);
            var packed = new nint((point.Y << 16) | (point.X & 65535));
            cleared.Validate(captureOnly: true);
            Win32.SendMessageValue(cleared.Hwnd, 0x0200, 0, packed, 2, 3000, out _);
            Win32.SendMessageValue(cleared.Hwnd, 0x0204, 2, packed, 2, 3000, out _);
            Win32.SendMessageValue(cleared.Hwnd, 0x0205, 0, packed, 2, 3000, out _);
        }
        await Task.Delay(400);
        ((ExpandCollapsePattern)MenuItem("用于创作").GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        await Task.Delay(200);
        ((InvokePattern)MenuItem("放到自由画布").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(1500);
        Assert.IsTrue(Find("ContextNewCanvas").Current.IsEnabled);
        File.Copy(finalCapture.Capture("16_toolbar_canvas").Path, Path.Combine(output, "16_toolbar_canvas.png"), true);
        ((InvokePattern)Find("PrimaryNavigationPlanning").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Console.WriteLine("Planning navigation.");
        await Task.Delay(800);
        File.Copy(finalCapture.Capture("15_toolbar_planning").Path, Path.Combine(output, "15_toolbar_planning.png"), true);
        var newPlanning = root.FindAll(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.NameProperty, "新建策划"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
            .Cast<AutomationElement>().First(e => !e.Current.IsOffscreen && e.Current.IsEnabled);
        ((InvokePattern)newPlanning.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(500);
        ((ValuePattern)Find("PlanningEditorNewPlanningName").GetCurrentPattern(ValuePattern.Pattern)).SetValue("Home custom planning");
        ((InvokePattern)Find("PlanningCreateConfirm").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(1000);
        ((InvokePattern)Find("PlanningPageCustom").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(400);
        ((ValuePattern)Find("PlanningEditorDocumentCustomBody").GetCurrentPattern(ValuePattern.Pattern)).SetValue("Home custom content: preserved across restart.");
        await Task.Delay(1500);
        File.Copy(finalCapture.Capture("12_custom_planning").Path, Path.Combine(output, "12_custom_planning.png"), true);
        await cleared.CloseAsync();
        using var planningRestart = await PixelTartProcessHost.StartAsync(repository, CancellationToken.None, homeValidation: true, existingRuntime: host.RuntimeRoot);
        root = AutomationElement.FromHandle(planningRestart.Hwnd);
        await PrepareRestart(root, output, "planning");
        ((InvokePattern)Find("PrimaryNavigationPlanning").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(1200);
        ((InvokePattern)Find("PlanningPageCustom").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        await Task.Delay(400);
        Assert.AreEqual("Home custom content: preserved across restart.", ((ValuePattern)Find("PlanningEditorDocumentCustomBody").GetCurrentPattern(ValuePattern.Pattern)).Current.Value);
        await planningRestart.CloseAsync();
    }

    private static void SetDialogPath(AutomationElement root, PixelTartProcessHost host, string path)
    {
        var edit = root.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.AutomationIdProperty, "1152")));
        Assert.IsNotNull(edit);
        var hwnd = new nint(edit.Current.NativeWindowHandle);
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        Assert.AreEqual(host.Pid, (int)pid);
        Assert.AreNotEqual(nint.Zero, Win32.SendMessageTimeout(hwnd, 0x000C, 0, Path.GetFullPath(path), 2, 3000, out _));
        var confirm = root.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
        Assert.IsNotNull(confirm);
        ((InvokePattern)confirm.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static nint OwnedPopupWindow(PixelTartProcessHost host, AutomationElement element)
    {
        using var dpi = new PhysicalDpiScope();
        host.Validate(captureOnly: true);
        var r = element.Current.BoundingRectangle;
        var matches = new List<(nint Hwnd, double Area)>();
        Win32.EnumWindows((hwnd, _) =>
        {
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == host.Pid && hwnd != host.Hwnd && Win32.IsWindowVisible(hwnd) && Win32.GetWindowRect(hwnd, out var bounds)
                && bounds.Bounds.Contains(new((int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2))))
                matches.Add((hwnd, bounds.Bounds.Width * bounds.Bounds.Height));
            return true;
        }, 0);
        return matches.OrderBy(item => item.Area).First().Hwnd;
    }

    private static async Task PrepareRestart(AutomationElement root, string output, string stage)
    {
        await Task.Delay(2500);
        Dump(root, Path.Combine(output, stage + "-restart-uia.json"));
        var tutorial = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "TutorialExitButton"));
        if (tutorial is not null && tutorial.Current.IsEnabled && !tutorial.Current.IsOffscreen)
        {
            ((InvokePattern)tutorial.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            await Task.Delay(500);
        }
    }

    private static void SendClientClick(PixelTartProcessHost host, int x, int y)
    {
        host.Validate(captureOnly: true);
        var point = new nint((y << 16) | (x & 65535));
        Win32.SendMessageValue(host.Hwnd, 0x0200, 0, point, 2, 3000, out _);
        Win32.SendMessageValue(host.Hwnd, 0x0201, 1, point, 2, 3000, out _);
        Win32.SendMessageValue(host.Hwnd, 0x0202, 0, point, 2, 3000, out _);
    }

    private static void WritePhoto(string path, string extension)
    {
        const int width = 960, height = 640;
        var pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 3; pixels[i] = (byte)(x * 255 / width);
            pixels[i + 1] = (byte)(y * 255 / height); pixels[i + 2] = (byte)((x + y) % 256);
        }
        BitmapEncoder encoder = extension switch { "jpg" => new JpegBitmapEncoder(), "tiff" => new TiffBitmapEncoder(), _ => new PngBitmapEncoder() };
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3)));
        using var file = File.Create(path); encoder.Save(file);
    }

    private static void Dump(AutomationElement root, string path)
    {
        var items = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Select(element => new { element.Current.AutomationId, element.Current.Name, Type = element.Current.ControlType.ProgrammaticName,
                element.Current.IsEnabled, element.Current.IsOffscreen, Bounds = element.Current.BoundingRectangle.ToString() }).ToArray();
        File.WriteAllText(path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string FindRepository()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "RAWSelectionAssistant.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }
}
