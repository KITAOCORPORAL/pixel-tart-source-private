using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace PixelTart.Modules.AssetLibrary.FreeCanvas;

/// <summary>Retained document rendering with shared previews; no image encoding or source writes.</summary>
public sealed class FreeCanvasSurface : FrameworkElement
{
    private readonly IAssetPreviewProvider _provider;
    private readonly Dictionary<Guid, BitmapSource> _images = [];
    private readonly Dictionary<Guid, int> _widths = [];
    private readonly HashSet<Guid> _loading = [];
    private Point _last, _start;
    private string? _gesture;
    private CanvasBounds? _marquee;
    private double _zoom = 1;
    private Vector _pan = new(80, 70);
    private bool _space;
    private double _rotationTotal, _rotationApplied;
    private Point _resizeAnchor;
    private Vector _resizeVector;
    public FreeCanvasSurface(CanvasEditor editor, IAssetPreviewProvider provider)
    {
        Editor = editor; _provider = provider; Focusable = true; ClipToBounds = true; AllowDrop = true;
        Editor.Changed += (_, _) => { InvalidateVisual(); _ = LoadPreviewsAsync(); };
        SizeChanged += (_, _) => InvalidateVisual();
    }
    public CanvasEditor Editor { get; }
    public string Tool { get; set; } = "选择";
    public bool CropMode { get; private set; }
    public CanvasCrop PendingCrop { get; set; } = new();
    public double Zoom => _zoom;
    public event EventHandler? ViewChanged;
    public event EventHandler? ContextRequested;
    public event EventHandler? TextEditRequested;
    public IReadOnlyDictionary<Guid, BitmapSource> LoadedPreviews => _images;
    public Point ScreenToWorld(Point point) => new((point.X - _pan.X) / _zoom, (point.Y - _pan.Y) / _zoom);
    public Point WorldToScreen(Point point) => new(point.X * _zoom + _pan.X, point.Y * _zoom + _pan.Y);
    public async Task LoadPreviewsAsync()
    {
        var currentIds=Editor.Document.Objects.Select(item=>item.ObjectId).ToHashSet();
        foreach(var id in _images.Keys.Where(id=>!currentIds.Contains(id)).ToArray()){_images.Remove(id);_widths.Remove(id);}
        var viewport=new Rect(ScreenToWorld(new(-300,-300)),ScreenToWorld(new(Math.Max(1,ActualWidth)+300,Math.Max(1,ActualHeight)+300)));
        foreach (var item in Editor.Document.Objects.Where(item => item.IsImage && (ActualWidth==0 || viewport.IntersectsWith(new Rect(item.X,item.Y,item.Width,item.Height)))).Take(128).ToArray())
        {
            var requested = (int)Math.Clamp(Math.Ceiling(item.Width * _zoom / item.CropRect.Width / 256) * 256, 256, 4096);
            if (_loading.Contains(item.ObjectId) || _widths.GetValueOrDefault(item.ObjectId) >= requested) continue;
            _loading.Add(item.ObjectId);
            try
            {
                var result = await _provider.GetAsync(new(item.SourcePath, AssetPreviewPurpose.Canvas, AssetPreviewQuality.High, requested,
                    File.Exists(item.SourcePath) ? AssetThumbnailState.Available : AssetThumbnailState.Offline, item.AssetId, item.ContentHash));
                if (result.Bitmap is null && !_images.ContainsKey(item.ObjectId))
                    foreach (var width in Enumerable.Range(1,16).Select(value=>value*256).OrderByDescending(value=>value))
                    {
                        result = await _provider.GetAsync(new(item.SourcePath, AssetPreviewPurpose.Canvas, AssetPreviewQuality.High, width, AssetThumbnailState.Offline, item.AssetId, item.ContentHash));
                        if (result.Bitmap is not null) break;
                    }
                if(result.Bitmap is null&&!File.Exists(item.SourcePath))
                    foreach(var purpose in new[]{AssetPreviewPurpose.ViewerPreview,AssetPreviewPurpose.QuickLoupe})
                    {
                        result=await _provider.GetAsync(new(item.SourcePath,purpose,AssetPreviewQuality.High,purpose==AssetPreviewPurpose.QuickLoupe?1600:2048,AssetThumbnailState.Offline,item.AssetId,item.ContentHash));
                        if(result.Bitmap is not null)break;
                    }
                if (result.Bitmap is { } bitmap) { _images[item.ObjectId] = bitmap; _widths[item.ObjectId] = requested; }
                while(_images.Count>1&&(_images.Count>128||_images.Values.Sum(image=>(long)image.PixelWidth*image.PixelHeight*4)>192L*1024*1024)){var remove=_images.Keys.First(id=>id!=item.ObjectId);_images.Remove(remove);_widths.Remove(remove);}
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException or ArgumentException) { }
            finally { _loading.Remove(item.ObjectId); }
        }
        InvalidateVisual();
    }
    private Brush Brush(string resource, Color fallback) => TryFindResource(resource) as Brush ?? new SolidColorBrush(fallback);
    protected override void OnRender(DrawingContext dc)
    {
        var accent = Brush("Brush.Accent", Colors.Turquoise);
        dc.DrawRectangle(Brush("Brush.Background", Color.FromRgb(20,23,27)), null, new Rect(RenderSize));
        dc.PushTransform(new TranslateTransform(_pan.X, _pan.Y)); dc.PushTransform(new ScaleTransform(_zoom,_zoom));
        foreach (var item in Editor.Document.Objects.OrderBy(item => item.ZIndex))
        {
            dc.PushTransform(new RotateTransform(item.Rotation, item.X+item.Width/2,item.Y+item.Height/2));
            var rect = DisplayRect(item);
            if (item.IsText)
            {
                Brush textBrush; try { textBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.TextColor)); } catch { textBrush = Brushes.White; }
                var text = new FormattedText(item.Text ?? "",CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),item.FontSize,textBrush,VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth=item.Width,MaxTextHeight=item.Height };
                dc.DrawText(text,new(item.X,item.Y));
            }
            else if (item.Palette is { } palette)
            {
                var colors=palette.Colors.OrderByDescending(color=>color.Weight).ToArray();var total=Math.Max(.0001,colors.Sum(color=>color.Weight));var x=rect.X;
                foreach(var color in colors){Brush swatch;try{swatch=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Hex));}catch{swatch=Brushes.Gray;}var width=rect.Width*color.Weight/total;dc.DrawRectangle(swatch,null,new(x,rect.Y,width,rect.Height-34));x+=width;}
                DrawLabel(dc,palette.Combined?$"组合配色 · {palette.SourceAssetIds.Count} 张":$"配色 · {colors.Length} 色",new(rect.X+8,rect.Bottom-28));
            }
            else if (_images.TryGetValue(item.ObjectId,out var bitmap))
            {
                dc.PushClip(new RectangleGeometry(rect));
                dc.PushTransform(new ScaleTransform(item.FlipX ? -1 : 1,item.FlipY ? -1 : 1,rect.X+rect.Width/2,rect.Y+rect.Height/2));
                var crop = CropMode && Editor.Selection.Contains(item.ObjectId) ? new CanvasCrop() : item.CropRect;
                if(item.Monochrome)bitmap=new FormatConvertedBitmap(bitmap,PixelFormats.Gray8,null,0);
                dc.DrawImage(bitmap,new(rect.X-crop.X*rect.Width/crop.Width,rect.Y-crop.Y*rect.Height/crop.Height,rect.Width/crop.Width,rect.Height/crop.Height));
                dc.Pop(); dc.Pop();
            }
            else { dc.DrawRectangle(Brush("Brush.Panel",Colors.DimGray),null,rect); DrawLabel(dc,"预览暂不可用",new(item.X+12,item.Y+12)); }
            if (Editor.Selection.Contains(item.ObjectId)) dc.DrawRectangle(null,new Pen(accent,1.5/_zoom),rect);
            if (item.Locked) DrawLabel(dc,"已锁定",new(item.X+6,item.Y+6));
            if (item.IsImage && !File.Exists(item.SourcePath)) DrawLabel(dc,"离线",new(item.X+6,item.Y+item.Height-24/_zoom));
            if (CropMode && Editor.Selection.Contains(item.ObjectId))
            {
                var crop = PendingCrop.Normalize();
                dc.PushTransform(new ScaleTransform(item.FlipX?-1:1,item.FlipY?-1:1,rect.X+rect.Width/2,rect.Y+rect.Height/2));
                dc.DrawRectangle(null,new Pen(accent,2/_zoom),new(rect.X+crop.X*rect.Width,rect.Y+crop.Y*rect.Height,crop.Width*rect.Width,crop.Height*rect.Height));
                dc.Pop();
            }
            dc.Pop();
        }
        if (Editor.Selected.Count>0 && !CropMode)
        {
            var b=Editor.Bounds(); var pen=new Pen(accent,1/_zoom) { DashStyle=DashStyles.Dash };
            dc.DrawRectangle(null,pen,new(b.X,b.Y,b.Width,b.Height));
            if (Editor.Selected.All(item=>!item.Locked))
            {
                foreach(var corner in Corners(b))dc.DrawRectangle(accent,null,new(corner.X-4/_zoom,corner.Y-4/_zoom,8/_zoom,8/_zoom));
                dc.DrawLine(pen,new(b.X+b.Width/2,b.Y),new(b.X+b.Width/2,b.Y-24/_zoom));
                dc.DrawEllipse(accent,null,new(b.X+b.Width/2,b.Y-24/_zoom),4/_zoom,4/_zoom);
            }
        }
        if (_marquee is { } m) dc.DrawRectangle(null,new Pen(accent,1/_zoom),new(m.X,m.Y,m.Width,m.Height));
        dc.Pop(); dc.Pop();
    }
    private void DrawLabel(DrawingContext dc,string label,Point point)
    {
        var text=new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),11/_zoom,Brush("Brush.Text.Secondary",Colors.LightGray),VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawRectangle(Brush("Brush.Surface.Elevated",Colors.DarkSlateGray),null,new(point,new Size(text.Width+8/_zoom,text.Height+4/_zoom))); dc.DrawText(text,new(point.X+4/_zoom,point.Y+2/_zoom));
    }
    public CanvasObject? Hit(Point world) => Editor.Document.Objects.OrderByDescending(item=>item.ZIndex).FirstOrDefault(item=>
    {
        var inverse=new RotateTransform(-item.Rotation,item.X+item.Width/2,item.Y+item.Height/2).Transform(world);
        return new Rect(item.X,item.Y,item.Width,item.Height).Contains(inverse);
    });
    private Rect DisplayRect(CanvasObject item)=>CropMode&&Editor.Selection.Contains(item.ObjectId)
        ? new(item.X,item.Y+item.Height/2-item.Width*item.SourceHeight/Math.Max(1,item.SourceWidth)/2,item.Width,item.Width*item.SourceHeight/Math.Max(1,item.SourceWidth))
        : new(item.X,item.Y,item.Width,item.Height);
    private Point CropPoint(CanvasObject item,Point world)
    {
        var rect=DisplayRect(item);var point=new RotateTransform(-item.Rotation,item.X+item.Width/2,item.Y+item.Height/2).Transform(world);
        return new(item.FlipX?1-(point.X-rect.X)/rect.Width:(point.X-rect.X)/rect.Width,item.FlipY?1-(point.Y-rect.Y)/rect.Height:(point.Y-rect.Y)/rect.Height);
    }
    private static Point[] Corners(CanvasBounds b)=>[new(b.X,b.Y),new(b.X+b.Width,b.Y),new(b.X,b.Y+b.Height),new(b.X+b.Width,b.Y+b.Height)];
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e); Focus(); var screen=e.GetPosition(this); var world=ScreenToWorld(screen); _start=world; _last=screen;
        if (e.ChangedButton==MouseButton.Right) { var hit=Hit(world); if(hit is not null&&!Editor.Selection.Contains(hit.ObjectId)) Editor.Select(hit.ObjectId); ContextRequested?.Invoke(this,EventArgs.Empty); e.Handled=true; return; }
        if(e.ChangedButton!=MouseButton.Left)return;
        if(_space||Tool=="移动画布") _gesture="pan";
        else if(Tool=="文本") { Editor.AddText(world.X,world.Y); Tool="选择"; TextEditRequested?.Invoke(this,EventArgs.Empty); e.Handled=true; return; }
        else if(CropMode) _gesture="crop";
        else
        {
            var b=Editor.Bounds();
            var corners=Corners(b);var cornerIndex=Array.FindIndex(corners,corner=>(world-corner).Length<12/_zoom);
            if(Editor.Selected.Count>0&&cornerIndex>=0){_gesture="resize";_resizeAnchor=corners[3-cornerIndex];_resizeVector=corners[cornerIndex]-_resizeAnchor;}
            else if(Editor.Selected.Count>0&&(world-new Point(b.X+b.Width/2,b.Y-24/_zoom)).Length<12/_zoom) _gesture="rotate";
            else if(Hit(world) is { } hit)
            {
                if(e.ClickCount==2) { Editor.Select(hit.ObjectId);if(hit.IsText)TextEditRequested?.Invoke(this,EventArgs.Empty);else BeginCrop();e.Handled=true;return; }
                if(!Editor.Selection.Contains(hit.ObjectId)||(Keyboard.Modifiers&ModifierKeys.Shift)!=0)Editor.Select(hit.ObjectId,(Keyboard.Modifiers&ModifierKeys.Shift)!=0);
                _gesture="move";
            }
            else { if((Keyboard.Modifiers&ModifierKeys.Shift)==0)Editor.Select(null);_gesture="marquee"; }
        }
        _rotationTotal=0;_rotationApplied=0;Editor.BeginGesture(); CaptureMouse(); e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if(!IsMouseCaptured||_gesture is null)return;
        var screen=e.GetPosition(this); var world=ScreenToWorld(screen); var delta=(screen-_last)/_zoom;
        switch(_gesture)
        {
            case "pan": _pan+=screen-_last; ViewChanged?.Invoke(this,EventArgs.Empty);break;
            case "move":Editor.Move(delta.X,delta.Y);break;
            case "resize":var vector=world-_resizeAnchor;var factor=Math.Max(.02,Vector.Multiply(vector,_resizeVector)/Math.Max(1,_resizeVector.LengthSquared));Editor.Scale(factor,_resizeAnchor.X,_resizeAnchor.Y);_resizeVector*=factor;break;
            case "rotate":var bounds=Editor.Bounds();var center=WorldToScreen(new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2));var from=_last-center;var to=screen-center;_rotationTotal+=Vector.AngleBetween(from,to);var target=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?Math.Round(_rotationTotal/15)*15:_rotationTotal;Editor.Rotate(target-_rotationApplied);_rotationApplied=target;break;
            case "marquee":_marquee=new(Math.Min(_start.X,world.X),Math.Min(_start.Y,world.Y),Math.Abs(_start.X-world.X),Math.Abs(_start.Y-world.Y));break;
            case "crop": if(Editor.Selected.FirstOrDefault() is { } item){var start=CropPoint(item,_start);var end=CropPoint(item,world);PendingCrop=new CanvasCrop(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Abs(start.X-end.X),Math.Abs(start.Y-end.Y)).Normalize();}break;
        }
        _last=screen; InvalidateVisual();e.Handled=true;
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e); if(!IsMouseCaptured)return;
        if(_marquee is { } rect)Editor.Marquee(rect,(Keyboard.Modifiers&ModifierKeys.Shift)!=0);
        _marquee=null;_gesture=null;ReleaseMouseCapture();Editor.EndGesture();InvalidateVisual();_=LoadPreviewsAsync();e.Handled=true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e);_gesture=null;_marquee=null;Editor.EndGesture(); }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var p=e.GetPosition(this);var world=ScreenToWorld(p);_zoom=Math.Clamp(_zoom*Math.Pow(1.12,e.Delta/120d),.03,8);_pan=new(p.X-world.X*_zoom,p.Y-world.Y*_zoom);InvalidateVisual();ViewChanged?.Invoke(this,EventArgs.Empty);_=LoadPreviewsAsync();e.Handled=true;
    }
    public void Fit(bool selection=false)
    {
        var b=Editor.Bounds(selection);_zoom=Math.Clamp(Math.Min(Math.Max(100,ActualWidth-140)/b.Width,Math.Max(100,ActualHeight-140)/b.Height),.03,2);_pan=new((ActualWidth-b.Width*_zoom)/2-b.X*_zoom,(ActualHeight-b.Height*_zoom)/2-b.Y*_zoom);InvalidateVisual();ViewChanged?.Invoke(this,EventArgs.Empty);_=LoadPreviewsAsync();
    }
    public void ActualSize() { _zoom=1;InvalidateVisual();ViewChanged?.Invoke(this,EventArgs.Empty);_=LoadPreviewsAsync(); }
    public void BeginCrop() { if(Editor.Selected.Count!=1||Editor.Selected[0].Locked||!Editor.Selected[0].IsImage)return;CropMode=true;PendingCrop=Editor.Selected[0].CropRect;ViewChanged?.Invoke(this,EventArgs.Empty);InvalidateVisual(); }
    public void FinishCrop(bool apply) { if(!CropMode)return;if(apply)Editor.Crop(PendingCrop);CropMode=false;ViewChanged?.Invoke(this,EventArgs.Empty);InvalidateVisual(); }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e);if(e.Key==Key.Space)_space=false; }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnLostKeyboardFocus(e);_space=false;}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);HandleShortcut(e);
    }
    public void HandleShortcut(KeyEventArgs e)
    {
        var ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0;var shift=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
        if(CropMode&&(e.Key is Key.Enter or Key.Escape)){FinishCrop(e.Key==Key.Enter);e.Handled=true;return;}
        if(e.Key==Key.Space){_space=true;e.Handled=true;return;}
        if(ctrl)switch(e.Key){case Key.A:Editor.SelectAll();break;case Key.C:Editor.Copy();break;case Key.V:Editor.Paste();break;case Key.D:Editor.Duplicate();break;case Key.G:if(shift)Editor.Ungroup();else Editor.Group();break;case Key.Z:if(shift)Editor.Redo();else Editor.Undo();break;case Key.Y:Editor.Redo();break;default:return;}
        else switch(e.Key){case Key.Delete:case Key.Back:Editor.Remove();break;case Key.Left:Editor.Move(shift?-10:-1,0);break;case Key.Right:Editor.Move(shift?10:1,0);break;case Key.Up:Editor.Move(0,shift?-10:-1);break;case Key.Down:Editor.Move(0,shift?10:1);break;case Key.Escape:Editor.EndGesture(true);Editor.Select(null);break;default:return;}
        e.Handled=true;
    }
}
