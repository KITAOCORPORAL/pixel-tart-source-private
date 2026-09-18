using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class PlanningCenterView : UserControl
{
    private Point _shotDragStart;
    private ProjectShot? _draggedShot;
    public PlanningCenterView(){InitializeComponent();}
    private PlanningCenterViewModel? ViewModel=>DataContext as PlanningCenterViewModel;
    private void FilterAll_Click(object sender,RoutedEventArgs e){if(ViewModel is not null)ViewModel.SelectedFilter=null;}
    private void FilterPending_Click(object sender,RoutedEventArgs e){if(ViewModel is not null)ViewModel.SelectedFilter=ProjectShotStatus.NotStarted;}
    private void FilterCompleted_Click(object sender,RoutedEventArgs e){if(ViewModel is not null)ViewModel.SelectedFilter=ProjectShotStatus.Completed;}
    private void ReferenceCard_MouseLeftButtonDown(object sender,MouseButtonEventArgs e){if(sender is Border{DataContext:ProjectShotReference reference}&&ViewModel is{} vm){vm.SelectedReference=reference;if(e.ClickCount==2&&vm.OpenQuickPreviewCommand.CanExecute(null))vm.OpenQuickPreviewCommand.Execute(null);}}
    private void PreviousReference_Click(object sender,RoutedEventArgs e)=>MoveReference(-1);
    private void NextReference_Click(object sender,RoutedEventArgs e)=>MoveReference(1);
    private void ReferenceKind_Loaded(object sender,RoutedEventArgs e){if(sender is TextBlock{DataContext:ProjectShotReference reference} text)text.Text=reference.Kind switch{ShotReferenceKind.Lighting=>"灯位",ShotReferenceKind.Pose=>"姿势",ShotReferenceKind.Storyboard=>"分镜",ShotReferenceKind.Styling=>"造型",_=>"普通参考"};}
    private void ShotStatus_Loaded(object sender,RoutedEventArgs e){if(sender is TextBlock{DataContext:ProjectShot shot} text)text.Text=shot.Status switch{ProjectShotStatus.NotStarted=>"待拍",ProjectShotStatus.InProgress=>"当前",ProjectShotStatus.Completed=>"已拍",ProjectShotStatus.Skipped=>"跳过",_=>"待拍"};}
    private void ShotList_PreviewMouseLeftButtonDown(object sender,MouseButtonEventArgs e){_shotDragStart=e.GetPosition(ShotList);_draggedShot=FindShot(e.OriginalSource as DependencyObject);}
    private void ShotList_MouseMove(object sender,MouseEventArgs e){if(e.LeftButton!=MouseButtonState.Pressed||_draggedShot is null)return;var point=e.GetPosition(ShotList);if(Math.Abs(point.X-_shotDragStart.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-_shotDragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;DragDrop.DoDragDrop(ShotList,_draggedShot,DragDropEffects.Move);}
    private void ShotList_DragOver(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(typeof(ProjectShot))?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;}
    private async void ShotList_Drop(object sender,DragEventArgs e){var source=e.Data.GetData(typeof(ProjectShot)) as ProjectShot;var target=FindShot(e.OriginalSource as DependencyObject);if(source is not null&&target is not null&&ViewModel is{} vm)await vm.ReorderShotAsync(source,target);_draggedShot=null;e.Handled=true;}
    private static ProjectShot? FindShot(DependencyObject? origin){for(var current=origin;current is not null;current=System.Windows.Media.VisualTreeHelper.GetParent(current))if(current is ListBoxItem{DataContext:ProjectShot shot})return shot;return null;}
    private void MoveReference(int delta){if(ViewModel is not{} vm||vm.References.Count==0)return;var index=vm.SelectedReference is null?0:vm.References.IndexOf(vm.SelectedReference);vm.SelectedReference=vm.References[Math.Clamp(index+delta,0,vm.References.Count-1)];}
    private void View_PreviewKeyDown(object sender,KeyEventArgs e)
    {
        if(ViewModel is not{} vm)return;if(e.Key==Key.Escape&&vm.IsQuickPreviewOpen){vm.CloseQuickPreviewCommand.Execute(null);e.Handled=true;return;}if(e.OriginalSource is TextBox)return;
        switch(e.Key){case Key.Up:SelectShot(-1);e.Handled=true;break;case Key.Down:SelectShot(1);e.Handled=true;break;case Key.Space:if(vm.OpenQuickPreviewCommand.CanExecute(null))vm.OpenQuickPreviewCommand.Execute(null);e.Handled=true;break;case Key.D when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):if(vm.DuplicateShotCommand.CanExecute(null))vm.DuplicateShotCommand.Execute(null);e.Handled=true;break;case Key.Delete:if(vm.RemoveReferenceCommand.CanExecute(null))vm.RemoveReferenceCommand.Execute(null);e.Handled=true;break;}
    }
    private void SelectShot(int delta){if(ViewModel is not{} vm||vm.Shots.Count==0)return;var index=vm.SelectedShot is null?0:vm.Shots.IndexOf(vm.SelectedShot);vm.SelectedShot=vm.Shots[Math.Clamp(index+delta,0,vm.Shots.Count-1)];ShotList.ScrollIntoView(vm.SelectedShot);}
}
