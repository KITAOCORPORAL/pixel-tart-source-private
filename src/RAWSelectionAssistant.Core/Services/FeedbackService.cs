using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public interface IFeedbackClipboard
{
    void SetText(string text);
}

public interface IFeedbackMailLauncher
{
    void Open(string mailtoUri);
}

public interface IFeedbackService
{
    FeedbackRequest Request { get; }
    FeedbackActionResult CopyEmail();
    FeedbackActionResult ComposeEmail();
}

public sealed class FeedbackService : IFeedbackService
{
    private readonly IFeedbackClipboard _clipboard;
    private readonly IFeedbackMailLauncher _mailLauncher;
    private readonly ILogService _logService;

    public FeedbackService(
        FeedbackRequest request,
        IFeedbackClipboard clipboard,
        IFeedbackMailLauncher mailLauncher,
        ILogService logService)
    {
        Request = request;
        _clipboard = clipboard;
        _mailLauncher = mailLauncher;
        _logService = logService;
    }

    public FeedbackRequest Request { get; }

    public FeedbackActionResult CopyEmail()
    {
        if (TryCopyEmail())
        {
            return new FeedbackActionResult(true, "邮箱已复制", true);
        }

        return new FeedbackActionResult(false, "复制失败，请手动选择邮箱地址复制。");
    }

    public FeedbackActionResult ComposeEmail()
    {
        try
        {
            _mailLauncher.Open(Request.MailtoUri);
            return new FeedbackActionResult(true, string.Empty);
        }
        catch (Exception ex)
        {
            _logService.Error("无法启动默认邮件应用。", ex);
            if (TryCopyEmail())
            {
                return new FeedbackActionResult(false, "未检测到可用的默认邮件应用，作者邮箱已为你复制。", true);
            }

            return new FeedbackActionResult(false, "未检测到可用的默认邮件应用，且邮箱复制失败，请手动选择邮箱地址复制。");
        }
    }

    private bool TryCopyEmail()
    {
        try
        {
            _clipboard.SetText(Request.EmailAddress);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("无法复制反馈邮箱。", ex);
            return false;
        }
    }
}
