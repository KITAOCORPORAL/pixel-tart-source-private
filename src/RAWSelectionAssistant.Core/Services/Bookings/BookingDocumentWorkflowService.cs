using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public sealed class BookingDocumentWorkflowService(
    IBookingDocumentRepository repository,
    IShootBookingService bookingService,
    IProjectRepository projectRepository,
    IFileOperationPlanner planner,
    IFileOperationExecutor executor,
    IFileVerificationService verification,
    IUndoJournalService undoJournal,
    TaskOperationBridge operationBridge,
    IAuditLogService auditLog) : IBookingDocumentWorkflowService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".jpg", ".jpeg", ".png"
    };

    public Task<IReadOnlyList<BookingDocumentRecord>> ListAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        repository.ListByBookingAsync(bookingId, cancellationToken);

    public async Task<string?> GetSuggestedDestinationAsync(Guid? projectId, BookingDocumentType documentType, CancellationToken cancellationToken = default)
    {
        if (!projectId.HasValue) return null;
        var project = (await projectRepository.ListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(item => item.Id == projectId.Value);
        if (project is null || string.IsNullOrWhiteSpace(project.OutputDirectory)) return null;
        return Path.Combine(project.OutputDirectory, "拍摄资料", CategoryFolder(documentType));
    }

    public async Task<BookingDocumentBatchResult> AddReferencesAsync(BookingDocumentAddRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBookingEditableAsync(request.BookingId, cancellationToken).ConfigureAwait(false);
        var outcomes = new List<BookingDocumentItemOutcome>();
        foreach (var path in request.FilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fullPath = ValidateSupportedFile(path);
                var normalized = Normalize(fullPath);
                var existing = await repository.GetByNormalizedPathAsync(request.BookingId, normalized, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    outcomes.Add(new(fullPath, null, BookingDocumentFileState.Normal, existing, null, ErrorCodeCatalog.DuplicateConflict, "该文件已关联到当前拍摄，已跳过重复记录。"));
                    continue;
                }
                var document = BuildDocument(request.BookingId, request.ProjectId, request.DocumentType, fullPath, BookingDocumentLinkMode.Reference, null, null);
                await repository.AddAsync(document, cancellationToken).ConfigureAwait(false);
                outcomes.Add(new(fullPath, null, BookingDocumentFileState.Normal, document, null, null, "已关联原位置，电脑文件未被修改。"));
                await WriteAuditAsync(request.BookingId, request.ProjectId, request.DocumentType, "ReferenceAdded", "Succeeded", null, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                var code = MapFileError(ex);
                outcomes.Add(new(path, null, BookingDocumentFileState.Failed, null, null, code, ErrorCodeCatalog.Describe(code)));
                await WriteAuditAsync(request.BookingId, request.ProjectId, request.DocumentType, "ReferenceAdded", "Failed", null, code, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                outcomes.Add(new(path, null, BookingDocumentFileState.Failed, null, null, ErrorCodeCatalog.DatabaseUnavailable, "关联记录未保存，原文件保持不变。"));
                await WriteAuditAsync(request.BookingId, request.ProjectId, request.DocumentType, "ReferenceAdded", "Failed", null, ErrorCodeCatalog.DatabaseUnavailable, cancellationToken).ConfigureAwait(false);
            }
        }
        return BuildBatchResult(null, outcomes, 0, 0);
    }

    public async Task<BookingDocumentBatchResult> CopyAndAssociateAsync(BookingDocumentCopyRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBookingEditableAsync(request.BookingId, cancellationToken).ConfigureAwait(false);
        if (request.FilePaths.Count == 0) return new(null, BookingDocumentBatchStatus.Completed, TaskResultSummary.Empty, []);
        var destinationRoot = ValidateDestination(request.DestinationRoot);
        var duplicateInputs = request.FilePaths.GroupBy(path => NormalizeLoose(path), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).SelectMany(group => group.Skip(1)).ToArray();
        var sourcePaths = request.FilePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var initialOutcomes = duplicateInputs.Select(path => new BookingDocumentItemOutcome(path, null, BookingDocumentFileState.Normal, null, null, ErrorCodeCatalog.DuplicateConflict, "同一次添加中存在重复文件，已跳过。" )).ToList();
        foreach (var source in sourcePaths) ValidateSupportedFile(source);

        BookingDocumentBatchResult? completedResult = null;
        Guid? taskId = null;
        try
        {
            taskId = await operationBridge.RunAsync("复制拍摄资料", async (context, token) =>
            {
                var operationTaskId = context.Definition.Id;
                var sourceRoot = Path.GetDirectoryName(Path.GetFullPath(sourcePaths[0])) ?? Path.GetPathRoot(Path.GetFullPath(sourcePaths[0]))!;
                var plan = await planner.CreateAsync(operationTaskId, request.ProjectId, FileOperationType.Copy, sourceRoot, destinationRoot, sourcePaths, FileConflictPolicy.AutoNumber, token).ConfigureAwait(false);
                if (request.VerifySha256)
                {
                    var hashed = new List<FileOperationItem>(plan.Items.Count);
                    foreach (var item in plan.Items)
                    {
                        var hash = await verification.ComputeSha256Async(item.SourcePath, token).ConfigureAwait(false);
                        hashed.Add(item with { OptionalSourceHash = hash });
                    }
                    plan = plan with { Items = hashed };
                }

                var progress = new Progress<(double Progress, string CurrentFile, TaskResultSummary Summary)>(value =>
                    _ = context.ReportProgressAsync(value.Progress, "复制拍摄资料", null, value.Summary, token));
                var execution = await executor.ExecuteAsync(plan, context.SafeBoundaryAsync, progress, token).ConfigureAwait(false);
                var outcomes = new List<BookingDocumentItemOutcome>(initialOutcomes);
                var associated = 0;
                var failed = 0;
                var skipped = initialOutcomes.Count;
                var waiting = 0;
                foreach (var item in plan.Items.OrderBy(item => item.Sequence))
                {
                    var itemResult = execution.Items.FirstOrDefault(result => result.ItemId == item.Id);
                    var effectiveResult = itemResult ?? execution.Items.FirstOrDefault(result => result.ItemId == Guid.Empty);
                    if (itemResult is null || itemResult.State != FileOperationItemState.Completed || string.IsNullOrWhiteSpace(itemResult.DestinationPath))
                    {
                        var state = effectiveResult?.State == FileOperationItemState.NeedsAttention ? BookingDocumentFileState.WaitingForConfirmation : BookingDocumentFileState.Failed;
                        if (state == BookingDocumentFileState.WaitingForConfirmation) waiting++; else failed++;
                        outcomes.Add(new(item.SourcePath, effectiveResult?.DestinationPath, state, null, null, effectiveResult?.ErrorCode, ErrorCodeCatalog.Describe(effectiveResult?.ErrorCode)));
                        continue;
                    }

                    try
                    {
                        var normalized = Normalize(itemResult.DestinationPath);
                        var existing = await repository.GetByNormalizedPathAsync(request.BookingId, normalized, token).ConfigureAwait(false);
                        if (existing is not null)
                        {
                            skipped++;
                            outcomes.Add(new(item.SourcePath, itemResult.DestinationPath, BookingDocumentFileState.Normal, existing, null, ErrorCodeCatalog.DuplicateConflict, "目标文件已关联，未创建重复记录。"));
                            continue;
                        }
                        var document = BuildDocument(request.BookingId, request.ProjectId, request.DocumentType, itemResult.DestinationPath, BookingDocumentLinkMode.ManagedCopy, operationTaskId, itemResult.Hash);
                        await repository.AddAsync(document, token).ConfigureAwait(false);
                        associated++;
                        outcomes.Add(new(item.SourcePath, itemResult.DestinationPath, BookingDocumentFileState.Normal, document, null, null, "文件已安全复制并关联。"));
                        await WriteAuditAsync(request.BookingId, request.ProjectId, request.DocumentType, "ManagedCopyAssociated", "Succeeded", operationTaskId, null, token).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        waiting++;
                        var info = new FileInfo(itemResult.DestinationPath);
                        var pending = new PendingDocumentAssociation(operationTaskId, request.BookingId, request.ProjectId, request.DocumentType, itemResult.DestinationPath, itemResult.Hash, info.Exists ? info.Length : null);
                        outcomes.Add(new(item.SourcePath, itemResult.DestinationPath, BookingDocumentFileState.PartiallyCompleted, null, pending, ErrorCodeCatalog.DatabaseUnavailable,
                            "文件已复制，但关联记录未保存；源文件仍安全。"));
                        await WriteAuditAsync(request.BookingId, request.ProjectId, request.DocumentType, "ManagedCopyAssociated", "PartiallyCompleted", operationTaskId, ErrorCodeCatalog.DatabaseUnavailable, token).ConfigureAwait(false);
                    }
                }

                var summary = new TaskResultSummary(plan.Items.Count + initialOutcomes.Count, associated + waiting, failed, skipped, 0, waiting,
                    execution.Summary.BytesProcessed, execution.Summary.BytesWritten);
                completedResult = BuildBatchResult(operationTaskId, outcomes, execution.Summary.BytesProcessed, execution.Summary.BytesWritten, summary);
                return summary;
            }, request.ProjectId, "booking-document-copy", cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var summary = new TaskResultSummary(sourcePaths.Length, 0, 0, initialOutcomes.Count, sourcePaths.Length, 0, 0, 0);
            return new(taskId, BookingDocumentBatchStatus.Cancelled, summary, initialOutcomes);
        }
        catch (Exception)
        {
            var outcome = new BookingDocumentItemOutcome(string.Empty, null, BookingDocumentFileState.Failed, null, null, ErrorCodeCatalog.DestinationNotWritable, "复制任务未完成，源文件保持不变。" );
            return BuildBatchResult(taskId, [.. initialOutcomes, outcome], 0, 0);
        }
        return completedResult ?? BuildBatchResult(taskId, initialOutcomes, 0, 0);
    }

    public async Task<BookingDocumentCheckResult?> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null) return null;
        var now = DateTimeOffset.UtcNow;
        try
        {
            if (!File.Exists(document.FilePath))
            {
                await repository.SetMissingAsync(document.Id, true, now, cancellationToken).ConfigureAwait(false);
                var missing = document with { IsMissing = true, MissingSinceAtUtc = document.MissingSinceAtUtc ?? now, LastVerifiedAtUtc = now, UpdatedAtUtc = now };
                return new(missing, BookingDocumentFileState.Missing, "文件已移动或当前不可访问。" );
            }
            var info = new FileInfo(document.FilePath);
            var modified = document.FileSize != info.Length || document.LastKnownModifiedAtUtc is DateTimeOffset last && Math.Abs((info.LastWriteTimeUtc - last.UtcDateTime).TotalSeconds) > 1;
            if (modified && !string.IsNullOrWhiteSpace(document.OptionalHash))
            {
                var hash = await verification.ComputeSha256Async(document.FilePath, cancellationToken).ConfigureAwait(false);
                modified = !string.Equals(hash, document.OptionalHash, StringComparison.OrdinalIgnoreCase);
            }
            if (modified)
            {
                await repository.SetMissingAsync(document.Id, false, now, cancellationToken).ConfigureAwait(false);
                return new(document with { IsMissing = false, MissingSinceAtUtc = null, LastVerifiedAtUtc = now, UpdatedAtUtc = now }, BookingDocumentFileState.Modified, "文件内容或元数据与上次记录不同。" );
            }
            await repository.UpdateLocationAsync(document.Id, info.FullName, Normalize(info.FullName), info.Extension.ToLowerInvariant(), info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), false, now, cancellationToken).ConfigureAwait(false);
            var normal = document with { FilePath = info.FullName, NormalizedPath = Normalize(info.FullName), FileExtension = info.Extension.ToLowerInvariant(), FileSize = info.Length,
                LastKnownModifiedAtUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), IsMissing = false, MissingSinceAtUtc = null, LastVerifiedAtUtc = now, UpdatedAtUtc = now };
            return new(normal, BookingDocumentFileState.Normal, "文件可访问。" );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await repository.SetMissingAsync(document.Id, true, now, cancellationToken).ConfigureAwait(false);
            return new(document with { IsMissing = true, MissingSinceAtUtc = document.MissingSinceAtUtc ?? now, LastVerifiedAtUtc = now, UpdatedAtUtc = now }, BookingDocumentFileState.Missing, "文件已移动或当前不可访问。" );
        }
    }

    public async Task<BookingDocumentRelocationResult> RelocateAsync(Guid documentId, string newFilePath, bool acceptHashMismatch = false, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null) return new(BookingDocumentRelocationStatus.NotFound, null, false, "文档关联不存在。" );
        try
        {
            var fullPath = ValidateSupportedFile(newFilePath);
            string? newHash = null;
            if (!string.IsNullOrWhiteSpace(document.OptionalHash))
            {
                newHash = await verification.ComputeSha256Async(fullPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(newHash, document.OptionalHash, StringComparison.OrdinalIgnoreCase) && !acceptHashMismatch)
                {
                    await WriteAuditAsync(document.BookingId, document.ProjectId, document.DocumentType, "Relocated", "NeedsAttention", document.ImportTaskId, ErrorCodeCatalog.HashMismatch, cancellationToken).ConfigureAwait(false);
                    return new(BookingDocumentRelocationStatus.HashMismatch, document, true, "所选文件与原记录哈希不一致，需要确认。" );
                }
            }
            var info = new FileInfo(fullPath);
            var now = DateTimeOffset.UtcNow;
            await repository.UpdateLocationAsync(document.Id, fullPath, Normalize(fullPath), info.Extension.ToLowerInvariant(), info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), false, now, cancellationToken).ConfigureAwait(false);
            if (acceptHashMismatch && newHash is not null) await repository.UpdateHashAsync(document.Id, newHash, now, cancellationToken).ConfigureAwait(false);
            var relocated = document with { FilePath = fullPath, NormalizedPath = Normalize(fullPath), FileExtension = info.Extension.ToLowerInvariant(), FileSize = info.Length,
                LastKnownModifiedAtUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), OptionalHash = acceptHashMismatch && newHash is not null ? newHash : document.OptionalHash,
                IsMissing = false, MissingSinceAtUtc = null, LastVerifiedAtUtc = now, UpdatedAtUtc = now };
            await WriteAuditAsync(document.BookingId, document.ProjectId, document.DocumentType, "Relocated", "Succeeded", document.ImportTaskId, null, cancellationToken).ConfigureAwait(false);
            return new(BookingDocumentRelocationStatus.Relocated, relocated, false, "文件位置已更新。" );
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            var code = MapFileError(ex);
            await WriteAuditAsync(document.BookingId, document.ProjectId, document.DocumentType, "Relocated", "Failed", document.ImportTaskId, code, cancellationToken).ConfigureAwait(false);
            return new(BookingDocumentRelocationStatus.Failed, document, false, ErrorCodeCatalog.Describe(code));
        }
    }

    public async Task<bool> RemoveAssociationAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null) return false;
        var booking = await bookingService.GetAsync(document.BookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking?.IsArchived == true) return false;
        var removed = await repository.RemoveAssociationAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (removed) await WriteAuditAsync(document.BookingId, document.ProjectId, document.DocumentType, "AssociationRemoved", "Succeeded", document.ImportTaskId, null, cancellationToken).ConfigureAwait(false);
        return removed;
    }

    public async Task<BookingDocumentRetryResult> RetryAssociationAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(pending.DestinationPath)) return new(false, null, pending, ErrorCodeCatalog.SourceNotFound, "已复制文件当前不存在，无法重试关联。" );
            var info = new FileInfo(pending.DestinationPath);
            if (pending.OutputSize is long size && info.Length != size) return new(false, null, pending, ErrorCodeCatalog.SourceChanged, "已复制文件已被修改，无法自动保存关联。" );
            if (!string.IsNullOrWhiteSpace(pending.OutputHash) && !string.Equals(pending.OutputHash, await verification.ComputeSha256Async(pending.DestinationPath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
                return new(false, null, pending, ErrorCodeCatalog.SourceChanged, "已复制文件已被修改，无法自动保存关联。" );
            var existing = await repository.GetByNormalizedPathAsync(pending.BookingId, Normalize(pending.DestinationPath), cancellationToken).ConfigureAwait(false);
            if (existing is not null) return new(true, existing, null, null, "关联记录已经存在。" );
            var document = BuildDocument(pending.BookingId, pending.ProjectId, pending.DocumentType, pending.DestinationPath, BookingDocumentLinkMode.ManagedCopy, pending.TaskId, pending.OutputHash);
            await repository.AddAsync(document, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(pending.BookingId, pending.ProjectId, pending.DocumentType, "AssociationRetried", "Succeeded", pending.TaskId, null, cancellationToken).ConfigureAwait(false);
            return new(true, document, null, null, "关联记录已保存。" );
        }
        catch (Exception)
        {
            await WriteAuditAsync(pending.BookingId, pending.ProjectId, pending.DocumentType, "AssociationRetried", "Failed", pending.TaskId, ErrorCodeCatalog.DatabaseUnavailable, cancellationToken).ConfigureAwait(false);
            return new(false, null, pending, ErrorCodeCatalog.DatabaseUnavailable, "关联记录仍未保存，文件保持不变。" );
        }
    }

    public async Task<TaskResultSummary> UndoCopiedFileAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default)
    {
        var associated = await repository.GetByNormalizedPathAsync(Normalize(pending.DestinationPath), cancellationToken).ConfigureAwait(false);
        if (associated is not null)
        {
            await WriteAuditAsync(pending.BookingId, pending.ProjectId, pending.DocumentType, "CopyUndo", "NeedsAttention", pending.TaskId, ErrorCodeCatalog.NeedsUserDecision, cancellationToken).ConfigureAwait(false);
            return new(1, 0, 0, 0, 0, 1, 0, 0);
        }
        var summary = await undoJournal.UndoFileAsync(pending.TaskId, pending.DestinationPath, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(pending.BookingId, pending.ProjectId, pending.DocumentType, "CopyUndo", summary.Succeeded == 1 ? "Succeeded" : "NeedsAttention", pending.TaskId,
            summary.Succeeded == 1 ? null : ErrorCodeCatalog.SourceChanged, cancellationToken).ConfigureAwait(false);
        return summary;
    }

    public Task AbandonAssociationAsync(PendingDocumentAssociation pending, CancellationToken cancellationToken = default) =>
        WriteAuditAsync(pending.BookingId, pending.ProjectId, pending.DocumentType, "AssociationAbandoned", "FileKept", pending.TaskId, null, cancellationToken);

    private async Task EnsureBookingEditableAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await bookingService.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("拍摄排期不存在。" );
        if (booking.IsArchived) throw new InvalidOperationException("已归档排期不能修改文档关联。" );
    }

    private static BookingDocumentRecord BuildDocument(Guid bookingId, Guid? projectId, BookingDocumentType type, string filePath, BookingDocumentLinkMode mode, Guid? taskId, string? hash)
    {
        var fullPath = Path.GetFullPath(filePath);
        var info = new FileInfo(fullPath);
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            BookingId = bookingId,
            ProjectId = projectId,
            DocumentType = type,
            DisplayName = info.Name,
            FilePath = fullPath,
            NormalizedPath = Normalize(fullPath),
            FileExtension = info.Extension.ToLowerInvariant(),
            FileSize = info.Exists ? info.Length : null,
            LastKnownModifiedAtUtc = info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : null,
            OptionalHash = hash,
            LinkMode = mode,
            ImportTaskId = taskId,
            AddedAtUtc = now,
            UpdatedAtUtc = now,
            LastVerifiedAtUtc = now,
            IsMissing = !info.Exists,
            MissingSinceAtUtc = info.Exists ? null : now
        };
    }

    private static BookingDocumentBatchResult BuildBatchResult(Guid? taskId, IReadOnlyList<BookingDocumentItemOutcome> outcomes, long bytesProcessed, long bytesWritten, TaskResultSummary? suppliedSummary = null)
    {
        var succeeded = outcomes.Count(item => item.Document is not null && item.ErrorCode != ErrorCodeCatalog.DuplicateConflict);
        var failed = outcomes.Count(item => item.State == BookingDocumentFileState.Failed);
        var skipped = outcomes.Count(item => item.ErrorCode == ErrorCodeCatalog.DuplicateConflict);
        var waiting = outcomes.Count(item => item.PendingAssociation is not null || item.State == BookingDocumentFileState.WaitingForConfirmation);
        var summary = suppliedSummary ?? new TaskResultSummary(outcomes.Count, succeeded, failed, skipped, 0, waiting, bytesProcessed, bytesWritten);
        var status = waiting > 0 && succeeded > 0 ? BookingDocumentBatchStatus.PartiallyCompleted : waiting > 0 ? BookingDocumentBatchStatus.NeedsAttention :
            failed > 0 && succeeded > 0 ? BookingDocumentBatchStatus.PartiallyCompleted : failed > 0 ? BookingDocumentBatchStatus.Failed : BookingDocumentBatchStatus.Completed;
        return new(taskId, status, summary, outcomes);
    }

    private static string ValidateSupportedFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("文件路径不能为空。", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath)) throw new NotSupportedException("当前版本只支持添加单个或多个文件。" );
        if (!File.Exists(fullPath)) throw new FileNotFoundException("文件不存在或当前不可访问。", fullPath);
        if (!AllowedExtensions.Contains(Path.GetExtension(fullPath))) throw new NotSupportedException("当前文件类型不受支持。" );
        return fullPath;
    }

    private static string ValidateDestination(string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot)) throw new ArgumentException("请选择目标资料目录。", nameof(destinationRoot));
        var fullPath = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsInside(fullPath, Path.GetFullPath(AppContext.BaseDirectory)) || IsInside(fullPath, Path.GetFullPath(AppDataPaths.Root)))
            throw new UnauthorizedAccessException("不能将拍摄资料复制到安装目录或应用数据目录。" );
        return fullPath;
    }

    private static bool IsInside(string path, string parent)
    {
        var normalizedParent = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(path, normalizedParent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => Path.GetFullPath(path).ToUpperInvariant();
    private static string NormalizeLoose(string path) { try { return Normalize(path); } catch { return path.Trim().ToUpperInvariant(); } }
    private static string CategoryFolder(BookingDocumentType type) => type switch
    {
        BookingDocumentType.PhotographyPlan => "摄影策划",
        BookingDocumentType.ShootAgreement => "拍摄协议",
        BookingDocumentType.ModelRelease => "模特授权书",
        BookingDocumentType.Quotation => "报价单",
        BookingDocumentType.VenueMaterial => "场地资料",
        BookingDocumentType.WardrobeReference => "服装参考",
        BookingDocumentType.LightingDiagram => "灯光图",
        _ => "其他"
    };

    private static string MapFileError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => ErrorCodeCatalog.PermissionDenied,
        FileNotFoundException or DirectoryNotFoundException => ErrorCodeCatalog.SourceNotFound,
        PathTooLongException => ErrorCodeCatalog.PathTooLong,
        NotSupportedException => ErrorCodeCatalog.UnsupportedFormat,
        IOException io when io.HResult == unchecked((int)0x80070020) => ErrorCodeCatalog.FileLocked,
        IOException => ErrorCodeCatalog.DestinationNotWritable,
        _ => ErrorCodeCatalog.DestinationNotWritable
    };

    private Task WriteAuditAsync(Guid bookingId, Guid? projectId, BookingDocumentType type, string operation, string result, Guid? taskId, string? errorCode, CancellationToken cancellationToken) =>
        auditLog.WriteAsync("BookingDocument", operation, result is "Succeeded" or "FileKept" ? "Information" : "Warning",
            $"BookingId={bookingId:D};DocumentType={type};Operation={operation};Result={result}", taskId, projectId, errorCode, taskId?.ToString("N") ?? Guid.NewGuid().ToString("N"), cancellationToken);
}
