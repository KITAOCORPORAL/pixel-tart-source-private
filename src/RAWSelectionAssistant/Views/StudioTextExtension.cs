using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

[MarkupExtensionReturnType(typeof(string))]
public sealed class StudioTextExtension : MarkupExtension
{
    public StudioTextExtension() { }
    public StudioTextExtension(string key) => Key=key;
    [ConstructorArgument("key")]
    public string Key { get; set; } = "";
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding($"[{Key}]") { Source=StudioLocalizationService.Current,Mode=BindingMode.OneWay }.ProvideValue(serviceProvider);
    public static void Bind(DependencyObject target,DependencyProperty property,string key)=>BindingOperations.SetBinding(target,property,new Binding($"[{key}]"){Source=StudioLocalizationService.Current,Mode=BindingMode.OneWay});
}

public sealed class StudioLanguageSelector : ComboBox
{
    public StudioLanguageSelector()
    {
        var initializing=true;
        SetResourceReference(StyleProperty,typeof(ComboBox));
        Width=112;MinWidth=100;ToolTip="Color Studio 界面语言 / UI language";
        ItemsSource=StudioLocalizationService.Current.Languages;DisplayMemberPath=nameof(StudioLanguageOption.DisplayText);SelectedValuePath=nameof(StudioLanguageOption.Code);
        SelectedValue=StudioLocalizationService.Current.Language;
        System.Windows.Automation.AutomationProperties.SetName(this,"Color Studio 界面语言");
        SelectionChanged+=(_,_)=>{if(!initializing && SelectedValue is string code && code!=StudioLocalizationService.Current.Language) { StudioLocalizationService.Current.SetLanguage(code);if(StudioLocalizationService.Current.LastPersistenceError is not null)ToolTip="本次语言已切换，但设置未能保存到磁盘。"; }};
        initializing=false;
    }
}
