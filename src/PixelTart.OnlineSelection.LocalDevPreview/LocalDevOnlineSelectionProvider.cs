using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PixelTart.SelectionApi.Contracts;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;

namespace PixelTart.OnlineSelection.LocalDevPreview;

public sealed class LocalDevOnlineSelectionProvider : ILocalDevOnlineSelectionProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient _client;
    private readonly string _accessStorePath;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, LocalDevSelectionAccess> _access = new();
    private readonly SemaphoreSlim _accessStoreGate = new(1, 1);
    private static readonly byte[] AccessEntropy = Encoding.UTF8.GetBytes("PixelTart/OnlineSelection/LocalDevAccess/v1");

    public LocalDevOnlineSelectionProvider(Uri endpoint, string accessStorePath)
    {
        if (!endpoint.IsLoopback || endpoint.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException("LocalDev endpoint must be loopback HTTP.", nameof(endpoint));
        _client = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(30) };
        _accessStorePath = Path.GetFullPath(accessStorePath);
        LoadAccess();
    }

    public OnlineSelectionProviderKind Kind => OnlineSelectionProviderKind.LocalDev;
    public bool IsConfigured => true;
    public string DisplayName => "LocalDev（仅本机预览）";
    public bool TryGetAccess(Guid projectId, out LocalDevSelectionAccess access) => _access.TryGetValue(projectId, out access!);

    public async Task<OnlineSelectionProviderResult<SelectionProject>> CreateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(SelectionApiRouteNames.Projects,
                new CreateSelectionProjectRequest(project.Name, project.ClientDisplayName, project.TargetCount, project.DeadlineUtc), JsonOptions, cancellationToken).ConfigureAwait(false);
            var created = await ReadAsync<LocalDevCreateProjectResponse>(response, cancellationToken).ConfigureAwait(false);
            if (created.Value is null) return OnlineSelectionProviderResult<SelectionProject>.Failed(created.ErrorCode!, created.Message);
            var access = new LocalDevSelectionAccess(created.Value.Project.Id, created.Value.Project.PublicId, created.Value.DevAccessToken,
                created.Value.Project.SelectionVersion, created.Value.Project.Revision);
            _access[access.ProjectId] = access;
            await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
            return OnlineSelectionProviderResult<SelectionProject>.Completed(ToModel(created.Value.Project, project.LocalSourceDirectory));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OnlineSelectionProviderResult<SelectionProject>.Failed(OnlineSelectionErrorCodes.UploadFailed, "LocalDev server is unavailable.");
        }
    }

    public Task<OnlineSelectionProviderResult<SelectionProject>> UpdateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default) =>
        Task.FromResult(OnlineSelectionProviderResult<SelectionProject>.Completed(project));

    public async Task<OnlineSelectionProviderResult<SelectionAsset>> UploadAssetAsync(Guid projectId, SelectionAsset asset, Stream content, IProgress<SelectionUploadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<SelectionAsset>();
        try
        {
            using var request = Authorized(HttpMethod.Post, $"{SelectionApiRouteNames.Projects}/{projectId}/assets/{asset.Id}/proxy", access.DevAccessToken);
            request.Headers.TryAddWithoutValidation("X-Original-File-Name", asset.OriginalFileName);
            request.Headers.TryAddWithoutValidation("X-Proxy-File-Name", Path.GetFileName(asset.ProxyJpegPath ?? "proxy.jpg"));
            if (asset.SourceAssetId.HasValue) request.Headers.TryAddWithoutValidation("X-Source-Asset-Id", asset.SourceAssetId.Value.ToString());
            request.Content = new StreamContent(content);
            request.Content.Headers.ContentType = new("image/jpeg");
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var uploaded = await ReadAsync<LocalDevAssetUploadResponse>(response, cancellationToken).ConfigureAwait(false);
            if (uploaded.Value is null) return OnlineSelectionProviderResult<SelectionAsset>.Failed(uploaded.ErrorCode!, uploaded.Message);
            progress?.Report(new(asset.Id, uploaded.Value.ProxyBytes, uploaded.Value.ProxyBytes));
            _access[projectId] = access with { SelectionVersion = uploaded.Value.SelectionVersion, Revision = uploaded.Value.Revision };
            await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
            return OnlineSelectionProviderResult<SelectionAsset>.Completed(asset with
            {
                Status = SelectionAssetStatus.Ready,
                CloudAssetId = asset.Id.ToString("N"),
                ProxyBytes = uploaded.Value.ProxyBytes,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OnlineSelectionProviderResult<SelectionAsset>.Failed(OnlineSelectionErrorCodes.UploadFailed, "LocalDev proxy upload failed.");
        }
    }

    public async Task<OnlineSelectionProviderResult<SelectionRule>> UpdateRuleAsync(SelectionRule rule, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(rule.ProjectId, out var access)) return Missing<SelectionRule>();
        var requestBody = new UpsertSelectionRuleRequest(rule.TargetCount, rule.MinimumCount, rule.MaximumCount, rule.AllowExtraSelections,
            rule.ExtraSelectionPriceMinor, rule.AllowComments, rule.AllowFavorites, rule.AllowDownload, rule.ShowFileNames,
            rule.ApplyWatermark, rule.DeadlineUtc, rule.RequirePin, rule.LockAfterConfirmation);
        var response = await SendJsonAsync(HttpMethod.Put, $"{SelectionApiRouteNames.Projects}/{rule.ProjectId}/rule", access.DevAccessToken, requestBody, cancellationToken).ConfigureAwait(false);
        var parsed = await ReadAsync<SelectionRuleResponse>(response, cancellationToken).ConfigureAwait(false);
        return parsed.Value is null ? OnlineSelectionProviderResult<SelectionRule>.Failed(parsed.ErrorCode!, parsed.Message) : OnlineSelectionProviderResult<SelectionRule>.Completed(rule);
    }

    public async Task<OnlineSelectionProviderResult<SelectionPublish>> PublishProjectAsync(Guid projectId, SelectionPublish publish, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<SelectionPublish>();
        var response = await SendJsonAsync(HttpMethod.Post, $"{SelectionApiRouteNames.Projects}/{projectId}/publish", access.DevAccessToken, null, cancellationToken).ConfigureAwait(false);
        var parsed = await ReadAsync<LocalDevPublishResponse>(response, cancellationToken).ConfigureAwait(false);
        if (parsed.Value is null) return OnlineSelectionProviderResult<SelectionPublish>.Failed(parsed.ErrorCode!, parsed.Message);
        _access[projectId] = access with { PublicId = parsed.Value.PublicId, SelectionVersion = parsed.Value.SelectionVersion, Revision = parsed.Value.Revision };
        await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
        return OnlineSelectionProviderResult<SelectionPublish>.Completed(publish with { PublicId = parsed.Value.PublicId });
    }

    public async Task<OnlineSelectionProviderResult<SelectionProject>> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<SelectionProject>();
        var parsed = await GetAsync<SelectionProjectResponse>($"{SelectionApiRouteNames.Projects}/{projectId}", access.DevAccessToken, cancellationToken).ConfigureAwait(false);
        if (parsed.Value is null) return OnlineSelectionProviderResult<SelectionProject>.Failed(parsed.ErrorCode!, parsed.Message);
        UpdateAccess(parsed.Value, access);
        await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
        return OnlineSelectionProviderResult<SelectionProject>.Completed(ToModel(parsed.Value));
    }

    public async Task<OnlineSelectionProviderResult<SelectionProgress>> GetSelectionProgressAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<SelectionProgress>();
        var parsed = await GetAsync<SelectionProgressResponse>($"{SelectionApiRouteNames.Projects}/{projectId}/progress", access.DevAccessToken, cancellationToken).ConfigureAwait(false);
        return parsed.Value is null
            ? OnlineSelectionProviderResult<SelectionProgress>.Failed(parsed.ErrorCode!, parsed.Message)
            : OnlineSelectionProviderResult<SelectionProgress>.Completed(new(projectId, parsed.Value.Total, parsed.Value.Ready, parsed.Value.Selected,
                parsed.Value.Favorites, parsed.Value.Comments, 0, parsed.Value.LastActivityUtc));
    }

    public async Task<OnlineSelectionProviderResult<SelectionFinalResult>> GetFinalSelectionAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<SelectionFinalResult>();
        var parsed = await GetAsync<FinalSelectionSnapshotResponse>($"{SelectionApiRouteNames.Projects}/{projectId}/final-selection", access.DevAccessToken, cancellationToken).ConfigureAwait(false);
        if (parsed.Value is null) return OnlineSelectionProviderResult<SelectionFinalResult>.Failed(parsed.ErrorCode!, parsed.Message);
        return OnlineSelectionProviderResult<SelectionFinalResult>.Completed(ToResult(parsed.Value));
    }

    public async Task<OnlineSelectionProviderResult<SelectionFinalResult>> ReopenAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<SelectionFinalResult>();
        var response = await SendJsonAsync(HttpMethod.Post, $"{SelectionApiRouteNames.Projects}/{projectId}/reopen", access.DevAccessToken, null, cancellationToken).ConfigureAwait(false);
        var parsed = await ReadAsync<ReopenSelectionResponse>(response, cancellationToken).ConfigureAwait(false);
        if (parsed.Value is null) return OnlineSelectionProviderResult<SelectionFinalResult>.Failed(parsed.ErrorCode!, parsed.Message);
        _access[projectId] = access with { SelectionVersion = parsed.Value.SelectionVersion, Revision = parsed.Value.Revision };
        await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await GetFinalSelectionAsync(projectId, cancellationToken).ConfigureAwait(false);
        return snapshot.Value is null ? snapshot : OnlineSelectionProviderResult<SelectionFinalResult>.Completed(snapshot.Value);
    }

    public async Task<OnlineSelectionProviderResult<bool>> UnpublishAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await SimpleAsync(HttpMethod.Post, $"{SelectionApiRouteNames.Projects}/{projectId}/unpublish", projectId, cancellationToken).ConfigureAwait(false);

    public async Task<OnlineSelectionProviderResult<bool>> DeleteCloudAssetAsync(Guid projectId, Guid assetId, CancellationToken cancellationToken = default) =>
        await SimpleAsync(HttpMethod.Delete, $"{SelectionApiRouteNames.Projects}/{projectId}/assets/{assetId}/cloud-copy", projectId, cancellationToken).ConfigureAwait(false);

    private async Task<OnlineSelectionProviderResult<bool>> SimpleAsync(HttpMethod method, string path, Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetAccess(projectId, out var access)) return Missing<bool>();
        using var response = await _client.SendAsync(Authorized(method, path, access.DevAccessToken), cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode ? OnlineSelectionProviderResult<bool>.Completed(true) : OnlineSelectionProviderResult<bool>.Failed("LocalDevRequestFailed", await ErrorMessageAsync(response, cancellationToken).ConfigureAwait(false));
    }

    private async Task<(T? Value, string? ErrorCode, string Message)> GetAsync<T>(string path, string token, CancellationToken cancellationToken)
    {
        using var response = await _client.SendAsync(Authorized(HttpMethod.Get, path, token), cancellationToken).ConfigureAwait(false);
        return await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string token, object? body, CancellationToken cancellationToken)
    {
        var request = Authorized(method, path, token);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-PixelTart-Dev-Token", token);
        return request;
    }

    private static async Task<(T? Value, string? ErrorCode, string Message)> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false), null, "Completed.");
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return (default, problem?.Code ?? $"Http{(int)response.StatusCode}", problem?.Message ?? "LocalDev request failed.");
        }
    }

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try { return (await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, cancellationToken).ConfigureAwait(false))?.Message ?? "LocalDev request failed."; }
        catch { return "LocalDev request failed."; }
    }

    private static SelectionProject ToModel(SelectionProjectResponse value, string? localSourceDirectory = null) => new(
        value.Id, value.PublicId, value.Name, value.ClientDisplayName ?? string.Empty,
        Enum.TryParse<SelectionProjectStatus>(value.Status, true, out var status) ? status : SelectionProjectStatus.Draft,
        value.TargetCount, value.DeadlineUtc, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, localSourceDirectory);

    private static SelectionFinalResult ToResult(FinalSelectionSnapshotResponse snapshot) => new(snapshot.ProjectId, snapshot.ConfirmedAtUtc,
        snapshot.Items.Select(item => new SelectionFinalItem(item.SelectionProjectId, item.ImageId, item.OriginalFileName,
            item.Selected, item.Favorite, item.CustomerNote, item.ExtraSelected) { SourceAssetId = item.SourceAssetId }).ToArray())
    { SelectionVersion = snapshot.SelectionVersion, IsLocked = snapshot.IsLocked };

    private void UpdateAccess(SelectionProjectResponse project, LocalDevSelectionAccess existing) =>
        _access[project.Id] = existing with { PublicId = project.PublicId, SelectionVersion = project.SelectionVersion, Revision = project.Revision };

    private static OnlineSelectionProviderResult<T> Missing<T>() => OnlineSelectionProviderResult<T>.Failed("LocalDevAccessMissing", "LocalDev project access is missing.");

    private void LoadAccess()
    {
        try
        {
            if (!File.Exists(_accessStorePath)) return;
            var protectedBytes = File.ReadAllBytes(_accessStorePath);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, AccessEntropy, DataProtectionScope.CurrentUser);
            foreach (var item in JsonSerializer.Deserialize<LocalDevSelectionAccess[]>(clearBytes, JsonOptions) ?? []) _access[item.ProjectId] = item;
        }
        catch { _access.Clear(); }
    }

    private async Task SaveAccessAsync(CancellationToken cancellationToken)
    {
        await _accessStoreGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_accessStorePath)!);
            var staging = _accessStorePath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                var clearBytes = JsonSerializer.SerializeToUtf8Bytes(_access.Values.ToArray(), JsonOptions);
                var protectedBytes = ProtectedData.Protect(clearBytes, AccessEntropy, DataProtectionScope.CurrentUser);
                await File.WriteAllBytesAsync(staging, protectedBytes, cancellationToken).ConfigureAwait(false);
                File.Move(staging, _accessStorePath, true);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
        }
        finally { _accessStoreGate.Release(); }
    }

    public void Dispose()
    {
        _client.Dispose();
        _accessStoreGate.Dispose();
    }
}
