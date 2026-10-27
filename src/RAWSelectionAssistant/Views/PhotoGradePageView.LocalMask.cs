using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private readonly List<StudioMaskPoint> maskPoints=[];private bool maskErasing,maskOverlay=true;private double maskRadius=.08,maskFeather=.5,maskFlow=.5,maskDensity=1;private Guid? maskTarget,maskVariant,maskNode;
    private Point? MaskPosition(Point screen)
    {
        if(PhotoViewport.Original is null)return null;if(model?.Editor.EffectiveViewMode is "并排对比" or "左右对比")return null;var rect=PhotoViewport.State.ImageRect(new Rect(PhotoViewport.RenderSize));if(rect.IsEmpty||!rect.Contains(screen))return null;
        InstantPaperLayout? crop=null;double zoom=1,cx=.5,cy=.5;
        if(model?.ActiveTarget is{} target&&target.RawDefinition?.InstantFilm is{Enabled:true} film&&target.LinearRawMaster is{} master){var size=ProfessionalRawPipeline.OrientedSize(master);crop=InstantPaperLayout.Create(size.Width,size.Height,film);zoom=film.V3?.Paper?.Zoom??1;cx=film.V3?.Geometry.CropX??.5;cy=film.V3?.Geometry.CropY??.5;}
        var p=StudioMaskCoordinates.FromViewport(screen.X,screen.Y,rect.Left,rect.Top,rect.Width,rect.Height,crop,zoom,cx,cy);return p is null?null:new Point(p.X,p.Y);
    }
    private bool BeginMask(Point screen)
    {
        if(activeTool!="画笔"||model is null)return false;if(!model.Editor.LocalCompositeEditingAvailable){ToolHint.Text="局部蒙版尚未通过发行验证。";return true;}if(model.IsProfessionalRaw&&!model.IsLightPrintLocked){ToolHint.Text="此专业RAW变体尚不支持局部蒙版，请新建普通编辑变体。";return true;}if(!model.Editor.SelectedNodeEditable||model.Editor.SelectedAdjustmentNode is null){ToolHint.Text="选择可编辑图层后绘制；原始变体/锁定层只读。";return true;}
        var point=MaskPosition(screen);if(point is null)return true;maskTarget=model.ActiveTarget?.Id;maskVariant=model.ActiveTarget?.ActiveEditVersionId;maskNode=model.Editor.SelectedAdjustmentNode.Id;maskPoints.Clear();maskPoints.Add(new(point.Value.X,point.Value.Y));PhotoViewport.CaptureMouse();DrawMaskOverlay();return true;
    }
    private bool MoveMask(Point screen)
    {if(activeTool!="画笔"||maskPoints.Count==0)return false;var point=MaskPosition(screen);if(point is{} p){var last=maskPoints[^1];if(maskPoints.Count<20000&&Math.Abs(last.X-p.X)+Math.Abs(last.Y-p.Y)>.001)maskPoints.Add(new(p.X,p.Y));}DrawMaskOverlay();return true;}
    private bool EndMask()
    {
        if(maskPoints.Count==0)return false;try{if(model?.ActiveTarget is{} active&&active.Id==maskTarget&&active.ActiveEditVersionId==maskVariant&&model.Editor.SelectedAdjustmentNode?.Id==maskNode)model.Editor.AddMaskStroke(new(maskPoints.ToArray(),maskRadius,maskFeather,maskFlow,maskDensity,maskErasing));}finally{maskPoints.Clear();PhotoViewport.ReleaseMouseCapture();DrawMaskOverlay();}return true;
    }
    private void CancelMaskStroke(){maskPoints.Clear();if(PhotoViewport.IsMouseCaptured)PhotoViewport.ReleaseMouseCapture();}
    private void MaskCaptureLost(object sender,MouseEventArgs e){maskPoints.Clear();DrawMaskOverlay();}
    private void MaskEditorChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName is nameof(RAWSelectionAssistant.ViewModels.TetherReferenceModeViewModel.SelectedAdjustmentNode) or nameof(RAWSelectionAssistant.ViewModels.TetherReferenceModeViewModel.AdjustmentStack)){if(maskPoints.Count>0&&(model?.Editor.SelectedAdjustmentNode?.Id!=maskNode||model?.ActiveTarget?.Id!=maskTarget||model?.ActiveTarget?.ActiveEditVersionId!=maskVariant))CancelMaskStroke();DrawMaskOverlay();}}
    private void ConfigureMaskPanel()
    {
        if(MaskTools is null)return;MaskTools.IsEnabled=model?.Editor.LocalCompositeEditingAvailable==true;MaskTools.Children.Clear();var row=new WrapPanel();
        void Button(string label,Action action){var b=new Button{Content=label};b.Click+=(_,_)=>{action();DrawMaskOverlay();};System.Windows.Automation.AutomationProperties.SetName(b,label);row.Children.Add(b);}
        Button("画笔",()=>maskErasing=false);Button("擦除",()=>maskErasing=true);Button("红色覆盖",()=>maskOverlay=!maskOverlay);Button("反相",()=>model?.Editor.InvertLocalMask());Button("清空",()=>model?.Editor.ClearLocalMask());Button("移除蒙版",()=>model?.Editor.RemoveLocalMask());MaskTools.Children.Add(row);
        void Slider(string name,double min,double max,double value,Action<double> set){var panel=new DockPanel();panel.Children.Add(new TextBlock{Text=name,Width=48});var control=new Slider{Minimum=min,Maximum=max,Value=value,Width=130};System.Windows.Automation.AutomationProperties.SetName(control,"蒙版"+name);control.ValueChanged+=(_,_)=>set(control.Value);panel.Children.Add(control);MaskTools.Children.Add(panel);}
        Slider("大小",.002,.5,maskRadius,x=>maskRadius=x);Slider("羽化",0,1,maskFeather,x=>maskFeather=x);Slider("流量",0,1,maskFlow,x=>maskFlow=x);Slider("密度",0,1,maskDensity,x=>maskDensity=x);MaskTools.Children.Add(new TextBlock{Text="主体识别未实现",Foreground=Brushes.Gray});
    }
    private void DrawMaskOverlay()
    {
        if(MaskOverlayCanvas is null)return;MaskOverlayCanvas.Children.Clear();if(!maskOverlay||activeTool!="画笔"||model?.Editor.SelectedAdjustmentNode is not{} node)return;
        var mask=node.LocalMask??new StudioLocalMask([]);if(maskPoints.Count>0)mask=mask with{Strokes=mask.Strokes.Append(new StudioMaskStroke(maskPoints.ToArray(),maskRadius,maskFeather,maskFlow,maskDensity,maskErasing)).ToArray()};var rect=PhotoViewport.State.ImageRect(new Rect(PhotoViewport.RenderSize));if(rect.IsEmpty||rect.Width<=0)return;var offset=PhotoViewport.TranslatePoint(new Point(),MaskOverlayCanvas);int w=128,h=Math.Min(256,Math.Max(1,(int)(w*rect.Height/rect.Width)));var pixels=new byte[w*h*4];var sample=mask.Compile(PhotoViewport.State.ImageSize.Width/PhotoViewport.State.ImageSize.Height);for(int y=0;y<h;y++)for(int x=0;x<w;x++){var i=(y*w+x)*4;var mapped=MaskPosition(new Point(rect.Left+(x+.5)*rect.Width/w,rect.Top+(y+.5)*rect.Height/h));var alpha=mapped is null?0:(byte)(sample(mapped.Value.X,mapped.Value.Y)*90);pixels[i+2]=(byte)alpha;pixels[i+3]=(byte)alpha;}
        var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,pixels,w*4);bitmap.Freeze();var image=new Image{Source=bitmap,Width=rect.Width,Height=rect.Height,IsHitTestVisible=false};Canvas.SetLeft(image,rect.Left+offset.X);Canvas.SetTop(image,rect.Top+offset.Y);MaskOverlayCanvas.Children.Add(image);
    }
}
