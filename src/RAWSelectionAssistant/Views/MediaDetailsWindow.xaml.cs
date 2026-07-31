using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Views;

public partial class MediaDetailsWindow : Window
{
    private readonly MediaSelectionItem _item;

    public MediaDetailsWindow(MediaSelectionItem item, bool showAdvancedDetails)
    {
        InitializeComponent();
        _item = item;
        DataContext = item;
        if (!showAdvancedDetails)
        {
            DetailsExplanation.Text = "免费版提供基础冲突候选选择；完整像素、EXIF、质量风险、推荐理由和来源对比属于专业版。";
            foreach (var columnIndex in new[] { 8, 9, 10, 11, 12, 13, 14, 15, 17, 18, 19 })
            {
                DetailsGrid.Columns[columnIndex].Visibility = Visibility.Collapsed;
            }
        }
        RefreshRows();
    }

    public bool SelectionChanged { get; private set; }

    private void RefreshRows()
    {
        var rows = _item.FormatResults.SelectMany(result => result.Candidates.Count == 0
            ? [new MediaDetailRow(result, null)]
            : result.Candidates.Select(file => new MediaDetailRow(result, file))).ToList();
        DetailsGrid.ItemsSource = rows;
        DetailsGrid.SelectedItem = rows.FirstOrDefault(x => x.File is not null && ReferenceEquals(x.Result.SelectedFile, x.File))
                                   ?? rows.FirstOrDefault(x => x.File is not null);
    }

    private void ChooseSelected()
    {
        if (DetailsGrid.SelectedItem is not MediaDetailRow { File: not null } row)
        {
            MessageBox.Show(this, "请选择一个实际候选文件。", Branding.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        row.Result.ConfirmSelection(row.File);
        SelectionChanged = true;
        _item.RefreshOverallStatus();
        RefreshRows();
    }

    private void Choose_Click(object sender, RoutedEventArgs e) => ChooseSelected();
    private void DetailsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ChooseSelected();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (DetailsGrid.SelectedItem is MediaDetailRow { File: not null } row && File.Exists(row.File.FullPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{row.File.FullPath}\"") { UseShellExecute = true });
        }
    }

    private sealed record MediaDetailRow(MediaFormatMatchResult Result, MediaFileRecord? File)
    {
        public string CategoryText => Result.Category.ToChinese();
        public string TargetText => string.Join(" / ", Result.TargetExtensions);
        public string StatusText => Result.Status.ToChinese();
        public string SelectedText => File is not null && ReferenceEquals(Result.SelectedFile, File) ? "已选择" : string.Empty;
        public string SourceText => File?.JpegSourceType.ToChinese() ?? string.Empty;
        public string RecommendedText => File is not null && ReferenceEquals(Result.RecommendedFile, File) ? "是" : string.Empty;
        public string ManualConfirmationText => Result.RequiresManualConfirmation && File is not null ? "是" : string.Empty;
        public long FileSizeBytes => File?.JpegQuality?.FileSizeBytes ?? File?.Size ?? 0;
        public string PixelDimensions => File?.JpegQuality?.PixelDimensions ?? "未知";
        public string TotalPixelsText => File?.JpegQuality?.TotalPixels is { } pixels ? $"{pixels:N0}" : "未知";
        public string ExifStatusText => File?.JpegQuality?.ExifStatusText ?? "未知";
        public string CameraText => File?.JpegQuality?.CameraText ?? "未知";
        public string DateTimeOriginalText => File?.JpegQuality?.DateTimeOriginalText ?? "未知";
        public string IccStatusText => File?.JpegQuality?.IccStatusText ?? "未知";
        public string SoftwareText => File?.JpegQuality?.SoftwareText ?? "未知";
        public string OrientationText => File?.JpegQuality?.OrientationText ?? "未知";
        public string QualityWarningsText => File?.JpegQuality?.QualityWarningsText ?? string.Empty;
        public string RecommendedReason => File is not null && ReferenceEquals(Result.RecommendedFile, File) ? Result.RecommendedCandidateReason : string.Empty;
        public string ComparisonSummary => File is not null && ReferenceEquals(Result.RecommendedFile, File) ? Result.JpegComparisonSummary : string.Empty;
    }
}
