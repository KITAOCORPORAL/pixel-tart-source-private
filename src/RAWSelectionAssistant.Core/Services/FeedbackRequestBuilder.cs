using System.Runtime.InteropServices;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class FeedbackRequestBuilder
{
    private const string Subject = "像素蛋挞建议与问题反馈";

    public FeedbackRequest Build(string? windowsVersion = null)
    {
        var resolvedWindowsVersion = string.IsNullOrWhiteSpace(windowsVersion)
            ? RuntimeInformation.OSDescription
            : windowsVersion.Trim();
        var body = BuildBody(Branding.ProductVersion, resolvedWindowsVersion);
        var mailtoUri = $"mailto:{Branding.SupportEmail}?subject={Uri.EscapeDataString(Subject)}&body={Uri.EscapeDataString(body)}";
        return new FeedbackRequest(Branding.SupportEmail, Subject, body, mailtoUri);
    }

    private static string BuildBody(string productVersion, string windowsVersion) =>
        $"你好，我在使用像素蛋挞时遇到了以下问题，或有以下建议：{Environment.NewLine}{Environment.NewLine}" +
        $"【问题或建议】{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}" +
        $"【操作步骤】{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}" +
        $"【期望结果】{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}" +
        $"【实际结果】{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}" +
        $"软件版本：{productVersion}{Environment.NewLine}" +
        $"Windows版本：{windowsVersion}";
}
