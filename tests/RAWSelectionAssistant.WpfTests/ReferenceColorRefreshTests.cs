using System.IO;
using System.Collections.Specialized;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ReferenceColorRefreshTests
{
    [TestMethod]
    public async Task BoundChoiceReset_DoesNotEraseSelectedSchemeOrReferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart-ReferenceRefresh", Guid.NewGuid().ToString("N"));
        var store = new ReferenceLookStore(directory);
        var project = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var look = CreateLook("Refresh regression", project, now);
        await store.SaveAsync(look, projectDefault: true);
        using var vm = new TetherReferenceModeViewModel(store);
        // Models ComboBox/ListBox SelectedItem=null on ObservableCollection.Reset.
        vm.Looks.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) vm.SelectedLook = null; };
        vm.SourceChoices.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) vm.SelectedLook = null; };
        await vm.SetProjectAsync(project);
        Assert.AreEqual(look.ReferenceLookId, vm.SelectedLook?.ReferenceLookId);
        await vm.LoadAsync();
        Assert.AreEqual(look.ReferenceLookId, vm.SelectedLook?.ReferenceLookId);
        Assert.AreEqual("Refresh regression", vm.CurrentLookText);
        Directory.Delete(directory, true);
    }

    [TestMethod]
    public async Task ExplicitlyClearingScheme_IsNotSuppressed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart-ReferenceRefresh", Guid.NewGuid().ToString("N"));
        using var vm = new TetherReferenceModeViewModel(new ReferenceLookStore(directory));
        var now = DateTimeOffset.UtcNow;
        vm.SelectedLook = CreateLook("Temporary", null, now);
        vm.SelectedLook = null;
        Assert.IsNull(vm.SelectedLook);
        Assert.IsEmpty(vm.ReferenceSources);
        Assert.IsFalse(vm.IsBusy);
        await Task.CompletedTask;
    }
    private static ReferenceLook CreateLook(string name, Guid? project, DateTimeOffset now)
    {
        var pixels = new VisualPixelBuffer(16, 16, Enumerable.Range(0, 256).SelectMany(i => new[] { (byte)i, (byte)100, (byte)180 }).ToArray());
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "synthetic", pixels));
        var source = new ReferenceLookSource(Guid.NewGuid(), analysis.AssetId, "Synthetic", "synthetic.png", "synthetic", 1, analysis);
        return new ReferenceLook(Guid.NewGuid(), name, project, [source], new(), now, now);
    }
}
