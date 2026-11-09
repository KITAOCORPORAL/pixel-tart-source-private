using System.IO;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace PixelTart.Modules.AssetLibrary.FreeCanvas;

public sealed partial class FreeCanvasView
{
    private readonly Button _favoriteButton = new() { Content = "收藏所选素材", IsEnabled = false };
    private IReadOnlySet<CanvasFavorite> _favoriteIdentities = new HashSet<CanvasFavorite>();
    private bool _changingFavorites;
    public Func<Task<IReadOnlySet<CanvasFavorite>>>? FavoriteLoader { get; set; }
    public Func<IReadOnlyList<CanvasObject>, bool, Task>? SetFavorites { get; set; }
    private void InitializeFavorites()
    {
        var itemStyle = new System.Windows.Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new System.Windows.Setter(AutomationProperties.NameProperty, new Binding(nameof(CanvasObject.Name))));
        _sources.ItemContainerStyle = itemStyle;
        _sources.SetValue(AutomationProperties.AutomationIdProperty, "CanvasSources");
        _sources.SetValue(AutomationProperties.NameProperty, "拼贴素材列表");
        _sourceKind.SetValue(AutomationProperties.AutomationIdProperty, "CanvasSourceFilter");
        _sourceKind.SetValue(AutomationProperties.NameProperty, "拼贴素材来源");
        _sourceSearch.SetValue(AutomationProperties.NameProperty, "搜索拼贴素材");
        _favoriteButton.SetResourceReference(StyleProperty, "PixelTart.Button.Ghost");
        _favoriteButton.SetValue(AutomationProperties.AutomationIdProperty, "CanvasToggleFavorite");
        _favoriteButton.Click += async (_, _) => await ToggleFavoritesAsync();
        foreach (var button in ((StackPanel)((DockPanel)_drawer.Child).Children[0]).Children.OfType<Button>())
            if (Equals(button.Content, "加入所选素材")) button.SetValue(AutomationProperties.AutomationIdProperty, "CanvasAddSources");
        foreach (var button in HeaderPanel.Children.OfType<Button>())
        {
            if (Equals(button.Content, "画布")) button.SetValue(AutomationProperties.AutomationIdProperty, "CanvasMore");
            if (Equals(button.Content, "保存")) button.SetValue(AutomationProperties.AutomationIdProperty, "CanvasSave");
            if (Equals(button.Content, "导出")) button.SetValue(AutomationProperties.AutomationIdProperty, "CanvasExport");
        }
        _sources.SelectionChanged += (_, _) => UpdateFavoriteButton();
        UpdateFavoriteButton();
    }
    private CanvasObject[] FavoriteSelection() => _sources.SelectedItems.Cast<CanvasObject>().ToArray();
    private bool AllFavorite(CanvasObject[] items) => items.Length > 0 && items.All(item => item.AssetId is Guid id && _favoriteIdentities.Contains(new(item.LibraryId, id)));
    private void UpdateFavoriteButton()
    {
        var selected = FavoriteSelection();
        _favoriteButton.IsEnabled = !_changingFavorites && SetFavorites is not null && selected.Length > 0 && selected.All(item => item.IsImage && item.LibraryId != Guid.Empty && item.AssetId is Guid id && id != Guid.Empty);
        var text = AllFavorite(selected) ? "取消收藏" : "收藏所选素材";
        _favoriteButton.Content = text;
        _favoriteButton.ToolTip = text;
        _favoriteButton.SetValue(AutomationProperties.NameProperty, text);
    }
    private async Task ToggleFavoritesAsync()
    {
        if (_changingFavorites || SetFavorites is null) return;
        var selected = FavoriteSelection();
        if (!_favoriteButton.IsEnabled) return;
        var favorite = !AllFavorite(selected);
        _changingFavorites = true; UpdateFavoriteButton();
        try
        {
            await SetFavorites(selected, favorite);
            await RefreshSourcesAsync();
            // Keep the current selection when it remains visible, including a new favorite.
            foreach (var item in _sources.Items.Cast<CanvasObject>().Where(item => selected.Any(old => old.LibraryId == item.LibraryId && old.AssetId == item.AssetId)).ToArray()) _sources.SelectedItems.Add(item);
            _status.Text = favorite ? "已收藏所选素材" : "已取消收藏";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        { _status.Text = "收藏操作未完成：" + error.Message; }
        finally { _changingFavorites = false; UpdateFavoriteButton(); }
    }
}
