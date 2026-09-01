using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibrarySmartFolderEditorWpfTests
{
    [TestMethod]
    public async Task ExistingRulesLoadIntoEditorAndSavePreservesUnknownRulesAndRuleIds()
    {
        await RunSta(() =>
        {
            var root = CreateTempRoot();
            AssetLibraryViewModel? viewModel = null;
            try
            {
                var databasePath = Path.Combine(root, "editor.db");
                var folderId = Guid.NewGuid();
                var from = DateTimeOffset.UtcNow.Date.AddDays(-7);
                var to = DateTimeOffset.UtcNow.Date.AddDays(1);
                var unknownId = Guid.NewGuid();
                SmartFolderRule[] rules =
                [
                    Rule(folderId, SmartFolderField.FileName, SmartFolderOperator.Contains, "海边"),
                    Rule(folderId, SmartFolderField.Extension, SmartFolderOperator.Equals, ".png"),
                    Rule(folderId, SmartFolderField.MediaType, SmartFolderOperator.Equals, "image/png"),
                    Rule(folderId, SmartFolderField.Folder, SmartFolderOperator.Equals, "参考"),
                    Rule(folderId, SmartFolderField.Tag, SmartFolderOperator.Equals, "像素"),
                    Rule(folderId, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, "3"),
                    Rule(folderId, SmartFolderField.AddedAt, SmartFolderOperator.GreaterThanOrEqual, from.ToString("O")),
                    Rule(folderId, SmartFolderField.AddedAt, SmartFolderOperator.LessThanOrEqual, to.ToString("O")),
                    Rule(folderId, SmartFolderField.IsMissing, SmartFolderOperator.IsFalse),
                    Rule(folderId, SmartFolderField.VisualAnalysisStatus, SmartFolderOperator.Equals, "Analyzed"),
                    new(unknownId, folderId, SmartFolderField.Comment, SmartFolderOperator.Contains, "保留旧字段", SortOrder: 10)
                ];
                _ = SeedSmartFolder(databasePath, new(folderId, "海边参考"), rules);

                viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.SelectedSmartFolder = viewModel.SmartFolders.Single(item => item.SmartFolderId == folderId);
                WaitForEditorLoad(viewModel);

                Assert.AreEqual("海边", viewModel.SmartFileNameValue);
                Assert.AreEqual(".png", viewModel.SmartExtensionValue);
                Assert.AreEqual("image/png", viewModel.SmartMediaTypeValue);
                Assert.AreEqual("参考", viewModel.SmartFolderValue);
                Assert.AreEqual("像素", viewModel.SmartTagValue);
                Assert.AreEqual("3", viewModel.SmartRuleValue);
                Assert.AreEqual("Present", viewModel.SmartMissingValue);
                Assert.AreEqual(from.ToString("O"), viewModel.SmartAddedAtFrom);
                Assert.AreEqual(to.ToString("O"), viewModel.SmartAddedAtTo);
                Assert.AreEqual("Analyzed", viewModel.SmartAnalysisStatus);

                viewModel.SmartFolderName = "海边参考（更新）";
                viewModel.SmartFileNameValue = "海边日落";
                InvokeSave(viewModel).GetAwaiter().GetResult();

                var verification = OpenRepository(databasePath);
                try
                {
                    var savedFolder = verification.ListSmartFoldersAsync().GetAwaiter().GetResult().Single(item => item.SmartFolderId == folderId);
                    var savedRules = verification.ListSmartFolderRulesAsync(folderId).GetAwaiter().GetResult();
                    Assert.AreEqual("海边参考（更新）", savedFolder.Name);
                    Assert.AreEqual(unknownId, savedRules.Single(item => item.Field == SmartFolderField.Comment).RuleId);
                    Assert.AreEqual(2, savedRules.Count(item => item.Field == SmartFolderField.AddedAt));
                    Assert.AreNotEqual(
                        savedRules.Single(item => item.Field == SmartFolderField.AddedAt && item.Operator == SmartFolderOperator.GreaterThanOrEqual).RuleId,
                        savedRules.Single(item => item.Field == SmartFolderField.AddedAt && item.Operator == SmartFolderOperator.LessThanOrEqual).RuleId,
                        "Lower/upper date bounds must never share a RuleId.");
                    CollectionAssert.AreEquivalent(
                        rules.Select(item => item.Field).ToArray(),
                        savedRules.Select(item => item.Field).ToArray());
                    Assert.AreEqual("海边日落", savedRules.Single(item => item.Field == SmartFolderField.FileName).Value);
                }
                finally { verification.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            }
            finally { DisposeViewModel(viewModel); DeleteTempRoot(root); }
        });
    }

    [TestMethod]
    public async Task MissingConditionDefaultsToAnyAndRoundTripsBothPolarityValues()
    {
        await RunSta(() =>
        {
            var root = CreateTempRoot();
            AssetLibraryViewModel? viewModel = null;
            try
            {
                var databasePath = Path.Combine(root, "missing.db");
                var folderId = Guid.NewGuid();
                _ = SeedSmartFolder(databasePath, new(folderId, "缺失条件"),
                    [Rule(folderId, SmartFolderField.Tag, SmartFolderOperator.Equals, "素材")]);
                viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.SelectedSmartFolder = viewModel.SmartFolders.Single(item => item.SmartFolderId == folderId);
                WaitForEditorLoad(viewModel);
                Assert.AreEqual("Any", viewModel.SmartMissingValue, "A legacy folder without IsMissing must not be treated as missing.");

                viewModel.SmartMissingValue = "Present";
                InvokeSave(viewModel).GetAwaiter().GetResult();
                var verification = OpenRepository(databasePath);
                try
                {
                    var presentRule = verification.ListSmartFolderRulesAsync(folderId).GetAwaiter().GetResult().Single(item => item.Field == SmartFolderField.IsMissing);
                    Assert.AreEqual(SmartFolderOperator.IsFalse, presentRule.Operator);
                }
                finally { verification.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

                viewModel.SmartMissingValue = "Missing";
                InvokeSave(viewModel).GetAwaiter().GetResult();
                var finalVerification = OpenRepository(databasePath);
                try
                {
                    var missingRule = finalVerification.ListSmartFolderRulesAsync(folderId).GetAwaiter().GetResult().Single(item => item.Field == SmartFolderField.IsMissing);
                    Assert.AreEqual(SmartFolderOperator.IsTrue, missingRule.Operator);
                }
                finally
                {
                    finalVerification.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            finally { DisposeViewModel(viewModel); DeleteTempRoot(root); }
        });
    }

    [TestMethod]
    public async Task LegacyVisualRulesRemainWhenAddingABasicEditorRule()
    {
        await RunSta(() =>
        {
            var root = CreateTempRoot();
            AssetLibraryViewModel? viewModel = null;
            try
            {
                var databasePath = Path.Combine(root, "legacy.db");
                var folderId = Guid.NewGuid();
                SmartFolderRule[] rules =
                [
                    Rule(folderId, SmartFolderField.Tag, SmartFolderOperator.Equals, "旧标签"),
                    Rule(folderId, SmartFolderField.VisualToneKey, SmartFolderOperator.Equals, "Low"),
                    Rule(folderId, SmartFolderField.VisualAnalysisStatus, SmartFolderOperator.Equals, "Analyzed"),
                    Rule(folderId, SmartFolderField.VisualAverageSaturation, SmartFolderOperator.LessThanOrEqual, ".30"),
                    Rule(folderId, SmartFolderField.VisualDominantHue, SmartFolderOperator.InRange, "80..150")
                ];
                SeedSmartFolder(databasePath, new(folderId, "旧规则"), rules);
                viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                viewModel.SelectedSmartFolder = viewModel.SmartFolders.Single(item => item.SmartFolderId == folderId);
                WaitForEditorLoad(viewModel);
                Assert.AreEqual("旧标签", viewModel.SmartTagValue);
                Assert.AreEqual("Low", viewModel.SmartToneKey);
                Assert.AreEqual("80..150", viewModel.SmartDominantHueRange);
                Assert.AreEqual(string.Empty, viewModel.SmartFileNameValue);

                viewModel.SmartFileNameValue = "新增基础条件";
                InvokeSave(viewModel).GetAwaiter().GetResult();
                var verification = OpenRepository(databasePath);
                try
                {
                    var saved = verification.ListSmartFolderRulesAsync(folderId).GetAwaiter().GetResult();
                    CollectionAssert.IsSubsetOf(
                        rules.Select(item => item.Field).ToArray(),
                        saved.Select(item => item.Field).ToArray());
                    Assert.AreEqual("新增基础条件", saved.Single(item => item.Field == SmartFolderField.FileName).Value);
                    Assert.AreEqual("80..150", saved.Single(item => item.Field == SmartFolderField.VisualDominantHue).Value);
                }
                finally
                {
                    verification.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            finally { DisposeViewModel(viewModel); DeleteTempRoot(root); }
        });
    }

    [TestMethod]
    public void SmartFolderEditorXamlExposesStableBasicFieldAutomationIds()
    {
        var xamlPath = FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml");
        var document = XDocument.Load(xamlPath);
        var text = document.ToString(SaveOptions.DisableFormatting);
        foreach (var id in new[]
        {
            "SmartFolderName", "SmartFolderEditorStatus", "SmartFolderFileName", "SmartFolderExtension",
            "SmartFolderMediaType", "SmartFolderFolder", "SmartFolderTag", "SmartFolderRating",
            "SmartFolderMissing", "SmartFolderAddedFrom", "SmartFolderAddedTo", "SmartFolderVisualAnalysisStatus"
        })
            StringAssert.Contains(text, id);

        var editor = document.Descendants().First(element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal) && attribute.Value == "VisualSmartFolderBuilder"));
        Assert.IsFalse(
            editor.Ancestors().Any(ancestor => ancestor.Attributes().Any(attribute =>
                attribute.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal) && attribute.Value == "AssetInspectorSingleState")),
            "The Smart Folder editor must remain reachable when no asset is selected.");
        foreach (var id in new[]
        {
            "AssetInspectorSourcePath", "AssetInspectorFileSize", "AssetInspectorDimensions", "AssetInspectorCaptureTime", "AssetInspectorAddedTime",
            "AssetInspectorRating", "AssetInspectorMissingState", "AssetInspectorFolders", "AssetInspectorTags"
        })
            StringAssert.Contains(text, id);
    }

    private static SmartFolder SeedSmartFolder(string databasePath, SmartFolder folder, IEnumerable<SmartFolderRule> rules)
    {
        var repository = OpenRepository(databasePath);
        try
        {
            repository.InitializeAsync().GetAwaiter().GetResult();
            return repository.SaveSmartFolderAsync(folder, rules).GetAwaiter().GetResult();
        }
        finally { repository.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private static SqliteAssetLibraryRepository OpenRepository(string databasePath) =>
        new(new AssetLibraryDatabase(databasePath));

    private static SmartFolderRule Rule(
        Guid folderId,
        SmartFolderField field,
        SmartFolderOperator op,
        string value = "") =>
        new(Guid.NewGuid(), folderId, field, op, value);

    private static void WaitForEditorLoad(AssetLibraryViewModel viewModel)
    {
        Assert.IsTrue(
            SpinWait.SpinUntil(() => !viewModel.IsSmartFolderEditorLoading, TimeSpan.FromSeconds(5)),
            "Smart Folder rules did not finish loading.");
    }

    private static Task InvokeSave(AssetLibraryViewModel viewModel)
    {
        var method = typeof(AssetLibraryViewModel).GetMethod("SaveSmartFolderAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return (Task)method!.Invoke(viewModel, null)!;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-SmartFolderEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
    }

    private static void DisposeViewModel(AssetLibraryViewModel? viewModel) =>
        viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();

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

}
