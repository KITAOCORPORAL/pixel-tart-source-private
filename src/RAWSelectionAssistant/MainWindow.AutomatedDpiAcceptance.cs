#if UI_REVIEW_BUILD
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant;

public partial class MainWindow
{
    private bool _automatedDpiAcceptanceEnabled;
    private double _automatedDpiScale = 1d;
    private int _automatedPhysicalWidth;
    private int _automatedPhysicalHeight;
    private string? _automatedMetadataPath;
    private string _automatedScenarioName = string.Empty;
    private string _automatedThemeName = "Dark";
    private Window? _automatedAuxiliaryWindow;
    private ContextMenu? _automatedContextMenu;
    private ToolTip? _automatedToolTip;

    private void ConfigureAutomatedDpiAcceptance(JsonElement root)
    {
        var scale = 0d;
        _automatedDpiAcceptanceEnabled = root.TryGetProperty("DpiScale", out var scaleElement) && scaleElement.TryGetDouble(out scale) && scale > 0;
        if (!_automatedDpiAcceptanceEnabled) return;

        _automatedDpiScale = scale;
        _automatedPhysicalWidth = root.TryGetProperty("PhysicalWidth", out var widthElement) ? widthElement.GetInt32() : 2560;
        _automatedPhysicalHeight = root.TryGetProperty("PhysicalHeight", out var heightElement) ? heightElement.GetInt32() : 1440;
        _automatedMetadataPath = root.TryGetProperty("MetadataPath", out var metadataElement) ? metadataElement.GetString() : null;
        _automatedScenarioName = root.TryGetProperty("State", out var stateElement) ? stateElement.GetString() ?? string.Empty : string.Empty;
        _automatedThemeName = root.TryGetProperty("Theme", out var themeElement) ? themeElement.GetString() ?? "Dark" : "Dark";
    }

    private async Task<bool> PrepareAutomatedDpiAcceptanceStateAsync(string? state)
    {
        if (!_automatedDpiAcceptanceEnabled || _viewModel is null || string.IsNullOrWhiteSpace(state)) return false;

        var demoDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KitaoPhotoSelector.UiReview",
            "DemoImages");
        var demoImages = Directory.Exists(demoDirectory)
            ? Directory.GetFiles(demoDirectory, "DPI_TEST_*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];

        if (state.StartsWith("Tether", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Lut", StringComparison.OrdinalIgnoreCase) || state.StartsWith("ColorProfile", StringComparison.OrdinalIgnoreCase) || state.StartsWith("ClientMonitor", StringComparison.OrdinalIgnoreCase) || state == "MixedDpi")
        {
            _viewModel.NavigateCommand.Execute("Tether");
            if (_viewModel.TetherPage is null) return false;
            var (assets, annotations) = CreateTetherReviewAssets(demoDirectory, state);
            _viewModel.TetherPage.ApplyReviewState(state, assets, annotations);
            return true;
        }

        switch (state)
        {
            case "WorkbenchDarkExpanded":
            case "WorkbenchDarkCollapsed":
            case "WorkbenchLight":
                return true;
            case "SettingsDialog":
                _viewModel.IsSettingsModalOpen = true;
                return true;
            case "ToolboxPopup":
            case "QuickToolsManager":
            case "FeedbackDialog":
            case "ConfirmationDialog":
            case "ContextMenu":
            case "Tooltip":
                return true;
            case "ToolboxFullPage":
                _viewModel.NavigateCommand.Execute("Toolbox");
                return true;
            case "OrganizeEmpty":
                _viewModel.NavigateCommand.Execute("PhotoGrouping");
                return true;
            case "OrganizeGrouped":
            case "OrganizeManifest":
                _viewModel.NavigateCommand.Execute("PhotoGrouping");
                await _viewModel.OrganizePhotosPage.AddPathsAsync(demoImages);
                if (state == "OrganizeManifest")
                {
                    _viewModel.OrganizePhotosPage.OutputPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "KitaoPhotoSelector.UiReview",
                        "OrganizeOutput");
                    if (_viewModel.OrganizePhotosPage.PreviewPlanCommand.CanExecute(null))
                        _viewModel.OrganizePhotosPage.PreviewPlanCommand.Execute(null);
                }
                return true;
            case "CollageEmpty":
                _viewModel.NavigateCommand.Execute("Collage");
                return true;
            case "Collage2x2":
            case "CollageVertical":
            case "CollageExport":
                _viewModel.NavigateCommand.Execute("Collage");
                _viewModel.CollagePage.AddPaths(demoImages);
                if (state == "Collage2x2")
                {
                    _viewModel.CollagePage.Mode = CollageMode.Template;
                    _viewModel.CollagePage.SelectedTemplate = CollageTemplateCatalog.Get("4-grid");
                    _viewModel.CollagePage.AspectRatio = "1:1";
                }
                else if (state == "CollageVertical")
                {
                    _viewModel.CollagePage.Mode = CollageMode.VerticalStrip;
                    _viewModel.CollagePage.AspectRatio = "9:16";
                    _viewModel.CollagePage.Spacing = 18;
                }
                else
                {
                    _viewModel.CollagePage.Mode = CollageMode.Template;
                    _viewModel.CollagePage.SelectedTemplate = CollageTemplateCatalog.Get("4-grid");
                    _viewModel.CollagePage.AspectRatio = "4:5";
                    _viewModel.CollagePage.BorderWidth = 5;
                    _viewModel.CollagePage.Shadow = true;
                    _viewModel.CollagePage.BackgroundColor = "#24364B";
                    _viewModel.CollagePage.Project.Export.Format = "PNG";
                }
                return true;
            default:
                return false;
        }
    }

    private static (IReadOnlyList<TetherAssetRecord> Assets, IReadOnlyDictionary<Guid, TetherAnnotationRecord> Annotations) CreateTetherReviewAssets(
        string demoDirectory,
        string state)
    {
        if (string.Equals(state, "TetherEmpty", StringComparison.OrdinalIgnoreCase))
            return ([], new Dictionary<Guid, TetherAnnotationRecord>());

        var sessionId = Guid.Parse("23000000-0000-0000-0000-000000000003");
        var now = DateTimeOffset.UtcNow;
        var imagePaths = Directory.Exists(demoDirectory)
            ? Directory.GetFiles(demoDirectory, "STAGEC_*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Take(12).ToArray()
            : [];
        var records = new List<TetherAssetRecord>();
        var rawId = Guid.Parse("23000000-0000-0000-0000-000000000099");
        var requestedCount = state switch
        {
            "TetherAssets1000" => 999,
            "TetherBurst" => 99,
            _ => imagePaths.Length
        };
        for (var index = 0; index < requestedCount && imagePaths.Length > 0; index++)
        {
            var id = Guid.Parse($"23000000-0000-0000-0000-{index + 1:000000000000}");
            var path = imagePaths[index % imagePaths.Length];
            var file = new FileInfo(path);
            records.Add(new(
                id, sessionId, null, path, path.ToUpperInvariant(), file.Name, file.Extension,
                TetherMediaKind.PreviewImage, file.Exists ? file.Length : null, file.Exists ? file.LastWriteTimeUtc : null,
                now.AddSeconds(-index * 12), TetherStabilityState.Stable,
                index == 7 ? TetherProcessingState.NeedsAttention : TetherProcessingState.Ready,
                TetherPreviewState.Ready, now.AddSeconds(-index * 12), now.AddSeconds(-index * 12),
                PairingKey: index == 0 ? "STAGEC_PAIR" : null, PairedAssetId: index == 0 ? rawId : null,
                LastErrorCode: index == 7 ? "TETHER_SOURCE_TEMPORARILY_UNAVAILABLE" : null));
        }

        var rawPath = Path.Combine(demoDirectory, "STAGEC_RAW.nef");
        var rawFile = new FileInfo(rawPath);
        records.Add(new(
            rawId, sessionId, null, rawPath, rawPath.ToUpperInvariant(), rawFile.Name, rawFile.Extension,
            TetherMediaKind.Raw, rawFile.Exists ? rawFile.Length : null, rawFile.Exists ? rawFile.LastWriteTimeUtc : null,
            now.AddSeconds(-160), TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Placeholder,
            now.AddSeconds(-160), now.AddSeconds(-160), PairingKey: "STAGEC_RAW_UNPAIRED"));

        var annotations = new Dictionary<Guid, TetherAnnotationRecord>();
        for (var index = 0; index < Math.Min(5, records.Count); index++)
        {
            var asset = records[index];
            annotations[asset.Id] = new(
                Guid.Parse($"23000000-0000-0000-0001-{index + 1:000000000000}"), asset.Id,
                index == 0 ? 5 : 4 - index % 3, index % 2 == 0 ? "绿" : "蓝",
                index == 0 ? "主光位置确认，保留这一张。" : null, now, now,
                ClientFavorite: index is 0 or 2, ClientNote: index == 0 ? "客户现场收藏" : null, IsRejected: index == 4);
        }
        return (records, annotations);
    }

    private void FinalizeAutomatedDpiAcceptanceState(string? state)
    {
        if (!_automatedDpiAcceptanceEnabled || _viewModel is null || string.IsNullOrWhiteSpace(state)) return;

        if (state == "ToolboxPopup")
        {
            WorkbenchToolboxPopup.IsOpen = true;
        }
        else if (state == "QuickToolsManager")
        {
            _automatedAuxiliaryWindow = new QuickToolsManagerWindow(_viewModel.Settings.PinnedQuickTools)
            {
                Owner = this,
                ShowInTaskbar = false
            };
            _automatedAuxiliaryWindow.Show();
        }
        else if (state == "FeedbackDialog")
        {
            var feedbackService = new FeedbackService(
                new FeedbackRequestBuilder().Build(),
                new WpfFeedbackClipboard(),
                new ShellFeedbackMailLauncher(),
                new FileLogService());
            _automatedAuxiliaryWindow = new FeedbackDialog(feedbackService)
            {
                Owner = this,
                ShowInTaskbar = false
            };
            _automatedAuxiliaryWindow.Show();
        }
        else if (state == "ConfirmationDialog")
        {
            _automatedAuxiliaryWindow = new UpgradeTutorialWindow
            {
                Owner = this,
                ShowInTaskbar = false
            };
            _automatedAuxiliaryWindow.Show();
        }
        else if (state == "ContextMenu")
        {
            _viewModel.SetQuickToolsCompact(false);
            QuickToolsOverflowButton.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(PinnedQuickToolsList, 3);
            PinnedQuickToolsList.UpdateLayout();
            var target = FindVisualChildren<Button>(PinnedQuickToolsList)
                .FirstOrDefault(button => button.ContextMenu is not null);
            if (target?.ContextMenu is not null)
            {
                _automatedContextMenu = target.ContextMenu;
                _automatedContextMenu.PlacementTarget = target;
                _automatedContextMenu.Placement = PlacementMode.Bottom;
                _automatedContextMenu.IsOpen = true;
            }
        }
        else if (state == "Tooltip")
        {
            _automatedToolTip = new ToolTip
            {
                Content = ToolboxQuickButton.ToolTip?.ToString() ?? "打开全部照片工具",
                PlacementTarget = ToolboxQuickButton,
                Placement = PlacementMode.Bottom,
                IsOpen = true
            };
        }
        else if (state.StartsWith("ClientMonitor", StringComparison.OrdinalIgnoreCase) || state == "MixedDpi")
        {
            var color = _viewModel.TetherPage?.ColorSettings;
            var client = new ClientMonitorViewModel
            {
                DisplayImage = color?.VisibleImage,
                FollowMode = state switch
                {
                    "ClientMonitorFollowLatest" => ClientMonitorFollowMode.FollowLatest,
                    "ClientMonitorLocked" => ClientMonitorFollowMode.Locked,
                    _ => ClientMonitorFollowMode.FollowMainSelection
                },
                ShowIdentifier = false,
                ShowTechnicalMetadata = false,
                ShowRating = state == "ClientMonitorFavoriteNote",
                ShowClientControls = state is not "ClientMonitorDisconnected" and not "ClientMonitorReconnected",
                IsFavorite = state == "ClientMonitorFavoriteNote",
                ClientNote = state == "ClientMonitorFavoriteNote" ? "喜欢服装和光线" : null,
                NewAssetCount = state == "ClientMonitorLocked" ? 3 : 0,
                StatusText = state switch
                {
                    "ClientMonitorDisconnected" => "客户显示器未连接 · 联机会话继续",
                    "ClientMonitorReconnected" => "客户显示器已重新连接 · 可恢复监看",
                    "ClientMonitorPrivacy" => "隐私默认：文件名与路径隐藏",
                    "MixedDpi" => "客户屏150% · 独立ICC",
                    _ => "客户监看开发版验证"
                }
            };
            _automatedAuxiliaryWindow = new ClientMonitorWindow { DataContext = client, Width = state == "MixedDpi" ? 900 : 1120, Height = state == "MixedDpi" ? 760 : 700, ShowInTaskbar = false };
            _automatedAuxiliaryWindow.Show();
        }

        _automatedAuxiliaryWindow?.UpdateLayout();
        _automatedContextMenu?.UpdateLayout();
        _automatedToolTip?.UpdateLayout();
    }

    private void CaptureAutomatedDpiFrame(string outputPath)
    {
        RootGrid.UpdateLayout();
        if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0) return;

        var physicalWidth = Math.Max(1, _automatedPhysicalWidth);
        var physicalHeight = Math.Max(1, _automatedPhysicalHeight);
        var scale = Math.Max(.25, _automatedDpiScale);
        var logicalWidth = physicalWidth / scale;
        var logicalHeight = physicalHeight / scale;
        var composition = new DrawingVisual();
        using (var drawing = composition.RenderOpen())
        {
            drawing.DrawRectangle(ResolveBackgroundBrush(), null, new Rect(0, 0, physicalWidth, physicalHeight));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.DrawRectangle(new VisualBrush(RootGrid), null, new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
            var toolboxPopupChild = string.Equals(_automatedScenarioName, "ToolboxPopup", StringComparison.OrdinalIgnoreCase)
                ? WorkbenchToolboxPopup.Child as FrameworkElement
                : WorkbenchToolboxPopup.IsOpen ? WorkbenchToolboxPopup.Child as FrameworkElement : null;
            DrawPopup(drawing, toolboxPopupChild, logicalWidth, logicalHeight, .63, .12);
            DrawPopup(drawing, QuickToolsOverflowPopup.IsOpen ? QuickToolsOverflowPopup.Child as FrameworkElement : null, logicalWidth, logicalHeight, .58, .12);
            DrawPopup(drawing, _automatedContextMenu, logicalWidth, logicalHeight, .46, .17);
            DrawPopup(drawing, _automatedToolTip, logicalWidth, logicalHeight, .60, .15);
            DrawAuxiliaryWindow(drawing, logicalWidth, logicalHeight);
            drawing.Pop();
        }

        var bitmap = new RenderTargetBitmap(physicalWidth, physicalHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(composition);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = outputPath + ".tmp";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(temporaryPath)) encoder.Save(stream);
        File.Move(temporaryPath, outputPath, true);

        WriteAutomatedDpiMetadata(outputPath, logicalWidth, logicalHeight);
        CloseAutomatedDpiOverlays();
    }

    private Brush ResolveBackgroundBrush() =>
        RootGrid.Background is SolidColorBrush brush ? new SolidColorBrush(brush.Color) : Brushes.Black;

    private static void DrawPopup(DrawingContext drawing, FrameworkElement? popup, double logicalWidth, double logicalHeight, double xRatio, double yRatio)
    {
        if (popup is null) return;
        popup.UpdateLayout();
        if (popup.ActualWidth <= 0 || popup.ActualHeight <= 0)
        {
            popup.Measure(new Size(logicalWidth, logicalHeight));
            popup.Arrange(new Rect(popup.DesiredSize));
        }
        var width = Math.Min(popup.ActualWidth > 0 ? popup.ActualWidth : popup.DesiredSize.Width, logicalWidth - 24);
        var height = Math.Min(popup.ActualHeight > 0 ? popup.ActualHeight : popup.DesiredSize.Height, logicalHeight - 24);
        var x = Math.Clamp(logicalWidth * xRatio, 12, Math.Max(12, logicalWidth - width - 12));
        var y = Math.Clamp(logicalHeight * yRatio, 12, Math.Max(12, logicalHeight - height - 12));
        drawing.DrawRectangle(new VisualBrush(popup), null, new Rect(x, y, width, height));
    }

    private void DrawAuxiliaryWindow(DrawingContext drawing, double logicalWidth, double logicalHeight)
    {
        if (_automatedAuxiliaryWindow?.Content is not FrameworkElement content) return;
        _automatedAuxiliaryWindow.UpdateLayout();
        content.UpdateLayout();
        var width = Math.Min(
            content.ActualWidth > 0 ? content.ActualWidth : Math.Max(320, _automatedAuxiliaryWindow.Width),
            logicalWidth - 32);
        var height = Math.Min(
            content.ActualHeight > 0 ? content.ActualHeight : Math.Max(220, _automatedAuxiliaryWindow.Height),
            logicalHeight - 32);
        var x = Math.Max(16, (logicalWidth - width) / 2);
        var y = Math.Max(16, (logicalHeight - height) / 2);
        drawing.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, new Rect(0, 0, logicalWidth, logicalHeight));
        drawing.DrawRectangle(new VisualBrush(content), null, new Rect(x, y, width, height));
    }

    private void WriteAutomatedDpiMetadata(string outputPath, double logicalWidth, double logicalHeight)
    {
        FrameworkElement layoutRoot = string.Equals(_automatedScenarioName, "SettingsDialog", StringComparison.OrdinalIgnoreCase)
            ? SettingsModal
            : string.Equals(_automatedScenarioName, "Settings", StringComparison.OrdinalIgnoreCase)
                ? SettingsPageContent
                : IsTetherColorReviewState(_automatedScenarioName)
                    ? TetherMonitorView
                    : RootGrid;
        var layout = InspectLayout(layoutRoot, layoutRoot.ActualWidth, layoutRoot.ActualHeight);
        var auxiliary = _automatedAuxiliaryWindow?.Content is FrameworkElement content
            ? InspectLayout(content, content.ActualWidth, content.ActualHeight)
            : null;
        var themeInspection = InspectThemeResources();
        var metadata = new
        {
            scenario = _automatedScenarioName,
            theme = _automatedThemeName,
            validationMode = "automated-logical-simulation",
            physicalDpiManuallyTested = false,
            targetDpiX = 96d * _automatedDpiScale,
            targetDpiY = 96d * _automatedDpiScale,
            scale = _automatedDpiScale,
            physicalViewport = new { width = _automatedPhysicalWidth, height = _automatedPhysicalHeight },
            logicalViewport = new { width = logicalWidth, height = logicalHeight },
            rootActual = new { width = RootGrid.ActualWidth, height = RootGrid.ActualHeight },
            screenshot = outputPath,
            sourceCommit = ResolveSourceCommit(),
            layout,
            auxiliaryLayout = auxiliary,
            themeInspection,
            passed = layout.BlockingIssueCount == 0 && (auxiliary?.BlockingIssueCount ?? 0) == 0 && themeInspection.Passed,
            generatedAt = DateTimeOffset.Now
        };
        var metadataPath = string.IsNullOrWhiteSpace(_automatedMetadataPath) ? outputPath + ".json" : _automatedMetadataPath;
        var directory = Path.GetDirectoryName(metadataPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }

    private LayoutInspection InspectLayout(FrameworkElement root, double viewportWidth, double viewportHeight)
    {
        root.UpdateLayout();
        var elements = FindVisualChildren<FrameworkElement>(root)
            .Prepend(root)
            .Where(element => element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0)
            .Distinct()
            .ToArray();
        var bounds = new List<ElementBounds>();
        var overflow = new List<string>();
        var zeroSized = new List<string>();
        var textClipping = new List<string>();
        var whiteSurface = new List<string>();

        foreach (var element in elements)
        {
            Rect rect;
            try
            {
                var origin = element.TransformToAncestor(root).Transform(new Point(0, 0));
                rect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            }
            catch
            {
                continue;
            }

            var identity = ElementIdentity(element);
            bounds.Add(new ElementBounds(identity, element.GetType().Name, rect.X, rect.Y, rect.Width, rect.Height));
            if (element.TemplatedParent is null &&
                (rect.Left < -2 || rect.Top < -2 || rect.Right > viewportWidth + 2 || rect.Bottom > viewportHeight + 2) &&
                !HasClippingAncestor(element, root))
                overflow.Add(identity);

            if (IsInteractive(element) && (element.ActualWidth < 1 || element.ActualHeight < 1)) zeroSized.Add(identity);
            if (element is TextBlock textBlock && IsTextClipped(textBlock)) textClipping.Add(identity);
            if (string.Equals(_automatedThemeName, "Dark", StringComparison.OrdinalIgnoreCase) && element is Control control && HasUnexpectedWhiteSurface(control))
                whiteSurface.Add(identity);
        }

        foreach (var interactive in FindVisualChildren<FrameworkElement>(root).Where(IsInteractive).Where(element => element.IsVisible && element.TemplatedParent is null))
        {
            if (interactive.ActualWidth < 1 || interactive.ActualHeight < 1) zeroSized.Add(ElementIdentity(interactive));
        }

        var explicitInteractiveBounds = elements
            .Where(IsInteractive)
            .Where(element => element.TemplatedParent is null)
            .Select(element => (Element: element, Bounds: TryGetElementBounds(element, root)))
            .Where(item => item.Bounds is not null)
            .ToArray();
        var overlaps = new List<string>();
        for (var leftIndex = 0; leftIndex < explicitInteractiveBounds.Length; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < explicitInteractiveBounds.Length; rightIndex++)
        {
            var left = explicitInteractiveBounds[leftIndex];
            var right = explicitInteractiveBounds[rightIndex];
            if (IsAncestor(left.Element, right.Element) || IsAncestor(right.Element, left.Element)) continue;
            var intersection = Rect.Intersect(left.Bounds!.Rect, right.Bounds!.Rect);
            if (!intersection.IsEmpty && intersection.Width > 2 && intersection.Height > 2 && !IsAllowedUtilityOverlay(left.Bounds, right.Bounds, intersection))
                overlaps.Add($"{left.Bounds.Identity} <> {right.Bounds.Identity}");
        }

        var focusTarget = elements.OfType<Button>().FirstOrDefault(button => button.IsEnabled && button.Focusable);
        var focusBounds = focusTarget is null ? null : TryGetElementBounds(focusTarget, root);
        var focusVisible = focusBounds is not null && focusBounds.Rect.IntersectsWith(new Rect(0, 0, viewportWidth, viewportHeight));
        var blocking = overflow.Distinct().Count() + overlaps.Distinct().Count() + zeroSized.Distinct().Count() + textClipping.Distinct().Count() + whiteSurface.Distinct().Count();
        return new LayoutInspection(
            bounds.Count,
            overflow.Distinct().ToArray(),
            overlaps.Distinct().ToArray(),
            zeroSized.Distinct().ToArray(),
            textClipping.Distinct().ToArray(),
            whiteSurface.Distinct().ToArray(),
            focusTarget is null ? string.Empty : ElementIdentity(focusTarget),
            focusVisible,
            blocking);
    }

    private ThemeInspection InspectThemeResources()
    {
        string[] requiredBrushes =
        [
            "WindowBackgroundBrush", "ContentBackgroundBrush", "SurfacePrimaryBrush", "SurfaceSecondaryBrush",
            "RaisedSurfaceBrush", "TextPrimaryBrush", "TextSecondaryBrush", "InputBackgroundBrush",
            "InputForegroundBrush", "InputBorderBrush", "DropdownBackgroundBrush", "TooltipBackgroundBrush",
            "ScrollBarTrackBrush", "ScrollBarThumbBrush", "MenuPopupBackgroundBrush", "MenuPopupBorderBrush"
        ];
        var missing = requiredBrushes.Where(key => Application.Current.TryFindResource(key) is not Brush).ToArray();
        var highContrastDictionaryExists = Application.Current.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains("HighContrast", StringComparison.OrdinalIgnoreCase) == true);
        var highContrastSourceExists = CanLoadResourceDictionary("Resources/DesignSystem/Theme.HighContrast.xaml");
        string[] controlTypes =
        [
            nameof(TextBox), nameof(PasswordBox), nameof(ComboBox), nameof(ComboBoxItem), nameof(CheckBox), nameof(RadioButton),
            nameof(ToggleButton), nameof(Slider), nameof(ScrollBar), nameof(ScrollViewer), nameof(DatePicker), nameof(DataGrid),
            nameof(DataGridColumnHeader), nameof(ContextMenu), nameof(MenuItem), nameof(Popup), nameof(TabControl), nameof(ToolTip), nameof(ProgressBar)
        ];
        var visibleTypes = FindVisualChildren<FrameworkElement>(RootGrid)
            .Where(element => element.IsVisible)
            .GroupBy(element => element.GetType().Name)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var renderedTypes = controlTypes.ToDictionary(type => type, type => visibleTypes.TryGetValue(type, out var count) ? count : 0);
        var controlRenderChecks = controlTypes.ToDictionary(type => type, VerifyControlStyleRender, StringComparer.OrdinalIgnoreCase);
        var primary = Application.Current.TryFindResource("TextPrimaryBrush") as SolidColorBrush;
        var background = Application.Current.TryFindResource("WindowBackgroundBrush") as SolidColorBrush;
        var contrast = primary is not null && background is not null ? ContrastRatio(primary.Color, background.Color) : 0;
        return new ThemeInspection(
            _automatedThemeName,
            missing,
            highContrastDictionaryExists || highContrastSourceExists,
            renderedTypes,
            controlRenderChecks,
            contrast,
            missing.Length == 0 && (highContrastDictionaryExists || highContrastSourceExists) && contrast >= 4.5 && controlRenderChecks.Values.All(value => value));
    }

    private static bool VerifyControlStyleRender(string typeName)
    {
        try
        {
            FrameworkElement element = typeName switch
            {
                nameof(TextBox) => new TextBox { Text = "DPI" },
                nameof(PasswordBox) => new PasswordBox { Password = "DPI" },
                nameof(ComboBox) => new ComboBox { ItemsSource = new[] { "DPI" }, SelectedIndex = 0 },
                nameof(ComboBoxItem) => new ComboBoxItem { Content = "DPI" },
                nameof(CheckBox) => new CheckBox { Content = "DPI", IsChecked = true },
                nameof(RadioButton) => new RadioButton { Content = "DPI", IsChecked = true },
                nameof(ToggleButton) => new ToggleButton { Content = "DPI", IsChecked = true },
                nameof(Slider) => new Slider { Value = 50 },
                nameof(ScrollBar) => new ScrollBar { Maximum = 100, Value = 25 },
                nameof(ScrollViewer) => new ScrollViewer { Content = new TextBlock { Text = "DPI" } },
                nameof(DatePicker) => new DatePicker { SelectedDate = DateTime.Today },
                nameof(DataGrid) => new DataGrid { ItemsSource = new[] { new { Value = "DPI" } }, AutoGenerateColumns = true },
                nameof(DataGridColumnHeader) => new DataGridColumnHeader { Content = "DPI" },
                nameof(ContextMenu) => new ContextMenu { Items = { new MenuItem { Header = "DPI" } } },
                nameof(MenuItem) => new MenuItem { Header = "DPI" },
                nameof(Popup) => new Popup { Child = new Border { Width = 120, Height = 40, Background = Brushes.Gray } },
                nameof(TabControl) => new TabControl { Items = { new TabItem { Header = "DPI", Content = "DPI" } }, SelectedIndex = 0 },
                nameof(ToolTip) => new ToolTip { Content = "DPI" },
                nameof(ProgressBar) => new ProgressBar { Maximum = 100, Value = 50 },
                _ => throw new InvalidOperationException(typeName)
            };
            var implicitStyle = Application.Current.TryFindResource(element.GetType()) as Style;
            if (implicitStyle is not null) element.Style = implicitStyle;
            element.Width = element is DataGrid ? 300 : 240;
            element.Height = element is DataGrid ? 100 : 44;
            element.Measure(new Size(element.Width, element.Height));
            element.Arrange(new Rect(0, 0, element.Width, element.Height));
            element.UpdateLayout();
            if (element is Control control) control.ApplyTemplate();
            var bitmap = new RenderTargetBitmap((int)element.Width, (int)element.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            return bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0 &&
                   (implicitStyle is not null || element is System.Windows.Controls.Primitives.Popup or System.Windows.Controls.ContextMenu or System.Windows.Controls.MenuItem or System.Windows.Controls.ToolTip);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInteractive(FrameworkElement element) =>
        element is ButtonBase or TextBoxBase or Selector or Slider or DatePicker;

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject child)
    {
        for (DependencyObject? current = child; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private static ElementBounds? TryGetElementBounds(FrameworkElement element, FrameworkElement root)
    {
        try
        {
            var origin = element.TransformToAncestor(root).Transform(new Point(0, 0));
            var visibleRect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            for (DependencyObject? current = VisualTreeHelper.GetParent(element); current is FrameworkElement ancestor && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
            {
                if (ancestor is not ScrollViewer and not ScrollContentPresenter && ancestor.Clip is null && !ancestor.ClipToBounds) continue;
                var ancestorOrigin = ancestor.TransformToAncestor(root).Transform(new Point(0, 0));
                visibleRect.Intersect(new Rect(ancestorOrigin.X, ancestorOrigin.Y, ancestor.ActualWidth, ancestor.ActualHeight));
                if (visibleRect.IsEmpty) return null;
            }
            return new ElementBounds(ElementIdentity(element), element.GetType().Name, visibleRect.X, visibleRect.Y, visibleRect.Width, visibleRect.Height);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasClippingAncestor(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? current = VisualTreeHelper.GetParent(element); current is not null && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer or ScrollContentPresenter) return true;
            if (current is UIElement uiElement && (uiElement.Clip is not null || uiElement.ClipToBounds)) return true;
        }
        return false;
    }

    private static bool IsAllowedUtilityOverlay(ElementBounds left, ElementBounds right, Rect intersection)
    {
        if (left.Identity.Contains("关闭检查器", StringComparison.OrdinalIgnoreCase) ||
            right.Identity.Contains("关闭检查器", StringComparison.OrdinalIgnoreCase))
            return true;
        var leftArea = left.Width * left.Height;
        var rightArea = right.Width * right.Height;
        var smaller = leftArea <= rightArea ? left : right;
        var smallerArea = Math.Min(leftArea, rightArea);
        var largerArea = Math.Max(leftArea, rightArea);
        var semanticUtility = smaller.Identity.Contains("说明", StringComparison.OrdinalIgnoreCase) ||
                              smaller.Identity.Contains("帮助", StringComparison.OrdinalIgnoreCase) ||
                              smaller.Identity.Contains("更多", StringComparison.OrdinalIgnoreCase);
        return semanticUtility && largerArea >= smallerArea * 4 && intersection.Width * intersection.Height >= smallerArea * .85;
    }

    private static bool IsTextClipped(TextBlock textBlock)
    {
        if (string.IsNullOrWhiteSpace(textBlock.Text) || textBlock.TextTrimming != TextTrimming.None) return false;
        if (textBlock.ActualWidth <= 0 || textBlock.ActualHeight <= 0) return true;
        var formatted = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
            textBlock.FontSize,
            textBlock.Foreground,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
        if (textBlock.TextWrapping == TextWrapping.NoWrap) return formatted.WidthIncludingTrailingWhitespace > textBlock.ActualWidth + 3;
        formatted.MaxTextWidth = Math.Max(1, textBlock.ActualWidth);
        return formatted.Height > textBlock.ActualHeight + 3;
    }

    private static bool HasUnexpectedWhiteSurface(Control control)
    {
        if (control is ButtonBase or CheckBox or RadioButton or ScrollViewer or ListBoxItem) return false;
        if (control.Background is not SolidColorBrush brush || brush.Opacity < .8 || brush.Color.A < 204) return false;
        var color = brush.Color;
        return color.R >= 245 && color.G >= 245 && color.B >= 245;
    }

    private static string ElementIdentity(FrameworkElement element)
    {
        var automationName = AutomationProperties.GetName(element);
        if (!string.IsNullOrWhiteSpace(automationName)) return automationName;
        if (!string.IsNullOrWhiteSpace(element.Name)) return element.Name;
        if (element is ContentControl content && content.Content is string value && !string.IsNullOrWhiteSpace(value)) return value;
        if (element is TextBlock text && !string.IsNullOrWhiteSpace(text.Text)) return text.Text.Length > 42 ? text.Text[..42] : text.Text;
        return element.GetType().Name;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= .03928 ? normalized / 12.92 : Math.Pow((normalized + .055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        var left = Luminance(first);
        var right = Luminance(second);
        return (Math.Max(left, right) + .05) / (Math.Min(left, right) + .05);
    }

    private static bool CanLoadResourceDictionary(string relativePath)
    {
        try
        {
            _ = new ResourceDictionary { Source = new Uri(relativePath, UriKind.Relative) };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveSourceCommit()
    {
        var value = Environment.GetEnvironmentVariable("PIXEL_TART_SOURCE_COMMIT");
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private void CloseAutomatedDpiOverlays()
    {
        if (_automatedContextMenu is not null) _automatedContextMenu.IsOpen = false;
        if (_automatedToolTip is not null) _automatedToolTip.IsOpen = false;
        if (_automatedAuxiliaryWindow is not null)
        {
            _automatedAuxiliaryWindow.Close();
            _automatedAuxiliaryWindow = null;
        }
    }

    private sealed record ElementBounds(string Identity, string Type, double X, double Y, double Width, double Height)
    {
        public Rect Rect => new(X, Y, Width, Height);
    }

    private sealed record LayoutInspection(
        int ElementCount,
        IReadOnlyList<string> Overflow,
        IReadOnlyList<string> Overlaps,
        IReadOnlyList<string> ZeroSizedInteractive,
        IReadOnlyList<string> TextClipping,
        IReadOnlyList<string> UnexpectedWhiteSurfaces,
        string FocusTarget,
        bool FocusVisible,
        int BlockingIssueCount);

    private sealed record ThemeInspection(
        string Theme,
        IReadOnlyList<string> MissingBrushResources,
        bool HighContrastResourceStructurePresent,
        IReadOnlyDictionary<string, int> RenderedControlTypes,
        IReadOnlyDictionary<string, bool> ControlStyleRenderChecks,
        double PrimaryTextContrastRatio,
        bool Passed);
}
#endif
