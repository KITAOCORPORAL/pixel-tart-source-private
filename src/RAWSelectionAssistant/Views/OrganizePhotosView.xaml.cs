using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class OrganizePhotosView : UserControl
{
    private Point _photoDragStart;
    public OrganizePhotosView()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is OrganizePhotosViewModel viewModel && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await viewModel.AddPathsAsync(paths);
    }

    private void MoveSelectedPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrganizePhotosViewModel viewModel)
            viewModel.MovePhotosToGroup(PhotosGrid.SelectedItems.Cast<RAWSelectionAssistant.Core.Models.OrganizePhotoItem>(), viewModel.SelectedGroup);
    }

    private void PhotosGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _photoDragStart=e.GetPosition(PhotosGrid);
    private void PhotosGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed)return;
        var point=e.GetPosition(PhotosGrid);
        if(Math.Abs(point.X-_photoDragStart.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-_photoDragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
        var photos=PhotosGrid.SelectedItems.Cast<OrganizePhotoItem>().ToArray();
        if(photos.Length>0)DragDrop.DoDragDrop(PhotosGrid,photos,DragDropEffects.Move);
    }
    private void GroupsList_DragOver(object sender, DragEventArgs e){e.Effects=e.Data.GetDataPresent(typeof(OrganizePhotoItem[]))?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;}
    private void GroupsList_Drop(object sender, DragEventArgs e)
    {
        if(DataContext is not OrganizePhotosViewModel viewModel||e.Data.GetData(typeof(OrganizePhotoItem[])) is not OrganizePhotoItem[] photos)return;
        var target=FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PhotoGroupDefinition??viewModel.SelectedGroup;
        viewModel.MovePhotosToGroup(photos,target);e.Handled=true;
    }
    private static T? FindAncestor<T>(DependencyObject? value) where T:DependencyObject{while(value is not null){if(value is T found)return found;value=value is Visual?VisualTreeHelper.GetParent(value):LogicalTreeHelper.GetParent(value);}return null;}
}
