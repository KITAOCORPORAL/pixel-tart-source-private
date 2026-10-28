using System.Windows;using System.Windows.Controls;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
 private void SaveFilmDraft(object sender,RoutedEventArgs e)=>model?.SaveFilmDraft();
 private void RestoreFilmDraft(object sender,RoutedEventArgs e)=>model?.RestoreFilmDraft();
 private void RenderFilmFrames(Panel target)
 {
  if(model is null)return;target.Children.Add(new TextBlock{Text="边框",Margin=new Thickness(4,12,4,4)});var group=new WrapPanel();foreach(var frame in model.FilteredFilmFrames){var cell=new StackPanel{Width=78,Margin=new Thickness(3)};var choose=new Button{Content=new LightPrintFrameThumbnail{Frame=frame,Width=70,Height=76},BorderThickness=new Thickness(model.SelectedLightPrintFrameId==frame.Id?2:0),BorderBrush=System.Windows.Media.Brushes.Teal};choose.Click+=(_,_)=>{model?.SelectFilmFrame(frame.Id);RenderR53Resources();};cell.Children.Add(choose);var row=new DockPanel();var star=new Button{Content=model.IsFilmResourceFavorite("frame:"+frame.Id)?"★":"☆",ToolTip="切换收藏边框"};DockPanel.SetDock(star,Dock.Right);star.Click+=(_,_)=>{model?.FavoriteFilmResource("frame:"+frame.Id);RenderR53Resources();};row.Children.Add(star);row.Children.Add(new TextBlock{Text=frame.Name,FontSize=10,TextTrimming=TextTrimming.CharacterEllipsis});cell.Children.Add(row);group.Children.Add(cell);}target.Children.Add(group);
 }
}
