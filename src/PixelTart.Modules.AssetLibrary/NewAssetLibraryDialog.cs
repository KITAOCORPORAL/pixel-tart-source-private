using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace PixelTart.Modules.AssetLibrary;

public sealed record NewAssetLibraryRequest(string DisplayName, string ParentDirectory, string ContainerPath);

/// <summary>Collects a library name and parent folder without exposing the directory-package format.</summary>
public sealed class NewAssetLibraryDialog : Window
{
    private readonly TextBox _nameInput = new();
    private readonly TextBox _locationInput = new();
    private readonly TextBlock _preview = new();
    private readonly TextBlock _validation = new();
    private readonly Button _createButton = new();

    public NewAssetLibraryDialog(string? initialParentDirectory = null)
    {
        Title = "新建素材库";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MinHeight = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock { Text = "新建素材库", FontSize = 22, FontWeight = FontWeights.SemiBold };
        AutomationProperties.SetAutomationId(title, "NewAssetLibraryDialogTitle");
        root.Children.Add(title);

        var namePanel = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        namePanel.Children.Add(new TextBlock { Text = "素材库名称", Margin = new Thickness(0, 0, 0, 6) });
        _nameInput.Text = "我的素材库";
        _nameInput.SelectAll();
        AutomationProperties.SetName(_nameInput, "素材库名称");
        AutomationProperties.SetAutomationId(_nameInput, "NewAssetLibraryNameInput");
        _nameInput.TextChanged += (_, _) => RefreshPreview();
        namePanel.Children.Add(_nameInput);
        Grid.SetRow(namePanel, 1);
        root.Children.Add(namePanel);

        var locationPanel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        locationPanel.Children.Add(new TextBlock { Text = "保存位置", Margin = new Thickness(0, 0, 0, 6) });
        var locationRow = new Grid();
        locationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _locationInput.Text = ResolveInitialParent(initialParentDirectory);
        _locationInput.IsReadOnly = true;
        AutomationProperties.SetName(_locationInput, "素材库保存位置");
        AutomationProperties.SetAutomationId(_locationInput, "NewAssetLibraryLocationInput");
        locationRow.Children.Add(_locationInput);
        var choose = new Button { Content = "选择…", MinWidth = 86, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetName(choose, "选择素材库保存文件夹");
        AutomationProperties.SetAutomationId(choose, "NewAssetLibraryChooseLocationButton");
        choose.Click += ChooseLocation_Click;
        Grid.SetColumn(choose, 1);
        locationRow.Children.Add(choose);
        locationPanel.Children.Add(locationRow);
        Grid.SetRow(locationPanel, 2);
        root.Children.Add(locationPanel);

        var previewPanel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        previewPanel.Children.Add(new TextBlock { Text = "最终位置预览", FontWeight = FontWeights.SemiBold });
        _preview.Margin = new Thickness(0, 5, 0, 0);
        _preview.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetAutomationId(_preview, "NewAssetLibraryPathPreview");
        previewPanel.Children.Add(_preview);
        _validation.Margin = new Thickness(0, 6, 0, 0);
        _validation.TextWrapping = TextWrapping.Wrap;
        _validation.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        AutomationProperties.SetAutomationId(_validation, "NewAssetLibraryValidationMessage");
        previewPanel.Children.Add(_validation);
        Grid.SetRow(previewPanel, 3);
        root.Children.Add(previewPanel);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        var cancel = new Button { Content = "取消", MinWidth = 86, IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "SecondaryButton");
        AutomationProperties.SetAutomationId(cancel, "NewAssetLibraryCancelButton");
        actions.Children.Add(cancel);
        _createButton.Content = "创建素材库";
        _createButton.MinWidth = 110;
        _createButton.Margin = new Thickness(8, 0, 0, 0);
        _createButton.IsDefault = true;
        _createButton.SetResourceReference(StyleProperty, "PrimaryButton");
        AutomationProperties.SetName(_createButton, "创建素材库");
        AutomationProperties.SetAutomationId(_createButton, "NewAssetLibraryCreateButton");
        _createButton.Click += Create_Click;
        actions.Children.Add(_createButton);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = root;
        RefreshPreview();
        Loaded += (_, _) => { _nameInput.Focus(); RefreshPreview(); };
    }

    public NewAssetLibraryRequest? Request { get; private set; }
    internal TextBox NameInput => _nameInput;
    internal TextBox LocationInput => _locationInput;
    internal Button CreateButton => _createButton;
    internal string PreviewPath => _preview.Text;

    private void ChooseLocation_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "选择素材库保存位置", Multiselect = false, InitialDirectory = _locationInput.Text };
        if (picker.ShowDialog(this) != true) return;
        _locationInput.Text = picker.FolderName;
        RefreshPreview();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildRequest(out var request, out var error))
        {
            _validation.Text = error;
            return;
        }
        Request = request;
        DialogResult = true;
    }

    internal bool TryBuildRequest(out NewAssetLibraryRequest? request, out string error)
    {
        request = null;
        var name = _nameInput.Text.Trim();
        var parent = _locationInput.Text.Trim();
        if (name.Length == 0) { error = "请输入素材库名称。"; return false; }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
        { error = "素材库名称包含文件夹名称不支持的字符。"; return false; }
        if (!Directory.Exists(parent)) { error = "保存位置不存在，请重新选择文件夹。"; return false; }
        var path = Path.Combine(parent, name + AssetLibraryContainerService.ContainerExtension);
        if (Directory.Exists(path) || File.Exists(path)) { error = "同名素材库已经存在，请更换名称或保存位置。"; return false; }
        request = new NewAssetLibraryRequest(name, parent, path);
        error = string.Empty;
        return true;
    }

    private void RefreshPreview()
    {
        var safeName = _nameInput.Text.Trim();
        _preview.Text = safeName.Length == 0
            ? _locationInput.Text
            : Path.Combine(_locationInput.Text, safeName + AssetLibraryContainerService.ContainerExtension);
        _validation.Text = string.Empty;
    }

    private static string ResolveInitialParent(string? initialParentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(initialParentDirectory) && Directory.Exists(initialParentDirectory)) return Path.GetFullPath(initialParentDirectory);
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Directory.Exists(pictures) ? pictures : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
