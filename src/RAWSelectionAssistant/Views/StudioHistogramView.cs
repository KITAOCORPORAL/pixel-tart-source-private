using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

/// <summary>Shared histogram drawing with Studio scale/readout. No second statistics engine.</summary>
public sealed class StudioHistogramView : Grid
{
    public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(nameof(Histogram), typeof(VisualHistogram), typeof(StudioHistogramView), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ChannelProperty = DependencyProperty.Register(nameof(Channel), typeof(string), typeof(StudioHistogramView), new PropertyMetadata("RGB", Changed));
    public VisualHistogram? Histogram { get => (VisualHistogram?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }
    public string Channel { get => (string)GetValue(ChannelProperty); set => SetValue(ChannelProperty, value); }
    private readonly HistogramDrawing _drawing = new() { ShowLuma = false, MinHeight = 30 };
    private readonly TextBlock _readout = new() { FontSize = 10, Margin = new Thickness(3, 0, 3, 1), TextTrimming = TextTrimming.CharacterEllipsis };
    public string Readout => _readout.Text;
    public StudioHistogramView()
    {
        RowDefinitions.Add(new RowDefinition()); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Children.Add(_drawing); SetRow(_readout, 1); Children.Add(_readout);
        _readout.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        _drawing.MouseMove += (_, e) => { var x=e.GetPosition(_drawing).X; UpdateReadout(Math.Clamp((int)Math.Round(x/Math.Max(1,_drawing.ActualWidth)*255),0,255)); };
        _drawing.MouseLeave += (_, _) => UpdateReadout(null);
        System.ComponentModel.PropertyChangedEventManager.AddHandler(StudioLocalizationService.Current, OnLanguageChanged,"Language");
        ToolTip = "RGB 横轴为 sRGB 编码值 0–255；亮度为线性 sRGB Y 0–1。纵轴为可见代理像素数，透明像素不计。";
        UpdateReadout(null);
    }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs _) { var view=(StudioHistogramView)target;view._drawing.Histogram=view.Histogram;view._drawing.Channel=view.Channel;view.UpdateReadout(null); }
    private void OnLanguageChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e) { if(Dispatcher.CheckAccess())UpdateReadout(null);else Dispatcher.BeginInvoke(()=>UpdateReadout(null)); }
    public void UpdateReadout(int? bin)
    {
        var strings=StudioLocalizationService.Current;
        if(Histogram is null){_readout.Text=(Channel=="亮度"?"Y · ":"RGB · ")+strings["NoTarget"];return;}
        if(bin is not { } index){_readout.Text=strings[Channel=="亮度"?"HistogramLumaScale":"HistogramRgbScale"];return;}
        index=Math.Clamp(index,0,255);var h=Histogram;
        var unit=strings["Pixels"];
        _readout.Text=Channel switch {"R"=>$"R {index}: {h.R[index]} {unit}","G"=>$"G {index}: {h.G[index]} {unit}","B"=>$"B {index}: {h.B[index]} {unit}","亮度"=>$"Y {index/255d:0.000}: {h.Luma[index]} {unit}",_=>$"{index}   R {h.R[index]}  G {h.G[index]}  B {h.B[index]}"};
    }
}
