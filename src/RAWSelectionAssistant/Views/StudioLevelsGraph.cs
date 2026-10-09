using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

/// <summary>Input/output handles edit the existing Levels node. Histogram is read-only analysis.</summary>
internal sealed class StudioLevelsGraph : FrameworkElement
{
    private readonly Func<TetherReferenceModeViewModel?> _editor;
    private readonly string _channel;
    private readonly ColorStudioToolParameter[] _parameters;
    private int _handle = -1;
    internal StudioLevelsGraph(Func<TetherReferenceModeViewModel?> editor, string channel)
    {
        _editor = editor; _channel = channel;
        _parameters = new[] { "black", "gamma", "white", "out_black", "out_white" }.Select(key => ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.Levels).Single(p => p.Key == channel + "_" + key)).ToArray();
        Height = 115; Focusable = true; Margin = new Thickness(0, 4, 0, 5);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ReferenceColorWorkspaceViewModel old)
                System.ComponentModel.PropertyChangedEventManager.RemoveHandler(old, AnalysisChanged, nameof(old.PreviewHistogram));
            if (e.NewValue is ReferenceColorWorkspaceViewModel current)
                System.ComponentModel.PropertyChangedEventManager.AddHandler(current, AnalysisChanged, nameof(current.PreviewHistogram));
            InvalidateVisual();
        };
        StudioToolPanel.Text(this, System.Windows.Automation.AutomationProperties.NameProperty, "LevelsGraph");
        MouseLeftButtonDown += (_, e) =>
        {
            if (_editor() is not { } editorModel) return;
            var point = e.GetPosition(this);
            _handle = Enumerable.Range(0, 5).OrderBy(i => (Handle(i) - point).LengthSquared).First();
            if ((Handle(_handle) - point).Length > 16) { _handle = -1; return; }
            Focus(); editorModel.BeginEditTransaction(); CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, e) => { if (_handle >= 0) { Update(e.GetPosition(this)); e.Handled = true; } };
        MouseLeftButtonUp += (_, e) => { Finish(false); e.Handled = true; };
        LostMouseCapture += (_, _) => Finish(false);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _handle >= 0) { Finish(true); e.Handled = true; } };
        Unloaded += (_, _) => Finish(true);
    }
    private void AnalysisChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();
    private double Read(int index) => _editor()?.ToolValue(_parameters[index]) ?? _parameters[index].DefaultValue;
    private double WidthOfPlot => Math.Max(1, ActualWidth - 20);
    private Point Handle(int index)
    {
        var black = Read(0); var white = Read(2);
        var position = index == 1 ? black + (white - black) * Math.Pow(.5, Read(1)) : Read(index);
        return new(10 + position * WidthOfPlot, index < 3 ? 75 : 101);
    }
    internal static double GammaForPosition(double normalized) => Math.Clamp(Math.Log(Math.Clamp(normalized, .001, .999)) / Math.Log(.5), .1, 5);
    private void Update(Point point)
    {
        if (_editor() is not { } model) return;
        var value = Math.Clamp((point.X - 10) / WidthOfPlot, 0, 1);
        value = _handle switch
        {
            0 => Math.Clamp(value, 0, Math.Min(.99, Read(2) - .001)),
            1 => GammaForPosition((value - Read(0)) / Math.Max(.001, Read(2) - Read(0))),
            2 => Math.Clamp(value, Math.Max(.01, Read(0) + .001), 1),
            3 => Math.Min(value, Read(4)),
            _ => Math.Max(value, Read(3))
        };
        model.SetToolParameter(_parameters[_handle], value); InvalidateVisual();
    }
    private void Finish(bool cancel)
    {
        if (_handle < 0) return; _handle = -1;
        if (cancel) _editor()?.CancelEditTransaction(); else _editor()?.CommitEditTransaction();
        if (IsMouseCaptured) ReleaseMouseCapture(); InvalidateVisual();
    }
    internal bool CancelPointerGesture() { if (_handle < 0) return false; Finish(true); return true; }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle((Brush?)TryFindResource("Surface02Brush") ?? Brushes.Black, null, new Rect(RenderSize));
        var workspace = DataContext as ReferenceColorWorkspaceViewModel;
        var histogram = workspace?.PreviewHistogram;
        if (histogram is not null)
        {
            var channels = _channel switch { "r" => new[] { histogram.R }, "g" => new[] { histogram.G }, "b" => new[] { histogram.B }, _ => new[] { histogram.R, histogram.G, histogram.B } };
            var maximum = Math.Max(1u, channels.Max(values => values.Max()));
            var brushes = _channel switch { "r" => new[] { Brushes.Coral }, "g" => new[] { Brushes.LightGreen }, "b" => new[] { Brushes.DodgerBlue }, _ => new[] { Brushes.Coral, Brushes.LightGreen, Brushes.DodgerBlue } };
            for (var channel = 0; channel < channels.Length; channel++)
                for (var bin = 0; bin < 256; bin++)
                    dc.DrawRectangle(brushes[channel], null, new Rect(10 + bin * WidthOfPlot / 256, 65 - channels[channel][bin] / (double)maximum * 55, WidthOfPlot / 256, channels[channel][bin] / (double)maximum * 55));
        }
        dc.DrawRectangle(new LinearGradientBrush(Colors.Black, Colors.White, 0), null, new Rect(10, 85, WidthOfPlot, 8));
        for (var i = 0; i < 5; i++)
            dc.DrawEllipse(i is 0 or 3 ? Brushes.Black : i == 1 ? Brushes.Gray : Brushes.White, new Pen(i == _handle ? Brushes.Turquoise : Brushes.Gray, 2), Handle(i), 5, 5);
    }
}
