using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>Reorders existing section controls without replacing their bindings or asset state.</summary>
public sealed class InspectorSectionPanel : StackPanel
{
    private const string DragFormat = "PixelTart.InspectorSection";
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings), typeof(AssetLibraryWorkspaceSettings), typeof(InspectorSectionPanel),
        new PropertyMetadata(null, (owner, _) =>
        {
            if (owner is InspectorSectionPanel { IsLoaded: true } panel) panel.InitializeSections();
        }));

    public AssetLibraryWorkspaceSettings? Settings
    {
        get => (AssetLibraryWorkspaceSettings?)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    private Point _start;
    private Expander? _drag;
    private bool _initialized;

    public InspectorSectionPanel()
    {
        Loaded += (_, _) => InitializeSections();
        AllowDrop = true;
        Drop += OnDrop;
        DragOver += (_, e) =>
        {
            if (e.Data.GetData(DragFormat) is not Expander section || !Children.Contains(section)) return;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        };
    }

    internal void InitializeSections()
    {
        if (_initialized || Settings is null) return;
        _initialized = true;
        var sections = Children.OfType<Expander>().ToArray();
        foreach (var section in sections.OrderBy(section =>
        {
            var index = Settings.InspectorSectionOrder.IndexOf(Key(section));
            return index < 0 ? int.MaxValue : index;
        }))
        {
            Children.Remove(section);
            Children.Add(section);
        }
        foreach (var section in sections)
        {
            if (Settings.InspectorSectionExpanded.TryGetValue(Key(section), out var expanded))
                section.IsExpanded = expanded;
            section.Expanded += (_, _) => SaveExpanded(section);
            section.Collapsed += (_, _) => SaveExpanded(section);
            section.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!IsHeader(section, e.OriginalSource as DependencyObject)) return;
                _start = e.GetPosition(this);
                _drag = section;
            };
            section.PreviewMouseLeftButtonUp += (_, _) => _drag = null;
            section.PreviewMouseMove += (_, e) =>
            {
                var delta = e.GetPosition(this) - _start;
                if (_drag != section || e.LeftButton != MouseButtonState.Pressed ||
                    Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                _drag = null;
                DragDrop.DoDragDrop(section, new DataObject(DragFormat, section), DragDropEffects.Move);
            };
            var menu = new ContextMenu();
            foreach (var (label, delta) in new[] { ("上移分组", -1), ("下移分组", 1) })
            {
                var entry = new MenuItem { Header = label };
                entry.Click += (_, _) => MoveSection(Key(section), Children.IndexOf(section) + delta);
                menu.Opened += (_, _) => entry.IsEnabled = Children.IndexOf(section) + delta >= 0 &&
                    Children.IndexOf(section) + delta < Children.Count;
                menu.Items.Add(entry);
            }
            section.ContextMenu = menu;
            section.ToolTip = "拖动标题调整顺序；右键可上移或下移。";
        }
    }

    private static string Key(Expander section) => section.Header?.ToString() ?? "";

    private void SaveExpanded(Expander section)
    {
        if (Settings is not null) Settings.InspectorSectionExpanded[Key(section)] = section.IsExpanded;
    }

    private static bool IsHeader(Expander section, DependencyObject? source)
    {
        while (source is not null && !ReferenceEquals(source, section))
        {
            if (source is FrameworkElement { Name: "HeaderSite" }) return true;
            if (source is ToggleButton button && ReferenceEquals(button.TemplatedParent, section)) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
        }
        return false;
    }

    internal void MoveSection(string key, int index)
    {
        var section = Children.OfType<Expander>().FirstOrDefault(section => Key(section) == key);
        if (section is null || Settings is null) return;
        Children.Remove(section);
        Children.Insert(Math.Clamp(index, 0, Children.Count), section);
        Settings.InspectorSectionOrder = Children.OfType<Expander>().Select(Key).ToList();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is not Expander section || !Children.Contains(section)) return;
        var target = Children.OfType<Expander>().FirstOrDefault(candidate =>
        {
            var y = e.GetPosition(candidate).Y;
            return y >= 0 && y < candidate.ActualHeight;
        });
        if (target is null) return;
        MoveSection(Key(section), Children.IndexOf(target));
        e.Handled = true;
    }
}
