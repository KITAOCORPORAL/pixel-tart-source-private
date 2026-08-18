using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP1SettingsTests
{
    [TestMethod]
    public void PrimaryNavigationPolicy_HasTheExactSevenPagesAndSafeAliases()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "Workbench",
                "AssetLibrary",
                "Workflow",
                "WorkCalendar",
                "Tether",
                "Finance",
                "History"
            },
            PrimaryNavigationPolicy.OrderedPages.ToArray());

        Assert.AreEqual(PrimaryNavigationPolicy.AssetLibrary, PrimaryNavigationPolicy.Normalize("asset-library"));
        Assert.AreEqual(PrimaryNavigationPolicy.Workbench, PrimaryNavigationPolicy.Normalize("ProjectCenter"));
        Assert.AreEqual(PrimaryNavigationPolicy.Workbench, PrimaryNavigationPolicy.Normalize("Toolbox"));
        Assert.AreEqual(PrimaryNavigationPolicy.Workbench, PrimaryNavigationPolicy.Normalize(null));
        Assert.IsFalse(PrimaryNavigationPolicy.IsPrimaryPage("OnlineSelection"));
    }

    [TestMethod]
    public async Task Settings_RoundTripLastPrimaryPageAndAssetWorkspaceLayout()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        var service = new SettingsService(new TestLogService(), path);
        var folderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tagId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var smartFolderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var settings = new AppSettings
        {
            LastPrimaryPage = PrimaryNavigationPolicy.AssetLibrary,
            AssetLibraryWorkspace = new AssetLibraryWorkspaceSettings
            {
                OrganizationPaneWidth = 286,
                InspectorPaneWidth = 414,
                OrganizationPaneCollapsed = true,
                InspectorPaneCollapsed = false,
                InspectorPinned = true,
                ThumbnailWidth = 232,
                SearchText = "  portrait reference  ",
                SelectedFolderId = folderId,
                SelectedTagId = tagId,
                SelectedSmartFolderId = smartFolderId
            }
        };

        await service.SaveAsync(settings);
        var restored = await new SettingsService(new TestLogService(), path).LoadAsync();

        Assert.AreEqual(PrimaryNavigationPolicy.AssetLibrary, restored.LastPrimaryPage);
        Assert.AreEqual(286d, restored.AssetLibraryWorkspace.OrganizationPaneWidth);
        Assert.AreEqual(414d, restored.AssetLibraryWorkspace.InspectorPaneWidth);
        Assert.IsTrue(restored.AssetLibraryWorkspace.OrganizationPaneCollapsed);
        Assert.IsFalse(restored.AssetLibraryWorkspace.InspectorPaneCollapsed);
        Assert.IsTrue(restored.AssetLibraryWorkspace.InspectorPinned);
        Assert.AreEqual(232d, restored.AssetLibraryWorkspace.ThumbnailWidth);
        Assert.AreEqual("portrait reference", restored.AssetLibraryWorkspace.SearchText);
        Assert.AreEqual(folderId, restored.AssetLibraryWorkspace.SelectedFolderId);
        Assert.AreEqual(tagId, restored.AssetLibraryWorkspace.SelectedTagId);
        Assert.AreEqual(smartFolderId, restored.AssetLibraryWorkspace.SelectedSmartFolderId);
    }

    [TestMethod]
    public async Task Settings_UpgradeNormalizesInvalidPrimaryPageAndLayoutBounds()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        var service = new SettingsService(new TestLogService(), path);
        var settings = new AppSettings
        {
            LastPrimaryPage = "Toolbox",
            AssetLibraryWorkspace = new AssetLibraryWorkspaceSettings
            {
                OrganizationPaneWidth = 12,
                InspectorPaneWidth = 5000,
                InspectorPaneCollapsed = true,
                InspectorPinned = true,
                ThumbnailWidth = 1,
                SearchText = $"  {new string('x', 510)}  "
            }
        };

        await service.SaveAsync(settings);
        var restored = await service.LoadAsync();

        Assert.AreEqual(PrimaryNavigationPolicy.Workbench, restored.LastPrimaryPage);
        Assert.AreEqual(180d, restored.AssetLibraryWorkspace.OrganizationPaneWidth);
        Assert.AreEqual(520d, restored.AssetLibraryWorkspace.InspectorPaneWidth);
        Assert.IsTrue(restored.AssetLibraryWorkspace.InspectorPinned);
        Assert.IsFalse(restored.AssetLibraryWorkspace.InspectorPaneCollapsed);
        Assert.AreEqual(120d, restored.AssetLibraryWorkspace.ThumbnailWidth);
        Assert.AreEqual(500, restored.AssetLibraryWorkspace.SearchText.Length);
        Assert.IsTrue(restored.AssetLibraryWorkspace.SearchText.All(character => character == 'x'));

        restored.AssetLibraryWorkspace.OrganizationPaneWidth = double.NaN;
        restored.AssetLibraryWorkspace.InspectorPaneWidth = double.PositiveInfinity;
        restored.AssetLibraryWorkspace.ThumbnailWidth = double.NegativeInfinity;
        restored.AssetLibraryWorkspace.Normalize();
        Assert.AreEqual(AssetLibraryWorkspaceSettings.DefaultOrganizationPaneWidth, restored.AssetLibraryWorkspace.OrganizationPaneWidth);
        Assert.AreEqual(AssetLibraryWorkspaceSettings.DefaultInspectorPaneWidth, restored.AssetLibraryWorkspace.InspectorPaneWidth);
        Assert.AreEqual(AssetLibraryWorkspaceSettings.DefaultThumbnailWidth, restored.AssetLibraryWorkspace.ThumbnailWidth);
    }
}
