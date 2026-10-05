using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

public sealed class StudioExportFormatSelector : ComboBox
{
    public StudioExportFormatSelector()
    {
        SetResourceReference(StyleProperty,typeof(ComboBox));Width=132;MinWidth=112;
        SelectedValuePath="Tag";
        foreach(var (format,key) in new[]{(StudioExportFormat.Source,"ExportSource"),(StudioExportFormat.Jpeg,"ExportJpeg"),(StudioExportFormat.Png,"ExportPng"),(StudioExportFormat.Tiff,"ExportTiff")})
        {var item=new ComboBoxItem{Tag=format};StudioTextExtension.Bind(item,ContentControl.ContentProperty,key);Items.Add(item);}
        SetBinding(SelectedValueProperty,new Binding("QuickExportFormat"){Mode=BindingMode.TwoWay});
        SetBinding(ToolTipProperty,new Binding("QuickExportFormatSummary"));
        StudioTextExtension.Bind(this,System.Windows.Automation.AutomationProperties.NameProperty,"ExportFormat");
        System.ComponentModel.PropertyChangedEventManager.AddHandler(StudioLocalizationService.Current,OnLanguageChanged,"Language");
    }
    private void OnLanguageChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    { if(Dispatcher.CheckAccess())GetBindingExpression(ToolTipProperty)?.UpdateTarget();else Dispatcher.BeginInvoke(()=>GetBindingExpression(ToolTipProperty)?.UpdateTarget()); }
}
