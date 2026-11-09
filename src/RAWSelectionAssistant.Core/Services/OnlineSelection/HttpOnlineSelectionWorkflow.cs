using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.OnlineSelection;
public interface IHttpOnlineSelectionWorkflow : IOnlineSelectionProvider
{
 Task<OnlineSelectionProviderResult<SelectionPublish>> PublishWithRuleAsync(Guid projectId,SelectionPublish publish,SelectionRule rule,CancellationToken ct=default);
 string? PublishedLink(Guid projectId);
 int? ExpectedSelectionVersion(Guid projectId);
}
