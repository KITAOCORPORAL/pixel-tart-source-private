using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3WpfTests
{
    private static readonly string[] P3ControlFiles =
    [
        "AssetQueryComposerView.xaml",
        "AssetSmartFolderEditorView.xaml",
        "AssetTagManagerView.xaml"
    ];

    [TestMethod]
    public void PageMountsThreeP3ControlsWithoutCreatingAnotherAssetGrid()
    {
        var page = Load("AssetLibraryPage.xaml");
        var localNames = page.Descendants().Select(element => element.Name.LocalName).ToArray();
        CollectionAssert.IsSubsetOf(
            new[] { "AssetQueryComposerView", "AssetSmartFolderEditorView", "AssetTagManagerView" },
            localNames);
        Assert.AreEqual(1, page.Descendants().Count(element => Attribute(element, "x:Name") == "AssetGrid"));

        var viewModel = File.ReadAllText(ModulePath("AssetLibraryViewModel.cs"));
        StringAssert.Contains(viewModel, "public ObservableCollection<AssetVisualMatchView> AssetCards { get; } = [];");
        StringAssert.Contains(viewModel, "public IReadOnlyList<Guid> SelectedAssetIds => _workspaceSettings.SelectedAssetIds;");
        Assert.AreEqual(1, Count(viewModel, "public ObservableCollection<AssetVisualMatchView> AssetCards"));
    }

    [TestMethod]
    public void EveryP3InteractiveControlHasAnExplicitDarkStyleAndAccessibleIdentity()
    {
        var acceptedStyles = new HashSet<string>(StringComparer.Ordinal)
        {
            "{StaticResource AssetLibraryP3PrimaryButton}", "{StaticResource AssetLibraryP3SecondaryButton}",
            "{StaticResource AssetLibraryP3ChipButton}", "{StaticResource AssetLibraryP3DangerButton}",
            "{StaticResource AssetLibraryP3Toggle}", "{StaticResource AssetLibraryP3TextBox}",
            "{StaticResource AssetLibraryP3ComboBox}", "{StaticResource AssetLibraryP3ListBox}",
            "{StaticResource AssetLibraryP3Tree}"
        };
        var ids = new List<string>();
        foreach (var file in P3ControlFiles.Append("AssetLibraryP3Styles.xaml"))
        {
            var document = Load(file);
            foreach (var element in document.Descendants().Where(IsInteractiveInstance))
            {
                var style = Attribute(element, "Style");
                Assert.Contains(style, acceptedStyles, $"{file}: {element.Name.LocalName} is missing an accepted explicit dark style ({style}).");
                var id = Attribute(element, "AutomationProperties.AutomationId");
                var name = Attribute(element, "AutomationProperties.Name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{file}: {element.Name.LocalName} is missing AutomationId.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"{file}: {element.Name.LocalName} is missing accessible Name.");
                if (!id.StartsWith("{Binding", StringComparison.Ordinal)) ids.Add(id);
            }
        }
        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.IsEmpty(duplicates, "Duplicate fixed P3 AutomationIds: " + string.Join(", ", duplicates));
    }

    [TestMethod]
    public void P3DarkRolesMeetTextContrastAndExposeFocusPressedDisabledStates()
    {
        var styles = File.ReadAllText(ModulePath("AssetLibraryP3Styles.xaml"));
        foreach (var token in new[] { "IsMouseOver", "IsPressed", "IsKeyboardFocused", "IsKeyboardFocusWithin", "IsEnabled", "IsChecked" })
            StringAssert.Contains(styles, token);
        Assert.IsGreaterThanOrEqualTo(4.5, Contrast("#F5FFFC", "#0B6658"), "primary normal text");
        Assert.IsGreaterThanOrEqualTo(4.5, Contrast("#F2F6F7", "#192129"), "secondary normal text");
        Assert.IsGreaterThanOrEqualTo(4.5, Contrast("#ADB8C0", "#252C32"), "disabled text");
        Assert.IsGreaterThanOrEqualTo(3.0, Contrast("#65E6C7", "#151D23"), "keyboard focus outline");
    }

    [TestMethod]
    public void NestedRuleViewRoundTripsAndClearUnlockedPreservesOnlyLockedRules()
    {
        var changes = 0;
        var root = P3QueryNodeView.CreateRoot(() => changes++);
        root.AddRuleCommand.Execute(null);
        root.Children[0].Field = AssetQueryField.Tag;
        root.Children[0].Operator = AssetQueryOperator.AnyOf;
        root.Children[0].ValueText = $"id:{Guid.NewGuid():D}，id:{Guid.NewGuid():D}";
        root.Children[0].Locked = true;
        root.AddRuleCommand.Execute(null);
        root.AddGroupCommand.Execute(null);
        var group = root.Children.Single(node => node.IsGroup);
        group.Logic = AssetQueryLogic.Any;
        group.AddGroupCommand.Execute(null);
        group.Children[0].AddRuleCommand.Execute(null);

        var document = new AssetQueryDocument { RootGroup = root.ToModel() };
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(document);
        var restored = AssetQueryDocumentCodec.Parse(canonical);
        Assert.IsTrue(restored.IsValid, restored.ErrorMessage);
        Assert.AreEqual(3, MaximumDepth(restored.Document!.RootGroup));

        root.ClearUnlocked();
        Assert.HasCount(1, root.Children);
        Assert.IsTrue(root.Children[0].Locked);
        Assert.IsGreaterThan(0, changes);
    }

    [TestMethod]
    public async Task ImeSeamDefersSearchDocumentUntilCompositionCompletesAndScopePersists()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3Wpf", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var settings = new AssetLibraryWorkspaceSettings();
                var viewModel = new AssetLibraryViewModel(Path.Combine(root, "p3.db"), new TaskOperationBridge(), workspaceSettings: settings);
                viewModel.BeginP3SearchComposition();
                viewModel.SearchText = "中文检索";
                Assert.IsTrue(viewModel.P3IsImeComposing);
                Assert.IsEmpty(viewModel.P3QueryChips);
                viewModel.CompleteP3SearchComposition();
                Assert.IsFalse(viewModel.P3IsImeComposing);
                Assert.IsTrue(viewModel.P3QueryChips.Any(chip => chip.Label.Contains("中文检索", StringComparison.Ordinal)));
                viewModel.P3QueryScope = AssetQueryScope.AllAssets;
                Assert.AreEqual(AssetQueryScope.AllAssets, settings.QueryScope);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });
    }

    [TestMethod]
    public async Task SubmittedSearchCancelsPendingSuggestionsAndKeepsThemClosed()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3Suggestions", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var viewModel = new AssetLibraryViewModel(Path.Combine(root, "p3.db"), new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.SearchText = "P3_000";
                viewModel.SubmitP3SearchCommand.Execute(null);
                Thread.Sleep(650);
                Assert.IsFalse(viewModel.P3SuggestionsVisible);
                Assert.IsEmpty(viewModel.P3QuerySuggestions);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });
    }

    [TestMethod]
    public async Task RearmedSearchDebounceIgnoresQueuedTickFromPriorTimer()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3Debounce", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var viewModel = new AssetLibraryViewModel(Path.Combine(root, "p3.db"), new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                var generationField = typeof(AssetLibraryViewModel).GetField(
                    "_queryGeneration",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(generationField);
                var timerField = typeof(AssetLibraryViewModel).GetField(
                    "_searchDebounce",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(timerField);
                var debounceGenerationField = typeof(AssetLibraryViewModel).GetField(
                    "_searchDebounceGeneration",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(debounceGenerationField);
                var start = typeof(AssetLibraryViewModel).GetMethod(
                    "StartSearchDebounce",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(start);
                start.Invoke(viewModel, null);
                var staleTimer = (System.Windows.Threading.DispatcherTimer)timerField.GetValue(viewModel)!;
                var staleGeneration = (long)debounceGenerationField.GetValue(viewModel)!;
                start.Invoke(viewModel, null);
                var currentTimer = (System.Windows.Threading.DispatcherTimer)timerField.GetValue(viewModel)!;
                Assert.AreNotSame(staleTimer, currentTimer);
                var before = (long)generationField.GetValue(viewModel)!;
                var handler = typeof(AssetLibraryViewModel).GetMethod(
                    "OnSearchDebounceTick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(handler);
                handler.Invoke(viewModel, [staleTimer, staleGeneration]);
                var after = (long)generationField.GetValue(viewModel)!;
                Assert.AreEqual(before, after);
                Assert.AreSame(currentTimer, timerField.GetValue(viewModel));
                Assert.IsTrue(currentTimer.IsEnabled);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });
    }

    [TestMethod]
    public void BatchApplyPublishesSuccessOnlyAfterUiRefreshCompletes()
    {
        var source = File.ReadAllText(ModulePath("AssetLibraryViewModel.P3TagManager.cs"));
        var methodStart = source.IndexOf("private async Task ApplyP3BatchMetadataAsync()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void RememberP3MetadataResult", methodStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, methodStart);
        Assert.IsGreaterThan(methodStart, methodEnd);
        var body = source[methodStart..methodEnd];

        var repositoryApply = body.IndexOf("await _repository.ApplyBatchMetadataAsync", StringComparison.Ordinal);
        var rememberResult = body.IndexOf("RememberP3MetadataResult(result);", StringComparison.Ordinal);
        var refreshFilters = body.IndexOf("await RefreshFilterListsAsync", StringComparison.Ordinal);
        var refreshAssets = body.IndexOf("await RefreshAsync(initializationAttempt: null", StringComparison.Ordinal);
        var refreshSelection = body.IndexOf("await RefreshSelectionSummaryAsync();", StringComparison.Ordinal);
        var publishSuccess = body.IndexOf("P3BatchPreviewSummary = $\"已安全更新", StringComparison.Ordinal);
        var publishCompletion = body.IndexOf("PublishP3BatchApplyCompletion(AssetLibraryP3BatchApplyOutcome.Succeeded", StringComparison.Ordinal);

        Assert.IsTrue(
            repositoryApply >= 0 && repositoryApply < rememberResult &&
            rememberResult < refreshFilters && refreshFilters < refreshAssets &&
            refreshAssets < refreshSelection && refreshSelection < publishSuccess &&
            publishSuccess < publishCompletion,
            "Batch apply must publish its stable success state only after every UI refresh has completed.");
        StringAssert.Contains(body, "refreshOutcome != AssetLibraryRefreshOutcome.Completed || IsLoading || HasLoadError ||");
        StringAssert.Contains(body, "IsOrganizationLoading || HasOrganizationError");
        StringAssert.Contains(body, "批量修改已提交");
        StringAssert.Contains(body, "但界面刷新失败");
    }

    [TestMethod]
    public async Task BatchApplyCompletesAgainstRealRepositoryAndSameSelectionResyncPreservesStableUiState()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3BatchStable", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "library.db");
        var assetPath = Path.Combine(root, "batch-stable.jpg");
        var tagId = Guid.NewGuid();
        try
        {
            await File.WriteAllTextAsync(assetPath, "batch-stable");
            await using (var repository = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath))
            {
                await repository.InitializeAsync();
                await repository.ImportAsync([new AssetImportRequest(assetPath)]);
                await repository.SaveTagAsync(new(tagId, "稳定提交标签"));
            }

            await RunSta(() =>
            {
                var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.SyncSelection([viewModel.AssetCards.Single().Asset]);
                viewModel.P3BatchTag = viewModel.Tags.Single(tag => tag.TagId == tagId);
                viewModel.P3BatchTagAction = "添加";
                InvokePrivateTask(viewModel, "PreviewP3BatchMetadataAsync").GetAwaiter().GetResult();
                Assert.IsTrue(viewModel.P3BatchPreviewReady, viewModel.P3BatchPreviewSummary);

                InvokePrivateTask(viewModel, "ApplyP3BatchMetadataAsync").GetAwaiter().GetResult();

                Assert.IsNotNull(viewModel.LastUndoToken);
                StringAssert.Contains(viewModel.P3BatchPreviewSummary, "已安全更新 1 项");
                Assert.IsFalse(viewModel.IsLoading);
                Assert.IsFalse(viewModel.HasLoadError);
                Assert.IsFalse(viewModel.IsOrganizationLoading);
                Assert.IsFalse(viewModel.HasOrganizationError);
                Assert.HasCount(1, viewModel.SelectedAssets);

                var selectedAfterRefresh = viewModel.SelectedAssets.ToArray();
                viewModel.SyncVisibleSelection(
                    selectedAfterRefresh,
                    viewModel.AssetCards.Select(card => card.Asset.AssetId));
                StringAssert.Contains(
                    viewModel.P3BatchPreviewSummary,
                    "已安全更新 1 项",
                    "A delayed WPF selection reconciliation for the same asset IDs must not erase the completed batch state.");

                viewModel.SyncSelection([]);
                StringAssert.Contains(
                    viewModel.P3BatchPreviewSummary,
                    "先选择素材",
                    "A real logical selection change must still invalidate the completed batch state.");
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });

            await using var verification = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
            await verification.InitializeAsync();
            Assert.HasCount(1, await verification.ListTagMembershipsAsync(tagId: tagId));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task PageBatchApplyCompletionSurvivesDeferredGridSelectionReconciliation()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3BatchGridStable", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "library.db");
        var assetPath = Path.Combine(root, "batch-grid-stable.jpg");
        var tagId = Guid.NewGuid();
        try
        {
            await File.WriteAllTextAsync(assetPath, "batch-grid-stable");
            await using (var repository = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath))
            {
                await repository.InitializeAsync();
                await repository.ImportAsync([new AssetImportRequest(assetPath)]);
                await repository.SaveTagAsync(new(tagId, "界面延迟同步标签"));
            }

            await RunSta(async () =>
            {
                var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(databasePath, new TaskOperationBridge(), []);
                try
                {
                    page.Measure(new Size(1600, 1000));
                    page.Arrange(new Rect(0, 0, 1600, 1000));
                    await page.ViewModel.InitializeAsync();
                    page.UpdateLayout();

                    await InvokePrivateTask(page.ViewModel, "ApplyP3BatchMetadataAsync");
                    AssertP3BatchCompletion(page.ViewModel, 1, "Rejected", operationId: null);

                    var grid = page.FindName("AssetGrid") as ListBox;
                    Assert.IsNotNull(grid);
                    Assert.HasCount(1, grid.Items);
                    grid.SelectedItems.Add(grid.Items[0]);
                    await page.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                    Assert.AreEqual(1, page.ViewModel.SelectionCount);

                    page.ViewModel.P3BatchTag = page.ViewModel.Tags.Single(tag => tag.TagId == tagId);
                    page.ViewModel.P3BatchTagAction = "添加";
                    await InvokePrivateTask(page.ViewModel, "PreviewP3BatchMetadataAsync");
                    Assert.IsTrue(page.ViewModel.P3BatchPreviewReady, page.ViewModel.P3BatchPreviewSummary);
                    await InvokePrivateTask(page.ViewModel, "ApplyP3BatchMetadataAsync");

                    await page.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                    StringAssert.Contains(page.ViewModel.P3BatchPreviewSummary, "已安全更新 1 项");
                    Assert.IsNotNull(page.ViewModel.LastUndoToken);
                    AssertP3BatchCompletion(page.ViewModel, 2, "Succeeded", page.ViewModel.LastUndoToken.OperationId);
                    Assert.AreEqual(1, page.ViewModel.SelectionCount);
                }
                finally
                {
                    await page.DisposeAsync();
                }
            });

            await using var verification = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
            await verification.InitializeAsync();
            Assert.HasCount(1, await verification.ListTagMembershipsAsync(tagId: tagId));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public void SearchSuggestionsAreViewportBoundedAndScrollable()
    {
        var composer = Load("AssetQueryComposerView.xaml");
        var panel = composer.Descendants().Single(element =>
            Attribute(element, "AutomationProperties.AutomationId") == "P3QuerySuggestionsPanel");
        var scrollViewer = panel.Descendants().Single(element => element.Name.LocalName == "ScrollViewer");
        Assert.AreEqual("180", Attribute(scrollViewer, "MaxHeight"));
        Assert.AreEqual("Auto", Attribute(scrollViewer, "VerticalScrollBarVisibility"));
        Assert.AreEqual("Disabled", Attribute(scrollViewer, "HorizontalScrollBarVisibility"));
    }

    [TestMethod]
    public async Task RuntimePageCreatesAllP3UserControlsWithSharedViewModel()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3Page", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(Path.Combine(root, "p3-page.db"), new TaskOperationBridge(), []);
                page.Measure(new Size(1600, 1000));
                page.Arrange(new Rect(0, 0, 1600, 1000));
                page.UpdateLayout();
                Assert.AreSame(page.ViewModel, FindLogical<AssetQueryComposerView>(page).DataContext);
                Assert.AreSame(page.ViewModel, FindLogical<AssetSmartFolderEditorView>(page).DataContext);
                Assert.AreSame(page.ViewModel, FindLogical<AssetTagManagerView>(page).DataContext);
                page.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });
    }

    [TestMethod]
    public void CurrentSmartFolderKeepsLiveComposerDocumentAndArchiveRuleExpandsCandidatePool()
    {
        var composer = File.ReadAllText(ModulePath("AssetLibraryViewModel.P3QueryComposer.cs"));
        var smartFolder = File.ReadAllText(ModulePath("AssetLibraryViewModel.P3SmartFolder.cs"));
        StringAssert.Contains(composer, "private AssetQueryDocument GetP3QueryDocumentForExecution() => GetP3QueryDocument();");
        StringAssert.Contains(composer, "ContainsEnabledP3ArchiveRule(root)");
        StringAssert.Contains(smartFolder, "IncludeArchived = P3SmartFolderIncludeArchived");
        StringAssert.Contains(smartFolder, "private AssetQueryDocument _p3SmartFolderDocument");
        Assert.DoesNotContain("SelectedSmartFolder is not null && P3QueryScope == AssetQueryScope.Current\r\n            ? null", composer);
    }

    [TestMethod]
    public void QueryNodeAutomationIdsAreStableAcrossRebuildsAndScopedPerEditor()
    {
        var model = AssetQueryNode.Group(AssetQueryLogic.All,
        [
            AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["3"]),
            AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["3"])
        ]);
        var first = P3QueryNodeView.FromModel(model, () => { }, "Query");
        var rebuilt = P3QueryNodeView.FromModel(model, () => { }, "Query");
        var smartFolder = P3QueryNodeView.FromModel(model, () => { }, "SmartFolder");

        CollectionAssert.AreEqual(
            first.DescendantsAndSelf().Select(node => node.AutomationId).ToArray(),
            rebuilt.DescendantsAndSelf().Select(node => node.AutomationId).ToArray());
        Assert.HasCount(first.DescendantsAndSelf().Count(), first.DescendantsAndSelf().Select(node => node.AutomationId).Distinct().ToArray());
        Assert.AreNotEqual(first.AutomationId, smartFolder.AutomationId);
    }

    [TestMethod]
    public void RuleTemplateUsesTypedEditorsAndRejectsNewFreeTextReferences()
    {
        var styles = File.ReadAllText(ModulePath("AssetLibraryP3Styles.xaml"));
        foreach (var binding in new[]
        {
            "P3FolderReferenceOptions", "P3TagReferenceOptions", "IsEnumEditor", "IsDateEditor",
            "IsDateRangeEditor", "IsNumericEditor", "IsNumericRangeEditor", "IsColorEditor", "IsTextEditor"
        }) StringAssert.Contains(styles, binding);
        StringAssert.Contains(styles, "<DatePicker");
        foreach (var binding in new[]
        {
            "PendingReferenceValue", "ReferenceValues", "AddReferenceValueCommand",
            "RemoveReferenceValueCommand", "P3ReferenceOptionsConverter", "P3ReferenceLabelConverter"
        }) StringAssert.Contains(styles, binding);

        var rawReference = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, ["自由输入文件夹名"])])
        };
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(rawReference).IsValid,
            "Folder/tag editors must emit stable id: references rather than silently converting free text to a name reference.");
    }

    [TestMethod]
    public void FolderAndTagReferenceEditorsRoundTripMultipleStableIdsAsTypedChips()
    {
        var folderIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var tagIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var model = AssetQueryNode.Group(AssetQueryLogic.All,
        [
            AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AllOf,
                folderIds.Take(2).Select(id => $"id:{id:D}")),
            AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.NoneOf,
                tagIds.Select(id => $"id:{id:D}"))
        ]);

        var root = P3QueryNodeView.FromModel(model, () => { });
        var folder = root.Children[0];
        var tag = root.Children[1];
        Assert.HasCount(2, folder.ReferenceValues);
        Assert.HasCount(2, tag.ReferenceValues);
        CollectionAssert.AreEqual(
            folderIds.Take(2).Select(id => $"id:{id:D}").ToArray(),
            folder.ReferenceValues.Select(value => value.Value).ToArray());

        folder.PendingReferenceValue = $"id:{folderIds[2]:D}";
        Assert.IsTrue(folder.AddReferenceValueCommand.CanExecute(null));
        folder.AddReferenceValueCommand.Execute(null);
        Assert.HasCount(3, folder.ReferenceValues);
        Assert.IsEmpty(folder.PendingReferenceValue);
        Assert.HasCount(3, folder.ReferenceValues.Select(value => value.AutomationId).Distinct().ToArray());

        folder.RemoveReferenceValueCommand.Execute(folder.ReferenceValues[1]);
        var roundTrip = P3QueryNodeView.FromModel(root.ToModel(), () => { });
        CollectionAssert.AreEqual(
            new[] { $"id:{folderIds[0]:D}", $"id:{folderIds[2]:D}" },
            roundTrip.Children[0].ReferenceValues.Select(value => value.Value).ToArray());
        CollectionAssert.AreEqual(
            tagIds.Select(id => $"id:{id:D}").ToArray(),
            roundTrip.Children[1].ReferenceValues.Select(value => value.Value).ToArray());
        CollectionAssert.AreEqual(
            root.ToModel().Children.SelectMany(rule => rule.Values).ToArray(),
            roundTrip.ToModel().Children.SelectMany(rule => rule.Values).ToArray());

        folder.PendingReferenceValue = "自由输入文件夹名";
        Assert.IsFalse(folder.AddReferenceValueCommand.CanExecute(null));
    }

    [TestMethod]
    public void QueryNodeRoundTripsCaseSensitivityAndUsesTheCoreOperatorMatrix()
    {
        var model = AssetQueryNode.Group(AssetQueryLogic.All,
        [
            AssetQueryNode.Rule(
                AssetQueryField.Comment,
                AssetQueryOperator.Regex,
                ["^MiXeD$"],
                caseSensitivity: AssetQueryCaseSensitivity.Sensitive)
        ]);
        var root = P3QueryNodeView.FromModel(model, () => { });
        var rule = root.Children.Single();

        Assert.IsTrue(rule.CanChooseCaseSensitivity);
        Assert.IsTrue(rule.IsCaseSensitive);
        Assert.AreEqual(AssetQueryCaseSensitivity.Sensitive, root.ToModel().Children.Single().CaseSensitivity);
        CollectionAssert.AreEqual(
            AssetQueryDocumentCodec.GetSupportedOperators(AssetQueryField.Comment).ToArray(),
            rule.OperatorOptions.Select(option => option.Value).ToArray());

        rule.Field = AssetQueryField.Extension;
        Assert.IsTrue(rule.CanChooseCaseSensitivity);
        Assert.IsFalse(rule.OperatorOptions.Any(option => option.Value == AssetQueryOperator.Regex));
        rule.Field = AssetQueryField.MediaType;
        Assert.IsFalse(rule.CanChooseCaseSensitivity);
        Assert.IsFalse(rule.IsCaseSensitive);
        Assert.AreEqual(AssetQueryCaseSensitivity.Insensitive, rule.ToModel().CaseSensitivity);
    }

    [TestMethod]
    public async Task SmartFolderEditorNoOpSavePreservesCompleteDocumentJsonHashAndResultsAcrossRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3NoOp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "library.db");
        try
        {
            await using (var repository = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath))
            {
                await repository.InitializeAsync();
                foreach (var name in new[] { "alpha-beta.jpg", "alpha-only.jpg", "archived-alpha-beta.jpg" })
                {
                    var path = Path.Combine(root, name);
                    await File.WriteAllTextAsync(path, name);
                    await repository.ImportAsync([new AssetImportRequest(path)]);
                }
                var archived = (await repository.QueryAsync(new(PageSize: 20))).Items.Single(item => item.DisplayName.StartsWith("archived", StringComparison.Ordinal));
                await repository.SetAssetsArchivedAsync([archived.AssetId], true);
                var original = new AssetQueryDocument
                {
                    Scope = AssetQueryScope.Current,
                    Text = "保留但由多段搜索取代",
                    SearchClauses = ["alpha", "beta"],
                    RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                    [
                        AssetQueryNode.Rule(
                            AssetQueryField.FileName,
                            AssetQueryOperator.Contains,
                            ["alpha"],
                            caseSensitivity: AssetQueryCaseSensitivity.Sensitive)
                    ]),
                    SortField = AssetLibrarySortField.FileName,
                    SortDirection = AssetLibrarySortDirection.Ascending,
                    IncludeArchived = true
                };
                var folder = await repository.SaveSmartFolderQueryDocumentAsync(new(Guid.NewGuid(), "完整顶层语义"), original);
                var before = await repository.GetSmartFolderQueryDocumentAsync(folder.SmartFolderId);
                Assert.IsNotNull(before);
                var beforeJson = AssetQueryDocumentCodec.SerializeCanonical(before.Document);
                var beforeResult = await repository.QueryAsync(new(SmartFolderId: folder.SmartFolderId, PageSize: 50));

                await RunSta(() =>
                {
                    var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                    viewModel.InitializeAsync().GetAwaiter().GetResult();
                    var selected = viewModel.SmartFolders.Single(item => item.SmartFolderId == folder.SmartFolderId);
                    InvokePrivateVoid(viewModel, "OpenP3SmartFolderEditor", selected);
                    Assert.IsTrue(SpinWait.SpinUntil(() => !viewModel.P3SmartFolderLoading, TimeSpan.FromSeconds(5)));
                    Assert.AreEqual(AssetLibrarySortField.FileName, viewModel.P3SmartFolderSortField);
                    Assert.AreEqual(AssetLibrarySortDirection.Ascending, viewModel.P3SmartFolderSortDirection);
                    Assert.IsTrue(viewModel.P3SmartFolderIncludeArchived);
                    StringAssert.Contains(viewModel.P3SmartFolderPreservedScopeAndSearch, "当前范围");
                    StringAssert.Contains(viewModel.P3SmartFolderPreservedScopeAndSearch, "2 段");
                    Assert.AreEqual(AssetQueryCaseSensitivity.Sensitive, viewModel.P3SmartFolderRoot.Children.Single().CaseSensitivity);
                    InvokePrivateTask(viewModel, "SaveP3SmartFolderAsync").GetAwaiter().GetResult();
                    viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
                });

                var after = await repository.GetSmartFolderQueryDocumentAsync(folder.SmartFolderId);
                Assert.IsNotNull(after);
                Assert.AreEqual(beforeJson, AssetQueryDocumentCodec.SerializeCanonical(after.Document));
                Assert.AreEqual(before.QueryHash, after.QueryHash);
                var afterResult = await repository.QueryAsync(new(SmartFolderId: folder.SmartFolderId, PageSize: 50));
                CollectionAssert.AreEqual(beforeResult.Items.Select(item => item.AssetId).ToArray(), afterResult.Items.Select(item => item.AssetId).ToArray());
            }

            await using var restarted = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
            await restarted.InitializeAsync();
            var reopenedFolder = (await restarted.ListSmartFoldersAsync(includeArchived: true)).Single(item => item.Name == "完整顶层语义");
            var reopened = await restarted.GetSmartFolderQueryDocumentAsync(reopenedFolder.SmartFolderId);
            Assert.IsNotNull(reopened);
            Assert.AreEqual(AssetLibrarySortField.FileName, reopened.Document.SortField);
            Assert.AreEqual(AssetLibrarySortDirection.Ascending, reopened.Document.SortDirection);
            Assert.AreEqual(AssetQueryScope.Current, reopened.Document.Scope);
            Assert.IsTrue(reopened.Document.IncludeArchived);
            CollectionAssert.AreEqual(new[] { "alpha", "beta" }, reopened.Document.SearchClauses!.ToArray());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task ComposerSaveThenEditorNoOpSaveAndRestartKeepsJsonHashAndResults()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3ComposerLoop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "library.db");
        try
        {
            await using (var seed = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath))
            {
                await seed.InitializeAsync();
                foreach (var name in new[] { "alpha-beta.jpg", "alpha-only.jpg", "beta-only.jpg" })
                {
                    var path = Path.Combine(root, name);
                    await File.WriteAllTextAsync(path, name);
                    await seed.ImportAsync([new AssetImportRequest(path)]);
                }
            }

            Guid savedId = Guid.Empty;
            string beforeJson = string.Empty;
            string beforeHash = string.Empty;
            Guid[] beforeResults = [];
            await RunSta(() =>
            {
                var workspaceDocument = new AssetQueryDocument
                {
                    Scope = AssetQueryScope.AllAssets,
                    Text = "alpha",
                    SearchClauses = ["alpha", "beta"],
                    RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                    [
                        AssetQueryNode.Rule(
                            AssetQueryField.FileName,
                            AssetQueryOperator.Contains,
                            ["alpha"],
                            caseSensitivity: AssetQueryCaseSensitivity.Sensitive)
                    ]),
                    SortField = AssetLibrarySortField.FileName,
                    SortDirection = AssetLibrarySortDirection.Ascending
                };
                var settings = new AssetLibraryWorkspaceSettings
                {
                    QueryScope = AssetQueryScope.AllAssets,
                    SearchText = "alpha",
                    QueryDocumentJson = AssetQueryDocumentCodec.SerializeCanonical(workspaceDocument),
                    SortField = AssetLibrarySortField.FileName,
                    SortDirection = AssetLibrarySortDirection.Ascending
                };
                var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge(), workspaceSettings: settings);
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.P3NewSmartFolderName = "Composer 往返";
                InvokePrivateTask(viewModel, "SaveP3QueryAsSmartFolderAsync").GetAwaiter().GetResult();
                var saved = viewModel.SmartFolders.Single(item => item.Name == "Composer 往返");
                savedId = saved.SmartFolderId;

                var repository = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
                repository.InitializeAsync().GetAwaiter().GetResult();
                var before = repository.GetSmartFolderQueryDocumentAsync(savedId).GetAwaiter().GetResult();
                Assert.IsNotNull(before);
                beforeJson = AssetQueryDocumentCodec.SerializeCanonical(before.Document);
                beforeHash = before.QueryHash;
                beforeResults = repository.QueryAsync(new(SmartFolderId: savedId, PageSize: 50)).GetAwaiter().GetResult()
                    .Items.Select(item => item.AssetId).ToArray();
                Assert.HasCount(1, beforeResults);
                repository.DisposeAsync().AsTask().GetAwaiter().GetResult();

                InvokePrivateVoid(viewModel, "OpenP3SmartFolderEditor", saved);
                Assert.IsTrue(SpinWait.SpinUntil(() => !viewModel.P3SmartFolderLoading, TimeSpan.FromSeconds(5)));
                InvokePrivateTask(viewModel, "SaveP3SmartFolderAsync").GetAwaiter().GetResult();
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });

            await using var restarted = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
            await restarted.InitializeAsync();
            var reopened = await restarted.GetSmartFolderQueryDocumentAsync(savedId);
            Assert.IsNotNull(reopened);
            Assert.AreEqual(beforeJson, AssetQueryDocumentCodec.SerializeCanonical(reopened.Document));
            Assert.AreEqual(beforeHash, reopened.QueryHash);
            CollectionAssert.AreEqual(
                beforeResults,
                (await restarted.QueryAsync(new(SmartFolderId: savedId, PageSize: 50))).Items.Select(item => item.AssetId).ToArray());
            CollectionAssert.AreEqual(new[] { "alpha", "beta" }, reopened.Document.SearchClauses!.ToArray());
            Assert.AreEqual(AssetLibrarySortField.FileName, reopened.Document.SortField);
            Assert.AreEqual(AssetLibrarySortDirection.Ascending, reopened.Document.SortDirection);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task ComposerAndEditorSaveFailuresKeepRetryableContentAndRetrySuccessfully()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3SaveFailure", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var databasePath = Path.Combine(root, "library.db");
            AssetLibraryViewModel? viewModel = null;
            try
            {
                viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.P3QueryScope = AssetQueryScope.AllAssets;
                viewModel.SearchText = "保留搜索";
                viewModel.P3NewSmartFolderName = "可重试 Composer";
                viewModel.P3QueryRoot.AddRuleCommand.Execute(null);
                var queryRule = viewModel.P3QueryRoot.Children.Single();
                queryRule.Field = AssetQueryField.Comment;
                queryRule.Operator = AssetQueryOperator.Contains;
                queryRule.ValueText = "保留规则";
                var beforeQuery = AssetQueryDocumentCodec.SerializeCanonical(new AssetQueryDocument { RootGroup = viewModel.P3QueryRoot.ToModel() });

                ExecuteSql(databasePath, "CREATE TRIGGER p3_fail_query_insert BEFORE INSERT ON SmartFolderQueryDocuments BEGIN SELECT RAISE(ABORT,'controlled-query-save-failure'); END;");
                InvokePrivateTask(viewModel, "SaveP3QueryAsSmartFolderAsync").GetAwaiter().GetResult();
                StringAssert.Contains(viewModel.P3QueryValidationMessage, "已保留，可重试");
                Assert.AreEqual("可重试 Composer", viewModel.P3NewSmartFolderName);
                Assert.AreEqual("保留搜索", viewModel.SearchText);
                Assert.AreEqual(beforeQuery, AssetQueryDocumentCodec.SerializeCanonical(new AssetQueryDocument { RootGroup = viewModel.P3QueryRoot.ToModel() }));
                ExecuteSql(databasePath, "DROP TRIGGER p3_fail_query_insert;");
                InvokePrivateTask(viewModel, "SaveP3QueryAsSmartFolderAsync").GetAwaiter().GetResult();
                var saved = viewModel.SmartFolders.Single(item => item.Name == "可重试 Composer");

                InvokePrivateVoid(viewModel, "OpenP3SmartFolderEditor", saved);
                Assert.IsTrue(SpinWait.SpinUntil(() => !viewModel.P3SmartFolderLoading, TimeSpan.FromSeconds(5)));
                viewModel.P3SmartFolderName = "可重试 Editor";
                viewModel.P3SmartFolderDescription = "失败后保留说明";
                viewModel.P3SmartFolderRoot.AddRuleCommand.Execute(null);
                var editorRule = viewModel.P3SmartFolderRoot.Children.Last();
                editorRule.Field = AssetQueryField.Rating;
                editorRule.Operator = AssetQueryOperator.GreaterThanOrEqual;
                editorRule.ValueText = "4";
                var beforeEditor = AssetQueryDocumentCodec.SerializeCanonical(new AssetQueryDocument { RootGroup = viewModel.P3SmartFolderRoot.ToModel() });

                ExecuteSql(databasePath, "CREATE TRIGGER p3_fail_query_update BEFORE UPDATE ON SmartFolderQueryDocuments BEGIN SELECT RAISE(ABORT,'controlled-editor-save-failure'); END;");
                InvokePrivateTask(viewModel, "SaveP3SmartFolderAsync").GetAwaiter().GetResult();
                StringAssert.Contains(viewModel.P3SmartFolderValidationMessage, "已保留，可重试");
                Assert.AreEqual("可重试 Editor", viewModel.P3SmartFolderName);
                Assert.AreEqual("失败后保留说明", viewModel.P3SmartFolderDescription);
                Assert.AreEqual(beforeEditor, AssetQueryDocumentCodec.SerializeCanonical(new AssetQueryDocument { RootGroup = viewModel.P3SmartFolderRoot.ToModel() }));
                ExecuteSql(databasePath, "DROP TRIGGER p3_fail_query_update;");
                InvokePrivateTask(viewModel, "SaveP3SmartFolderAsync").GetAwaiter().GetResult();

                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM SmartFolders WHERE Name='可重试 Editor';";
                Assert.AreEqual(1L, Convert.ToInt64(command.ExecuteScalar()));

                var smartFolderSource = File.ReadAllText(ModulePath("AssetLibraryViewModel.P3SmartFolder.cs"));
                StringAssert.Contains(smartFolderSource, "private async Task CopyP3SmartFolderAsync()");
                StringAssert.Contains(smartFolderSource, "复制失败，当前编辑内容已保留，可重试");
                StringAssert.Contains(smartFolderSource, "归档操作失败，当前编辑内容已保留，可重试");
            }
            finally
            {
                viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                try { Directory.Delete(root, true); } catch { }
            }
        });
    }

    [TestMethod]
    public async Task ClearUnlockedAndClearAllAlsoRemoveVisibleSearchText()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3Clear", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var viewModel = new AssetLibraryViewModel(Path.Combine(root, "library.db"), new TaskOperationBridge());
                viewModel.SearchText = "必须清除";
                viewModel.P3QueryRoot.AddRuleCommand.Execute(null);
                viewModel.P3QueryRoot.Children.Single().Locked = true;
                viewModel.ClearP3UnlockedCommand.Execute(null);
                Assert.IsEmpty(viewModel.SearchText);
                Assert.HasCount(1, viewModel.P3QueryRoot.Children);

                viewModel.SearchText = "再次清除";
                viewModel.ClearP3AllCommand.Execute(null);
                Assert.IsEmpty(viewModel.SearchText);
                Assert.IsEmpty(viewModel.P3QueryRoot.Children);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });
    }

    [TestMethod]
    public void TagManagerAndSelectionSourcesExposeRaceSafeMultiSourceAndArchivedSemantics()
    {
        var manager = File.ReadAllText(ModulePath("AssetLibraryViewModel.P3TagManager.cs"));
        var managerView = File.ReadAllText(ModulePath("AssetTagManagerView.xaml"));
        var viewModel = File.ReadAllText(ModulePath("AssetLibraryViewModel.cs"));
        foreach (var token in new[]
        {
            "_p3TagMergePreviewCancellation", "_p3TagMergePreviewGeneration", "_p3TagMergePreviewFingerprint",
            "SetP3MergeSourceTags", "CaptureP3TagMergePreviewAsync", "MergeTagsAsync(sources.Select",
            "IsP3TagEffectivelyActive", "ListTagGroupsAsync(includeArchived: true", "stateContract",
            "源标签或目标标签已归档（包括其标签组已归档）"
        }) StringAssert.Contains(manager, token);
        StringAssert.Contains(manager, "tag.TagGroupId is null || visibleGroupIds.Contains");
        StringAssert.Contains(managerView, "SelectionMode=\"Extended\"");
        StringAssert.Contains(viewModel, "var archiveScope = BuildQuery().EffectiveArchiveScope;");
    }

    [TestMethod]
    public async Task SavingCurrentFolderScopeMaterializesStableSourceRuleAndSameResults()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3SaveCurrent", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var databasePath = Path.Combine(root, "library.db");
            var repository = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
            await repository.InitializeAsync();
            var paths = new[] { Path.Combine(root, "inside.jpg"), Path.Combine(root, "outside.jpg") };
            foreach (var path in paths) await File.WriteAllTextAsync(path, path);
            await repository.ImportAsync(paths.Select(path => new AssetImportRequest(path)));
            var assets = (await repository.QueryAsync(new AssetLibraryQuery(PageSize: 10))).Items;
            var folder = await repository.SaveFolderAsync(new(Guid.NewGuid(), null, "当前来源"));
            await repository.AddToFolderAsync([assets.Single(asset => asset.DisplayName == "inside.jpg").AssetId], folder.FolderId);

            await RunSta(() =>
            {
                var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.SelectedFolder = viewModel.Folders.Single(item => item.FolderId == folder.FolderId);
                var method = typeof(AssetLibraryViewModel).GetMethod(
                    "BuildP3SmartFolderDocumentFromCurrentAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var task = (Task)method.Invoke(viewModel, [CancellationToken.None])!;
                task.GetAwaiter().GetResult();
                var document = (AssetQueryDocument)task.GetType().GetProperty("Result")!.GetValue(task)!;
                Assert.AreEqual(AssetQueryScope.AllAssets, document.Scope);
                Assert.IsTrue(document.RootGroup.Children.SelectMany(node => node.Children)
                    .Any(rule => rule.Field == AssetQueryField.Folder &&
                                 rule.Values.Contains($"id:{folder.FolderId:D}", StringComparer.Ordinal)));
                var savedPage = repository.QueryAsync(new AssetLibraryQuery(PageSize: 10) { Document = document })
                    .GetAwaiter().GetResult();
                Assert.HasCount(1, savedPage.Items);
                Assert.AreEqual("inside.jpg", savedPage.Items[0].DisplayName);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
            await repository.DisposeAsync();
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task SavingCurrentFilterPreservesAllAssetsRegexAndCurrentEffectiveSystemSort()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3SaveEffective", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var databasePath = Path.Combine(root, "library.db");
            var repository = new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(databasePath);
            await repository.InitializeAsync();
            foreach (var name in new[] { "keep-one.jpg", "drop-one.jpg", "keep-two.jpg" })
            {
                var path = Path.Combine(root, name);
                await File.WriteAllTextAsync(path, name);
                await repository.ImportAsync([new AssetImportRequest(path)]);
            }

            await RunSta(() =>
            {
                var settings = new AssetLibraryWorkspaceSettings
                {
                    ActiveCollection = AssetLibrarySystemCollection.RecentlyAdded,
                    SortField = AssetLibrarySortField.Rating,
                    SortDirection = AssetLibrarySortDirection.Ascending
                };
                var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge(), workspaceSettings: settings);
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.FileNameRegexFilterText = "^keep-";

                var saveMethod = typeof(AssetLibraryViewModel).GetMethod(
                    "BuildP3SmartFolderDocumentFromCurrentAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var queryMethod = typeof(AssetLibraryViewModel).GetMethod(
                    "BuildQuery",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                AssetQueryDocument BuildSavedDocument()
                {
                    var task = (Task)saveMethod.Invoke(viewModel, [CancellationToken.None])!;
                    task.GetAwaiter().GetResult();
                    return (AssetQueryDocument)task.GetType().GetProperty("Result")!.GetValue(task)!;
                }

                var currentSaved = BuildSavedDocument();
                Assert.AreEqual(AssetLibrarySortField.AddedAt, currentSaved.SortField);
                Assert.AreEqual(AssetLibrarySortDirection.Descending, currentSaved.SortDirection);
                Assert.IsTrue(QueryDescendantsAndSelf(currentSaved.RootGroup).Any(rule =>
                    rule.Field == AssetQueryField.FileName && rule.Operator == AssetQueryOperator.Regex &&
                    rule.Values.SequenceEqual(new[] { "^keep-" }, StringComparer.Ordinal)));

                viewModel.P3QueryScope = AssetQueryScope.AllAssets;
                var allAssetsActualQuery = (AssetLibraryQuery)queryMethod.Invoke(viewModel, [null])!;
                var allAssetsSaved = BuildSavedDocument();
                Assert.AreEqual(AssetQueryScope.AllAssets, allAssetsSaved.Scope);
                Assert.IsTrue(QueryDescendantsAndSelf(allAssetsSaved.RootGroup).Any(rule =>
                    rule.Field == AssetQueryField.FileName && rule.Operator == AssetQueryOperator.Regex &&
                    rule.Values.SequenceEqual(new[] { "^keep-" }, StringComparer.Ordinal)));

                var actual = repository.QueryAsync(allAssetsActualQuery).GetAwaiter().GetResult();
                var saved = repository.QueryAsync(new AssetLibraryQuery(PageSize: 500) { Document = allAssetsSaved })
                    .GetAwaiter().GetResult();
                CollectionAssert.AreEqual(
                    actual.Items.Select(item => item.AssetId).ToArray(),
                    saved.Items.Select(item => item.AssetId).ToArray());
                Assert.HasCount(2, saved.Items);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
            await repository.DisposeAsync();
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static bool IsInteractiveInstance(XElement element) => element.Name.LocalName switch
    {
        "Button" or "ToggleButton" or "TextBox" or "ComboBox" or "ListBox" or "TreeView" =>
            element.Attribute(XNamespace.Xmlns + "Key") is null && Attribute(element, "x:Key").Length == 0,
        _ => false
    };

    private static int MaximumDepth(AssetQueryNode node) => node.Children.Count == 0 ? 0 : 1 + node.Children.Max(MaximumDepth);

    private static IEnumerable<AssetQueryNode> QueryDescendantsAndSelf(AssetQueryNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var nested in QueryDescendantsAndSelf(child)) yield return nested;
    }

    private static T FindLogical<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) return match;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            try { return FindLogical<T>(child); } catch (AssertFailedException) { }
        throw new AssertFailedException($"{typeof(T).Name} was not found.");
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) count++;
        return count;
    }

    private static Task InvokePrivateTask(AssetLibraryViewModel viewModel, string methodName)
    {
        var method = typeof(AssetLibraryViewModel).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName);
        return (Task)method.Invoke(viewModel, null)!;
    }

    private static void AssertP3BatchCompletion(
        AssetLibraryViewModel viewModel,
        long generation,
        string outcome,
        Guid? operationId)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(AssetLibraryViewModel);
        Assert.AreEqual(generation, type.GetProperty("P3BatchApplyCompletionGeneration", flags)?.GetValue(viewModel));
        Assert.AreEqual(outcome, type.GetProperty("P3BatchApplyCompletionOutcome", flags)?.GetValue(viewModel)?.ToString());
        Assert.AreEqual(operationId, type.GetProperty("P3BatchApplyCompletionOperationId", flags)?.GetValue(viewModel));
    }

    private static void InvokePrivateVoid(AssetLibraryViewModel viewModel, string methodName, object? argument)
    {
        var method = typeof(AssetLibraryViewModel).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName);
        method.Invoke(viewModel, [argument]);
    }

    private static void ExecuteSql(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static double Contrast(string foreground, string background)
    {
        static double Luminance(string value)
        {
            var rgb = new[] { value[1..3], value[3..5], value[5..7] }
                .Select(component => Convert.ToInt32(component, 16) / 255d)
                .Select(component => component <= .04045d ? component / 12.92d : Math.Pow((component + .055d) / 1.055d, 2.4d))
                .ToArray();
            return .2126d * rgb[0] + .7152d * rgb[1] + .0722d * rgb[2];
        }
        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + .05d) / (Math.Min(first, second) + .05d);
    }

    private static XDocument Load(string file) => XDocument.Load(ModulePath(file));
    private static string ModulePath(string file) => FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", file);
    private static string Attribute(XElement element, string localName)
    {
        if (localName.StartsWith("x:", StringComparison.Ordinal))
            return element.Attribute(XName.Get(localName[2..], "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value ?? string.Empty;
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value ?? string.Empty;
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

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var operation = action();
                _ = operation.ContinueWith(
                    _ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                Dispatcher.Run();
                operation.GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
