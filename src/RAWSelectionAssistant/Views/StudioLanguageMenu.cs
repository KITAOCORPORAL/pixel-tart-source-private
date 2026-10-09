using System.ComponentModel;
using System.Windows.Controls;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

/// <summary>One global entry using the existing language/persistence authority.</summary>
public sealed class StudioLanguageMenu : MenuItem
{
    public StudioLanguageMenu()
    {
        StudioTextExtension.Bind(this, HeaderProperty, "Language");
        foreach (var option in StudioLocalizationService.Current.Languages)
        {
            var item = new MenuItem { Header = option.DisplayText, Tag = option.Code, IsCheckable = true };
            item.Click += (_, _) => StudioLocalizationService.Current.SetLanguage(option.Code);
            Items.Add(item);
        }
        PropertyChangedEventManager.AddHandler(StudioLocalizationService.Current, Changed, nameof(StudioLocalizationService.Language));
        RefreshChecks();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess()) RefreshChecks(); else Dispatcher.BeginInvoke(RefreshChecks);
    }
    private void RefreshChecks()
    {
        foreach (var item in Items.OfType<MenuItem>()) item.IsChecked = Equals(item.Tag, StudioLocalizationService.Current.Language);
        StudioTextExtension.Bind(this, ToolTipProperty, StudioLocalizationService.Current.LastPersistenceError is null ? "LanguageHelp" : "LanguageSaveError");
    }
}
