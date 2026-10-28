using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private StudioStillSnapshot? r54SelectedStill;
    private void R54StillsTab(object s,RoutedEventArgs e){r53ResourceTab="剧照";RenderR53Resources();}
    private void RenderR54Stills(Panel target)
    {
        if(model is null)return;var capture=new Button{Content="抓取剧照  Ctrl+Alt+G"};capture.Click+=(_,_)=>{model?.CaptureR54Still();RenderR53Resources();};target.Children.Add(capture);
        foreach(var still in model.R54Stills.Where(s=>string.IsNullOrEmpty(r53Search)||s.Name.Contains(r53Search,StringComparison.CurrentCultureIgnoreCase)))
        {
            var row=new StackPanel{Margin=new Thickness(2,6,2,2)};var image=new Image{Source=ViewModels.ReferenceColorWorkspaceViewModel.R54StillImage(still),Height=112,Stretch=System.Windows.Media.Stretch.Uniform};var choose=new Button{Content=image,Height=116,ToolTip=still.Name+" · 右键应用或划像"};
            choose.Click+=(_,_)=>{r54SelectedStill=still;R54CompareStill(still);};var menu=new ContextMenu();var apply=new MenuItem{Header="应用到当前所选照片",IsEnabled=still.Stack is not null};apply.Click+=(_,_)=>model?.ApplyR54Still(still);menu.Items.Add(apply);var compare=new MenuItem{Header="划像对比  W"};compare.Click+=(_,_)=>R54CompareStill(still);menu.Items.Add(compare);choose.ContextMenu=menu;row.Children.Add(choose);row.Children.Add(new TextBlock{Text=still.Name});target.Children.Add(row);
        }
    }
    private void R54CompareStill(StudioStillSnapshot still)
    {
        if(model?.Editor.MatchedImage is not{} current)return;
        var viewport=new ColorStudioImageViewport{Original=ViewModels.ReferenceColorWorkspaceViewModel.R54StillImage(still),Matched=current,Mode="左右对比",Split=.5};
        var panel=new DockPanel();var split=new Slider{Minimum=0,Maximum=1,Value=.5,ToolTip="划像位置"};DockPanel.SetDock(split,Dock.Bottom);panel.Children.Add(split);panel.Children.Add(viewport);split.ValueChanged+=(_,_)=>viewport.Split=split.Value;
        var window=new Window{Title="光镜 · 剧照划像 · "+still.Name,Owner=Window.GetWindow(this),Width=900,Height=650,Content=panel,Background=Background};viewport.MouseLeftButtonDown+=(_,e)=>{viewport.CaptureMouse();viewport.Split=Math.Clamp(e.GetPosition(viewport).X/Math.Max(1,viewport.ActualWidth),0,1);e.Handled=true;};viewport.MouseMove+=(_,e)=>{if(viewport.IsMouseCaptured)viewport.Split=Math.Clamp(e.GetPosition(viewport).X/Math.Max(1,viewport.ActualWidth),0,1);};viewport.MouseLeftButtonUp+=(_,_)=>viewport.ReleaseMouseCapture();window.Show();
    }
}
