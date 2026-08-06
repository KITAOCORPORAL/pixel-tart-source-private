using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class BookingDocumentsPanel : UserControl
{
    private BookingDocumentsViewModel? _subscribed;
    private Point? _previewPanStart;
    private double _previewHorizontalOffset;
    private double _previewVerticalOffset;
    public BookingDocumentsPanel() => InitializeComponent();

    private void BookingDocumentsPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BookingDocumentsViewModel viewModel || ReferenceEquals(_subscribed, viewModel)) return;
        Unsubscribe();
        _subscribed = viewModel;
        viewModel.OpenFileRequested += ViewModel_OpenFileRequested;
        viewModel.RevealFileRequested += ViewModel_RevealFileRequested;
        viewModel.RevealDirectoryRequested += ViewModel_RevealDirectoryRequested;
    }

    private void BookingDocumentsPanel_Unloaded(object sender, RoutedEventArgs e) => Unsubscribe();

    private void BookingDocumentsPanel_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DataContext is BookingDocumentsViewModel { CanModify: true } && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void BookingDocumentsPanel_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not BookingDocumentsViewModel { CanModify: true } viewModel || e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        if (paths.Any(Directory.Exists))
        {
            ThemedMessageDialog.Show(Window.GetWindow(this), "本地摄影资料", "当前版本只支持添加单个或多个文件，不会扫描文件夹。", ThemedMessageKind.Information);
            return;
        }
        var previousFocus = Keyboard.FocusedElement;
        var dialog = new DocumentDropChoiceWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Choice is { } choice)
            await viewModel.HandleDroppedFilesAsync(paths, choice);
        previousFocus?.Focus();
    }

    private static void ViewModel_OpenFileRequested(object? sender, string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { ThemedMessageDialog.Show(Application.Current.MainWindow, "本地摄影资料", "文件当前无法打开。", ThemedMessageKind.Warning); }
    }

    private static void ViewModel_RevealFileRequested(object? sender, string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch { ThemedMessageDialog.Show(Application.Current.MainWindow, "本地摄影资料", "文件所在位置当前不可访问。", ThemedMessageKind.Warning); }
    }

    private static void ViewModel_RevealDirectoryRequested(object? sender, string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new DirectoryNotFoundException();
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch { ThemedMessageDialog.Show(Application.Current.MainWindow, "本地摄影资料", "输出目录当前不可访问。", ThemedMessageKind.Warning); }
    }

    private void Unsubscribe()
    {
        if (_subscribed is null) return;
        _subscribed.OpenFileRequested -= ViewModel_OpenFileRequested;
        _subscribed.RevealFileRequested -= ViewModel_RevealFileRequested;
        _subscribed.RevealDirectoryRequested -= ViewModel_RevealDirectoryRequested;
        _subscribed = null;
    }

    private void PreviewScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        _previewPanStart = e.GetPosition(viewer);
        _previewHorizontalOffset = viewer.HorizontalOffset;
        _previewVerticalOffset = viewer.VerticalOffset;
        viewer.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewScrollViewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_previewPanStart is null || sender is not ScrollViewer viewer || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(viewer);
        viewer.ScrollToHorizontalOffset(_previewHorizontalOffset + _previewPanStart.Value.X - current.X);
        viewer.ScrollToVerticalOffset(_previewVerticalOffset + _previewPanStart.Value.Y - current.Y);
    }

    private void PreviewScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndPreviewPan(sender as ScrollViewer);
    private void PreviewScrollViewer_MouseLeave(object sender, MouseEventArgs e) { if (e.LeftButton != MouseButtonState.Pressed) EndPreviewPan(sender as ScrollViewer); }
    private void EndPreviewPan(ScrollViewer? viewer) { _previewPanStart = null; viewer?.ReleaseMouseCapture(); }
}
