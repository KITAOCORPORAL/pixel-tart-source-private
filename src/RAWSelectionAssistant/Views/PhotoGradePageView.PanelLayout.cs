using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

public partial class PhotoGradePageView
{
 private bool restoringPanelLayout,savingPanelLayout;
 private double panelWidth=304;
 private readonly Dictionary<string,string[]> defaultSectionOrder=new();
 private readonly Dictionary<string,bool> defaultSectionExpanded=new();
 private ScrollViewer[] PanelPanes()=>[AdjustmentScroll,PaperScroll,LayersScroll,StylesScroll,VariantsScroll];
 private StackPanel? CurrentPanelGroup()=>PanelPanes()[Math.Clamp(EditModes.SelectedIndex,0,4)].Content as StackPanel;
 private void InitializePanelLayout()
 {
  foreach(var pane in PanelPanes())if(pane.Content is StackPanel group)
  {
   var sections=group.Children.OfType<Expander>().ToArray();
   defaultSectionOrder[pane.Name]=sections.Select(s=>(string)s.Tag).ToArray();
   foreach(var section in sections)defaultSectionExpanded[(string)section.Tag]=section.IsExpanded;
  }
 }
 private void SetPanelOpen(bool open)
 {
  if(RightPanel.Visibility==Visibility.Visible&&PanelColumn.ActualWidth>=280)panelWidth=Math.Clamp(PanelColumn.ActualWidth,280,460);
  RightPanel.Visibility=open?Visibility.Visible:Visibility.Collapsed;
  PanelSplitter.Visibility=open?Visibility.Visible:Visibility.Collapsed;
  PanelColumn.MinWidth=open?280:0;PanelColumn.Width=new GridLength(open?panelWidth:0);SplitterColumn.Width=new GridLength(open?4:0);
 }
 private void PanelWidthChanged(object sender,DragCompletedEventArgs e)
 {
  panelWidth=Math.Clamp(PanelColumn.ActualWidth,280,460);PanelColumn.Width=new GridLength(panelWidth);SavePanelLayout();
 }
 private void SavePanelLayout()
 {
  if(restoringPanelLayout||model is null||EditModes is null||defaultSectionOrder.Count==0)return;
  if(RightPanel.Visibility==Visibility.Visible&&PanelColumn.Width.IsAbsolute)panelWidth=Math.Clamp(PanelColumn.Width.Value,280,460);
  var sections=PanelPanes().SelectMany(p=>(p.Content as StackPanel)?.Children.OfType<Expander>()??[]).ToArray();
  var layout=new DarkroomPanelLayout(panelWidth,RightPanel.Visibility==Visibility.Visible,EditModes.SelectedIndex,solo,
   sections.Select(s=>(string)s.Tag).ToArray(),sections.Where(s=>!s.IsExpanded).Select(s=>(string)s.Tag).ToArray());
  savingPanelLayout=true;try{model.Layout=model.Layout with{Darkroom=layout};}finally{savingPanelLayout=false;}
 }
 private void RestorePanelLayout()
 {
  if(restoringPanelLayout||EditModes is null||defaultSectionOrder.Count==0)return;
  restoringPanelLayout=true;try
  {
   var layout=(model?.Layout.Darkroom??new DarkroomPanelLayout()).Normalize();
   solo=layout.Solo;
   // Reset the visibility before applying the stored width: a prior document must not leak its size.
   RightPanel.Visibility=Visibility.Collapsed;panelWidth=layout.Width;SetPanelOpen(layout.Open);EditModes.SelectedIndex=layout.Tab;
   foreach(var pane in PanelPanes())if(pane.Content is StackPanel group)
   {
    var sections=group.Children.OfType<Expander>().ToArray();var order=layout.SectionOrder??defaultSectionOrder[pane.Name];
    var ordered=order.Select(id=>sections.FirstOrDefault(s=>Equals(s.Tag,id))).OfType<Expander>().Distinct().Concat(sections).Distinct().ToArray();
    foreach(var section in sections)group.Children.Remove(section);
    foreach(var section in ordered)group.Children.Add(section);
    var expanded=false;foreach(var section in ordered)
    {
     var id=(string)section.Tag;var open=layout.CollapsedSections is null?defaultSectionExpanded[id]:!layout.CollapsedSections.Contains(id,StringComparer.Ordinal);
     section.IsExpanded=open&&(!solo||!expanded);expanded|=section.IsExpanded;
    }
   }
  }finally{restoringPanelLayout=false;}
 }
 private void SetSolo(bool enabled)
 {
  restoringPanelLayout=true;try
  {
   solo=enabled;if(solo)foreach(var pane in PanelPanes())if(pane.Content is StackPanel group)
   {var expanded=false;foreach(var section in group.Children.OfType<Expander>())if(section.IsExpanded){section.IsExpanded=!expanded;expanded=true;}}
  }finally{restoringPanelLayout=false;}
  SavePanelLayout();
 }
 private void SetSectionsExpanded(bool expanded)
 {
  restoringPanelLayout=true;try
  {
   // Explicitly expanding everything exits Solo instead of silently leaving just the final section open.
   if(expanded)solo=false;foreach(var section in CurrentPanelGroup()?.Children.OfType<Expander>()??[])section.IsExpanded=expanded;
  }finally{restoringPanelLayout=false;}
  SavePanelLayout();
 }
 private void MoveSection(Expander section,int direction)
 {
  if(section.Parent is not StackPanel group)return;var old=group.Children.IndexOf(section);var next=old+direction;
  if(next<0||next>=group.Children.Count)return;
  group.Children.RemoveAt(old);group.Children.Insert(next,section);SavePanelLayout();
 }
}
