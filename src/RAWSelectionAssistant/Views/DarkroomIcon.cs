using System.Windows;using System.Windows.Media;
namespace RAWSelectionAssistant.Views;
/// <summary>Resolution-independent 20-unit line icons; no font fallback or emoji.</summary>
public sealed class DarkroomIcon:FrameworkElement
{
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(DarkroomIcon),new FrameworkPropertyMetadata("photo",FrameworkPropertyMetadataOptions.AffectsRender));
 public string Kind{get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 public DarkroomIcon(){Width=16;Height=16;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);var brush=(TryFindResource("R3IconBrush") as Brush)??new SolidColorBrush(Color.FromRgb(166,166,171));var pen=new Pen(brush,1.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.PushTransform(new ScaleTransform(ActualWidth/20,ActualHeight/20));string path=Kind switch{
 "import"=>"M10,2 L10,12 M6,8 L10,12 14,8 M3,12 L3,17 17,17 17,12",
 "export"=>"M10,13 L10,3 M6,7 L10,3 14,7 M3,12 L3,17 17,17 17,12",
 "undo"=>"M7,4 L3,8 7,12 M3,8 L12,8 C18,8 18,16 12,16",
 "redo"=>"M13,4 L17,8 13,12 M17,8 L8,8 C2,8 2,16 8,16",
 "pointer"=>"M4,2 L4,16 8,12 11,18 14,16 11,11 17,10 Z",
 "hand"=>"M5,10 L5,6 Q5,4 7,6 L7,3 Q8,1 9,3 L9,7 9,2 Q11,1 11,3 L11,7 11,4 Q13,3 13,5 L13,9 15,7 Q18,6 17,9 L15,16 Q14,18 11,18 L8,18 Q6,18 4,15 L2,12 Q1,9 3,9 Z",
 "zoom"=>"M8,2 A6,6 0 1 1 8,14 A6,6 0 1 1 8,2 M13,13 L18,18 M5,8 L11,8 M8,5 L8,11",
 "compare"=>"M3,3 L17,3 17,17 3,17 Z M10,3 L10,17 M4,5 L8,5 M4,8 L8,8 M4,11 L8,11 M4,14 L8,14",
 "grid"=>"M3,3 L8,3 8,8 3,8 Z M12,3 L17,3 17,8 12,8 Z M3,12 L8,12 8,17 3,17 Z M12,12 L17,12 17,17 12,17 Z",
 "photo"=>"M3,3 L17,3 17,17 3,17 Z M3,14 L7,9 11,13 14,10 17,14 M13,5 A1.5,1.5 0 1 1 13,8 A1.5,1.5 0 1 1 13,5",
 "adjust"=>"M4,3 L4,17 M10,3 L10,17 M16,3 L16,17 M1,7 L7,7 M7,13 L13,13 M13,6 L19,6",
 "paper"=>"M4,2 L16,2 16,18 4,18 Z M6,4 L14,4 14,12 6,12 Z",
 "layers"=>"M10,2 L18,6 10,10 2,6 Z M2,10 L10,14 18,10 M2,14 L10,18 18,14",
 "star"=>"M10,2 L12.4,7 18,8 14,12 15,18 10,15 5,18 6,12 2,8 7.6,7 Z",
 "history"=>"M4,6 A7,7 0 1 1 3,13 M3,2 L3,7 8,7 M10,6 L10,10 13,12",
 "copy"=>"M7,7 L17,7 17,18 7,18 Z M3,13 L3,2 13,2",
 "paste"=>"M6,4 L3,4 3,18 17,18 17,4 14,4 M7,2 L13,2 13,6 7,6 Z",
 "sync"=>"M3,8 A7,7 0 0 1 15,4 L18,7 M18,2 L18,7 13,7 M17,12 A7,7 0 0 1 5,16 L2,13 M2,18 L2,13 7,13",
 "panel"=>"M2,3 L18,3 18,17 2,17 Z M12,3 L12,17 M15,7 L15,13",
 "save"=>"M3,2 L14,2 18,6 18,18 3,18 Z M6,2 L6,7 13,7 13,2 M6,18 L6,11 15,11 15,18",
 "folder"=>"M2,5 L8,5 10,7 18,7 18,17 2,17 Z M2,5 L2,3 8,3 10,5",
 "more"=>"M4,9 L4,11 M10,9 L10,11 M16,9 L16,11",
 _=>"M4,4 L16,4 16,16 4,16 Z"};dc.DrawGeometry(null,pen,Geometry.Parse(path));dc.Pop();}
}
