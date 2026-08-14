using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using Microsoft.Data.Sqlite;
using PixelTart.SelectionApi.Contracts;

namespace PixelTart.SelectionApi.Server;

public sealed class SelectionApiException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public int? CurrentSelectionVersion { get; init; }
    public long? CurrentRevision { get; init; }
}

public sealed record LocalDevProjectRecord(
    Guid Id,
    string PublicId,
    string Name,
    string ClientDisplayName,
    string Status,
    int TargetCount,
    DateTimeOffset? DeadlineUtc,
    int SelectionVersion,
    long Revision,
    bool IsLocked,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LocalDevAssetRecord(
    Guid SelectionAssetId,
    Guid ProjectId,
    Guid? SourceAssetId,
    string OriginalFileName,
    string OriginalStem,
    string Status,
    int SortOrder,
    string ProxyObjectKey,
    string ThumbObjectKey,
    string PreviewObjectKey,
    long ContentLength,
    DateTimeOffset UpdatedAtUtc);

public sealed record LocalDevSnapshotRecord(
    Guid ProjectId,
    int SelectionVersion,
    long Revision,
    DateTimeOffset ConfirmedAtUtc,
    IReadOnlyList<FinalSelectionItemResponse> Items,
    bool IsLocked);

public sealed class LocalDevSelectionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalDevSelectionRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("A development database path is required.", nameof(databasePath));
        DatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;
                CREATE TABLE IF NOT EXISTS SelectionProjects(
                    Id TEXT PRIMARY KEY,
                    PublicId TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    ClientDisplayName TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    TargetCount INTEGER NOT NULL,
                    DeadlineUtc TEXT NULL,
                    TokenHash TEXT NOT NULL,
                    SelectionVersion INTEGER NOT NULL,
                    Revision INTEGER NOT NULL,
                    IsLocked INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    RuleJson TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS SelectionAssets(
                    SelectionAssetId TEXT PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    SourceAssetId TEXT NULL,
                    OriginalFileName TEXT NOT NULL,
                    OriginalStem TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    ProxyObjectKey TEXT NOT NULL,
                    ThumbObjectKey TEXT NOT NULL,
                    PreviewObjectKey TEXT NOT NULL,
                    ContentLength INTEGER NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    FOREIGN KEY(ProjectId) REFERENCES SelectionProjects(Id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS IX_SelectionAssets_ProjectSort ON SelectionAssets(ProjectId, SortOrder, SelectionAssetId);
                CREATE TABLE IF NOT EXISTS SelectionChoices(
                    ProjectId TEXT NOT NULL,
                    SelectionAssetId TEXT NOT NULL,
                    Selected INTEGER NOT NULL,
                    Favorite INTEGER NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY(ProjectId, SelectionAssetId),
                    FOREIGN KEY(ProjectId) REFERENCES SelectionProjects(Id) ON DELETE CASCADE,
                    FOREIGN KEY(SelectionAssetId) REFERENCES SelectionAssets(SelectionAssetId) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS SelectionComments(
                    ProjectId TEXT NOT NULL,
                    SelectionAssetId TEXT NOT NULL,
                    CustomerNote TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY(ProjectId, SelectionAssetId),
                    FOREIGN KEY(ProjectId) REFERENCES SelectionProjects(Id) ON DELETE CASCADE,
                    FOREIGN KEY(SelectionAssetId) REFERENCES SelectionAssets(SelectionAssetId) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS SelectionSnapshots(
                    ProjectId TEXT NOT NULL,
                    SelectionVersion INTEGER NOT NULL,
                    Revision INTEGER NOT NULL,
                    ConfirmationNonce TEXT NOT NULL,
                    ConfirmedAtUtc TEXT NOT NULL,
                    ItemsJson TEXT NOT NULL,
                    IsLocked INTEGER NOT NULL,
                    PRIMARY KEY(ProjectId, SelectionVersion),
                    FOREIGN KEY(ProjectId) REFERENCES SelectionProjects(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS ClientOperations(
                    ProjectId TEXT NOT NULL,
                    OperationId TEXT NOT NULL,
                    Kind TEXT NOT NULL,
                    ResponseJson TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY(ProjectId, OperationId),
                    FOREIGN KEY(ProjectId) REFERENCES SelectionProjects(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS LocalDevSchema(
                    Id INTEGER PRIMARY KEY CHECK(Id=1),
                    Version INTEGER NOT NULL
                );
                INSERT OR IGNORE INTO LocalDevSchema(Id,Version) VALUES(1,2);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "SelectionProjects", "Revision", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "SelectionSnapshots", "Revision", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "SelectionSnapshots", "ConfirmationNonce", "TEXT NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
            var schema = connection.CreateCommand();
            schema.CommandText = "UPDATE LocalDevSchema SET Version=2 WHERE Id=1;";
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<(LocalDevProjectRecord Project, string Token)> CreateProjectAsync(
        CreateSelectionProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ClientDisplayName) || request.TargetCount <= 0)
            throw new SelectionApiException(400, "InvalidProject", "Project name, client display name, and a positive target count are required.");
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid();
            var publicId = Convert.ToHexString(RandomNumberGenerator.GetBytes(18)).ToLowerInvariant();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var rule = DefaultRule(request.TargetCount, request.DeadlineUtc);
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO SelectionProjects(Id,PublicId,Name,ClientDisplayName,Status,TargetCount,DeadlineUtc,TokenHash,SelectionVersion,Revision,IsLocked,CreatedAtUtc,UpdatedAtUtc,RuleJson)
                VALUES($id,$publicId,$name,$client,$status,$target,$deadline,$tokenHash,1,0,0,$created,$updated,$rule);
                """;
            command.Parameters.AddWithValue("$id", id.ToString("N"));
            command.Parameters.AddWithValue("$publicId", publicId);
            command.Parameters.AddWithValue("$name", request.Name.Trim());
            command.Parameters.AddWithValue("$client", request.ClientDisplayName.Trim());
            command.Parameters.AddWithValue("$status", "Draft");
            command.Parameters.AddWithValue("$target", request.TargetCount);
            command.Parameters.AddWithValue("$deadline", DbDate(request.DeadlineUtc));
            command.Parameters.AddWithValue("$tokenHash", TokenHash(token));
            command.Parameters.AddWithValue("$created", now.ToString("O"));
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            command.Parameters.AddWithValue("$rule", JsonSerializer.Serialize(rule, JsonOptions));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return (new(id, publicId, request.Name.Trim(), request.ClientDisplayName.Trim(), "Draft", request.TargetCount,
                request.DeadlineUtc, 1, 0, false, now, now), token);
        }
        finally { _gate.Release(); }
    }

    public Task<LocalDevProjectRecord> GetProjectAsync(Guid projectId, string token, CancellationToken cancellationToken = default) =>
        WithProjectAsync(projectId, token, requirePublished: false, cancellationToken);

    public async Task<LocalDevProjectRecord> GetPublicProjectAsync(string publicId, string token, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var project = await ReadProjectByPublicIdAsync(connection, publicId, cancellationToken).ConfigureAwait(false)
            ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
        ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken).ConfigureAwait(false));
        if (project.Status is not ("Published" or "Selecting" or "ClientConfirmed"))
            throw new SelectionApiException(404, "ProjectNotPublished", "Selection project is not published.");
        return project;
    }

    public async Task<SelectionRuleResponse> GetRuleAsync(Guid projectId, string token, CancellationToken cancellationToken = default)
    {
        await WithProjectAsync(projectId, token, false, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadRuleAsync(connection, projectId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SelectionRuleResponse> UpsertRuleAsync(Guid projectId, string token, UpsertSelectionRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TargetCount <= 0 || request.MinimumCount < 0 || request.MaximumCount < request.MinimumCount || request.TargetCount < request.MinimumCount || request.TargetCount > request.MaximumCount)
            throw new SelectionApiException(400, "InvalidRule", "Selection count rules are invalid.");
        var project = await WithProjectAsync(projectId, token, false, cancellationToken).ConfigureAwait(false);
        if (project.IsLocked) throw Locked();
        var response = new SelectionRuleResponse(request.TargetCount, request.MinimumCount, request.MaximumCount,
            request.AllowExtraSelections, request.AllowComments, request.AllowFavorites, request.AllowDownload,
            request.ShowFileNames, request.ApplyWatermark, request.DeadlineUtc, request.RequirePin, request.LockAfterConfirmation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE SelectionProjects SET TargetCount=$target,DeadlineUtc=$deadline,RuleJson=$rule,UpdatedAtUtc=$updated WHERE Id=$id;";
            command.Parameters.AddWithValue("$target", request.TargetCount);
            command.Parameters.AddWithValue("$deadline", DbDate(request.DeadlineUtc));
            command.Parameters.AddWithValue("$rule", JsonSerializer.Serialize(response, JsonOptions));
            command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", projectId.ToString("N"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return response;
        }
        finally { _gate.Release(); }
    }

    public async Task<LocalDevAssetRecord> AddAssetAsync(
        Guid projectId,
        string token,
        Guid selectionAssetId,
        Guid? sourceAssetId,
        string originalFileName,
        long contentLength,
        string proxyObjectKey,
        string thumbObjectKey,
        string previewObjectKey,
        CancellationToken cancellationToken = default)
    {
        if (selectionAssetId == Guid.Empty || string.IsNullOrWhiteSpace(originalFileName))
            throw new SelectionApiException(400, "InvalidAsset", "A stable SelectionAssetId and original filename are required.");
        var safeName = Path.GetFileName(originalFileName);
        var now = DateTimeOffset.UtcNow;
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = connection.BeginTransaction();
            var project = await ReadProjectAsync(connection, projectId, cancellationToken, transaction).ConfigureAwait(false)
                ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
            ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken, transaction).ConfigureAwait(false));
            if (project.IsLocked) throw Locked();
            var owner = connection.CreateCommand();
            owner.Transaction = transaction;
            owner.CommandText = "SELECT ProjectId FROM SelectionAssets WHERE SelectionAssetId=$asset;";
            owner.Parameters.AddWithValue("$asset", selectionAssetId.ToString("N"));
            var ownerProject = await owner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (ownerProject is not null && !string.Equals(ownerProject, projectId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                throw new SelectionApiException(409, "SelectionAssetOwnershipConflict", "SelectionAssetId belongs to another project.");
            var orderCommand = connection.CreateCommand();
            orderCommand.Transaction = transaction;
            orderCommand.CommandText = "SELECT COALESCE(MAX(SortOrder),-1)+1 FROM SelectionAssets WHERE ProjectId=$project;";
            orderCommand.Parameters.AddWithValue("$project", projectId.ToString("N"));
            var sortOrder = Convert.ToInt32(await orderCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO SelectionAssets(SelectionAssetId,ProjectId,SourceAssetId,OriginalFileName,OriginalStem,Status,SortOrder,ProxyObjectKey,ThumbObjectKey,PreviewObjectKey,ContentLength,UpdatedAtUtc)
                VALUES($asset,$project,$source,$name,$stem,'Ready',$sort,$proxy,$thumb,$preview,$length,$updated)
                ON CONFLICT(SelectionAssetId) DO UPDATE SET SourceAssetId=excluded.SourceAssetId,OriginalFileName=excluded.OriginalFileName,OriginalStem=excluded.OriginalStem,Status='Ready',ProxyObjectKey=excluded.ProxyObjectKey,ThumbObjectKey=excluded.ThumbObjectKey,PreviewObjectKey=excluded.PreviewObjectKey,ContentLength=excluded.ContentLength,UpdatedAtUtc=excluded.UpdatedAtUtc WHERE SelectionAssets.ProjectId=excluded.ProjectId;
                """;
            command.Parameters.AddWithValue("$asset", selectionAssetId.ToString("N"));
            command.Parameters.AddWithValue("$project", projectId.ToString("N"));
            command.Parameters.AddWithValue("$source", sourceAssetId?.ToString("N") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$name", safeName);
            command.Parameters.AddWithValue("$stem", Path.GetFileNameWithoutExtension(safeName));
            command.Parameters.AddWithValue("$sort", sortOrder);
            command.Parameters.AddWithValue("$proxy", proxyObjectKey);
            command.Parameters.AddWithValue("$thumb", thumbObjectKey);
            command.Parameters.AddWithValue("$preview", previewObjectKey);
            command.Parameters.AddWithValue("$length", contentLength);
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            var nextRevision = project.Revision + 1;
            await SetProjectStateAsync(connection, projectId, project.Status, project.SelectionVersion, nextRevision, project.IsLocked, cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(selectionAssetId, projectId, sourceAssetId, safeName, Path.GetFileNameWithoutExtension(safeName), "Ready", sortOrder,
                proxyObjectKey, thumbObjectKey, previewObjectKey, contentLength, now);
        }
        finally { _gate.Release(); }
    }

    public async Task<(IReadOnlyList<LocalDevAssetRecord> Items, string? NextCursor)> GetAssetsAsync(
        Guid projectId,
        string token,
        string? cursor,
        int limit,
        bool requirePublished,
        CancellationToken cancellationToken = default)
    {
        await WithProjectAsync(projectId, token, requirePublished, cancellationToken).ConfigureAwait(false);
        limit = Math.Clamp(limit, 1, 100);
        var after = int.TryParse(cursor, out var parsed) ? parsed : -1;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT SelectionAssetId,ProjectId,SourceAssetId,OriginalFileName,OriginalStem,Status,SortOrder,ProxyObjectKey,ThumbObjectKey,PreviewObjectKey,ContentLength,UpdatedAtUtc FROM SelectionAssets WHERE ProjectId=$project AND SortOrder>$after ORDER BY SortOrder,SelectionAssetId LIMIT $limit;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$after", after);
        command.Parameters.AddWithValue("$limit", limit + 1);
        var items = new List<LocalDevAssetRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadAsset(reader));
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return (items, hasMore && items.Count > 0 ? items[^1].SortOrder.ToString() : null);
    }

    public async Task<LocalDevAssetRecord> GetAssetAsync(Guid projectId, Guid assetId, string token, bool requirePublished, CancellationToken cancellationToken = default)
    {
        await WithProjectAsync(projectId, token, requirePublished, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT SelectionAssetId,ProjectId,SourceAssetId,OriginalFileName,OriginalStem,Status,SortOrder,ProxyObjectKey,ThumbObjectKey,PreviewObjectKey,ContentLength,UpdatedAtUtc FROM SelectionAssets WHERE ProjectId=$project AND SelectionAssetId=$asset;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new SelectionApiException(404, "AssetNotFound", "Selection asset was not found.");
        return ReadAsset(reader);
    }

    public async Task<LocalDevAssetRecord> GetAssetByMediaSessionAsync(Guid projectId, Guid assetId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var project = await ReadProjectAsync(connection, projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
        if (project.Status is not ("Published" or "Selecting" or "ClientConfirmed"))
            throw new SelectionApiException(404, "ProjectNotPublished", "Selection project is not published.");
        var command = connection.CreateCommand();
        command.CommandText = "SELECT SelectionAssetId,ProjectId,SourceAssetId,OriginalFileName,OriginalStem,Status,SortOrder,ProxyObjectKey,ThumbObjectKey,PreviewObjectKey,ContentLength,UpdatedAtUtc FROM SelectionAssets WHERE ProjectId=$project AND SelectionAssetId=$asset;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new SelectionApiException(404, "AssetNotFound", "Selection asset was not found.");
        return ReadAsset(reader);
    }

    public Task<SelectionMutationResponse> SetChoiceAsync(Guid projectId, Guid assetId, string token, SelectionChoiceRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(projectId, assetId, token, request.ExpectedSelectionVersion, request.ExpectedRevision, request.OperationId, "choice", async (connection, now, tokenValue) =>
        {
            var command = connection.CreateCommand();
            command.Transaction = tokenValue;
            command.CommandText = "INSERT INTO SelectionChoices(ProjectId,SelectionAssetId,Selected,Favorite,UpdatedAtUtc) VALUES($project,$asset,$selected,$favorite,$updated) ON CONFLICT(ProjectId,SelectionAssetId) DO UPDATE SET Selected=excluded.Selected,Favorite=excluded.Favorite,UpdatedAtUtc=excluded.UpdatedAtUtc;";
            command.Parameters.AddWithValue("$project", projectId.ToString("N"));
            command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
            command.Parameters.AddWithValue("$selected", request.Selected ? 1 : 0);
            command.Parameters.AddWithValue("$favorite", request.Favorite ? 1 : 0);
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task<SelectionMutationResponse> SetFavoriteAsync(Guid projectId, Guid assetId, string token, SelectionChoiceRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(projectId, assetId, token, request.ExpectedSelectionVersion, request.ExpectedRevision, request.OperationId, "favorite", async (connection, now, transaction) =>
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO SelectionChoices(ProjectId,SelectionAssetId,Selected,Favorite,UpdatedAtUtc) VALUES($project,$asset,0,$favorite,$updated) ON CONFLICT(ProjectId,SelectionAssetId) DO UPDATE SET Favorite=excluded.Favorite,UpdatedAtUtc=excluded.UpdatedAtUtc;";
            command.Parameters.AddWithValue("$project", projectId.ToString("N"));
            command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
            command.Parameters.AddWithValue("$favorite", request.Favorite ? 1 : 0);
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task<SelectionMutationResponse> SetCommentAsync(Guid projectId, Guid assetId, string token, SelectionCommentRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(projectId, assetId, token, request.ExpectedSelectionVersion, request.ExpectedRevision, request.OperationId, "comment", async (connection, now, transaction) =>
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO SelectionComments(ProjectId,SelectionAssetId,CustomerNote,UpdatedAtUtc) VALUES($project,$asset,$note,$updated) ON CONFLICT(ProjectId,SelectionAssetId) DO UPDATE SET CustomerNote=excluded.CustomerNote,UpdatedAtUtc=excluded.UpdatedAtUtc;";
            command.Parameters.AddWithValue("$project", projectId.ToString("N"));
            command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
            command.Parameters.AddWithValue("$note", request.CustomerNote?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public async Task<LocalDevPublishResponse> PublishAsync(Guid projectId, string token, CancellationToken cancellationToken = default)
    {
        var project = await WithProjectAsync(projectId, token, false, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM SelectionAssets WHERE ProjectId=$project AND Status='Ready';";
        countCommand.Parameters.AddWithValue("$project", projectId.ToString("N"));
        if (Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0)
            throw new SelectionApiException(409, "NoReadyAssets", "At least one ready proxy is required before publishing.");
        await SetProjectStateAsync(connection, projectId, "Published", project.SelectionVersion, project.Revision, project.IsLocked, cancellationToken).ConfigureAwait(false);
        return new(projectId, project.PublicId, token, project.SelectionVersion, project.Revision, project.IsLocked);
    }

    public async Task UnpublishAsync(Guid projectId, string token, CancellationToken cancellationToken = default)
    {
        var project = await WithProjectAsync(projectId, token, false, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await SetProjectStateAsync(connection, projectId, "Draft", project.SelectionVersion, project.Revision, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LocalDevSnapshotRecord> ConfirmAsync(Guid projectId, string token, ConfirmSelectionRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed) throw new SelectionApiException(400, "ConfirmationRequired", "Explicit confirmation is required.");
        if (string.IsNullOrWhiteSpace(request.ConfirmationNonce) || request.ConfirmationNonce.Length > 128)
            throw new SelectionApiException(400, "InvalidConfirmationNonce", "A stable confirmation nonce is required.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = connection.BeginTransaction();
            var project = await ReadProjectAsync(connection, projectId, cancellationToken, transaction).ConfigureAwait(false)
                ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
            ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken, transaction).ConfigureAwait(false));
            var existing = await ReadSnapshotByNonceAsync(connection, projectId, request.ConfirmationNonce, cancellationToken, transaction).ConfigureAwait(false);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }
            if (project.IsLocked) throw Locked();
            EnsureVersion(project, request.ExpectedSelectionVersion, request.ExpectedRevision);
            var items = await ReadFinalItemsAsync(connection, projectId, cancellationToken, transaction).ConfigureAwait(false);
            var nextRevision = project.Revision + 1;
            var now = DateTimeOffset.UtcNow;
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SelectionSnapshots(ProjectId,SelectionVersion,Revision,ConfirmationNonce,ConfirmedAtUtc,ItemsJson,IsLocked) VALUES($project,$version,$revision,$nonce,$confirmed,$items,1);";
            insert.Parameters.AddWithValue("$project", projectId.ToString("N"));
            insert.Parameters.AddWithValue("$version", project.SelectionVersion);
            insert.Parameters.AddWithValue("$revision", nextRevision);
            insert.Parameters.AddWithValue("$nonce", request.ConfirmationNonce);
            insert.Parameters.AddWithValue("$confirmed", now.ToString("O"));
            insert.Parameters.AddWithValue("$items", JsonSerializer.Serialize(items, JsonOptions));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await SetProjectStateAsync(connection, projectId, "ClientConfirmed", project.SelectionVersion, nextRevision, true, cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(projectId, project.SelectionVersion, nextRevision, now, items, true);
        }
        finally { _gate.Release(); }
    }

    public async Task<ReopenSelectionResponse> ReopenAsync(Guid projectId, string token, CancellationToken cancellationToken = default)
    {
        var project = await WithProjectAsync(projectId, token, false, cancellationToken).ConfigureAwait(false);
        if (!project.IsLocked)
            throw new SelectionApiException(409, "SelectionNotLocked", "Only a confirmed selection can be reopened.");
        var nextVersion = project.SelectionVersion + 1;
        var nextRevision = project.Revision + 1;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await SetProjectStateAsync(connection, projectId, "Selecting", nextVersion, nextRevision, false, cancellationToken).ConfigureAwait(false);
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM SelectionSnapshots WHERE ProjectId=$project;";
        countCommand.Parameters.AddWithValue("$project", projectId.ToString("N"));
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        return new(projectId, nextVersion, nextRevision, false, count);
    }

    public async Task<LocalDevSnapshotRecord> GetLatestSnapshotAsync(Guid projectId, string token, CancellationToken cancellationToken = default)
    {
        await WithProjectAsync(projectId, token, false, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT SelectionVersion,Revision,ConfirmedAtUtc,ItemsJson,IsLocked FROM SelectionSnapshots WHERE ProjectId=$project ORDER BY SelectionVersion DESC LIMIT 1;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new SelectionApiException(404, "FinalSelectionUnavailable", "The client has not confirmed a selection.");
        return new(projectId, reader.GetInt32(0), reader.GetInt64(1), DateTimeOffset.Parse(reader.GetString(2)),
            JsonSerializer.Deserialize<FinalSelectionItemResponse[]>(reader.GetString(3), JsonOptions) ?? [], reader.GetInt32(4) != 0);
    }

    public async Task<SelectionProgressResponse> GetProgressAsync(Guid projectId, string token, bool requirePublished, CancellationToken cancellationToken = default)
    {
        await WithProjectAsync(projectId, token, requirePublished, cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM SelectionAssets WHERE ProjectId=$project),
              (SELECT COUNT(*) FROM SelectionAssets WHERE ProjectId=$project AND Status='Ready'),
              (SELECT COUNT(*) FROM SelectionChoices WHERE ProjectId=$project AND Selected=1),
              (SELECT COUNT(*) FROM SelectionChoices WHERE ProjectId=$project AND Favorite=1),
              (SELECT COUNT(*) FROM SelectionComments WHERE ProjectId=$project AND length(CustomerNote)>0),
              (SELECT MAX(UpdatedAtUtc) FROM (SELECT UpdatedAtUtc FROM SelectionChoices WHERE ProjectId=$project UNION ALL SELECT UpdatedAtUtc FROM SelectionComments WHERE ProjectId=$project));
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new(projectId, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)));
    }

    public async Task DeleteAssetAsync(Guid projectId, Guid assetId, string token, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = connection.BeginTransaction();
            var project = await ReadProjectAsync(connection, projectId, cancellationToken, transaction).ConfigureAwait(false)
                ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
            ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken, transaction).ConfigureAwait(false));
            if (project.IsLocked) throw Locked();
            await EnsureAssetAsync(connection, projectId, assetId, cancellationToken, transaction).ConfigureAwait(false);
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM SelectionAssets WHERE ProjectId=$project AND SelectionAssetId=$asset;";
            command.Parameters.AddWithValue("$project", projectId.ToString("N"));
            command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await SetProjectStateAsync(connection, projectId, project.Status, project.SelectionVersion, project.Revision + 1, false, cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<SelectionMutationResponse> MutateAsync(
        Guid projectId,
        Guid assetId,
        string token,
        int expectedSelectionVersion,
        long expectedRevision,
        string operationId,
        string operationKind,
        Func<SqliteConnection, DateTimeOffset, SqliteTransaction, Task> mutation,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = connection.BeginTransaction();
            var project = await ReadProjectAsync(connection, projectId, cancellationToken, transaction).ConfigureAwait(false)
                ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
            ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken, transaction).ConfigureAwait(false));
            ValidateOperationId(operationId);
            var cached = await ReadOperationAsync(connection, projectId, operationId, cancellationToken, transaction).ConfigureAwait(false);
            if (cached is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return cached;
            }
            if (project.IsLocked) throw Locked();
            EnsureVersion(project, expectedSelectionVersion, expectedRevision);
            await EnsureAssetAsync(connection, projectId, assetId, cancellationToken, transaction).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            await mutation(connection, now, transaction).ConfigureAwait(false);
            var nextRevision = project.Revision + 1;
            await SetProjectStateAsync(connection, projectId, "Selecting", project.SelectionVersion, nextRevision, false, cancellationToken, transaction).ConfigureAwait(false);
            var response = new SelectionMutationResponse(projectId, assetId, project.SelectionVersion, nextRevision, false, now);
            await SaveOperationAsync(connection, projectId, operationId, operationKind, response, cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return response;
        }
        finally { _gate.Release(); }
    }

    private async Task<LocalDevProjectRecord> WithProjectAsync(Guid projectId, string token, bool requirePublished, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var project = await ReadProjectAsync(connection, projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
        ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken).ConfigureAwait(false));
        if (requirePublished && project.Status is not ("Published" or "Selecting" or "ClientConfirmed"))
            throw new SelectionApiException(404, "ProjectNotPublished", "Selection project is not published.");
        return project;
    }

    private static void EnsureVersion(LocalDevProjectRecord project, int expectedSelectionVersion, long expectedRevision)
    {
        if (expectedSelectionVersion != project.SelectionVersion || expectedRevision != project.Revision)
            throw new SelectionApiException(409, "SelectionConflict",
                $"Expected selection version/revision {expectedSelectionVersion}/{expectedRevision}; current is {project.SelectionVersion}/{project.Revision}.")
            {
                CurrentSelectionVersion = project.SelectionVersion,
                CurrentRevision = project.Revision
            };
    }

    private static void ValidateOperationId(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 128)
            throw new SelectionApiException(400, "InvalidOperationId", "A stable idempotency operation id is required.");
    }

    private static SelectionApiException Locked() => new(409, "SelectionLocked", "The confirmed selection is locked. Reopen it from Desktop before editing.");

    private static void ValidateToken(Guid projectId, string token, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new SelectionApiException(401, "InvalidToken", "A valid local development token is required.");
        var expected = Convert.FromHexString(storedHash);
        var actual = Convert.FromHexString(TokenHash(token));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new SelectionApiException(401, "InvalidToken", "A valid local development token is required.");
    }

    private static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static object DbDate(DateTimeOffset? value) => value?.ToString("O") ?? (object)DBNull.Value;

    private static SelectionRuleResponse DefaultRule(int target, DateTimeOffset? deadline) =>
        new(target, 0, Math.Max(1, target), true, true, true, false, true, false, deadline, false, true);

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<LocalDevProjectRecord?> ReadProjectAsync(SqliteConnection connection, Guid projectId, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id,PublicId,Name,ClientDisplayName,Status,TargetCount,DeadlineUtc,SelectionVersion,Revision,IsLocked,CreatedAtUtc,UpdatedAtUtc FROM SelectionProjects WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", projectId.ToString("N"));
        return await ReadProjectCommandAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LocalDevProjectRecord?> ReadProjectByPublicIdAsync(SqliteConnection connection, string publicId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,PublicId,Name,ClientDisplayName,Status,TargetCount,DeadlineUtc,SelectionVersion,Revision,IsLocked,CreatedAtUtc,UpdatedAtUtc FROM SelectionProjects WHERE PublicId=$publicId;";
        command.Parameters.AddWithValue("$publicId", publicId);
        return await ReadProjectCommandAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LocalDevProjectRecord?> ReadProjectCommandAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new(Guid.ParseExact(reader.GetString(0), "N"), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            reader.GetInt32(5), reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)), reader.GetInt32(7), reader.GetInt64(8), reader.GetInt32(9) != 0,
            DateTimeOffset.Parse(reader.GetString(10)), DateTimeOffset.Parse(reader.GetString(11)));
    }

    private static async Task<string> ReadTokenHashAsync(SqliteConnection connection, Guid projectId, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TokenHash FROM SelectionProjects WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", projectId.ToString("N"));
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))
            ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
    }

    private static async Task<SelectionRuleResponse> ReadRuleAsync(SqliteConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT RuleJson FROM SelectionProjects WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", projectId.ToString("N"));
        var json = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        return JsonSerializer.Deserialize<SelectionRuleResponse>(json ?? string.Empty, JsonOptions)
            ?? throw new SelectionApiException(500, "RuleCorrupt", "Selection rule could not be read.");
    }

    private static LocalDevAssetRecord ReadAsset(SqliteDataReader reader) => new(
        Guid.ParseExact(reader.GetString(0), "N"), Guid.ParseExact(reader.GetString(1), "N"),
        reader.IsDBNull(2) ? null : Guid.ParseExact(reader.GetString(2), "N"), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6),
        reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10), DateTimeOffset.Parse(reader.GetString(11)));

    private static async Task EnsureAssetAsync(SqliteConnection connection, Guid projectId, Guid assetId, CancellationToken cancellationToken, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM SelectionAssets WHERE ProjectId=$project AND SelectionAssetId=$asset;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$asset", assetId.ToString("N"));
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0)
            throw new SelectionApiException(404, "AssetNotFound", "Selection asset was not found.");
    }

    private static async Task<IReadOnlyList<FinalSelectionItemResponse>> ReadFinalItemsAsync(SqliteConnection connection, Guid projectId, CancellationToken cancellationToken, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT a.SelectionAssetId,a.SourceAssetId,a.OriginalFileName,COALESCE(c.Selected,0),COALESCE(c.Favorite,0),m.CustomerNote
            FROM SelectionAssets a
            LEFT JOIN SelectionChoices c ON c.ProjectId=a.ProjectId AND c.SelectionAssetId=a.SelectionAssetId
            LEFT JOIN SelectionComments m ON m.ProjectId=a.ProjectId AND m.SelectionAssetId=a.SelectionAssetId
            WHERE a.ProjectId=$project ORDER BY a.SortOrder,a.SelectionAssetId;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        var items = new List<FinalSelectionItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new(projectId, Guid.ParseExact(reader.GetString(0), "N"), reader.GetString(2), reader.GetInt32(3) != 0,
                reader.GetInt32(4) != 0, reader.IsDBNull(5) ? null : reader.GetString(5), false)
            {
                SourceAssetId = reader.IsDBNull(1) ? null : Guid.ParseExact(reader.GetString(1), "N")
            });
        }
        return items;
    }

    private static async Task SetProjectStateAsync(SqliteConnection connection, Guid projectId, string status, int version, long revision, bool locked, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE SelectionProjects SET Status=$status,SelectionVersion=$version,Revision=$revision,IsLocked=$locked,UpdatedAtUtc=$updated WHERE Id=$id;";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$revision", revision);
        command.Parameters.AddWithValue("$locked", locked ? 1 : 0);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", projectId.ToString("N"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SelectionMutationResponse?> ReadOperationAsync(
        SqliteConnection connection,
        Guid projectId,
        string operationId,
        CancellationToken cancellationToken,
        SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ResponseJson FROM ClientOperations WHERE ProjectId=$project AND OperationId=$operation;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$operation", operationId);
        var json = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SelectionMutationResponse>(json, JsonOptions);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var inspect = connection.CreateCommand();
        inspect.CommandText = $"PRAGMA table_info({table});";
        await using (var reader = await inspect.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
            }
        }
        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AuthorizeAssetUploadAsync(Guid projectId, Guid selectionAssetId, string token, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = connection.BeginTransaction();
            var project = await ReadProjectAsync(connection, projectId, cancellationToken, transaction).ConfigureAwait(false)
                ?? throw new SelectionApiException(404, "ProjectNotFound", "Selection project was not found.");
            ValidateToken(project.Id, token, await ReadTokenHashAsync(connection, project.Id, cancellationToken, transaction).ConfigureAwait(false));
            if (project.IsLocked) throw Locked();
            var owner = connection.CreateCommand();
            owner.Transaction = transaction;
            owner.CommandText = "SELECT ProjectId FROM SelectionAssets WHERE SelectionAssetId=$asset;";
            owner.Parameters.AddWithValue("$asset", selectionAssetId.ToString("N"));
            var ownerProject = await owner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (ownerProject is not null && !string.Equals(ownerProject, projectId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                throw new SelectionApiException(409, "SelectionAssetOwnershipConflict", "SelectionAssetId belongs to another project.");
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    private static async Task<LocalDevSnapshotRecord?> ReadSnapshotByNonceAsync(
        SqliteConnection connection,
        Guid projectId,
        string confirmationNonce,
        CancellationToken cancellationToken,
        SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT SelectionVersion,Revision,ConfirmedAtUtc,ItemsJson,IsLocked FROM SelectionSnapshots WHERE ProjectId=$project AND ConfirmationNonce=$nonce LIMIT 1;";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$nonce", confirmationNonce);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new LocalDevSnapshotRecord(projectId, reader.GetInt32(0), reader.GetInt64(1), DateTimeOffset.Parse(reader.GetString(2)),
            JsonSerializer.Deserialize<FinalSelectionItemResponse[]>(reader.GetString(3), JsonOptions) ?? [], reader.GetInt32(4) != 0);
    }

    private static async Task SaveOperationAsync(
        SqliteConnection connection,
        Guid projectId,
        string operationId,
        string kind,
        SelectionMutationResponse response,
        CancellationToken cancellationToken,
        SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO ClientOperations(ProjectId,OperationId,Kind,ResponseJson,CreatedAtUtc) VALUES($project,$operation,$kind,$response,$created);";
        command.Parameters.AddWithValue("$project", projectId.ToString("N"));
        command.Parameters.AddWithValue("$operation", operationId);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$response", JsonSerializer.Serialize(response, JsonOptions));
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
