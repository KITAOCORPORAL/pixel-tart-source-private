using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using static RAWSelectionAssistant.WpfTests.RuntimeUserFindingsBatchATests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ManualAcceptanceInteractionTests
{
    [TestMethod]
    public void EverySliderStepChangesEveryPhotoSizeWithoutEarlySaturation()
    {
        var ratios=new[]{1.5,2d/3,1d,16d/9,9d/16,.1,10};
        foreach(var scale in new[]{1d,1.25,1.5,2d})
        foreach(var pixels in new[]{1180d,1600d,1920d})
        foreach(var mode in new[]{AssetLibraryViewMode.Grid,AssetLibraryViewMode.Masonry,AssetLibraryViewMode.Justified})
        {
            var width=Math.Max(280,pixels/scale-400);
            var previous=new double[ratios.Length];
            for(var step=0;step<=100;step++)
            {
                var target=120+(width-144)*step/100;
                var layout=AssetLayoutEngine.Arrange(mode,ratios,width,target);
                for(var i=0;i<ratios.Length;i++)
                {
                    var rect=layout.Items[i];
                    Assert.IsGreaterThan(previous[i],rect.Width,$"{mode}/{scale}/{pixels}/{step}/{i} plateau");
                    Assert.AreEqual(ratios[i],rect.Width/(rect.Height-40),.0001);
                    Assert.IsLessThanOrEqualTo(width+.01,rect.Right);
                    previous[i]=rect.Width;
                }
            }
        }
    }

    [TestMethod]
    public void ScrollbarWidthCannotSaturateTheLastPartOfTheSlider()
    {
        foreach (var scrollbar in new[] { 0d, 17d, 24d })
        foreach (var mode in new[] { AssetLibraryViewMode.Grid, AssetLibraryViewMode.Masonry, AssetLibraryViewMode.Justified })
        {
            const double outerWidth = 850;
            const double sliderMaximum = outerWidth - 24;
            var viewport = outerWidth - scrollbar;
            var previous = 0d;
            for (var step = 0; step <= 100; step++)
            {
                var target = 120 + (sliderMaximum - 120) * step / 100;
                var layout = AssetLayoutEngine.Arrange(mode, [1.5], viewport, target, sliderMaximum);
                Assert.IsGreaterThan(previous, layout.Items[0].Width, $"{mode}/{scrollbar}/{step}");
                previous = layout.Items[0].Width;
            }
            Assert.AreEqual(viewport, previous, .001);
        }
    }

    [TestMethod]
    public Task GalleryPanelUsesTheBoundSliderMaximumWithItsOwnViewport() => RunSta(async () =>
    {
        var root = await Fixture();
        await using var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(System.IO.Path.Combine(root, "assets.db"), new RAWSelectionAssistant.Core.Services.Tasks.TaskOperationBridge(), []);
        await page.InitializeForSessionAsync();
        var vm = (AssetLibraryViewModel)page.DataContext;
        page.Measure(new Size(1600, 920)); page.Arrange(new Rect(0, 0, 1600, 920)); page.UpdateLayout();
        var panel = Descendants(page).OfType<VirtualizingAssetPanel>().Single();
        Assert.AreEqual(vm.ThumbnailMaximumWidth, panel.ThumbnailMaximumWidth, .001);
        var prior = 0d;
        for (var step = 0; step <= 20; step++)
        {
            vm.ThumbnailWidth = 120 + (vm.ThumbnailMaximumWidth - 120) * step / 20;
            page.UpdateLayout();
            var card = Descendants(panel).OfType<ListBoxItem>().First();
            Assert.IsGreaterThan(prior, card.ActualWidth, $"Bound gallery step {step}");
            prior = card.ActualWidth;
        }
        Assert.AreEqual(panel.ViewportWidth, prior, .01);
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    [TestMethod]
    public Task InspectorReorderAndCollapseSurviveSerializationKeepingSameControls() => RunSta(async()=>
    {
        var settings=new AssetLibraryWorkspaceSettings();
        var panel=new InspectorSectionPanel{Settings=settings};
        var rating=new TextBox{Text="4"};
        var first=new Expander{Header="图片信息",IsExpanded=true};
        var second=new Expander{Header="评分与颜色",Content=rating,IsExpanded=true};
        panel.Children.Add(first);panel.Children.Add(second);panel.InitializeSections();
        panel.MoveSection("评分与颜色",0);second.IsExpanded=false;
        Assert.AreSame(second,panel.Children[0]);Assert.AreSame(rating,second.Content);
        var restored=JsonSerializer.Deserialize<AssetLibraryWorkspaceSettings>(JsonSerializer.Serialize(settings))!;restored.Normalize();
        var reopened=new InspectorSectionPanel{Settings=restored};reopened.Children.Add(new Expander{Header="图片信息"});reopened.Children.Add(new Expander{Header="评分与颜色",IsExpanded=true});reopened.InitializeSections();
        Assert.AreEqual("评分与颜色",((Expander)reopened.Children[0]).Header);Assert.IsFalse(((Expander)reopened.Children[0]).IsExpanded);
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task NewFolderSelectsItsQueryAndUnsupportedConversionIsDisabled() => RunSta(async()=>
    {
        var root=await Fixture();await using var vm=ViewModel(root);await vm.InitializeAsync();
        vm.NewFolderName="验收新文件夹";vm.NewFolderCommand.Execute(null);await vm.NewFolderCommand.ExecutionTask;
        Assert.AreEqual("验收新文件夹",vm.SelectedFolder?.Name);Assert.IsEmpty(vm.AssetCards);
        var parent = vm.SelectedFolder!;
        vm.NewFolderName = "验收子文件夹"; vm.NewSubfolderCommand.Execute(null); await vm.NewSubfolderCommand.ExecutionTask;
        Assert.AreEqual(parent.FolderId, vm.SelectedFolder?.ParentFolderId);
        Assert.AreEqual("验收子文件夹", vm.SelectedFolder?.Name);
        vm.SelectSystemCollection(AssetLibrarySystemCollection.AllAssets);vm.RefreshCommand.Execute(null);await vm.RefreshCommand.ExecutionTask;vm.SyncSelection([vm.AssetCards[0].Asset]);
        vm.SelectionToolHandler=(_,_)=>Task.CompletedTask;
        Assert.IsFalse(vm.SelectionToolCommand.CanExecute("RawToJpeg"));
        Assert.IsTrue(vm.SelectionToolCommand.CanExecute("Export"));
    });
    [TestMethod]
    public Task SharedMenuStyleAndScrollBoundsApplyToNewCascadeItems() => RunSta(async()=>
    {
        var resources=(ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/Resources/DesignSystem/Controls.Menu.xaml",UriKind.Relative));
        var item=new MenuItem { Header="视觉分析", Style=(Style)resources[typeof(MenuItem)] };
        item.Items.Add(new MenuItem { Header="提取颜色" });item.ApplyTemplate();
        Assert.IsTrue(ContextMenuPlacement.GetEnabled(item));
        var popup=(System.Windows.Controls.Primitives.Popup)item.Template.FindName("PART_Popup",item);
        Assert.AreEqual(0d,popup.HorizontalOffset);Assert.AreEqual(0d,popup.VerticalOffset);
        var separator=new Separator { Style=(Style)resources[MenuItem.SeparatorStyleKey] };
        separator.ApplyTemplate();separator.Measure(new Size(240,100));separator.Arrange(new Rect(0,0,240,12));
        Assert.IsTrue(separator.OverridesDefaultStyle);Assert.IsLessThanOrEqualTo(240d,separator.ActualWidth);
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task TrashMenuExposesTopLevelRestoreForTheSelectedSet() => RunSta(async () =>
    {
        var root = await Fixture();
        await using var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(System.IO.Path.Combine(root, "assets.db"), new RAWSelectionAssistant.Core.Services.Tasks.TaskOperationBridge(), []);
        await page.InitializeForSessionAsync();
        var vm = (AssetLibraryViewModel)page.DataContext;
        vm.SyncSelection(vm.AssetCards.Select(card => card.Asset).ToArray());
        var count = vm.SelectedAssetIds.Count;
        vm.TrashContextCommand.Execute(vm.AssetCards[0]);
        await vm.TrashContextCommand.ExecutionTask;
        vm.SelectSystemCollection(AssetLibrarySystemCollection.RecycleBin);
        vm.RefreshCommand.Execute(null); await vm.RefreshCommand.ExecutionTask;
        vm.SyncSelection(vm.AssetCards.Select(card => card.Asset).ToArray());
        var card = vm.AssetCards[0];
        var menu = new ContextMenu();
        var management = new MenuItem { Header = "管理" };
        management.Items.Add(new MenuItem { Header = "恢复", Command = vm.RestoreTrashContextCommand, CommandParameter = card });
        menu.Items.Add(management);
        menu.DataContext = card;
        page.ConfigureTrashContextActions(menu, card);
        var visible = menu.Items.OfType<MenuItem>().Where(item => item.Visibility == Visibility.Visible).ToArray();
        Assert.HasCount(2, visible);
        var restore = visible.Single(item => item.Tag as string == "RestoreTrashRecords");
        Assert.IsTrue(restore.Command.CanExecute(restore.CommandParameter));
        restore.Command.Execute(restore.CommandParameter);
        await vm.RestoreTrashContextCommand.ExecutionTask;
        Assert.IsEmpty(vm.AssetCards);
        vm.SelectSystemCollection(AssetLibrarySystemCollection.AllAssets);
        vm.RefreshCommand.Execute(null); await vm.RefreshCommand.ExecutionTask;
        Assert.HasCount(count, vm.AssetCards);
        page.ConfigureTrashContextActions(menu, vm.AssetCards[0]);
        Assert.AreEqual(Visibility.Collapsed, restore.Visibility);
    });

    [TestMethod]
    public void EveryRuleFieldRejectsOperatorsOutsideItsContract()
    {
        var root=P3QueryNodeView.CreateRoot(()=>{});root.AddRuleCommand.Execute(null);var rule=root.Children[0];
        foreach(var field in Enum.GetValues<AssetQueryField>())
        {
            rule.Field=field;
            Assert.IsTrue(rule.OperatorOptions.Any(option=>option.Value==rule.Operator),field.ToString());
            foreach(var option in rule.OperatorOptions)
            {
                rule.Operator=option.Value;
                Assert.AreEqual(option.Value,rule.Operator);
            }
        }
        rule.Field = AssetQueryField.Rating;
        Assert.IsTrue(rule.OperatorOptions.Any(option => option.Value == AssetQueryOperator.Between));
        Assert.IsFalse(rule.OperatorOptions.Any(option => option.Value == AssetQueryOperator.Contains));
        rule.Field = AssetQueryField.Tag;
        CollectionAssert.AreEquivalent(new[] { AssetQueryOperator.AnyOf, AssetQueryOperator.AllOf, AssetQueryOperator.NoneOf }, rule.OperatorOptions.Select(option => option.Value).ToArray());
        rule.Field = AssetQueryField.IsMissing;
        CollectionAssert.AreEquivalent(new[] { AssetQueryOperator.IsTrue, AssetQueryOperator.IsFalse }, rule.OperatorOptions.Select(option => option.Value).ToArray());
    }

}
