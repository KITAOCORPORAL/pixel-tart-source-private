using System.Windows;
#if UI_REVIEW_BUILD
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
#endif
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Views;

public partial class FeedbackDialog : Window
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackDialog(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
        InitializeComponent();
        EmailTextBox.Text = feedbackService.Request.EmailAddress;
#if UI_REVIEW_BUILD
        Loaded += FeedbackDialog_Loaded;
#endif
    }

#if UI_REVIEW_BUILD
    private void FeedbackDialog_Loaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, CaptureUiReviewFrame);

    private void CaptureUiReviewFrame()
    {
        var statePath = Path.Combine(AppDataPaths.Root, "ui-review-state.json");
        if (!File.Exists(statePath)) return;

        using var document = JsonDocument.Parse(File.ReadAllText(statePath));
        var root = document.RootElement;
        if (!string.Equals(root.GetProperty("State").GetString(), "Feedback", StringComparison.OrdinalIgnoreCase)) return;
        var outputPath = root.GetProperty("OutputPath").GetString();
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        FeedbackRoot.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(FeedbackRoot);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(FeedbackRoot.ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(FeedbackRoot.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(FeedbackRoot);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(outputPath)) encoder.Save(stream);
        Close();
    }
#endif

    private void CopyEmail_Click(object sender, RoutedEventArgs e) =>
        ShowResult(_feedbackService.CopyEmail());

    private void ComposeEmail_Click(object sender, RoutedEventArgs e)
    {
        var result = _feedbackService.ComposeEmail();
        if (!result.Succeeded)
        {
            ShowResult(result);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowResult(Core.Models.FeedbackActionResult result)
    {
        StatusText.Text = result.Message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(result.Succeeded ? "SuccessBrush" : "WarningBrush");
        if (!result.Succeeded && result.EmailCopied)
        {
            CopyEmailButton.Content = "再次复制邮箱";
        }
    }
}
