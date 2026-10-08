using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

// Only attach to product-authored labels/statuses. Never to filenames or user node names.
public sealed class StudioProductTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        values.Length == 0 || values[0] is null ? "" : parameter is string key ? StudioLocalizationService.Current.Format(key, values[0]) : StudioLocalizationService.Current[values[0].ToString() ?? ""];
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class StudioProductTextExtension : MarkupExtension
{
    public StudioProductTextExtension() { }
    public StudioProductTextExtension(string path) => Path = path;
    public string Path { get; set; } = ".";
    public string? FormatKey { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding { Converter = new StudioProductTextConverter(), ConverterParameter = FormatKey, Mode = BindingMode.OneWay };
        binding.Bindings.Add(new Binding(Path));
        binding.Bindings.Add(new Binding(nameof(StudioLocalizationService.Language)) { Source = StudioLocalizationService.Current });
        return binding.ProvideValue(serviceProvider);
    }
}

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
        Width=112;MinWidth=100;StudioTextExtension.Bind(this,ToolTipProperty,"LanguageHelp");
        ItemsSource=StudioLocalizationService.Current.Languages;DisplayMemberPath=nameof(StudioLanguageOption.DisplayText);SelectedValuePath=nameof(StudioLanguageOption.Code);
        SelectedValue=StudioLocalizationService.Current.Language;
        StudioTextExtension.Bind(this,System.Windows.Automation.AutomationProperties.NameProperty,"LanguageHelp");
        SelectionChanged+=(_,_)=>{if(!initializing && SelectedValue is string code && code!=StudioLocalizationService.Current.Language) { StudioLocalizationService.Current.SetLanguage(code);StudioTextExtension.Bind(this,ToolTipProperty,StudioLocalizationService.Current.LastPersistenceError is not null?"LanguageSaveError":"LanguageHelp"); }};
        initializing=false;
    }
}
