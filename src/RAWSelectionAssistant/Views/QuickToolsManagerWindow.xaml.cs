using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public partial class QuickToolsManagerWindow : Window
{
    private Point _dragStart;
    private readonly ObservableCollection<ToolDefinition> _pinned = [];
    private readonly ObservableCollection<ToolDefinition> _available = [];

    public QuickToolsManagerWindow(IReadOnlyList<string> currentToolIds)
    {
        InitializeComponent();
        foreach (var id in QuickToolsService.Normalize(currentToolIds)) if (ToolRegistry.TryGet(id, out var tool)) _pinned.Add(tool);
        RefreshAvailable();
        PinnedList.ItemsSource = _pinned;
        PinnedList.DisplayMemberPath = nameof(ToolDefinition.DisplayName);
        AvailableList.ItemsSource = _available;
        AvailableList.DisplayMemberPath = nameof(ToolDefinition.DisplayName);
    }

    public IReadOnlyList<string> ResultToolIds { get; private set; } = [];
    private void RefreshAvailable() { _available.Clear(); foreach (var tool in ToolRegistry.Pinnable.Where(x => _pinned.All(p => p.Id != x.Id))) _available.Add(tool); }
    private void Add_Click(object sender, RoutedEventArgs e) { if (AvailableList.SelectedItem is ToolDefinition tool && _pinned.Count < QuickToolsService.MaximumPinnedTools) { _pinned.Add(tool); RefreshAvailable(); } }
    private void Remove_Click(object sender, RoutedEventArgs e) { if (PinnedList.SelectedItem is ToolDefinition tool) { _pinned.Remove(tool); RefreshAvailable(); } }
    private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void Down_Click(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int offset) { if (PinnedList.SelectedItem is not ToolDefinition tool) return; var from=_pinned.IndexOf(tool); var to=Math.Clamp(from+offset,0,_pinned.Count-1); if(from==to)return; _pinned.Move(from,to); PinnedList.SelectedItem=tool; }
    private void Reset_Click(object sender, RoutedEventArgs e) { _pinned.Clear(); foreach(var id in QuickToolsService.DefaultPinnedTools) if(ToolRegistry.TryGet(id,out var tool)) _pinned.Add(tool); RefreshAvailable(); }
    private void Save_Click(object sender, RoutedEventArgs e) { ResultToolIds=_pinned.Select(x=>x.SettingsId).ToArray(); DialogResult=true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult=false;
    private void PinnedList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _dragStart=e.GetPosition(PinnedList);
    private void PinnedList_MouseMove(object sender, MouseEventArgs e) { if(e.LeftButton!=MouseButtonState.Pressed || PinnedList.SelectedItem is not ToolDefinition tool)return; var p=e.GetPosition(PinnedList); if(Math.Abs(p.X-_dragStart.X)>SystemParameters.MinimumHorizontalDragDistance || Math.Abs(p.Y-_dragStart.Y)>SystemParameters.MinimumVerticalDragDistance) DragDrop.DoDragDrop(PinnedList,tool,DragDropEffects.Move); }
    private void PinnedList_DragOver(object sender, DragEventArgs e) { e.Effects=e.Data.GetDataPresent(typeof(ToolDefinition))?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; }
    private void PinnedList_Drop(object sender, DragEventArgs e) { if(e.Data.GetData(typeof(ToolDefinition)) is not ToolDefinition tool)return; var target=(e.OriginalSource as FrameworkElement)?.DataContext as ToolDefinition; var from=_pinned.IndexOf(tool); var to=target is null?_pinned.Count-1:_pinned.IndexOf(target); if(from>=0 && to>=0 && from!=to)_pinned.Move(from,to); }
}
