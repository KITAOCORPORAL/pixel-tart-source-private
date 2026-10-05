using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

/// <summary>CPU-rendered, pickable color surface and real sample cloud. Inspection only.</summary>
public sealed class ColorSpace3DViewport : FrameworkElement
{
    public static readonly DependencyProperty PointSizeProperty = DP(nameof(PointSize), typeof(double), 2.6);
    public static readonly DependencyProperty PointOpacityProperty = DP(nameof(PointOpacity), typeof(double), .7);
    public static readonly DependencyProperty SelectionToleranceProperty = DP(nameof(SelectionTolerance), typeof(double), .06);
    public static readonly DependencyProperty ViewSettingsProperty = DP(nameof(ViewSettings), typeof(ColorSpaceViewSettings), new ColorSpaceViewSettings());
    public static readonly DependencyProperty IsPanModeProperty = DP(nameof(IsPanMode), typeof(bool), false);
    private static DependencyProperty DP(string name, Type type, object value) => DependencyProperty.Register(name, type, typeof(ColorSpace3DViewport), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));
    public double PointSize { get => (double)GetValue(PointSizeProperty); set => SetValue(PointSizeProperty, value); }
    public double PointOpacity { get => (double)GetValue(PointOpacityProperty); set => SetValue(PointOpacityProperty, value); }
    public double SelectionTolerance { get => (double)GetValue(SelectionToleranceProperty); set => SetValue(SelectionToleranceProperty, value); }
    public ColorSpaceViewSettings ViewSettings { get => (ColorSpaceViewSettings)GetValue(ViewSettingsProperty); set => SetValue(ViewSettingsProperty, value); }
    public bool IsPanMode { get => (bool)GetValue(IsPanModeProperty); set => SetValue(IsPanModeProperty, value); }
    private Point? _pointer, _clickStart;
    private ColorSpaceRendererState? _state;
    private ColorSpaceCamera _emptyCamera = ColorSpaceCamera.Default;
    private long _renderCount;
    private double _lastRenderMilliseconds;
    private BitmapSource? _surface;
    private ColorSpaceCloud? _cachedCloud;
    private (ColorSpacePoint Point, int Index, ColorSpaceCoordinate Sphere, double Chroma)[] _samples = [];
    private (ColorSpaceCamera, ColorSpaceViewSettings, Size, int)? _surfaceKey;
    private static readonly Lazy<double[,]> GamutTable = new(() =>
    {
        var table = new double[129, 361];
        for (var l = 0; l <= 128; l++) for (var h = 0; h <= 360; h++) table[l, h] = ColorSpaceSurface.MaximumChroma(l / 128d, h * Math.PI / 180);
        return table;
    });
    public event EventHandler<ColorSpaceSelection>? SelectionChanged;
    public event EventHandler<OklabColor>? SurfaceSelectionChanged;
    public ColorSpaceRendererState? State { get => _state; set { _state = value; InvalidateVisual(); } }
    public ColorCloudMode Mode => State?.Mode ?? ColorCloudMode.Source;
    public bool IsAvailable => State is not null;
    private ColorSpaceCamera Camera => State?.Camera ?? _emptyCamera;
    private void CameraChanged(ColorSpaceCamera value, bool fit = false) { _emptyCamera = value; if (State is not null) State = State with { Camera = value, IsFit = fit }; else InvalidateVisual(); }
    internal object ReadNativeEvidence() => new { Bounds = ReferenceColorWorkspaceView.NativeBounds(this), Camera, State?.IsFit, ModelLoaded = State is not null, SampleCount = State?.Model.Source.Count ?? 0, RenderCount = _renderCount, LastRenderMilliseconds = _lastRenderMilliseconds, Backend = "CPU analytic OKLab gamut sphere / real sampled cloud", ViewSettings, IsMouseCaptured };
    public ColorSpace3DViewport()
    {
        Focusable = true; ClipToBounds = true;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(StudioLocalizationService.Current,OnLanguageChanged,"Language");
        ToolTip = "OKLab D65 · sRGB 色域归一化球形显示（非线性）。L 沿竖轴，±a/±b 为颜色方向；真实样本与距离仍为 OKLab。拖动旋转，Shift 拖动平移，滚轮缩放；点击点或球面仅高亮预览。";
        MouseLeftButtonDown += OnMouseDown; MouseMove += OnMouseMove; MouseLeftButtonUp += OnMouseUp; MouseWheel += OnMouseWheel;
        SizeChanged += (_, _) => { if (State?.IsFit == true) FitCamera(); else InvalidateVisual(); };
        LostMouseCapture += (_, _) => { _pointer = null; InvalidateVisual(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _pointer is not null) { _pointer = null; ReleaseMouseCapture(); e.Handled = true; } };
    }
    public void SetModel(ColorSpaceVisualizationModel model) => State = ColorSpaceRendererContract.Create(model);
    private void OnLanguageChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e) { if(Dispatcher.CheckAccess())InvalidateVisual();else Dispatcher.BeginInvoke(InvalidateVisual); }
    public void ResetCamera() => CameraChanged(ColorSpaceCamera.Default);
    public void FitCamera()
    {
        if (!ColorSpaceProjection.ValidViewport(ActualWidth, ActualHeight)) return;
        var settings = ViewSettings.Normalize(); var points = VisiblePoints(settings).Select(p => ColorSpaceSurface.Rotate(settings.IsSpherical ? p.Sphere : new(p.Point.Lab.A / .4, 2 * (p.Point.Lab.L - .5), p.Point.Lab.B / .4), Camera, settings)).ToList();
        if (settings.IsSpherical) { points.Add(new(-1,-1,0)); points.Add(new(1,1,0)); }
        if (points.Count == 0) { CameraChanged(Camera with { PanX = 0, PanY = 0 }, true); return; }
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X); var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var distance = .42 * 1.8 / .8 * Math.Max((maxX-minX) * Math.Min(ActualWidth,ActualHeight)/ActualWidth, (maxY-minY)*Math.Min(ActualWidth,ActualHeight)/ActualHeight);
        CameraChanged(Camera with { PanX = -(minX+maxX)/2, PanY=-(minY+maxY)/2, Distance=Math.Clamp(distance < 1e-9 ? 2.4 : distance, .5, 10) }, true);
    }
    private IEnumerable<(ColorSpacePoint Point, int Index, ColorSpaceCoordinate Sphere, double Chroma)> VisiblePoints(ColorSpaceViewSettings settings)
    {
        if(!ReferenceEquals(_cachedCloud,State?.Model.Source))
        {
            _cachedCloud=State?.Model.Source;
            _samples=_cachedCloud?.Points.Select((p,i)=>(p,i,ColorSpaceSurface.ToSphere(p.Lab),ColorSpaceSurface.RelativeChroma(p.Lab))).ToArray()??[];
        }
        return _samples.Where(p=>p.Chroma>=settings.ChromaMin-1e-8&&p.Chroma<=settings.ChromaMax+1e-8&&(!settings.SliceEnabled||Math.Abs(p.Point.Lab.L-settings.SliceCenter)<=settings.SliceThickness/2));
    }
    private IReadOnlyList<ColorSpaceProjectedPoint> Projected(ColorSpaceViewSettings settings)
    {
        if(settings.SurfaceMode==2)return [];
        var scale=ColorSpaceSurface.Scale(Camera,ActualWidth,ActualHeight);
        return VisiblePoints(settings).Select(item=>
        {
            var lab=item.Point.Lab;var p=ColorSpaceSurface.Rotate(settings.IsSpherical?item.Sphere:new(lab.A/.4,(lab.L-.5)*2,lab.B/.4),Camera,settings);
            return new ColorSpaceProjectedPoint(item.Index,ActualWidth/2+(p.X+Camera.PanX)*scale,ActualHeight/2-(p.Y+Camera.PanY)*scale,p.Z,item.Point.PreviewRgb);
        }).ToArray();
    }
    protected override void OnRender(DrawingContext dc)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew(); _renderCount++; base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var settings=ViewSettings.Normalize();
        dc.DrawRectangle(new SolidColorBrush(settings.Background switch { 1=>Color.FromRgb(75,75,75),2=>Color.FromRgb(190,190,190),_=>Color.FromRgb(13,17,19)}),null,new Rect(RenderSize));
        if (settings.IsSpherical && settings.SurfaceOpacity > 0) DrawSurface(dc,settings);
        if(settings.ShowGrid) DrawGrid(dc,settings);
        if(settings.ShowGamut) DrawGamut(dc,settings);
        if(settings.ShowAxes) DrawAxes(dc,settings);
        var points=Projected(settings).OrderBy(p=>p.Depth).ToArray();
        var selected = State?.Selection.Color is { } selectedColor
            ? State.Model.Source.Points.Select((p,i)=>(p,i)).Where(item=>ColorSpaceSurface.SelectionWeight(item.p.Lab,selectedColor,SelectionTolerance,0)>0).Select(item=>item.i).ToHashSet()
            : State?.Selection.Kind == ColorSpaceMarkerKind.SelectedCluster && State.Selection.PointIndex >= 0
                ? ColorSpaceLinking.SelectCluster(State.Model.Source,State.Selection.PointIndex,SelectionTolerance).ToHashSet() : [];
        foreach(var point in points)
        {
            var radius=double.IsFinite(PointSize)?Math.Clamp(PointSize,1,6):2.6;
            // Reference surface is translucent; rear samples remain visible with reduced opacity.
            var depthOpacity = settings.IsSpherical && point.Depth < 0 ? 1-settings.SurfaceOpacity*.65 : 1;
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(double.IsFinite(PointOpacity)?PointOpacity:.7,0,1)*255*depthOpacity),point.Color.R,point.Color.G,point.Color.B)),selected.Contains(point.PointIndex)?new Pen(Brushes.White,1):null,new(point.X,point.Y),radius,radius);
        }
        if(State?.Selection.Kind==ColorSpaceMarkerKind.SelectedCluster && State.Selection.Color is null)
            foreach(var p in points.Where(p=>p.PointIndex==State.Selection.PointIndex)) { dc.DrawEllipse(null,new Pen(Brushes.Black,5),new(p.X,p.Y),8,8);dc.DrawEllipse(null,new Pen(Brushes.White,2),new(p.X,p.Y),8,8); }
        if(State?.Selection.Color is { } exact)
        {
            var p=ColorSpaceSurface.Project(exact,OklabColorSpace.ToSrgb(exact),-1,Camera,settings,ActualWidth,ActualHeight);
            dc.DrawEllipse(null,new Pen(Brushes.Black,5),new(p.X,p.Y),11,11);dc.DrawEllipse(null,new Pen(Brushes.Cyan,2),new(p.X,p.Y),11,11);
        }
        var strings=StudioLocalizationService.Current;
        DrawLabel(dc,strings[settings.IsSpherical ? "SphereLabel" : "AffineLabel"],new(8,6));
        DrawLabel(dc, State is null ? strings["EmptySphere"] : $"{strings["RealSamples"]} {points.Length} · {strings["ObserveOnly"]}",new(8,Math.Max(24,ActualHeight-22)));
        _lastRenderMilliseconds=clock.Elapsed.TotalMilliseconds;
    }
    private void DrawSurface(DrawingContext dc,ColorSpaceViewSettings settings)
    {
        var edge=_pointer is null ? 320 : 160;
        var key=(Camera,settings,RenderSize,edge);
        if(_surfaceKey!=key)
        {
            var width=Math.Max(1,(int)(ActualWidth/Math.Max(ActualWidth,ActualHeight)*edge));var height=Math.Max(1,(int)(ActualHeight/Math.Max(ActualWidth,ActualHeight)*edge));
            var bytes=new byte[width*height*4];var scale=ColorSpaceSurface.Scale(Camera,ActualWidth,ActualHeight);var table=GamutTable.Value;
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var px=((x+.5)*ActualWidth/width-ActualWidth/2)/scale-Camera.PanX;var py=-((y+.5)*ActualHeight/height-ActualHeight/2)/scale-Camera.PanY;
                var depth=1-px*px-py*py;if(depth<0)continue;
                var p=ColorSpaceSurface.Rotate(new(px,py,Math.Sqrt(depth)),Camera,settings,inverse:true);
                var l=Math.Clamp((p.Y+1)/2,0,1);var hue=Math.Atan2(p.Z,p.X);if(hue<0)hue+=2*Math.PI;
                var li=l*128;var hi=hue*180/Math.PI;var il=Math.Min(127,(int)li);var ih=Math.Min(359,(int)hi);
                var c0=table[il,ih]*(1-(hi-ih))+table[il,ih+1]*(hi-ih);var c1=table[il+1,ih]*(1-(hi-ih))+table[il+1,ih+1]*(hi-ih);var chroma=c0*(1-(li-il))+c1*(li-il);
                var lab=new OklabColor(l,chroma*Math.Cos(hue),chroma*Math.Sin(hue));
                if(settings.ChromaMax<.999 || settings.SliceEnabled&&Math.Abs(l-settings.SliceCenter)>settings.SliceThickness/2)continue;
                var rgb=OklabColorSpace.ToSrgb(lab);var offset=(y*width+x)*4;
                bytes[offset]=rgb.B;bytes[offset+1]=rgb.G;bytes[offset+2]=rgb.R;bytes[offset+3]=(byte)(settings.SurfaceOpacity*255);
            }
            _surface=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4);_surface.Freeze();_surfaceKey=key;
        }
        dc.DrawImage(_surface,new Rect(RenderSize));
    }
    private Point ProjectCoordinate(ColorSpaceCoordinate p,ColorSpaceViewSettings settings)
    {p=ColorSpaceSurface.Rotate(p,Camera,settings);var scale=ColorSpaceSurface.Scale(Camera,ActualWidth,ActualHeight);return new(ActualWidth/2+(p.X+Camera.PanX)*scale,ActualHeight/2-(p.Y+Camera.PanY)*scale);}
    private void DrawGrid(DrawingContext dc,ColorSpaceViewSettings settings)
    {
        var pen=new Pen(new SolidColorBrush(Color.FromArgb(90,175,205,205)),.6);
        for(var lat=-60;lat<=60;lat+=30)Ring(a=>new(Math.Cos(lat*Math.PI/180)*Math.Cos(a),Math.Sin(lat*Math.PI/180),Math.Cos(lat*Math.PI/180)*Math.Sin(a)));
        for(var mer=0;mer<180;mer+=30){var phi=mer*Math.PI/180;Ring(a=>new(Math.Cos(a)*Math.Cos(phi),Math.Sin(a),Math.Cos(a)*Math.Sin(phi)));}
        void Ring(Func<double,ColorSpaceCoordinate> point){Point? previous=null;for(var i=0;i<=120;i++){var current=ProjectCoordinate(point(i*Math.PI/60),settings);if(previous is { } p)dc.DrawLine(pen,p,current);previous=current;}}
    }
    private void DrawGamut(DrawingContext dc,ColorSpaceViewSettings settings)
    {
        var pen=new Pen(Brushes.Teal,1.4);
        foreach(var l in new[]{.2,.4,.6,.8}){Point? previous=null;for(var h=0;h<=360;h+=3){var hue=h*Math.PI/180;var c=ColorSpaceSurface.MaximumChroma(l,hue);var lab=new OklabColor(l,c*Math.Cos(hue),c*Math.Sin(hue));var p=ColorSpaceSurface.Project(lab,new(0,0,0),0,Camera,settings,ActualWidth,ActualHeight);var current=new Point(p.X,p.Y);if(previous is { } old)dc.DrawLine(pen,old,current);previous=current;}}
    }
    private void DrawAxes(DrawingContext dc,ColorSpaceViewSettings settings)
    {
        var pen=new Pen(new SolidColorBrush(Color.FromArgb(175,180,195,195)),1);
        foreach(var (a,b,labelA,labelB) in new[]{(new ColorSpaceCoordinate(-1.12,0,0),new ColorSpaceCoordinate(1.12,0,0),"−a","+a"),(new ColorSpaceCoordinate(0,-1.12,0),new ColorSpaceCoordinate(0,1.12,0),"L0","L1"),(new ColorSpaceCoordinate(0,0,-1.12),new ColorSpaceCoordinate(0,0,1.12),"−b","+b")})
        {var start=ProjectCoordinate(a,settings);var end=ProjectCoordinate(b,settings);dc.DrawLine(pen,start,end);DrawLabel(dc,labelA,Safe(start));DrawLabel(dc,labelB,Safe(end));}
        Point Safe(Point p)=>new(Math.Clamp(p.X,4,Math.Max(4,ActualWidth-25)),Math.Clamp(p.Y,26,Math.Max(26,ActualHeight-42)));
    }
    private void DrawLabel(DrawingContext dc,string text,Point p)=>dc.DrawText(new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10.5,TryFindResource("TextSecondaryBrush") as Brush??Brushes.LightGray,VisualTreeHelper.GetDpi(this).PixelsPerDip),p);
    private void OnMouseDown(object sender,MouseButtonEventArgs e){Focus();_pointer=_clickStart=e.GetPosition(this);CaptureMouse();}
    private void OnMouseMove(object sender,MouseEventArgs e){if(_pointer is not { } previous||e.LeftButton!=MouseButtonState.Pressed)return;var current=e.GetPosition(this);var delta=current-previous;_pointer=current;CameraChanged(IsPanMode||Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?ColorSpaceProjection.PanByDisplayDelta(Camera,delta.X,delta.Y,ActualWidth,ActualHeight):Camera.Rotate(delta.X*.35,-delta.Y*.35));}
    private void OnMouseUp(object sender,MouseButtonEventArgs e)
    {
        var start=_clickStart;_pointer=null;_clickStart=null;ReleaseMouseCapture();InvalidateVisual();
        if(start is not { } point||(e.GetPosition(this)-point).Length>SystemParameters.MinimumHorizontalDragDistance)return;
        var settings=ViewSettings.Normalize();var index=ColorSpaceProjection.HitTest(Projected(settings),point.X,point.Y);
        if(index>=0&&State is not null){State=State with{Selection=new(ColorSpaceMarkerKind.SelectedCluster,index)};SelectionChanged?.Invoke(this,State.Selection);}
        else if(ColorSpaceSurface.PickSurface(point.X,point.Y,ActualWidth,ActualHeight,Camera,settings) is { } lab)SurfaceSelectionChanged?.Invoke(this,lab);
        else SelectionChanged?.Invoke(this,ColorSpaceSelection.None);
    }
    private void OnMouseWheel(object sender,MouseWheelEventArgs e){e.Handled=true;CameraChanged(Camera.Zoom(e.Delta>0?1.12:.89));}
}
