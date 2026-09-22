using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>A shared, vector-based photography rating control. Hover is preview-only; persistence begins at commit.</summary>
public sealed class PixelTartRatingControl : Control
{
    private static readonly Geometry Star = Geometry.Parse("M10,1.5 L12.55,6.65 L18.25,7.48 L14.12,11.5 L15.1,17.18 L10,14.5 L4.9,17.18 L5.88,11.5 L1.75,7.48 L7.45,6.65 Z");
    public static readonly DependencyProperty RatingProperty = DependencyProperty.Register(nameof(Rating), typeof(int), typeof(PixelTartRatingControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed, CoerceRating));
    public static readonly DependencyProperty HoverRatingProperty = DependencyProperty.Register(nameof(HoverRating), typeof(int?), typeof(PixelTartRatingControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(PixelTartRatingControl));
    public static readonly DependencyProperty StarSizeProperty = DependencyProperty.Register(nameof(StarSize), typeof(double), typeof(PixelTartRatingControl), new FrameworkPropertyMetadata(18d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HitTargetProperty = DependencyProperty.Register(nameof(HitTarget), typeof(double), typeof(PixelTartRatingControl), new FrameworkPropertyMetadata(28d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    private int? _keyboardPreview;

    static PixelTartRatingControl() => FocusableProperty.OverrideMetadata(typeof(PixelTartRatingControl), new FrameworkPropertyMetadata(true));
    public PixelTartRatingControl()
    {
        Cursor = Cursors.Hand;
        AutomationProperties.SetName(this, "照片评分");
        MouseMove += (_, e) => HoverRating = Hit(e.GetPosition(this).X);
        MouseLeave += (_, _) => HoverRating = null;
        MouseLeftButtonUp += (_, e) => { Focus(); Commit(Hit(e.GetPosition(this).X)); e.Handled = true; };
        PreviewKeyDown += OnPreviewKeyDown;
    }
    public int Rating { get => (int)GetValue(RatingProperty); set => SetValue(RatingProperty, value); }
    public int? HoverRating { get => (int?)GetValue(HoverRatingProperty); set => SetValue(HoverRatingProperty, value); }
    public int? PreviewRating => _keyboardPreview ?? HoverRating;
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public double StarSize { get => (double)GetValue(StarSizeProperty); set => SetValue(StarSizeProperty, value); }
    public double HitTarget { get => (double)GetValue(HitTargetProperty); set => SetValue(HitTargetProperty, value); }
    protected override Size MeasureOverride(Size constraint) => new(HitTarget * 5, HitTarget);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var preview = PreviewRating;
        for (var index = 1; index <= 5; index++)
        {
            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(StarSize / 20, StarSize / 20));
            transform.Children.Add(new TranslateTransform((index - 1) * HitTarget + (HitTarget - StarSize) / 2, (ActualHeight - StarSize) / 2));
            var geometry = Star.Clone(); geometry.Transform = transform;
            var committed = index <= Rating;
            var previewAdd = preview is int p && p > Rating && index > Rating && index <= p;
            var previewRemove = preview is int r && r < Rating && index > r && index <= Rating;
            var fill = committed && !previewRemove ? Brush("RatingCommittedBrush", Colors.Goldenrod) : previewAdd || previewRemove ? Brush("RatingPreviewBrush", Color.FromRgb(186, 150, 89)) : Brushes.Transparent;
            var stroke = committed && !previewRemove ? Brush("RatingCommittedBrush", Colors.Goldenrod) : previewAdd || previewRemove ? Brush("RatingPreviewBrush", Color.FromRgb(186, 150, 89)) : Brush("RatingEmptyBrush", Color.FromRgb(92, 88, 84));
            dc.DrawGeometry(fill, new Pen(stroke, 1.15), geometry);
        }
        if (IsKeyboardFocused) dc.DrawRoundedRectangle(null, new Pen(Brush("Brush.Accent", Color.FromRgb(110,155,145)), 1), new Rect(.5, .5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1)), 5, 5);
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        int? direct = e.Key switch { Key.D0 or Key.NumPad0 => 0, Key.D1 or Key.NumPad1 => 1, Key.D2 or Key.NumPad2 => 2, Key.D3 or Key.NumPad3 => 3, Key.D4 or Key.NumPad4 => 4, Key.D5 or Key.NumPad5 => 5, _ => null };
        if (direct is int value) { Commit(value); e.Handled = true; return; }
        if (e.Key is Key.Left or Key.Right) { _keyboardPreview = Math.Clamp((_keyboardPreview ?? Rating) + (e.Key == Key.Right ? 1 : -1), 0, 5); InvalidateVisual(); RaiseAutomation(); e.Handled = true; }
        else if (e.Key == Key.Enter && _keyboardPreview is int pending) { Commit(pending); e.Handled = true; }
        else if (e.Key == Key.Escape) { _keyboardPreview = null; HoverRating = null; InvalidateVisual(); RaiseAutomation(); e.Handled = true; }
    }
    private int Hit(double x) => Math.Clamp((int)Math.Floor(x / Math.Max(1, HitTarget)) + 1, 1, 5);
    private void Commit(int value) { _keyboardPreview = null; Rating = value; if (Command?.CanExecute(value) == true) Command.Execute(value); HoverRating = null; }
    private Brush Brush(string key, Color fallback) => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
    private static object CoerceRating(DependencyObject d, object value) => Math.Clamp((int)value, 0, 5);
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((PixelTartRatingControl)d).InvalidateVisual(); ((PixelTartRatingControl)d).RaiseAutomation(); }
    private void RaiseAutomation()
    {
        AutomationProperties.SetItemStatus(this, PreviewRating is int preview ? $"预览 {preview} 星；当前评分 {Rating} 星" : $"当前评分 {Rating} 星");
    }
}
