using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public partial class PlanningCenterView : UserControl
{
    private PlanningCenterViewModel? _vm;
    private bool _rendering;
    private bool _switchingDocument;
    private readonly List<TextBlock> _referenceCaptions = [];
    private PlanningReferenceTile? _previewReturnTarget;
    public PlanningCenterView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_vm is not null) { _vm.DocumentPresentationChanged -= PresentationChanged; _vm.PropertyChanged -= ViewModelChanged; }
            _vm = e.NewValue as PlanningCenterViewModel;
            if (_vm is not null) { _vm.DocumentPresentationChanged += PresentationChanged; _vm.PropertyChanged += ViewModelChanged; }
            Render();
        };
        Loaded += (_, _) => Render();
        Unloaded += async (_, _) => { if (_vm is not null) await _vm.FlushAsync(); };
        IsVisibleChanged += async (_, _) => { if (!IsVisible && _vm is not null) await _vm.FlushAsync(); };
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        void OverlayChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            WorkspaceLayout.IsHitTestVisible = ModalOverlay.Visibility != Visibility.Visible && ImagePreview.Visibility != Visibility.Visible && ContextDrawer.Visibility != Visibility.Visible;
            if (WorkspaceLayout.IsHitTestVisible) Focus();
        }
        ModalOverlay.IsVisibleChanged += OverlayChanged; ImagePreview.IsVisibleChanged += OverlayChanged; ContextDrawer.IsVisibleChanged += OverlayChanged;
    }
    private Brush Brush(string key) => (Brush)FindResource(key);
    private TextBlock Text(string value, double size = 15, bool strong = false) => new()
    { Text = value, FontSize = size, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal, Foreground = Brush("TextPrimaryBrush"), TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.65 };
    private Button Action(string label, Action action, string style = "GhostButton")
    {
        var button = new Button { Content = label, Style = (Style)FindResource(style), HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
    private Button AsyncAction(string label, Func<Task> action, string style = "GhostButton") =>
        Action(label, async () => { try { await action(); } catch (Exception error) { System.Diagnostics.Trace.TraceError(error.ToString()); _vm!.Dialogs.ShowError("操作未完成，原资料已保留。请重试；若仍失败，请提供日志文件。"); } }, style);
    private void Heading(Panel panel, string label, double size = 23) { var title = Text(label, size, true); title.Margin = new Thickness(0, 24, 0, 14); panel.Children.Add(title); }
    private TextBox Editor(string property, int lines = 1)
    {
        var box = new TextBox { AcceptsReturn = lines > 1, TextWrapping = TextWrapping.Wrap, MinHeight = lines > 1 ? lines * 24 : 38, Margin = new Thickness(0, 5, 0, 16) };
        box.SetBinding(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        AutomationProperties.SetName(box, property switch {
            "DocumentTitle" => "策划案标题", "DocumentBody" => "策划正文", "DocumentSubtitle" => "副标题",
            "DocumentPeople" => "参与人物", "DocumentLocation" or "NewLocation" => "拍摄地点", "NewPlanningName" => "新策划名称",
            "ShootGoal" => "拍摄目标", "Keywords" => "视觉关键词", "ClientRequirements" => "客户要求", "MustCapture" => "必拍内容",
            "PlanningNotes" => "注意事项", "OutputPurpose" => "交付用途", "ShotTitle" => "构图与镜头名称", "ShotNotes" => "人物动作与备注",
            "ShotScene" => "场景", "ShotEstimatedMinutes" => "预计分钟", "SourceSearch" => "搜索视觉资料", _ => "编辑内容" });
        return box;
    }
    private void PresentationChanged(object? sender, EventArgs e) => Render();
    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        if (e.PropertyName is nameof(_vm.IsCreateOpen) or nameof(_vm.IsBookingOpen)) RenderModal();
        if (e.PropertyName == nameof(_vm.IsShotDrawerOpen)) RenderShotDrawer();
        if (e.PropertyName == nameof(_vm.OverviewFilter)) RenderNavigation();
        if (e.PropertyName == nameof(_vm.SaveStatus)) SaveStatusText.Foreground = Brush(_vm.SaveStatus.StartsWith("保存失败", StringComparison.Ordinal) ? "DangerBrush" : "TextSecondaryBrush");
    }
    public void Render()
    {
        if (_vm is null || _rendering) return;
        _rendering = true;
        try
        {
            var preview = _vm.IsPreviewMode;
            AutomationProperties.SetAutomationId(DocumentContent, preview ? "PlanningPreviewSurface" : _vm.ContentPage switch
            {
                "参考图" => "PlanningReferencesSurface", "情绪板" => "PlanningMoodboardSurface",
                "镜头清单" => "PlanningShotListSurface", "灯光图" => "PlanningLightingSurface",
                "服化道" => "PlanningStylingSurface", "文件" => "PlanningFilesSurface", _ => "PlanningTextSurface"
            });
            DocumentListColumn.Width = new GridLength(preview ? 0 : 280);
            DocumentListPane.Visibility = EditorHeader.Visibility = ContentNavigationBorder.Visibility = preview ? Visibility.Collapsed : Visibility.Visible;
            ContextDrawer.Visibility = Visibility.Collapsed;
            RenderNavigation(); DocumentContent.Children.Clear(); _referenceCaptions.Clear();
            DocumentList.SelectedItem = _vm.PlanningProjects.FirstOrDefault(item => item.ProjectId == _vm.ProjectId);
            DocumentContent.MaxWidth = _vm.ContentPage == "文字" || preview ? 940 : 1320;
            if (!_vm.HasProject || _vm.IsOverview)
            {
                var empty = new StackPanel { Margin = new Thickness(40, 110, 40, 0), MaxWidth = 580, HorizontalAlignment = HorizontalAlignment.Center };
                empty.Children.Add(Text(_vm.PlanningProjects.Count == 0 ? "还没有策划案" : "选择一个策划案开始", 32, true));
                empty.Children.Add(Text("从一次拍摄开始，把灵感、镜头和执行资料整理到一起。", 17));
                var create = AsyncAction("新建策划", () => _vm.ShowCreatePlanningCommand.ExecuteAsync(null), "PrimaryButton"); create.Margin = new Thickness(0, 26, 0, 0); empty.Children.Add(create);
                DocumentContent.Children.Add(empty); return;
            }
            if (preview)
            {
                var note = Text("策划案预览 · 按 Esc 返回", 12); note.Foreground = Brush("TextSecondaryBrush"); DocumentContent.Children.Add(note);
                RenderText(false);
                foreach (var section in new[] { "情绪板", "镜头清单", "灯光图", "服化道" })
                {
                    if (section == "镜头清单" ? _vm.Shots.Count == 0 : !ReferencesFor(section).Any()) continue;
                    Heading(DocumentContent, section, 28);
                    if (section == "镜头清单") RenderShots(false);
                    else RenderImages(section, false);
                }
                return;
            }
            switch (_vm.ContentPage)
            {
                case "文字": RenderText(_vm.IsDocumentEditing); break;
                case "镜头清单": RenderShots(true); break;
                case "文件": RenderFiles(); break;
                default: RenderImages(_vm.ContentPage, true); break;
            }
        }
        finally { _rendering = false; UpdateResponsiveLayout(); }
    }
    private void RenderNavigation()
    {
        if (_vm is null) return;
        StatusChips.Children.Clear();
        foreach (var status in _vm.OverviewFilters)
        {
            var chip = Action(status, () => { _vm.OverviewFilter = status; RenderNavigation(); });
            chip.FontSize = 12; chip.Padding = new Thickness(8, 4, 8, 4); chip.MinWidth = 0;
            if (_vm.OverviewFilter == status) { chip.Foreground = Brush("AccentBrush"); chip.Background = Brush("SurfaceSecondaryBrush"); }
            StatusChips.Children.Add(chip);
        }
        ContentNavigation.Children.Clear();
        var icons = new[] { "IconPlanning", "IconPhotoStack", "IconAssetLibrary", "IconArchiveHistory", "IconCameraTether", "IconProjectCenter", "IconHistory" };
        for (var i = 0; i < PlanningCenterViewModel.ContentPages.Count; i++)
        {
            var page = PlanningCenterViewModel.ContentPages[i];
            var button = Action(page, () => { _vm.ContentPage = page; DocumentScroll.ScrollToTop(); });
            AutomationProperties.SetAutomationId(button, "PlanningPage" + new[] { "Text", "References", "Moodboard", "Shots", "Lighting", "Styling", "Files" }[i]);
            button.Padding = new Thickness(5, 12, 5, 12); button.MinWidth = 0;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new System.Windows.Shapes.Path { Data = (Geometry)FindResource(icons[i]), Width = 14, Height = 14, Stretch = Stretch.Uniform, Fill = Brushes.Transparent, Stroke = Brush(_vm.ContentPage == page ? "AccentBrush" : "TextSecondaryBrush"), StrokeThickness = 1.4, Margin = new Thickness(0, 0, 4, 0) });
            var label = Text(page, 13); label.TextWrapping = TextWrapping.NoWrap; content.Children.Add(label);
            button.Content = content;
            var line = new Border { Child = button, BorderThickness = new Thickness(0, 0, 0, 2), BorderBrush = _vm.ContentPage == page ? Brush("AccentBrush") : Brushes.Transparent };
            ContentNavigation.Children.Add(line);
        }
    }
    private void UpdateResponsiveLayout()
    {
        if (ContentNavigationBorder is null || ActualWidth <= 0) return;
        var width = Math.Max(0, ActualWidth - (_vm?.IsPreviewMode == true ? 0 : 280));
        ContentNavigation.Width = Math.Max(0, Math.Min(720, width - 32));
        var compact = width < 700;
        EditorHeader.Margin = new Thickness(compact ? 16 : 32, 18, compact ? 16 : 32, 12);
        DocumentContent.Margin = new Thickness(compact ? 20 : 40, compact ? 20 : 28, compact ? 20 : 40, 64);
        // Keep every module on one line, reducing spacing rather than wrapping labels.
        foreach (Border line in ContentNavigation.Children)
        {
            var button = (Button)line.Child; button.Padding = new Thickness(compact ? 1 : 5, 12, compact ? 1 : 5, 12);
            var content = (StackPanel)button.Content;
            var icon = (System.Windows.Shapes.Path)content.Children[0]; icon.Width = icon.Height = compact ? 12 : 14;
            icon.Margin = new Thickness(0, 0, compact ? 2 : 4, 0);
            ((TextBlock)content.Children[1]).FontSize = compact ? 12 : 13;
        }
        var headerActions = (StackPanel)EditorHeader.Children[1];
        EditorHeader.RowDefinitions.Clear(); EditorHeader.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (width < 650) EditorHeader.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Grid.SetRow(headerActions, width < 650 ? 1 : 0); Grid.SetColumn(headerActions, width < 650 ? 0 : 1);
        Grid.SetColumnSpan(headerActions, width < 650 ? 2 : 1);
        headerActions.Margin = new Thickness(0, width < 650 ? 10 : 0, 0, 0);
    }
    private IEnumerable<PlanningReferenceItem> ReferencesFor(string page) => _vm!.AllProjectReferences.Where(item => page switch
    { "情绪板" => item.IsMoodboard, "灯光图" => item.Category == "灯光", "服化道" => item.Category == "造型", _ => _vm.ReferenceCategory == "全部" || item.Category == _vm.ReferenceCategory });
    private MenuItem MenuEntry(string label, string icon = "IconPhotoStack")
    {
        var entry = new MenuItem { Header = label, Icon = new System.Windows.Shapes.Path { Data = (Geometry)FindResource(icon), Style = (Style)FindResource("Icon20"), Width = 15, Height = 15 } };
        entry.SetResourceReference(StyleProperty, "PixelTart.Menu.Item"); AutomationProperties.SetName(entry, label); return entry;
    }
    private void RenderText(bool editing)
    {
        if (_vm is null) return;
        if (!_vm.IsPreviewMode)
        {
            var edit = Action(editing ? "完成编辑" : "编辑", () => { _vm.EditDocumentCommand.Execute(null); });
            edit.HorizontalAlignment = HorizontalAlignment.Right; DocumentContent.Children.Add(edit);
        }
        var eyebrow = Text("创作策划 · " + _vm.ProposalStatus, 12); eyebrow.Foreground = Brush("AccentBrush"); eyebrow.Margin = new Thickness(0, 6, 0, 14); DocumentContent.Children.Add(eyebrow);
        if (editing)
        {
            DocumentContent.Children.Add(Editor(nameof(_vm.DocumentTitle)));
            DocumentContent.Children.Add(Editor(nameof(_vm.DocumentSubtitle), 2));
            DocumentContent.Children.Add(Text("参与人物", 12));
            DocumentContent.Children.Add(Editor(nameof(_vm.DocumentPeople)));
            DocumentContent.Children.Add(Text("拍摄地点（关联档期时以档期为准）", 12));
            DocumentContent.Children.Add(Editor(nameof(_vm.DocumentLocation)));
            var status = new ComboBox { ItemsSource = _vm.ProposalStatuses, Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            status.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(_vm.ProposalStatus))); AutomationProperties.SetName(status, "策划案状态"); DocumentContent.Children.Add(status);
            Heading(DocumentContent, "核心概念");
            DocumentContent.Children.Add(Text("支持 # 标题、- 列表、> 引用、**粗体** 与 --- 分隔线。", 12));
            DocumentContent.Children.Add(Editor(nameof(_vm.DocumentBody), 8));
        }
        else
        {
            DocumentContent.Children.Add(Text(_vm.DocumentTitle, 40, true));
            var subtitle = Text(_vm.DocumentSubtitle, 21); subtitle.Margin = new Thickness(0, 14, 0, 16); DocumentContent.Children.Add(subtitle);
        }
        var info = Text(_vm.DocumentDate + "  ·  " + _vm.DocumentLocation + (string.IsNullOrWhiteSpace(_vm.DocumentPeople) ? "" : "  ·  " + _vm.DocumentPeople), 13);
        info.Foreground = Brush("TextSecondaryBrush"); info.Margin = new Thickness(0, 14, 0, 22); DocumentContent.Children.Add(info);
        var heroes = _vm.HeroReferences.ToArray();
        if (heroes.Length > 0)
        {
            var hero = new Grid { Margin = new Thickness(0, 4, 0, 20) };
            hero.ColumnDefinitions.Add(new() { Width = new GridLength(heroes.Length == 1 ? 1 : 2, GridUnitType.Star) });
            if (heroes.Length > 1) hero.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            hero.Children.Add(ImageTile(heroes[0], 350, !_vm.IsPreviewMode));
            if (heroes.Length > 1)
            {
                var secondary = new StackPanel(); Grid.SetColumn(secondary, 1);
                foreach (var reference in heroes.Skip(1).Take(2)) secondary.Children.Add(ImageTile(reference, heroes.Length == 2 ? 350 : 140, !_vm.IsPreviewMode));
                hero.Children.Add(secondary);
            }
            DocumentContent.Children.Add(hero);
            if (heroes.Length > 3)
            {
                var additional = new WrapPanel();
                foreach (var reference in heroes.Skip(3)) { var tile = ImageTile(reference, 160, !_vm.IsPreviewMode); tile.Width = 220; additional.Children.Add(tile); }
                DocumentContent.Children.Add(additional);
            }
        }
        if (!editing && !string.IsNullOrWhiteSpace(_vm.DocumentBody)) AddBody(DocumentContent, _vm.DocumentBody);
        Heading(DocumentContent, "视觉方向");
        var direction = new StackPanel();
        var swatches = new WrapPanel();
        foreach (var color in _vm.PaletteColors) swatches.Children.Add(new Border { Width = 64, Height = 28, Background = (Brush)new BrushConverter().ConvertFromString(color.Hex)!, Margin = new Thickness(0, 0, 8, 8), ToolTip = color.Hex });
        direction.Children.Add(Text("项目配色", 12)); direction.Children.Add(swatches);
        var tones = new StackPanel { Orientation = Orientation.Horizontal, Height = 38, VerticalAlignment = VerticalAlignment.Bottom };
        foreach (var zone in _vm.ToneZones) tones.Children.Add(new Border { Width = 25, Height = Math.Max(3, zone.Ratio * 150), VerticalAlignment = VerticalAlignment.Bottom, Background = Brush("AccentBrush"), Opacity = .35 + zone.Zone * .05, Margin = new Thickness(0, 0, 3, 0) });
        direction.Children.Add(Text("目标影调", 12)); direction.Children.Add(tones);
        foreach (var look in _vm.ColorSchemes)
        {
            var summary = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            summary.Children.Add(new Image { Source = LoadImage(look.ReferenceSources.FirstOrDefault()?.SourcePath), Width = 72, Height = 54, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 12, 0) });
            var label = new StackPanel(); label.Children.Add(Text(look.Name, 15)); label.Children.Add(Text(look.ReferenceSources.Count + " 张参考", 12)); summary.Children.Add(label); direction.Children.Add(summary);
        }
        if (!_vm.IsPreviewMode) direction.Children.Add(Action("编辑色彩方案", () => _vm.OpenColorSchemeCommand.Execute(null)));
        DocumentContent.Children.Add(direction);
        foreach (var section in new[] { ("拍摄目标", nameof(_vm.ShootGoal), _vm.ShootGoal), ("视觉关键词", nameof(_vm.Keywords), _vm.Keywords), ("客户要求", nameof(_vm.ClientRequirements), _vm.ClientRequirements), ("必拍内容", nameof(_vm.MustCapture), _vm.MustCapture), ("注意事项", nameof(_vm.PlanningNotes), _vm.PlanningNotes), ("交付用途", nameof(_vm.OutputPurpose), _vm.OutputPurpose) })
        {
            if (_vm.IsPreviewMode && string.IsNullOrWhiteSpace(section.Item3)) continue;
            Heading(DocumentContent, section.Item1);
            if (editing) DocumentContent.Children.Add(Editor(section.Item2, 3));
            else AddBody(DocumentContent, string.IsNullOrWhiteSpace(section.Item3) ? "尚未填写" : section.Item3);
        }
    }
    private void AddBody(Panel panel, string body)
    {
        foreach (var line in body.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Trim() == "---") { panel.Children.Add(new Border { Height = 1, Background = Brush("DividerBrush"), Margin = new Thickness(0, 20, 0, 20) }); continue; }
            var title = line.StartsWith("# ", StringComparison.Ordinal); var quote = line.StartsWith("> ", StringComparison.Ordinal);
            var value = title || quote ? line[2..] : line.StartsWith("- ", StringComparison.Ordinal) ? "•  " + line[2..] : line;
            var text = Text("", title ? 26 : 17, title); text.Margin = new Thickness(quote ? 20 : 0, title ? 18 : 4, 0, title ? 12 : 6);
            var segments = value.Split("**");
            for (var i = 0; i < segments.Length; i++) text.Inlines.Add(i % 2 == 1 ? new Bold(new Run(segments[i])) : new Run(segments[i]));
            if (quote) text.Foreground = Brush("TextSecondaryBrush");
            panel.Children.Add(text);
        }
    }
    private FrameworkElement ImageTile(PlanningReferenceItem item, double height, bool interactive)
    {
        StackPanel stack = interactive ? new PlanningReferenceTile() : new StackPanel();
        stack.Margin = new Thickness(0, 0, 18, 22);
        stack.Children.Add(new Image { Source = LoadImage(item.PreviewPath), Height = height, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center });
        var caption = Text(item.Title, 13); caption.Margin = new Thickness(0, 8, 0, 0); stack.Children.Add(caption); _referenceCaptions.Add(caption);
        var status = Text(item.Availability, 11); status.Foreground = Brush("TextSecondaryBrush"); stack.Children.Add(status);
        if (item.Category is "灯光" or "造型" && _vm is not null)
        {
            var linked = _vm.Shots.Where(shot => shot.References.Any(reference => reference.ReferenceId == item.Id)).Select(shot => $"{shot.Order + 1:00} {shot.Name}").ToArray();
            if (linked.Length > 0) { var note = Text("关联镜头：" + string.Join(" · ", linked), 11); note.Foreground = Brush("TextSecondaryBrush"); stack.Children.Add(note); }
        }
        if (interactive)
        {
            var tile = (PlanningReferenceTile)stack;
            AutomationProperties.SetName(tile, "参考图：" + item.Title);
            void Select() { if (_vm is null) return; _vm.SelectedDocumentReference = item; foreach (var other in _referenceCaptions) other.Foreground = Brush("TextPrimaryBrush"); caption.Foreground = Brush("AccentBrush"); }
            tile.Selected += (_, _) => Select();
            tile.OpenRequested += (_, _) => { _previewReturnTarget = tile; ShowImage(item); };
            stack.ContextMenu = ReferenceMenu(item);
            stack.ContextMenu.Closed += (_, _) => { if (tile.IsVisible && ImagePreview.Visibility != Visibility.Visible) tile.Focus(); };
        }
        return stack;
    }
    private static BitmapSource? LoadImage(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
        catch (Exception error) when (error is IOException or NotSupportedException or System.Runtime.InteropServices.COMException) { return null; }
    }
    private void RenderImages(string page, bool interactive)
    {
        if (_vm is null) return;
        if (interactive)
        {
            var toolbar = new WrapPanel();
            toolbar.Children.Add(AsyncAction("关联本地图片", () => _vm.AddDocumentImagesCommand.ExecuteAsync(null), "SecondaryButton"));
            toolbar.Children.Add(Action("从素材库 / 灵感板 / 自由画布选择", ShowSources));
            if (page == "情绪板") toolbar.Children.Add(AsyncAction("在自由画布中排布", () => _vm.SendToCanvasAsync()));
            DocumentContent.Children.Add(toolbar);
            if (page == "参考图")
            {
                var filters = new WrapPanel { Margin = new Thickness(0, 12, 0, 18) };
                foreach (var category in _vm.ReferenceCategories) { var button = Action(category, () => _vm.ReferenceCategory = category); if (_vm.ReferenceCategory == category) button.Foreground = Brush("AccentBrush"); filters.Children.Add(button); }
                DocumentContent.Children.Add(filters);
            }
        }
        var items = ReferencesFor(page).ToArray();
        if (items.Length == 0 && interactive) { var empty = Text("还没有" + page + "。关联已有参考，或从本地添加；原图保持不变。", 18); empty.Margin = new Thickness(0, 70, 0, 40); DocumentContent.Children.Add(empty); }
        foreach (var group in items.GroupBy(item => page == "服化道" ? (_vm.StylingGroups.Contains(item.Group) ? item.Group : "未分组造型参考") : page == "情绪板" ? item.Group : "")
            .OrderBy(group => page == "服化道" ? group.Key switch { "服装" => 0, "妆发" => 1, "道具" => 2, _ => 3 } : 0))
        {
            if (group.Key.Length > 0) Heading(DocumentContent, group.Key);
            var gallery = new WrapPanel();
            foreach (var item in group) { var tile = ImageTile(item, 230, interactive); tile.Width = 280; gallery.Children.Add(tile); }
            DocumentContent.Children.Add(gallery);
        }
        if (page == "情绪板" && interactive)
        {
            Heading(DocumentContent, "关联的灵感板与自由画布", 17);
            foreach (var board in _vm.ProjectBoards) DocumentContent.Children.Add(Text(board.Name + " · " + board.EntryCount + " 张参考", 14));
            foreach (var canvas in _vm.ProjectCanvases) DocumentContent.Children.Add(Action(canvas.Name, () => _vm.OpenLinkedCanvas(canvas)));
        }
    }
    private ContextMenu ReferenceMenu(PlanningReferenceItem item)
    {
        var menu = new ContextMenu(); menu.SetResourceReference(StyleProperty, "PixelTart.Menu.Context");
        void Add(string label, Action action) { var entry = MenuEntry(label); entry.Click += (_, _) => action(); menu.Items.Add(entry); }
        async Task Safely(Func<Task> action) { try { await action(); } catch (Exception error) { System.Diagnostics.Trace.TraceError(error.ToString()); _vm!.Dialogs.ShowError("操作未完成，原资料已保留。请重试。"); } }
        void AddAsync(string label, Func<Task> action) => Add(label, async () => await Safely(action));
        Add("查看大图", () => ShowImage(item));
        Add("打开来源", () => _vm!.OpenReferenceSource(item));
        menu.Items.Add(new Separator());
        AddAsync(item.IsHero ? "取消策划封面" : "设为策划封面", () => _vm!.UpdateReferenceAsync(item, "封面"));
        AddAsync(item.IsMoodboard ? "移出情绪板" : "加入情绪板", () => _vm!.UpdateReferenceAsync(item, "情绪板"));
        AddAsync("用于当前镜头", () => _vm!.UseReferenceForShotAsync(item, ShotReferenceKind.General));
        AddAsync("用于灯光", async () => { await _vm!.UpdateReferenceAsync(item, "分类", "灯光"); await _vm.UseReferenceForShotAsync(item, ShotReferenceKind.Lighting); });
        AddAsync("用于造型", async () => { await _vm!.UpdateReferenceAsync(item, "分类", "造型"); await _vm.UseReferenceForShotAsync(item, ShotReferenceKind.Styling); });
        menu.Items.Add(new Separator());
        var categories = MenuEntry("参考分类", "IconAssetLibrary");
        foreach (var category in _vm!.ReferenceCategories.Skip(1)) { var child = MenuEntry(category); child.Click += async (_, _) => await Safely(() => _vm.UpdateReferenceAsync(item, "分类", category)); categories.Items.Add(child); } menu.Items.Add(categories);
        var groups = MenuEntry("分组", "IconProjectCenter");
        foreach (var group in _vm.MoodGroups.Concat(_vm.StylingGroups)) { var child = MenuEntry(group); child.Click += async (_, _) => await Safely(() => _vm.UpdateReferenceAsync(item, "分组", group)); groups.Items.Add(child); } menu.Items.Add(groups);
        AddAsync("上移", () => _vm.MoveDocumentReferenceAsync(item, -1)); AddAsync("下移", () => _vm.MoveDocumentReferenceAsync(item, 1));
        AddAsync("发送到自由画布", () => _vm.SendToCanvasAsync(item));
        AddAsync("用于参考仿色", () => _vm.UseReferenceForColorAsync(item));
        menu.Items.Add(new Separator());
        AddAsync("从策划中移除", () => _vm.UpdateReferenceAsync(item, "移除"));
        ((MenuItem)menu.Items[menu.Items.Count - 1]).ToolTip = "仅移除引用，不删除原图";
        return menu;
    }
    private void RenderShots(bool interactive)
    {
        if (_vm is null) return;
        if (interactive)
        {
            var toolbar = new DockPanel(); toolbar.Children.Add(Text(_vm.ProgressText, 15));
            var add = AsyncAction("新建镜头", async () => { await _vm.NewShotCommand.ExecuteAsync(null); Render(); }); add.HorizontalAlignment = HorizontalAlignment.Right; toolbar.Children.Add(add); DocumentContent.Children.Add(toolbar);
        }
        foreach (var shot in _vm.Shots)
        {
            var row = new Grid { Margin = new Thickness(0, 20, 0, 0) }; row.ColumnDefinitions.Add(new() { Width = new GridLength(58) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var number = Text((shot.Order + 1).ToString("00"), 24); number.Foreground = Brush("TextSecondaryBrush"); row.Children.Add(number);
            var body = new StackPanel(); Grid.SetColumn(body, 1); body.Children.Add(Text(shot.Name, 21, true)); body.Children.Add(Text(shot.Scene ?? "场景待定", 14)); body.Children.Add(Text(shot.Notes ?? "", 14)); row.Children.Add(body);
            var state = Text(shot.Status switch { ProjectShotStatus.Completed => "已拍", ProjectShotStatus.InProgress => "当前", ProjectShotStatus.Skipped => "跳过", _ => "待拍" }, 13); Grid.SetColumn(state, 2); row.Children.Add(state);
            Border border = interactive ? new PlanningShotRow() : new Border();
            border.Child = row; border.BorderBrush = Brush("DividerBrush"); border.BorderThickness = new Thickness(0, 0, 0, 1); border.Padding = new Thickness(0, 0, 0, 22);
            if (interactive)
            {
                void Open() { _vm.SelectedShot = shot; _vm.IsShotDrawerOpen = true; RenderShotDrawer(); FocusOverlay(DrawerContent); }
                border.Focusable = true; AutomationProperties.SetName(border, "镜头 " + (shot.Order + 1) + "：" + shot.Name);
                ((PlanningShotRow)border).OpenRequested = Open;
                border.MouseLeftButtonDown += (_, _) => Open();
                border.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { Open(); e.Handled = true; } };
            }
            DocumentContent.Children.Add(border);
        }
    }
    private void RenderShotDrawer()
    {
        if (_vm is null) return;
        ContextDrawer.Visibility = _vm.IsShotDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!_vm.IsShotDrawerOpen) return;
        DrawerContent.Children.Clear();
        DrawerContent.Children.Add(Action("关闭", () => _vm.IsShotDrawerOpen = false)); Heading(DrawerContent, "镜头详情");
        foreach (var field in new[] { ("构图 / 镜头名称", nameof(_vm.ShotTitle), 1), ("人物动作与备注", nameof(_vm.ShotNotes), 4), ("场景", nameof(_vm.ShotScene), 1), ("预计分钟", nameof(_vm.ShotEstimatedMinutes), 1) })
        { DrawerContent.Children.Add(Text(field.Item1, 12)); DrawerContent.Children.Add(Editor(field.Item2, field.Item3)); }
        var kind = new ComboBox { ItemsSource = _vm.ReferenceKinds, DisplayMemberPath = "Label", Margin = new Thickness(0, 12, 0, 8) };
        kind.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(_vm.SelectedReferenceKind))); AutomationProperties.SetName(kind, "镜头参考类别"); DrawerContent.Children.Add(kind);
        DrawerContent.Children.Add(AsyncAction("关联本地参考", async () => { await _vm.AddLocalReferenceCommand.ExecuteAsync(null); await _vm.RefreshDocumentReferencesAsync(); RenderShotDrawer(); }));
        foreach (var reference in _vm.References) { DrawerContent.Children.Add(new Image { Source = LoadImage(reference.ExternalReference), MaxHeight = 180, Stretch = Stretch.Uniform }); DrawerContent.Children.Add(Text(reference.Title ?? "参考", 12)); }
        var actions = new WrapPanel();
        actions.Children.Add(AsyncAction("当前", async () => { await _vm.SetCurrentShotCommand.ExecuteAsync(null); Render(); }));
        actions.Children.Add(AsyncAction("已拍", async () => { await _vm.MarkShotCompletedCommand.ExecuteAsync(null); Render(); }));
        actions.Children.Add(AsyncAction("跳过", async () => { await _vm.MarkShotSkippedCommand.ExecuteAsync(null); Render(); }));
        actions.Children.Add(AsyncAction("上移", async () => { await _vm.MoveShotUpCommand.ExecuteAsync(null); Render(); }));
        actions.Children.Add(AsyncAction("下移", async () => { await _vm.MoveShotDownCommand.ExecuteAsync(null); Render(); }));
        actions.Children.Add(AsyncAction("复制镜头", async () => { await _vm.DuplicateShotCommand.ExecuteAsync(null); Render(); }));
        DrawerContent.Children.Add(actions);
    }
    private void RenderFiles()
    {
        if (_vm is null) return;
        DocumentContent.Children.Add(AsyncAction("关联文件", async () => { await _vm.AddAttachmentCommand.ExecuteAsync(null); Render(); }, "SecondaryButton"));
        Heading(DocumentContent, "项目文件");
        DocumentContent.Children.Add(Text("保留原位置，使用系统应用打开。PDF、通告单、合同和场地资料统一关联。", 14));
        foreach (var file in _vm.Attachments)
        {
            var row = new StackPanel { Margin = new Thickness(0, 22, 0, 0) }; row.Children.Add(Text(file.Name, 19, true));
            row.Children.Add(Text(File.Exists(file.Path) ? Path.GetExtension(file.Path).TrimStart('.').ToUpperInvariant() : "源文件暂不可用", 12));
            var metadata = Text("来源：" + file.Path + (File.Exists(file.Path) ? "\n更新于 " + File.GetLastWriteTime(file.Path).ToString("yyyy-MM-dd HH:mm") : ""), 12);
            metadata.Foreground = Brush("TextSecondaryBrush"); row.Children.Add(metadata);
            var actions = new WrapPanel(); actions.Children.Add(AsyncAction("打开文件", () => { _vm.OpenAttachment(file); return Task.CompletedTask; }));
            actions.Children.Add(Action("打开所在位置", () => _vm.Dialogs.RevealFile(file.Path)));
            actions.Children.Add(AsyncAction("移除关联", async () => { await _vm.RemoveAttachmentAsync(file); Render(); })); row.Children.Add(actions); DocumentContent.Children.Add(row);
        }
    }
    private void RenderModal()
    {
        if (_vm is null) return;
        ModalContent.Children.Clear(); ModalOverlay.Visibility = _vm.IsCreateOpen || _vm.IsBookingOpen ? Visibility.Visible : Visibility.Collapsed;
        if (_vm.IsCreateOpen)
        {
            AutomationProperties.SetAutomationId(ModalContent, "PlanningCreateDialog");
            Heading(ModalContent, "新建策划案"); ModalContent.Children.Add(Text("名称", 13)); ModalContent.Children.Add(Editor(nameof(_vm.NewPlanningName)));
            ModalContent.Children.Add(Text("关联已有项目（可选）", 13));
            var project = new ComboBox { ItemsSource = _vm.AvailableProjects, DisplayMemberPath = "Name", Margin = new Thickness(0, 8, 0, 16) }; project.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(_vm.NewLinkedProject))); AutomationProperties.SetName(project, "关联已有项目"); ModalContent.Children.Add(project);
            ModalContent.Children.Add(Text("拍摄日期", 13)); var date = new DatePicker { Style = (Style)FindResource("ProposalDatePicker"), Margin = new Thickness(0, 6, 0, 14) }; date.SetBinding(DatePicker.SelectedDateProperty, new Binding(nameof(_vm.NewShootDate))); AutomationProperties.SetName(date, "拍摄日期"); ModalContent.Children.Add(date);
            ModalContent.Children.Add(Text("地点", 13)); ModalContent.Children.Add(Editor(nameof(_vm.NewLocation)));
            ModalContent.Children.Add(AsyncAction("新建策划", async () => { await _vm.CreatePlanningCommand.ExecuteAsync(null); Render(); RenderModal(); }, "PrimaryButton"));
        }
        if (_vm.IsBookingOpen)
        {
            AutomationProperties.SetAutomationId(ModalContent, "PlanningBookingPicker");
            Heading(ModalContent, "关联工作日历档期"); ModalContent.Children.Add(Text("选择未关联或属于当前项目的档期，不复制日历资料。", 14));
            var list = new ListBox { ItemsSource = _vm.AvailableBookings, DisplayMemberPath = "Title", MaxHeight = 330, Margin = new Thickness(0, 20, 0, 20) }; list.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(_vm.SelectedBooking))); AutomationProperties.SetName(list, "可关联档期"); ModalContent.Children.Add(list);
            ModalContent.Children.Add(AsyncAction("关联", () => _vm.LinkBookingCommand.ExecuteAsync(null), "PrimaryButton"));
        }
        ModalContent.Children.Add(Action("取消", () => _vm.CancelPlanningModalCommand.Execute(null)));
        if (ModalOverlay.Visibility == Visibility.Visible) FocusOverlay(ModalContent);
    }
    private void ShowSources()
    {
        if (_vm is null) return;
        ModalContent.Children.Clear(); ModalOverlay.Visibility = Visibility.Visible; Heading(ModalContent, "关联现有视觉资料");
        AutomationProperties.SetAutomationId(ModalContent, "PlanningSourcePicker");
        var category = new ComboBox { ItemsSource = _vm.SourceCategories, SelectedItem = _vm.SourceCategory }; category.SelectionChanged += (_, _) => _vm.SourceCategory = category.SelectedItem?.ToString() ?? "素材库"; AutomationProperties.SetName(category, "参考来源"); ModalContent.Children.Add(category);
        ModalContent.Children.Add(Editor(nameof(_vm.SourceSearch)));
        var candidates = new ListBox { ItemsSource = _vm.SourceCandidates, DisplayMemberPath = "Name", MaxHeight = 340 }; candidates.SelectionChanged += (_, _) => _vm.SelectedSourceCandidate = candidates.SelectedItem as CanvasObject; AutomationProperties.SetName(candidates, "参考来源搜索结果");
        ModalContent.Children.Add(AsyncAction("搜索", () => _vm.SearchReferenceSourcesAsync())); ModalContent.Children.Add(candidates);
        ModalContent.Children.Add(AsyncAction("关联选中图片", async () => { await _vm.AddSourceCandidateAsync(); ModalOverlay.Visibility = Visibility.Collapsed; Render(); }, "PrimaryButton"));
        ModalContent.Children.Add(Action("取消", () => ModalOverlay.Visibility = Visibility.Collapsed));
        FocusOverlay(ModalContent);
    }
    private async void DocumentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rendering || _switchingDocument || _vm is null || DocumentList.SelectedItem is not PlanningProjectCard card || card.ProjectId == _vm.ProjectId && !_vm.IsOverview) return;
        _switchingDocument = true; DocumentList.IsEnabled = false;
        try { await _vm.OpenProjectSafelyAsync(card.ProjectId); Render(); DocumentScroll.ScrollToTop(); }
        finally { _switchingDocument = false; DocumentList.IsEnabled = true; }
    }
    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not Button button) return;
        var menu = new ContextMenu { PlacementTarget = button }; menu.SetResourceReference(StyleProperty, "PixelTart.Menu.Context");
        var start = MenuEntry("开始拍摄", "IconCameraTether"); start.Command = _vm.EnterTetherCommand; menu.Items.Add(start);
        var calendar = MenuEntry("查看关联档期", "IconCalendar"); calendar.Command = _vm.OpenCalendarCommand; menu.Items.Add(calendar);
        var overview = MenuEntry("返回策划列表", "IconPlanning"); overview.Command = _vm.ShowOverviewCommand; menu.Items.Add(overview); menu.IsOpen = true;
    }
    private async void ShowImage(PlanningReferenceItem item)
    {
        try
        {
            var path = await _vm!.ResolveOriginalAsync(item);
            var original = path is not null && File.Exists(path);
            PreviewImage.Source = LoadImage(original ? path : item.PreviewPath);
            PreviewCaption.Text = item.Category + " · " + item.Title + (original ? "" : " · 源文件暂不可用，显示已保存的预览");
            ImagePreview.Visibility = Visibility.Visible;
            CloseImageButton.Focus();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { _vm!.Dialogs.ShowInfo("源文件暂不可用。"); }
    }
    private void CloseImagePreview()
    {
        ImagePreview.Visibility = Visibility.Collapsed;
        if (_previewReturnTarget is { IsVisible: true } target) target.Focus();
        _previewReturnTarget = null;
    }
    private void ClosePreview_Click(object sender, RoutedEventArgs e) => CloseImagePreview();
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || !await _vm.FlushAsync()) return;
        ModalContent.Children.Clear(); Heading(ModalContent, "导出策划案 PDF");
        AutomationProperties.SetAutomationId(ModalContent, "PlanningExportDialog");
        ModalContent.Children.Add(Text("按 A4 排版输出图片式 PDF，文字不可选择。高质量适合审阅和打印。", 14));
        var quality = new ComboBox { ItemsSource = new[] { "高质量 · 300 DPI", "标准 · 216 DPI" }, SelectedIndex = 0, Margin = new Thickness(0, 20, 0, 20) };
        AutomationProperties.SetName(quality, "PDF 导出质量"); ModalContent.Children.Add(quality);
        ModalContent.Children.Add(AsyncAction("导出 PDF", async () => {
            if (!await _vm.FlushAsync()) return;
            var path = _vm.Dialogs.ChooseSaveFile("导出策划案 PDF", "PDF|*.pdf", ".pdf", _vm.DocumentTitle + ".pdf"); if (path is null) return;
            PlanningProposalPdf.Export(_vm, path, quality.SelectedIndex == 0 ? 300 : 216); ModalOverlay.Visibility = Visibility.Collapsed;
            _vm.Dialogs.ShowInfo("策划案 PDF 已导出（图片式，文字不可选择）。");
        }, "PrimaryButton"));
        ModalContent.Children.Add(Action("取消", () => ModalOverlay.Visibility = Visibility.Collapsed));
        ModalOverlay.Visibility = Visibility.Visible; FocusOverlay(ModalContent);
    }
    private void FocusOverlay(FrameworkElement content) => Dispatcher.BeginInvoke(new Action(() => content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));
    public async Task HandleWorkspaceKeyAsync(KeyEventArgs e)
    {
        if (_vm is null) return;
        var overlay = ImagePreview.Visibility == Visibility.Visible ? ImagePreview : ModalOverlay.Visibility == Visibility.Visible ? ModalOverlay : ContextDrawer.Visibility == Visibility.Visible ? ContextDrawer : null;
        if (overlay is not null && e.Key == Key.Tab && (Keyboard.FocusedElement is not DependencyObject focused || !overlay.IsAncestorOf(focused)))
        { FocusOverlay(overlay); e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            if (ImagePreview.Visibility == Visibility.Visible) CloseImagePreview();
            else if (ModalOverlay.Visibility == Visibility.Visible) { ModalOverlay.Visibility = Visibility.Collapsed; _vm.CancelPlanningModalCommand.Execute(null); }
            else if (_vm.IsShotDrawerOpen) _vm.IsShotDrawerOpen = false;
            else _vm.IsPreviewMode = false;
            e.Handled = true; return;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.S) { e.Handled = true; await _vm.FlushAsync(); return; }
            if (ModalOverlay.Visibility == Visibility.Visible || ImagePreview.Visibility == Visibility.Visible) return;
            if (e.Key == Key.P) { e.Handled = true; await _vm.PreviewDocumentCommand.ExecuteAsync(null); return; }
            if (e.Key == Key.F) { e.Handled = true; ProjectSearchBox.Focus(); ProjectSearchBox.SelectAll(); return; }
        }
        if (e.OriginalSource is TextBoxBase || e.Key is Key.ImeProcessed or Key.DeadCharProcessed) return;
        if (e.Key == Key.Delete && _vm.SelectedDocumentReference is { } reference && _vm.ContentPage is "参考图" or "情绪板" or "灯光图" or "服化道") { e.Handled = true; await _vm.UpdateReferenceAsync(reference, "移除"); }
    }
    private async void View_PreviewKeyDown(object sender, KeyEventArgs e) => await HandleWorkspaceKeyAsync(e);
}
