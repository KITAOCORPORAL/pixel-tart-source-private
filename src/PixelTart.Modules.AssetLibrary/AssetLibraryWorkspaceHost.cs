using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>
/// Keeps the library switch affordance deliberately small so the image workspace owns the space.
/// The next page is fully validated and initialized before the current page is released.
/// </summary>
public sealed class AssetLibraryWorkspaceHost : UserControl, IAsyncDisposable
{
    private readonly Func<string, AssetLibraryPage> _pageFactory;
    private readonly AssetLibraryContainerService _containers = new();
    private readonly AssetLibraryPortableSettings _settings;
    private readonly Func<Task>? _persistSettings;
    private readonly string _legacyDatabasePath;
    private readonly ContentControl _content = new();
    private readonly TextBlock _state = new();
    private readonly Button _libraryButton = new();
    private readonly ContextMenu _menu = new();
    private readonly SemaphoreSlim _switchGate = new(1, 1);
    private AssetLibraryPage? _page;
    private AssetLibraryContainerDescriptor? _descriptor;
    private AssetLibraryWriteLease? _lease;
    private Task? _startupTask;
    private Task? _disposeTask;
    private int _disposed;

    public AssetLibraryWorkspaceHost(
        string initialDatabasePath,
        Func<string, AssetLibraryPage> pageFactory,
        AssetLibraryPortableSettings settings,
        Func<Task>? persistSettings = null)
    {
        _pageFactory = pageFactory ?? throw new ArgumentNullException(nameof(pageFactory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _legacyDatabasePath = Path.GetFullPath(initialDatabasePath);
        _settings.Normalize();
        _persistSettings = persistSettings;

        var root = new Grid { Background = Brushes.Transparent };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var bar = new Grid { Margin = new Thickness(10, 4, 10, 3), MinHeight = 30 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _libraryButton.Content = "当前库";
        _libraryButton.ToolTip = "打开素材库菜单（当前库、最近库、新建、打开、信息）";
        _libraryButton.Padding = new Thickness(10, 3, 10, 3);
        _libraryButton.MinHeight = 28;
        _libraryButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        _libraryButton.Click += (_, _) => _menu.IsOpen = true;
        _libraryButton.ContextMenu = _menu;
        Grid.SetColumn(_libraryButton, 0);
        bar.Children.Add(_libraryButton);
        _state.Text = "正在准备素材库…";
        _state.Margin = new Thickness(10, 0, 0, 0);
        _state.VerticalAlignment = VerticalAlignment.Center;
        _state.Foreground = SystemColors.GrayTextBrush;
        _state.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_state, 1);
        bar.Children.Add(_state);
        Grid.SetRow(bar, 0);
        root.Children.Add(bar);
        var divider = new Border { Background = new SolidColorBrush(Color.FromArgb(45, 128, 128, 128)) };
        Grid.SetRow(divider, 1);
        root.Children.Add(divider);
        Grid.SetRow(_content, 2);
        root.Children.Add(_content);
        Content = root;

        BuildMenu(initialDatabasePath);
        Loaded += (_, _) => StartStartup(initialDatabasePath);
        Unloaded += async (_, _) =>
        {
            if (Application.Current?.MainWindow?.IsLoaded == true) return;
            await DisposeAsync();
        };
    }

    public AssetLibraryPage? CurrentPage => _page;
    public AssetLibraryContainerDescriptor? CurrentDescriptor => _descriptor;
    public bool IsOffline => _descriptor is null && !string.IsNullOrWhiteSpace(_settings.CurrentContainerPath);

    private void BuildMenu(string initialDatabasePath)
    {
        _menu.Items.Add(MenuItem("新建素材库…", (_, _) => CreateLibraryAsync()));
        _menu.Items.Add(MenuItem("打开素材库…", (_, _) => OpenLibraryAsync()));
        _menu.Items.Add(MenuItem("迁移本机旧库…", (_, _) => MigrateLegacyLibraryAsync()));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(MenuItem("重新定位离线库…", (_, _) => RelocateOfflineLibraryAsync()));
        _menu.Items.Add(MenuItem("定位当前库…", (_, _) => LocateCurrentLibrary()));
        _menu.Items.Add(MenuItem("当前库信息", (_, _) => ShowCurrentInfo()));
        _menu.Items.Add(new Separator());
        RefreshRecentMenu();
        _menu.Opened += (_, _) => RefreshRecentMenu();
    }

    private MenuItem MenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header, Padding = new Thickness(10, 5, 24, 5) };
        item.Click += handler;
        return item;
    }

    private void RefreshRecentMenu()
    {
        while (_menu.Items.Count > 8) _menu.Items.RemoveAt(8);
        if (_settings.RecentLibraries.Count == 0)
        {
            _menu.Items.Add(new MenuItem { Header = "暂无最近素材库", IsEnabled = false });
            return;
        }
        foreach (var recent in _settings.RecentLibraries.Take(AssetLibraryPortableSettings.MaximumRecentLibraries))
        {
            var item = MenuItem($"{recent.DisplayName}  ·  {recent.ContainerPath}", async (_, _) => await SwitchToContainerAsync(recent.ContainerPath));
            item.ToolTip = recent.ContainerPath;
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, "AssetLibraryRecent_" + recent.LibraryId.ToString("N"));
            _menu.Items.Add(item);
        }
    }

    private void StartStartup(string initialDatabasePath)
    {
        if (_startupTask is not null) return;
        _startupTask = InitializeStartupAsync(initialDatabasePath);
    }

    private async Task InitializeStartupAsync(string initialDatabasePath)
    {
        try
        {
            var configured = _settings.CurrentContainerPath;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                try
                {
                    var descriptor = await _containers.OpenAsync(configured);
                    await SwitchToDescriptorAsync(descriptor, recordRecent: false);
                    return;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
                {
                    _state.Text = $"离线：当前库不可用（{exception.Message}）。原有数据库未修改。";
                    ShowChoiceState(
                        "当前素材库离线",
                        $"仍保留：{configured}\n请从“当前库”菜单重新打开已移动的库，或选择其他库。",
                        "打开其他库",
                        OpenLibraryAsync);
                    return;
                }
            }
            if (File.Exists(initialDatabasePath))
            {
                _state.Text = "发现本机旧库 · 请选择继续使用或迁移";
                ShowChoiceState(
                    "发现本机旧素材库",
                    "可以继续使用原库，也可以从“当前库”菜单安全迁移为可移动素材库。迁移不会删除或改写原库。",
                    "继续使用旧库",
                    async () => await ShowLegacyPageAsync(initialDatabasePath));
            }
            else
            {
                _state.Text = "尚未选择素材库";
                ShowChoiceState("开始使用素材库", "创建一个可迁移素材库，或打开已有的 .ptlibrary。", "新建素材库", CreateLibraryAsync);
            }
        }
        catch (Exception exception)
        {
            _state.Text = $"素材库启动失败：{exception.Message}";
        }
    }

    private async Task ShowLegacyPageAsync(string databasePath)
    {
        var page = _pageFactory(databasePath);
        await page.InitializeForSessionAsync();
        _page = page;
        _content.Content = page;
        _libraryButton.Content = "当前库 · 本机旧库";
        if (page.ViewModel.HasLoadError) _state.Text = page.ViewModel.LoadErrorMessage;
        else _state.Text = "本机旧库 · 可从当前库菜单创建或打开可迁移素材库";
    }

    private void ShowChoiceState(string title, string message, string actionText, Action action)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 560 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 16), Foreground = SystemColors.GrayTextBrush });
        var button = new Button { Content = actionText, MinWidth = 130, MinHeight = 34, Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
        _content.Content = panel;
    }

    public async Task SwitchToContainerAsync(string containerPath)
    {
        try
        {
            var descriptor = await _containers.OpenAsync(containerPath);
            await SwitchToDescriptorAsync(descriptor, recordRecent: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _state.Text = $"切换失败：{exception.Message}；仍保留当前库。";
        }
    }

    private async Task SwitchToDescriptorAsync(AssetLibraryContainerDescriptor descriptor, bool recordRecent)
    {
        await _switchGate.WaitAsync();
        AssetLibraryWriteLease? nextLease = null;
        AssetLibraryPage? nextPage = null;
        try
        {
            nextLease = await _containers.AcquireWriteLeaseAsync(descriptor);
            nextPage = _pageFactory(descriptor.DatabasePath);
            await nextPage.InitializeForSessionAsync();
            if (nextPage.ViewModel.HasLoadError) throw new InvalidDataException(nextPage.ViewModel.LoadErrorMessage);

            var oldPage = _page;
            var oldLease = _lease;
            _page = nextPage;
            _descriptor = descriptor;
            _content.Content = nextPage;
            _lease = nextLease;
            nextLease = null;
            nextPage = null;
            if (recordRecent)
            {
                _settings.RecordOpened(descriptor.LibraryId, descriptor.DisplayName, descriptor.ContainerPath, DateTimeOffset.UtcNow);
                if (_persistSettings is not null) await _persistSettings();
            }
            _libraryButton.Content = $"当前库 · {descriptor.DisplayName}";
            _state.Text = $"{descriptor.ContentMode} · {descriptor.ContainerPath}";
            if (oldPage is not null) await oldPage.DisposeAsync();
            if (oldLease is not null) await oldLease.DisposeAsync();
        }
        finally
        {
            if (nextPage is not null) await nextPage.DisposeAsync();
            if (nextLease is not null) await nextLease.DisposeAsync();
            _switchGate.Release();
        }
    }

    private async void CreateLibraryAsync()
    {
        var dialog = new SaveFileDialog { Title = "新建可迁移素材库", Filter = "素材库 (*.ptlibrary)|*.ptlibrary", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var descriptor = await _containers.CreateAsync(dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName));
            await SwitchToDescriptorAsync(descriptor, recordRecent: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { _state.Text = $"新建失败：{exception.Message}；仍保留当前库。"; }
    }

    private async void OpenLibraryAsync()
    {
        var dialog = new OpenFolderDialog { Title = "打开可迁移素材库", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        await SwitchToContainerAsync(dialog.FolderName);
    }

    private async void RelocateOfflineLibraryAsync()
    {
        var currentPath = _settings.CurrentContainerPath;
        var expected = _settings.RecentLibraries.FirstOrDefault(entry =>
            string.Equals(entry.ContainerPath, currentPath, StringComparison.OrdinalIgnoreCase));
        if (expected is null) { _state.Text = "当前没有需要重新定位的离线库记录。"; return; }
        var dialog = new OpenFolderDialog { Title = $"重新定位：{expected.DisplayName}", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var descriptor = await _containers.OpenAsync(dialog.FolderName);
            if (descriptor.LibraryId != expected.LibraryId) throw new InvalidDataException("所选目录不是原素材库（库 ID 不一致）。");
            await SwitchToDescriptorAsync(descriptor, recordRecent: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { _state.Text = $"重新定位失败：{exception.Message}"; }
    }

    private async void MigrateLegacyLibraryAsync()
    {
        if (!File.Exists(_legacyDatabasePath)) { _state.Text = "未找到可迁移的本机旧库。"; return; }
        var dialog = new SaveFileDialog { Title = "将本机旧库迁移为可迁移素材库", Filter = "素材库 (*.ptlibrary)|*.ptlibrary", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var descriptor = await _containers.MigrateLegacyDatabaseAsync(_legacyDatabasePath, dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName));
            await SwitchToDescriptorAsync(descriptor, recordRecent: true);
            _state.Text = $"迁移完成 · 旧库仍保留在 {_legacyDatabasePath}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { _state.Text = $"迁移失败：{exception.Message}；旧库未修改。"; }
    }

    private void LocateCurrentLibrary()
    {
        if (_descriptor is null) { _state.Text = "当前仍在使用本机旧库。"; return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{_descriptor.ContainerPath}\"") { UseShellExecute = true }); }
        catch (Exception exception) { _state.Text = $"无法定位当前库：{exception.Message}"; }
    }

    private void ShowCurrentInfo()
    {
        if (_descriptor is null) { MessageBox.Show("当前使用本机旧数据库。可从菜单新建或打开 .ptlibrary。", "当前库", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        MessageBox.Show($"{_descriptor.DisplayName}\n\n路径：{_descriptor.ContainerPath}\n库 ID：{_descriptor.LibraryId}\n内容模式：{_descriptor.ContentMode}", "当前库信息", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
        _disposeTask ??= DisposeCoreAsync();
        return new ValueTask(_disposeTask);
    }

    private async Task DisposeCoreAsync()
    {
        if (_startupTask is not null) await _startupTask;
        if (_page is not null) await _page.DisposeAsync();
        if (_lease is not null) await _lease.DisposeAsync();
        _page = null;
        _lease = null;
        _switchGate.Dispose();
    }
}
