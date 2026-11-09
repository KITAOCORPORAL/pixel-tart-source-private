using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PixelTart.SelectionApi.Contracts;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class HttpOnlineSelectionProvider : IOnlineSelectionProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient _client;
    private readonly string _accessStorePath;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, LocalDevSelectionAccess> _access = new();
    private readonly SemaphoreSlim _createGate = new(1, 1);
    private readonly SemaphoreSlim _accessStoreGate = new(1, 1);
    private readonly byte[] _entropy;

    public HttpOnlineSelectionProvider(Uri endpoint, string accessStorePath)
    {
        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.IsLoopback && endpoint.Scheme == Uri.UriSchemeHttp)))
            throw new ArgumentException("选片服务需要 HTTPS 或本机 HTTP 地址。", nameof(endpoint));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("桌面在线选片凭据需要 Windows 用户加密存储。");
        var endpointIdentity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.GetLeftPart(UriPartial.Authority).ToLowerInvariant())));
        _entropy = Encoding.UTF8.GetBytes("Guangjing/OnlineSelection/HttpAccess/v1/" + endpointIdentity);
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(30) };
        _accessStorePath = Path.GetFullPath(accessStorePath);
        LoadAccess();
    }

    public OnlineSelectionProviderKind Kind => OnlineSelectionProviderKind.Http;
    public bool IsConfigured => true;
    public string DisplayName => "HTTP 在线选片";
    public string? GetClientUrl(Guid projectId) => _access.TryGetValue(projectId, out var value) ? value.ClientUrl : null;
    public bool TryGetAccess(Guid projectId, out LocalDevSelectionAccess access) => _access.TryGetValue(projectId, out access!);

    public async Task<OnlineSelectionProviderResult<SelectionProject>> CreateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default)
    {
        await _createGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_access.TryGetValue(project.Id, out var access))
            {
                access = new LocalDevSelectionAccess(project.Id, project.PublicId,
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(), 1, 0);
                _access[project.Id] = access;
                // Persist BEFORE sending: a lost HTTP response can be replayed with exactly the same identity.
                await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
            }
            var response = await _client.PostAsJsonAsync(SelectionApiRouteNames.Projects,
                new CreateSelectionProjectRequest(project.Name, project.ClientDisplayName, project.TargetCount, project.DeadlineUtc)
                { ProjectId = project.Id, OwnerToken = access.DevAccessToken }, JsonOptions, cancellationToken).ConfigureAwait(false);
            var created = await ReadAsync<LocalDevCreateProjectResponse>(response, cancellationToken).ConfigureAwait(false);
            if (created.Value is null) return OnlineSelectionProviderResult<SelectionProject>.Failed(created.ErrorCode!, created.Message);
            access = access with { PublicId = created.Value.Project.PublicId,
                SelectionVersion = created.Value.Project.SelectionVersion, Revision = created.Value.Project.Revision };
            _access[access.ProjectId] = access;
            await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
            return OnlineSelectionProviderResult<SelectionProject>.Completed(project with { PublicId = created.Value.Project.PublicId, Status = Enum.Parse<SelectionProjectStatus>(created.Value.Project.Status, true), UpdatedAtUtc = DateTimeOffset.UtcNow });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OnlineSelectionProviderResult<SelectionProject>.Failed(OnlineSelectionErrorCodes.UploadFailed, "在线选片项目同步失败，请保留项目后重试。");
        }
        finally { _createGate.Release(); }
    }

    public Task<OnlineSelectionProviderResult<SelectionProject>> UpdateProjectAsync(SelectionProject project, CancellationToken cancellationToken = default) =>
        Task.FromResult(OnlineSelectionProviderResult<SelectionProject>.Failed("UpdateNotSupported", "请使用已有项目同步规则；本版尚不支持远端项目重命名。"));

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
            return OnlineSelectionProviderResult<SelectionAsset>.Failed(OnlineSelectionErrorCodes.UploadFailed, "代理上传失败，请重试。");
        }
    }

    public async Task<OnlineSelectionProviderResult<SelectionRule>> UpdateRuleAsync(SelectionRule rule, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccess(rule.ProjectId, out var access)) return Missing<SelectionRule>();
        var requestBody = new UpsertSelectionRuleRequest(rule.TargetCount, rule.MinimumCount, rule.MaximumCount, rule.AllowExtraSelections,
            rule.ExtraSelectionPriceMinor, rule.AllowComments, rule.AllowFavorites, rule.AllowDownload, rule.ShowFileNames,
            rule.ApplyWatermark, rule.DeadlineUtc, rule.RequirePin, rule.LockAfterConfirmation) { AccessExpiresAtUtc = rule.AccessExpiresAtUtc };
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
        _access[projectId] = access with { PublicId = parsed.Value.PublicId, SelectionVersion = parsed.Value.SelectionVersion, Revision = parsed.Value.Revision, ClientUrl = parsed.Value.ClientUrl };
        await SaveAccessAsync(cancellationToken).ConfigureAwait(false);
        return OnlineSelectionProviderResult<SelectionPublish>.Completed(publish with { PublicId = parsed.Value.PublicId, TokenVersion = parsed.Value.SelectionVersion });
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
        using var request = Authorized(HttpMethod.Get, path, token);
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string token, object? body, CancellationToken cancellationToken)
    {
        using var request = Authorized(method, path, token);
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
            var message = problem?.Code == "FinalSelectionUnavailable" ? "客户尚未确认选片，请稍后刷新。" : problem?.Message ?? "服务请求未完成，请重试。";
            return (default, problem?.Code ?? $"Http{(int)response.StatusCode}", message);
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
    { SelectionVersion = snapshot.SelectionVersion, Revision = snapshot.Revision, IsLocked = snapshot.IsLocked };

    private void UpdateAccess(SelectionProjectResponse project, LocalDevSelectionAccess existing) =>
        _access[project.Id] = existing with { PublicId = project.PublicId, SelectionVersion = project.SelectionVersion, Revision = project.Revision };

    private static OnlineSelectionProviderResult<T> Missing<T>() => OnlineSelectionProviderResult<T>.Failed("LocalDevAccessMissing", "当前项目的服务访问凭据缺失，请重新连接原服务。");

    private void LoadAccess()
    {
        try
        {
            if (!File.Exists(_accessStorePath)) return;
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            var protectedBytes = File.ReadAllBytes(_accessStorePath);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
            foreach (var item in JsonSerializer.Deserialize<LocalDevSelectionAccess[]>(clearBytes, JsonOptions) ?? []) _access[item.ProjectId] = item;
        }
        catch (Exception exception) { throw new InvalidDataException("在线选片凭据无法读取，已保留原文件。", exception); }
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
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                var clearBytes = JsonSerializer.SerializeToUtf8Bytes(_access.Values.ToArray(), JsonOptions);
                var protectedBytes = ProtectedData.Protect(clearBytes, _entropy, DataProtectionScope.CurrentUser);
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
        _createGate.Dispose();
    }
}

public sealed record LocalDevSelectionAccess(Guid ProjectId, string PublicId, string DevAccessToken, int SelectionVersion, long Revision)
{
    public string? ClientUrl { get; init; }
}
