using System.IO;
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
        if(PhotoViewport.Original is null&&PhotoViewport.Matched is null)return null;if(model?.Editor.EffectiveViewMode is "并排对比" or "左右对比")return null;var rect=PhotoViewport.State.ImageRect(new Rect(PhotoViewport.RenderSize));if(rect.IsEmpty||!rect.Contains(screen))return null;
        InstantPaperLayout? crop=null;double zoom=1,cx=.5,cy=.5;
        if(model?.ActiveTarget is{} target&&target.RawDefinition?.InstantFilm is{Enabled:true} film&&target.LinearRawMaster is{} master){var size=ProfessionalRawPipeline.OrientedSize(master);crop=InstantPaperLayout.Create(size.Width,size.Height,film);zoom=film.V3?.Paper?.Zoom??1;cx=film.V3?.Geometry.CropX??.5;cy=film.V3?.Geometry.CropY??.5;}
        var p=StudioMaskCoordinates.FromViewport(screen.X,screen.Y,rect.Left,rect.Top,rect.Width,rect.Height,crop,zoom,cx,cy);if(p is not null&&model?.ActiveTarget?.RawDefinition?.Geometry is{} geometry)p=geometry.Source(p.X,p.Y);return p is null?null:new Point(p.X,p.Y);
    }
    private bool BeginMask(Point screen)
    {
        if(activeTool!="画笔"||model is null)return false;if(!model.Editor.LocalCompositeEditingAvailable){ToolHint.Text="局部蒙版尚未通过发行验证。";return true;}if(model.IsProfessionalRaw&&!model.RawLocalEditing&&!model.IsLightPrintLocked){ToolHint.Text="此历史配方尚不支持通用局部层；线性RAW基础版本可直接编辑。";return true;}if(!model.Editor.SelectedNodeEditable||model.Editor.SelectedAdjustmentNode is null){ToolHint.Text="选择可编辑图层后绘制；原始变体/锁定层只读。";return true;}
        var existing=model.Editor.SelectedAdjustmentNode.LocalMask;if(existing is not null&&(existing.Strokes.Count>=2000||existing.Strokes.Sum(s=>s.Points.Count)>=100000)){ToolHint.Text="蒙版达到2,000笔或100,000点限制，请撤销或清空后继续。";return true;}var point=MaskPosition(screen);if(point is null)return true;maskTarget=model.ActiveTarget?.Id;maskVariant=model.ActiveTarget?.ActiveEditVersionId;maskNode=model.Editor.SelectedAdjustmentNode.Id;maskPoints.Clear();maskPoints.Add(new(point.Value.X,point.Value.Y));PhotoViewport.CaptureMouse();DrawMaskOverlay();return true;
    }
    private bool MoveMask(Point screen)
    {if(activeTool!="画笔"||maskPoints.Count==0)return false;var point=MaskPosition(screen);if(point is null){EndMask();ToolHint.Text="笔迹已在图像边界结束；重新按下继续绘制。";return true;}if(point is{} p){var last=maskPoints[^1];if(maskPoints.Count>=20000||(model?.Editor.SelectedAdjustmentNode?.LocalMask?.Strokes.Sum(s=>s.Points.Count)??0)+maskPoints.Count>=100000){ToolHint.Text="本笔已达到20,000点限制，请松开后继续新笔迹。";return true;}if(Math.Abs(last.X-p.X)+Math.Abs(last.Y-p.Y)>.0001)maskPoints.Add(new(p.X,p.Y));}DrawMaskOverlay();return true;}
    private bool EndMask()
    {
        if(maskPoints.Count==0)return false;try{if(model?.ActiveTarget is{} active&&active.Id==maskTarget&&active.ActiveEditVersionId==maskVariant&&model.Editor.SelectedAdjustmentNode?.Id==maskNode){model.Editor.AddMaskStroke(new(maskPoints.ToArray(),maskRadius,maskFeather,maskFlow,maskDensity,maskErasing));if(model.Editor.StatusText.Contains("限制"))ToolHint.Text=model.Editor.StatusText;}}finally{maskPoints.Clear();PhotoViewport.ReleaseMouseCapture();DrawMaskOverlay();}return true;
    }
    private void CancelMaskStroke(){StopMaskOverlay();maskPoints.Clear();if(PhotoViewport.IsMouseCaptured)PhotoViewport.ReleaseMouseCapture();}
    private void MaskCaptureLost(object sender,MouseEventArgs e){maskPoints.Clear();DrawMaskOverlay();}
    private Guid? overlayNodeContext;
    private void MaskEditorChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){var selectedId=model?.Editor.SelectedAdjustmentNode?.Id;if(selectedId!=overlayNodeContext){overlayNodeContext=selectedId;StopMaskOverlay();MaskOverlayCanvas.Children.Clear();}if(e.PropertyName is nameof(RAWSelectionAssistant.ViewModels.TetherReferenceModeViewModel.SelectedAdjustmentNode) or nameof(RAWSelectionAssistant.ViewModels.TetherReferenceModeViewModel.AdjustmentStack) or nameof(RAWSelectionAssistant.ViewModels.TetherReferenceModeViewModel.SelectedNodeEditable)){if(maskPoints.Count>0&&(model?.Editor.SelectedNodeEditable!=true||model?.Editor.SelectedAdjustmentNode?.Id!=maskNode||model?.ActiveTarget?.Id!=maskTarget||model?.ActiveTarget?.ActiveEditVersionId!=maskVariant))CancelMaskStroke();DrawMaskOverlay();}}
    private Guid? maskTargetContext,maskVariantContext;
    private bool? maskPanelRaw;private bool? maskPanelAvailable;
    private void ConfigureMaskPanel()
    {
        if(MaskTools is null)return;var raw=model?.RawLocalEditing==true;var available=model?.Editor.LocalCompositeEditingAvailable==true;if(maskPanelRaw==raw&&maskPanelAvailable==available&&MaskTools.Children.Count>0)return;maskPanelRaw=raw;maskPanelAvailable=available;MaskTools.IsEnabled=available;MaskTools.Children.Clear();var row=new WrapPanel();
        void Button(string label,Action action){var b=new Button{Content=label};b.Click+=(_,_)=>{action();DrawMaskOverlay();};System.Windows.Automation.AutomationProperties.SetName(b,label);row.Children.Add(b);}
        Button("画笔",()=>maskErasing=false);Button("擦除",()=>maskErasing=true);Button("红色覆盖",()=>maskOverlay=!maskOverlay);Button("反相",()=>model?.Editor.InvertLocalMask());Button("清空",()=>model?.Editor.ClearLocalMask());Button("移除蒙版",()=>model?.Editor.RemoveLocalMask());MaskTools.Children.Add(row);
        void Slider(string name,double min,double max,double value,Action<double> set){var panel=new DockPanel();panel.Children.Add(new TextBlock{Text=name,Width=48});var control=new Slider{Minimum=min,Maximum=max,Value=value,Width=130};System.Windows.Automation.AutomationProperties.SetName(control,"蒙版"+name);control.ValueChanged+=(_,_)=>set(control.Value);panel.Children.Add(control);MaskTools.Children.Add(panel);}
        Slider("大小",.002,.5,maskRadius,x=>maskRadius=x);Slider("羽化",0,1,maskFeather,x=>maskFeather=x);Slider("流量",0,1,maskFlow,x=>maskFlow=x);Slider("密度",0,1,maskDensity,x=>maskDensity=x);MaskTools.ToolTip="V1：流量为每笔覆盖权重，密度为累计上限；不是连续喷涂。X切换擦除，[ ]改变大小。";MaskTools.Children.Add(new TextBlock{Text="主体识别未实现",Foreground=Brushes.Gray});

    }
    private readonly System.Windows.Threading.DispatcherTimer overlayTimer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    private long overlayRevision;private bool overlayDirty;private LatestPreviewQueue<OverlaySnapshot,byte[]>? overlayQueue;private bool overlayTimerAttached;
    internal Task MaskOverlayWork{get;private set;}=Task.CompletedTask;
    private void StopMaskOverlay(){overlayTimer.Stop();overlayQueue?.Invalidate();overlayRevision++;}
    private void DrawMaskOverlay()
    {
        if(!overlayTimerAttached){overlayTimerAttached=true;overlayTimer.Tick+=(_,_)=>{overlayTimer.Stop();if(overlayDirty){overlayDirty=false;RefreshMaskOverlay();}};}
        // Coalesce notifications. In-flight work is cancelled and cannot publish over a newer stroke.
        var targetContext=model?.ActiveTarget?.Id;var variantContext=model?.ActiveTarget?.ActiveEditVersionId;if(targetContext!=maskTargetContext||variantContext!=maskVariantContext){maskTargetContext=targetContext;maskVariantContext=variantContext;overlayQueue?.Invalidate();MaskOverlayCanvas.Children.Clear();}
        overlayRevision++;overlayDirty=true;if(!overlayTimer.IsEnabled)overlayTimer.Start();
    }
    private void RefreshMaskOverlay()
    {
        if(!maskOverlay||activeTool!="画笔"||model?.Editor.SelectedAdjustmentNode is not{} node){StopMaskOverlay();MaskOverlayCanvas.Children.Clear();return;}
        var rect=PhotoViewport.State.ImageRect(new Rect(PhotoViewport.RenderSize));if(rect.IsEmpty||rect.Width<=0||rect.Height<=0)return;
        var revision=overlayRevision;var target=model.ActiveTarget?.Id;var variant=model.ActiveTarget?.ActiveEditVersionId;var selected=node.Id;var offset=PhotoViewport.TranslatePoint(new Point(),MaskOverlayCanvas);
        var mask=node.LocalMask??new StudioLocalMask([]);if(maskPoints.Count>0)mask=mask with{Strokes=mask.Strokes.Append(new StudioMaskStroke(maskPoints.ToArray(),maskRadius,maskFeather,maskFlow,maskDensity,maskErasing)).ToArray()};try{mask=mask.Freeze();}catch(InvalidDataException error){ToolHint.Text=error.Message;return;}
        int w=Math.Max(1,Math.Min(768,(int)Math.Ceiling(rect.Width))),h=Math.Max(1,Math.Min(768,(int)Math.Ceiling(w*rect.Height/rect.Width)));
        if(rect.Height>rect.Width){h=Math.Min(768,(int)Math.Ceiling(rect.Height));w=Math.Max(1,(int)Math.Round(h*rect.Width/rect.Height));}
        // Only scalar geometry crosses threads. No per-pixel WPF reads or 768x768 object graph on the input thread.
        InstantPaperLayout? crop=null;double zoom=1,cx=.5,cy=.5;var geometry=model.ActiveTarget?.RawDefinition?.Geometry;
        double aspect=PhotoViewport.State.ImageSize.Width/PhotoViewport.State.ImageSize.Height;
        if(model.ActiveTarget?.LinearRawMaster is{} master){var size=ProfessionalRawPipeline.OrientedSize(master);aspect=(double)size.Width/size.Height;if(model.ActiveTarget.RawDefinition?.InstantFilm is{Enabled:true} film){crop=InstantPaperLayout.Create(size.Width,size.Height,film);zoom=film.V3?.Paper?.Zoom??1;cx=film.V3?.Geometry.CropX??.5;cy=film.V3?.Geometry.CropY??.5;}}
        overlayQueue??=new LatestPreviewQueue<OverlaySnapshot,byte[]>(async(snapshot,token)=>await Task.Run(()=>{
            return StudioMaskRaster.Alpha(snapshot.Mask,snapshot.Aspect,snapshot.Width,snapshot.Height,token,(u,v)=>{bool valid=true;if(snapshot.Crop is not null){var p=StudioMaskCoordinates.FromViewport(u,v,0,0,1,1,snapshot.Crop,snapshot.Zoom,snapshot.CenterX,snapshot.CenterY);valid=p is not null;if(p is not null){u=p.X;v=p.Y;}}if(snapshot.Geometry is not null)(u,v)=snapshot.Geometry.SourceTuple(u,v);return(u,v,valid);});
        },token),(serial,snapshot,pixels)=>{
            if(model?.ActiveTarget?.Id!=snapshot.Target||model?.ActiveTarget?.ActiveEditVersionId!=snapshot.Variant||model?.Editor.SelectedAdjustmentNode?.Id!=snapshot.Node||model?.ActiveTarget?.RawDefinition?.Geometry!=snapshot.Geometry||PhotoViewport.State.ImageRect(new Rect(PhotoViewport.RenderSize))!=snapshot.Rect)return;
            var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(snapshot.Width,snapshot.Height,96,96,PixelFormats.Pbgra32,null,pixels,snapshot.Width*4);bitmap.Freeze();var image=new Image{Source=bitmap,Width=snapshot.Rect.Width,Height=snapshot.Rect.Height,IsHitTestVisible=false};Canvas.SetLeft(image,snapshot.Rect.Left+snapshot.Offset.X);Canvas.SetTop(image,snapshot.Rect.Top+snapshot.Offset.Y);MaskOverlayCanvas.Children.Clear();MaskOverlayCanvas.Children.Add(image);MaskOverlayPublishedFrames++;MaskOverlayPublishedRevision=snapshot.Revision;
        },error=>ToolHint.Text="覆盖预览未更新："+error.Message);
        overlayQueue.Submit(new(mask,aspect,w,h,crop,zoom,cx,cy,geometry,rect,offset,target,variant,selected,revision));MaskOverlayWork=overlayQueue.Work;
    }
    internal int MaskOverlayPublishedFrames{get;private set;}
    internal long MaskOverlayPublishedRevision{get;private set;}
    private sealed record OverlaySnapshot(StudioLocalMask Mask,double Aspect,int Width,int Height,InstantPaperLayout? Crop,double Zoom,double CenterX,double CenterY,ProfessionalRawGeometry? Geometry,Rect Rect,Point Offset,Guid? Target,Guid? Variant,Guid Node,long Revision);
}
