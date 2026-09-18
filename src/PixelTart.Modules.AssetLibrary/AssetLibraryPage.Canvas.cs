using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage
{
    private FreeCanvasView? _canvas;
    private readonly List<InspirationTrayCardView> _boardSelection=[];
    public FreeCanvasView? ActiveCanvas => _canvas;
    private Window? _canvasOwner;
    private bool _allowCanvasOwnerClose;
    private async Task OpenSavedCanvasMenuAsync(FrameworkElement target)
    {
        var menu=new ContextMenu { PlacementTarget=target };
        foreach(var document in await new CanvasDocumentStore(_viewModel.CanvasDirectory).ListAsync())
        {
            var item=new MenuItem { Header=document.Name };
            item.Click+=async(_,_)=>await ShowCanvasAsync(document);menu.Items.Add(item);
        }
        if(menu.Items.Count==0)menu.Items.Add(new MenuItem { Header="暂无已保存画布",IsEnabled=false });
        menu.IsOpen=true;
    }
    public Task OpenCanvasAsync(IReadOnlyList<AssetItem> assets)=>OpenCanvasReferencesAsync(assets.Select(_viewModel.CanvasReference).ToArray());
    public async Task OpenCanvasReferencesAsync(IReadOnlyList<CanvasObject> references)
    {
        var editor=new CanvasEditor(new());editor.Add(references);editor.Arrange("grid");editor.Select(null);
        await ShowCanvasAsync(editor.Document);
    }
    public async Task ShowCanvasAsync(CanvasDocument document)
    {
        if(_canvas is not null&&!await _canvas.FlushAsync())return;
        var store=new CanvasDocumentStore(_viewModel.CanvasDirectory);var editor=new CanvasEditor(document);
        var canvas=new FreeCanvasView(editor,_previewProvider,store);
        _canvas=canvas;
        canvas.SourceLoader=(source,search)=>_viewModel.LoadCanvasSourcesAsync(source,search,editor.Document.ProjectId);
        canvas.SaveBoard=(objects,target)=>_viewModel.SaveCanvasBoardAsync(objects,target,editor.Document.ProjectId);
        canvas.BoardLoader=_viewModel.CanvasBoardsAsync;
        canvas.ViewAsset=item=>{var window=new AssetViewerWindow([item.SourcePath],0,_previewProvider){Owner=Window.GetWindow(this)};window.Show();return Task.CompletedTask;};
        canvas.RevealAsset=async item=>{if(await canvas.FlushAsync()){await _viewModel.RevealCanvasAssetAsync(item);HideCanvas();}};
        canvas.AnalyzePalette=_viewModel.AnalyzeCanvasPaletteAsync;
        canvas.CloseRequested=()=>{HideCanvas();return Task.CompletedTask;};canvas.OpenDocument=ShowCanvasAsync;
        var projects=await _viewModel.CanvasProjectsAsync();
        var picker=new ComboBox{Width=220,Margin=new(8,2,8,2),ItemsSource=projects.Select(item=>new CanvasProjectChoice(item.Id,item.Name)).ToArray(),DisplayMemberPath=nameof(CanvasProjectChoice.Name),SelectedValuePath=nameof(CanvasProjectChoice.Id),ToolTip="关联项目"};
        picker.SelectedItem=picker.Items.Cast<CanvasProjectChoice>().FirstOrDefault(item=>item.Id==document.ProjectId)??picker.Items[0];
        picker.SelectionChanged+=(_,_)=>{if(picker.SelectedItem is CanvasProjectChoice item)editor.SetProject(item.Id);};canvas.HeaderPanel.Children.Add(picker);
        var analyzeProject = new Button { Content="保存配色 / 影调到项目", Margin=new(4), ToolTip="分析选中的照片并保存项目视觉参考" };
        analyzeProject.SetResourceReference(StyleProperty,"PixelTart.Button.Ghost");
        analyzeProject.Click += async (_,_) =>
        {
            var selected = editor.Selected.Where(item=>item.IsImage).ToArray();
            var assets = new List<AssetItem>();
            foreach(var item in selected)
                if(item.LibraryId==_viewModel.CanvasLibraryId && item.AssetId is Guid id && await _viewModel.GetAssetForAnalysisAsync(id) is {} asset) assets.Add(asset);
            if(assets.Count==0){MessageBox.Show(Window.GetWindow(this),"请先选择画布中的照片。","项目视觉参考");return;}
            _visualSourceKind="Canvas";_visualContainerId=editor.Document.CanvasId;_visualSurfaceAssets=assets;
            VisualAnalysisContextTabs.SelectedIndex=0;await RefreshVisualSurfaceAsync();
            VisualProjectPicker.SelectedItem=VisualProjectPicker.Items.Cast<VisualProjectChoice>().FirstOrDefault(item=>item.Id==editor.Document.ProjectId);
            LibraryWorkspace.Visibility=Visibility.Visible;CanvasWorkspace.Visibility=Visibility.Collapsed;VisualAnalysisSurface.Visibility=Visibility.Visible;
        };
        canvas.HeaderPanel.Children.Add(analyzeProject);
        editor.Changed+=(_,_)=>{var choice=picker.Items.Cast<CanvasProjectChoice>().FirstOrDefault(item=>item.Id==editor.Document.ProjectId);if(choice is not null&&!Equals(choice,picker.SelectedItem))picker.SelectedItem=choice;};
        LibraryWorkspace.Visibility=Visibility.Collapsed;
        CanvasWorkspace.Content=canvas;CanvasWorkspace.Visibility=Visibility.Visible;
        if(_canvasOwner is null&&Window.GetWindow(this) is {} owner){_canvasOwner=owner;owner.Closing+=CanvasOwnerClosing;}
        canvas.UpdateLayout();await canvas.Surface.LoadPreviewsAsync();await canvas.FlushAsync();
    }
    private void HideCanvas(){CanvasWorkspace.Visibility=Visibility.Collapsed;CanvasWorkspace.Content=null;_canvas=null;LibraryWorkspace.Visibility=Visibility.Visible;}
    private async void CanvasOwnerClosing(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(_allowCanvasOwnerClose||_canvas is null)return;e.Cancel=true;
        if(await _canvas.FlushAsync()){_allowCanvasOwnerClose=true;_canvasOwner?.Close();}
        else MessageBox.Show(_canvasOwner,"画布尚未保存成功，请检查存储位置后重试。","画布保存失败",MessageBoxButton.OK,MessageBoxImage.Warning);
    }
    private void CanvasBoardSelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(sender is not ListBox list)return;_boardSelection.Clear();_boardSelection.AddRange(list.SelectedItems.Cast<InspirationTrayCardView>());
    }
    private async void OpenBoardCanvas_Click(object sender,RoutedEventArgs e)
    {
        if(_boardSelection.Count==0){MessageBox.Show(Window.GetWindow(this),"请先选择灵感板中的照片。","自由画布",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        await OpenCanvasReferencesAsync(await _viewModel.CanvasBoardReferencesAsync(_boardSelection.ToArray()));
    }
    private async void ContextCanvas_Click(object sender,RoutedEventArgs e)
    {
        if(sender is FrameworkElement{DataContext:AssetVisualMatchView card}&&!_viewModel.SelectedAssetIds.Contains(card.Asset.AssetId))_viewModel.SyncSelection([card.Asset]);
        await OpenCanvasAsync(_viewModel.CaptureOrderedSelection());e.Handled=true;
    }
    private sealed record CanvasProjectChoice(Guid? Id,string Name);
}
