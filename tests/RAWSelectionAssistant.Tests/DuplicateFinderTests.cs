using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ExactDuplicateTests
{
    [TestMethod]
    public void GroupsDifferentNamesAndPathsByReliableContentFingerprint()
    {
        var hash = new string('A', 64);
        var first = Candidate("original.jpg", hash, 6000, 4000, rating: 3);
        var second = Candidate("copy.jpg", hash.ToLowerInvariant(), 6000, 4000, rating: 5);
        var unique = Candidate("other.jpg", new string('B', 64), 2000, 1000);
        var groups = DuplicateFinder.FindExact([first, second, unique]);
        Assert.HasCount(1, groups);
        Assert.AreEqual(DuplicateGroupKind.Exact, groups[0].Kind);
        Assert.AreEqual(second.Asset.AssetId, groups[0].SuggestedKeepAssetId);
    }

    internal static DuplicateCandidate Candidate(string name, string? hash, int width, int height, int rating = 0, DateTimeOffset? modified = null) => new(new(
        Guid.NewGuid(), Path.Combine("C:\\photos", name), name, ".jpg", "image/jpeg", width * (long)height / 2,
        hash, width, height, null, null, DateTimeOffset.UtcNow.AddDays(-2), modified ?? DateTimeOffset.UtcNow.AddDays(-1), rating));
}

[TestClass]
public sealed class VisualSimilarityTests
{
    [TestMethod]
    public void ResizedReencodedAndLightlyGradedVersionsRemainSimilarButUnrelatedFramesDoNot()
    {
        var original = Gradient(96, 72, 0);
        var graded = Gradient(48, 36, 6);
        var unrelated = Checker(48, 36);
        var a = Analyzed("a.jpg", original);
        var b = Analyzed("b.jpg", graded);
        var c = Analyzed("c.jpg", unrelated);
        Assert.IsGreaterThan(DuplicateFinder.Similarity(a.Analysis!, c.Analysis!), DuplicateFinder.Similarity(a.Analysis!, b.Analysis!));
        Assert.HasCount(1, DuplicateFinder.FindSimilar([a, b, c], .5));
    }

    private static DuplicateCandidate Analyzed(string name, VisualPixelBuffer pixels)
    {
        var hash = VisualAnalysisFingerprint.Compute(pixels);
        var asset = ExactDuplicateTests.Candidate(name, hash, pixels.Width, pixels.Height);
        return asset with { Analysis = VisualAnalysisEngine.Analyze(new(asset.Asset.AssetId, hash, pixels)) };
    }

    private static VisualPixelBuffer Gradient(int width, int height, int grade)
    {
        var bytes = new byte[width * height * 3];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var value = (byte)Math.Clamp(x * 180 / Math.Max(1, width - 1) + y * 60 / Math.Max(1, height - 1) + grade, 0, 255);
            var offset = (y * width + x) * 3; bytes[offset] = value; bytes[offset + 1] = (byte)Math.Clamp(value + grade, 0, 255); bytes[offset + 2] = (byte)Math.Clamp(value - grade, 0, 255);
        }
        return new(width, height, bytes);
    }

    private static VisualPixelBuffer Checker(int width, int height)
    {
        var bytes = new byte[width * height * 3];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) bytes.AsSpan((y * width + x) * 3, 3).Fill((byte)(((x / 4 + y / 4) & 1) == 0 ? 8 : 245));
        return new(width, height, bytes);
    }
}

[TestClass]
public sealed class DuplicateImportGuardTests
{
    [TestMethod]
    public async Task ExactExistingContentDefaultsToSkipButAllowsExplicitImport()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("incoming.jpg", [1, 3, 3, 7]);
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
        var existing = ExactDuplicateTests.Candidate("existing.jpg", hash, 10, 10).Asset;
        var result = await DuplicateImportGuard.CheckAsync(path, [existing]);
        Assert.IsTrue(result.AlreadyExists);
        Assert.AreEqual(existing.AssetId, result.ExistingAssetId);
        Assert.AreEqual(DuplicateImportChoice.Skip, result.DefaultChoice);
    }
}

[TestClass]
public sealed class DuplicateReferenceProtectionTests
{
    [TestMethod]
    public async Task ReportsEveryConsumerAndPreventsUnconfirmedTrash()
    {
        var repository = new FakeRepository();
        var participants = new[]
        {
            new FakeParticipant(DuplicateReferenceKind.InspirationBoard, 1), new FakeParticipant(DuplicateReferenceKind.Canvas, 2),
            new FakeParticipant(DuplicateReferenceKind.Project, 1), new FakeParticipant(DuplicateReferenceKind.Planning, 1)
        };
        var protection = new DuplicateReferenceProtectionService(participants);
        var usage = await protection.GetUsageAsync(Guid.NewGuid());
        Assert.AreEqual(5, usage.Total);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DuplicateTrashSafetyService(repository, protection).MoveToTrashAsync(Guid.NewGuid(), false));
        Assert.AreEqual(0, repository.TrashCalls);
    }

    internal sealed class FakeParticipant(DuplicateReferenceKind kind, int count, bool fail = false) : IDuplicateReferenceParticipant
    {
        public DuplicateReferenceKind Kind { get; } = kind;
        public int Value { get; private set; } = count;
        public Task<int> CountAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task<object> CaptureAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default) => Task.FromResult<object>(Value);
        public Task ReplaceAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default) { Value = 0; if (fail) throw new IOException("simulated"); return Task.CompletedTask; }
        public Task RestoreAsync(object snapshot, CancellationToken cancellationToken = default) { Value = (int)snapshot; return Task.CompletedTask; }
    }

    internal sealed class FakeRepository : IAssetLibraryRepository
    {
        public int TrashCalls { get; private set; }
        public string DatabasePath => "fake";
        public Task<AssetLibraryBatchResult> SetAssetsTrashedAsync(IEnumerable<Guid> assetIds, bool isTrashed, CancellationToken cancellationToken = default) { TrashCalls++; return Task.FromResult(new AssetLibraryBatchResult(assetIds.Count(), null, [])); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AssetLibraryMetadataIndexResult> ImportAsync(IEnumerable<AssetImportRequest> requests, CancellationToken cancellationToken = default, IProgress<int>? progress = null) => throw new NotSupportedException();
        public Task<AssetItem?> GetAssetAsync(Guid assetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryPage> QueryAsync(AssetLibraryQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetQueryValidationIssue>> ValidateQueryReferencesAsync(AssetQueryDocument document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetQuerySuggestion>> GetQuerySuggestionsAsync(string text, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAssetAsync(Guid assetId, int? rating = null, string? comment = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> UpdateAssetMetadataAsync(Guid assetId, int? rating = null, string? comment = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> UpdateAssetsMetadataAsync(IEnumerable<Guid> assetIds, int? rating = null, string? comment = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> SetAssetsArchivedAsync(IEnumerable<Guid> assetIds, bool isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetTrashEntry>> ListTrashEntriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveProjectAssetLinkAsync(ProjectAssetLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RemoveProjectAssetLinkAsync(Guid projectId, Guid assetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveBookingAssetLinkAsync(BookingAssetLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RemoveBookingAssetLinkAsync(Guid bookingId, Guid assetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectAssetLink>> ListProjectAssetLinksAsync(Guid? assetId = null, Guid? projectId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BookingAssetLink>> ListBookingAssetLinksAsync(Guid? assetId = null, Guid? bookingId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAssetWorkflowMetadataAsync(AssetWorkflowMetadata metadata, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetWorkflowMetadata?> GetAssetWorkflowMetadataAsync(Guid assetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> SetAssetsMissingAsync(IEnumerable<Guid> assetIds, bool isMissing, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetFolder>> ListFoldersAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetFolderTreeItem>> GetFolderTreeAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetFolder> SaveFolderAsync(AssetFolder folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> RenameFolderAsync(Guid folderId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> MoveFolderAsync(AssetFolderMoveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> ReorderFoldersAsync(Guid? parentFolderId, IEnumerable<Guid> orderedFolderIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetFolderBatchCreateResult> BatchCreateFoldersAsync(string paths, Guid? parentFolderId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetFolder>> CopyFolderStructureAsync(Guid sourceFolderId, Guid? targetParentFolderId, string? rootName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ArchiveFolderAsync(Guid folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> SetFolderArchivedAsync(Guid folderId, bool isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> RestoreFolderAsync(Guid folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetFolderMembership>> ListFolderMembershipsAsync(Guid? folderId = null, Guid? assetId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> AddToFolderAsync(IEnumerable<Guid> assetIds, Guid folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> AddToFoldersAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> folderIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> RemoveFromFolderAsync(IEnumerable<Guid> assetIds, Guid folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TagGroup>> ListTagGroupsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TagGroup> SaveTagGroupAsync(TagGroup group, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetTag>> ListTagsAsync(Guid? tagGroupId = null, bool includeArchived = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetTag> SaveTagAsync(AssetTag tag, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetTag>> BatchCreateTagsAsync(string values, Guid? tagGroupId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> RenameTagAsync(Guid tagId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> MoveTagsToGroupAsync(IEnumerable<Guid> tagIds, Guid? tagGroupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetTag>> SearchTagsAsync(string searchText, int limit = 30, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetTagUsageSummary>> GetTagUsageSummaryAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ArchiveTagAsync(Guid tagId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetTagMembership>> ListTagMembershipsAsync(Guid? tagId = null, Guid? assetId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> AddTagsAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> RemoveTagsAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> MergeTagsAsync(Guid sourceTagId, Guid targetTagId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> MergeTagsAsync(IEnumerable<Guid> sourceTagIds, Guid targetTagId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> SetTagArchivedAsync(Guid tagId, bool isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> SetTagGroupArchivedAsync(Guid tagGroupId, bool isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> ReorderTagGroupsAsync(IEnumerable<Guid> orderedTagGroupIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> ReorderTagsAsync(Guid? tagGroupId, IEnumerable<Guid> orderedTagIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SmartFolder>> ListSmartFoldersAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SmartFolder> SaveSmartFolderAsync(SmartFolder folder, IEnumerable<SmartFolderRule> rules, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SmartFolderRule>> ListSmartFolderRulesAsync(Guid smartFolderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SmartFolderQueryDocument?> GetSmartFolderQueryDocumentAsync(Guid smartFolderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SmartFolder> SaveSmartFolderQueryDocumentAsync(SmartFolder folder, AssetQueryDocument document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SmartFolder> CopySmartFolderAsync(Guid smartFolderId, string? copyName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> SetSmartFolderArchivedAsync(Guid smartFolderId, bool isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetBatchMetadataPreview> PreviewBatchMetadataAsync(AssetBatchMetadataRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetLibraryBatchResult> ApplyBatchMetadataAsync(AssetBatchMetadataRequest request, AssetBatchMetadataPreview previewContract, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssetRelinkResult> RelinkMissingAssetsAsync(AssetRelinkRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssetUndoJournalEntry>> ListUndoJournalAsync(int limit = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UndoAsync(AssetLibraryUndoToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RedoAsync(AssetLibraryUndoToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

[TestClass]
public sealed class DuplicateReferenceReplacementTests
{
    [TestMethod]
    public async Task AnyParticipantFailureRollsBackEveryReferenceStore()
    {
        var first = new DuplicateReferenceProtectionTests.FakeParticipant(DuplicateReferenceKind.Canvas, 2);
        var second = new DuplicateReferenceProtectionTests.FakeParticipant(DuplicateReferenceKind.InspirationBoard, 1, fail: true);
        var third = new DuplicateReferenceProtectionTests.FakeParticipant(DuplicateReferenceKind.Project, 1);
        var service = new DuplicateReferenceProtectionService([first, second, third]);
        var target = ExactDuplicateTests.Candidate("keep.jpg", new string('C', 64), 2000, 1200).Asset;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceAsync(Guid.NewGuid(), new(Guid.NewGuid(), target)));
        Assert.AreEqual(2, first.Value); Assert.AreEqual(1, second.Value); Assert.AreEqual(1, third.Value);
    }

    [TestMethod]
    public async Task GroupFailureRestoresEarlierSourceAndTargetReferences()
    {
        var sourceA = Guid.NewGuid(); var sourceB = Guid.NewGuid();
        var participant = new GroupParticipant(sourceA, sourceB);
        var service = new DuplicateReferenceProtectionService([participant]);
        var target = ExactDuplicateTests.Candidate("keep.jpg", new string('D', 64), 2000, 1200).Asset;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceGroupAsync([sourceA, sourceB], new(Guid.NewGuid(), target)));
        CollectionAssert.AreEquivalent(new[] { sourceA, sourceB }, participant.References.ToArray());
    }

    private sealed class GroupParticipant(Guid sourceA, Guid sourceB) : IDuplicateReferenceParticipant
    {
        public DuplicateReferenceKind Kind => DuplicateReferenceKind.Canvas;
        public HashSet<Guid> References { get; } = [sourceA, sourceB];
        public Task<int> CountAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult(References.Contains(assetId) ? 1 : 0);
        public Task<object> CaptureAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default) => Task.FromResult<object>(References.ToArray());
        public Task ReplaceAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default)
        {
            References.Remove(sourceAssetId); References.Add(target.Asset.AssetId);
            if (sourceAssetId == sourceB) throw new IOException("second source failed");
            return Task.CompletedTask;
        }
        public Task RestoreAsync(object snapshot, CancellationToken cancellationToken = default)
        {
            References.Clear(); foreach (var reference in (Guid[])snapshot) References.Add(reference);
            return Task.CompletedTask;
        }
    }
}

[TestClass]
public sealed class DuplicateTrashSafetyTests
{
    [TestMethod]
    public async Task ConfirmedRemovalUsesRecoverableLibraryTrashOnly()
    {
        var repository = new DuplicateReferenceProtectionTests.FakeRepository();
        var protection = new DuplicateReferenceProtectionService([new DuplicateReferenceProtectionTests.FakeParticipant(DuplicateReferenceKind.Canvas, 0)]);
        var result = await new DuplicateTrashSafetyService(repository, protection).MoveToTrashAsync(Guid.NewGuid(), false);
        Assert.AreEqual(1, result.ChangedCount);
        Assert.AreEqual(1, repository.TrashCalls);
    }
}
