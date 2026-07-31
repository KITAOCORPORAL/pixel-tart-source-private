namespace RAWSelectionAssistant.Core.Models;

public sealed record FeedbackRequest(
    string EmailAddress,
    string Subject,
    string Body,
    string MailtoUri);

public sealed record FeedbackActionResult(
    bool Succeeded,
    string Message,
    bool EmailCopied = false);
