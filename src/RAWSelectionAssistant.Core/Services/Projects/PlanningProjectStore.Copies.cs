namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed partial class PlanningProjectStore
{
    /// <summary>Copy a batch before committing one planning file. Failure leaves old document bytes/identities unchanged.</summary>
    public async Task ImportProjectCopiesAsync(Guid projectId, IReadOnlyList<PlanningDocumentReference> references,
        IReadOnlyList<PlanningAttachment> attachments, CancellationToken token = default, IProgress<long>? progress = null)
    {
        var created = new List<string>();
        await UpdateAsync(projectId, async state =>
            {
                var document = (state.Document ?? new PlanningDocument()).EnsureIdentity(projectId);
                var refs = document.References.ToList(); var files = document.Attachments.ToList();
                foreach (var requested in references)
                {
                    token.ThrowIfCancellationRequested();
                    if (requested.Source.LibraryId is not null || requested.Source.ExternalReference is not string source)
                        throw new InvalidDataException("仅明确选择的外部本地参考图可导入项目副本；不会隐式读取素材库原件。");
                    source = Path.GetFullPath(source);
                    var existing = refs.FirstOrDefault(r => r.Source.ReferenceId == requested.Source.ReferenceId ||
                        (r.Source.LibraryId is null && r.Source.ExternalReference is string path && Path.IsPathFullyQualified(path) && Path.GetFullPath(path).Equals(source, StringComparison.OrdinalIgnoreCase)));
                    var link = existing ?? requested;
                    if(existing is not null && !Path.GetFullPath(existing.Source.ExternalReference!).Equals(source,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("同一引用身份不能替换为另一原来源。");
                    var copy = await PlanningProjectCopyStorage.CopyAsync(StorageRoot, projectId, document.Package!.DocumentId, link.Source.ReferenceId, "reference", source, created, token, progress).ConfigureAwait(false);
                    if (existing?.ProjectCopy is {} previous && !previous.Sha256.Equals(copy.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("来源已变化；已有项目副本保持不变，请以新来源/新引用明确导入。");
                    if (existing?.ProjectCopy is {} old) PlanningProjectCopyStorage.ReadVerified(old);
                    if (existing is not null) refs.Remove(existing);
                    refs.Add(link with { ProjectCopy = copy });
                }
                foreach (var requested in attachments)
                {
                    token.ThrowIfCancellationRequested(); var source = Path.GetFullPath(requested.Path);
                    var existing = files.FirstOrDefault(a => a.Id == requested.Id || Path.IsPathFullyQualified(a.Path) && Path.GetFullPath(a.Path).Equals(source, StringComparison.OrdinalIgnoreCase));
                    var link = existing ?? requested;
                    if(existing is not null && !Path.GetFullPath(existing.Path).Equals(source,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("同一附件身份不能替换为另一原来源。");
                    var copy = await PlanningProjectCopyStorage.CopyAsync(StorageRoot, projectId, document.Package!.DocumentId, link.Id, "attachment", source, created, token, progress).ConfigureAwait(false);
                    if (existing?.ProjectCopy is {} previous && !previous.Sha256.Equals(copy.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("来源已变化；已有项目副本保持不变，请明确导入新引用。");
                    if (existing?.ProjectCopy is {} old) PlanningProjectCopyStorage.ReadVerified(old);
                    if (existing is not null) files.Remove(existing);
                    files.Add(link with { ProjectCopy = copy });
                }
                return state with { Document = document with { References = refs, Attachments = files } };
            }, token, () =>
            {
                // Rollback runs under the same project gate, before another import can adopt these files.
                foreach (var path in created) { PlanningImageStorage.RejectLinks(StorageRoot, path); if (File.Exists(path)) File.Delete(path); }
            }).ConfigureAwait(false);
    }
}
