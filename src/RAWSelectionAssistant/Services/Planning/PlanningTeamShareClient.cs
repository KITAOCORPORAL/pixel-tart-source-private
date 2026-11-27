#if PLANNING_REVIEW_PILOT
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services.Planning;

internal sealed record PlanningTeamShareBinding(Guid DestinationId, Guid ProjectId, Guid DocumentId,
    Guid PublicationId, string PublishedFingerprint);
internal sealed record PlanningTeamShareManifestFile(string Path, long Length, string Sha256);
internal sealed record PlanningTeamShareReceipt(Guid DestinationId, Guid ProjectId, Guid DocumentId,
    Guid? PublicationId, string State, PlanningTeamShareManifestFile[]? Missing,
    string? OwnerUrl, string? MemberUrl, string? PublishedFingerprint, string? ManifestSha256);

/// <summary>The candidate desktop owns a destination-scoped control connection. It never synchronizes the old snapshot after Ready.</summary>
internal sealed class PlanningTeamShareClient
{
    private sealed record Config(Guid DestinationId, string ControlBase, string MemberBase, string CertificateSha256, string SecretFile);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly object BindingGate = new();
    private readonly Config _config;
    private readonly Uri _controlBase, _memberBase;
    private readonly HttpClient _http;
    private readonly string _local;
    private Window? _ownerWindow;
    private Guid? _openPublication;

    private PlanningTeamShareClient(Config config, string configDirectory)
    {
        _config = config;
        _controlBase = Base(config.ControlBase);
        _memberBase = Base(config.MemberBase);
        if (config.DestinationId == Guid.Empty || _controlBase.Host != "127.0.0.1" ||
            _memberBase.Port != _controlBase.Port || !Hex(config.CertificateSha256) ||
            string.IsNullOrWhiteSpace(config.SecretFile) || Path.GetFileName(config.SecretFile) != config.SecretFile)
            throw new InvalidDataException("团队工作台配置无效。");
        string secretPath = Path.Combine(configDirectory, config.SecretFile);
        PlanningImageStorage.RejectLinks(configDirectory, secretPath);
        if (new FileInfo(secretPath).Length > 128) throw new InvalidDataException("团队工作台配置无效。");
        string secret = File.ReadAllText(secretPath).Trim();
        if (!Hex(secret)) throw new InvalidDataException("团队工作台配置无效。");
        var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false };
        handler.ServerCertificateCustomValidationCallback = (request, certificate, _, _) =>
            request.RequestUri is {} uri && SameAuthority(uri, _controlBase) &&
            certificate is not null && DateTime.UtcNow >= certificate.NotBefore.ToUniversalTime() &&
            DateTime.UtcNow <= certificate.NotAfter.ToUniversalTime() &&
            string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), config.CertificateSha256, StringComparison.OrdinalIgnoreCase);
        _http = new HttpClient(handler) { BaseAddress = _controlBase, Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        _local = Path.Combine(AppDataPaths.DataDirectory, "PlanningTeamShare", config.DestinationId.ToString("N"));
    }

    private static bool Hex(string? value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
    private static Uri Base(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" &&
        uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : throw new InvalidDataException("团队工作台地址无效。");
    private static bool SameAuthority(Uri a, Uri b) => a.Scheme == b.Scheme && a.IdnHost == b.IdnHost && a.Port == b.Port && a.UserInfo.Length == 0;

    internal static PlanningTeamShareClient? TryCreate()
    {
        string? path = Environment.GetEnvironmentVariable("GUANGJING_TEAM_SHARE_CONFIG");
        if (!Path.IsPathFullyQualified(path ?? "") || Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME") != "1") return null;
        try
        {
            string full = Path.GetFullPath(path!);
            string folder = Path.GetDirectoryName(full)!;
            if (!File.Exists(full) || new FileInfo(full).Length > 8_192) return null;
            PlanningImageStorage.RejectLinks(folder, full);
            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(full), Json);
            return config is null ? null : new(config, folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        { return null; }
    }

    private string BindingPath(Guid projectId, Guid documentId) => Path.Combine(_local, $"{projectId:N}-{documentId:N}.json");
    internal PlanningTeamShareBinding? ReadBinding(Guid projectId, Guid documentId)
    {
        lock (BindingGate)
        {
            string path = BindingPath(projectId, documentId);
            try
            {
                if (!File.Exists(path)) return null;
                PlanningImageStorage.RejectLinks(AppDataPaths.Root, path);
                if (new FileInfo(path).Length > 8_192) return null;
                var binding = JsonSerializer.Deserialize<PlanningTeamShareBinding>(File.ReadAllText(path), Json);
                return binding is { } b && b.DestinationId == _config.DestinationId && b.ProjectId == projectId && b.DocumentId == documentId &&
                    b.PublicationId != Guid.Empty && Hex(b.PublishedFingerprint) ? b : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { return null; }
        }
    }

    internal void AcceptReceipt(PlanningTeamShareReceipt receipt, string? fallbackFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.State != "ready") return;
        Check(receipt, receipt.ProjectId, receipt.DocumentId);
        var actual = receipt.PublishedFingerprint ?? fallbackFingerprint;
        if (!Hex(actual)) throw new InvalidDataException("无法核对团队发布基线，原绑定保留。");
        lock (BindingGate)
        {
            var prior = ReadBinding(receipt.ProjectId, receipt.DocumentId);
            if (prior != null && (prior.PublicationId != receipt.PublicationId ||
                !string.Equals(prior.PublishedFingerprint, actual, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("团队项目身份或发布基线已改变，原绑定保留。");
            if (prior != null) return;
            var path = BindingPath(receipt.ProjectId, receipt.DocumentId);
            PlanningImageStorage.RejectLinks(AppDataPaths.Root, path);
            Directory.CreateDirectory(_local);
            PlanningImageStorage.RejectLinks(AppDataPaths.Root, path);
            var tmp = path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, new PlanningTeamShareBinding(_config.DestinationId, receipt.ProjectId,
                        receipt.DocumentId, receipt.PublicationId!.Value, actual!), Json);
                    stream.Flush(true);
                }
                File.Move(tmp, path, overwrite: true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }

    internal async Task<PlanningTeamShareReceipt> GetStatusAsync(Guid projectId, Guid documentId, CancellationToken token = default)
    {
        var receipt = await ReadReceiptAsync("api/share/status", new { _config.DestinationId, projectId, documentId }, token);
        return Check(receipt, projectId, documentId);
    }

    internal async Task<PlanningTeamShareReceipt> PublishAsync(PlanningTeamSharePrepared prepared, CancellationToken token = default)
    {
        if (prepared.Issues.Any(issue => issue.Blocking)) throw new InvalidOperationException("请先补齐策划发布所需内容。");
        var projectId = prepared.ProjectId;
        var documentId = prepared.DocumentId;
        if (!Hex(prepared.Fingerprint) || projectId == Guid.Empty || documentId == Guid.Empty)
            throw new InvalidDataException("发布清单未绑定已保存的策划。");
        var files = prepared.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var manifest = files.Values.Select(x => new PlanningTeamShareManifestFile(x.RelativePath, x.Length, x.Sha256)).ToArray();
        var staged = Check(await ReadReceiptAsync("api/share/begin",
            new { _config.DestinationId, projectId, documentId, sourceFingerprint = prepared.Fingerprint, files = manifest }, token), projectId, documentId);
        if (staged.State == "ready") return staged;
        if (staged is not { State: "partial", PublicationId: { } publication, Missing: { } missing } || !Hex(staged.ManifestSha256))
            throw new InvalidDataException("团队工作台未返回可继续的发布记录。");
        if (missing.Any(m => !files.TryGetValue(m.Path, out var f) || f.Length != m.Length || !string.Equals(f.Sha256, m.Sha256, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("工作台的待补清单与当前策划不符，请重试。");
        foreach (var required in missing.DistinctBy(m => m.Sha256, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var file = files[required.Path];
            using var content = new StreamContent(new FileStream(file.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan));
            content.Headers.ContentLength = file.Length;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var response = await _http.PutAsync($"api/share/{publication:N}/files/{required.Sha256}", content, token);
            await RequireSuccessAsync(response, token);
        }
        return Check(await ReadReceiptAsync($"api/share/{publication:N}/seal",
            new { _config.DestinationId, projectId, documentId, manifestSha256 = staged.ManifestSha256 }, token), projectId, documentId);
    }

    private PlanningTeamShareReceipt Check(PlanningTeamShareReceipt receipt, Guid projectId, Guid documentId)
    {
        if (projectId == Guid.Empty || documentId == Guid.Empty || receipt.DestinationId != _config.DestinationId ||
            receipt.ProjectId != projectId || receipt.DocumentId != documentId || receipt.State is not ("ready" or "partial" or "notPublished") ||
            (receipt.State != "notPublished" && (receipt.PublicationId is null || receipt.PublicationId == Guid.Empty)))
            throw new InvalidDataException("团队工作台回执身份与当前策划不符。");
        if (receipt.State == "ready")
        {
            if (!Hex(receipt.PublishedFingerprint) || (receipt.Missing?.Length ?? 0) != 0)
                throw new InvalidDataException("团队项目尚未核对完成。");
            _ = OwnerUri(receipt);
            var member = RoomUri(receipt.MemberUrl, _memberBase, receipt.PublicationId!.Value);
            if (member.Fragment.Length != 0) throw new InvalidDataException("成员入口不应包含负责人身份。");
        }
        return receipt;
    }

    private static Uri RoomUri(string? text, Uri origin, Guid id)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !SameAuthority(uri, origin) || uri.Query.Length != 0 ||
            !string.Equals(uri.AbsolutePath, $"/r/{id:N}/workbench.html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("团队工作台返回的入口不属于当前策划。");
        return uri;
    }
    private Uri OwnerUri(PlanningTeamShareReceipt receipt)
    {
        var uri = RoomUri(receipt.OwnerUrl, _controlBase, receipt.PublicationId!.Value);
        if (!uri.Fragment.StartsWith("#code=", StringComparison.Ordinal) || !uri.Fragment.EndsWith("&route=overview", StringComparison.Ordinal))
            throw new InvalidDataException("团队工作台未签发负责人进入凭据，请重试。");
        return uri;
    }

    private async Task<PlanningTeamShareReceipt> ReadReceiptAsync<T>(string path, T message, CancellationToken token)
    {
        using var response = await _http.PostAsJsonAsync(path, message, Json, token);
        await RequireSuccessAsync(response, token);
        return await response.Content.ReadFromJsonAsync<PlanningTeamShareReceipt>(Json, token) ??
            throw new InvalidDataException("团队工作台返回空回执。");
    }
    private static async Task RequireSuccessAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        string? text = null;
        if ((response.Content.Headers.ContentLength ?? 0) <= 8_192)
        {
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
                if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String && error.GetString() is { Length: <= 280 } detail)
                    text = detail;
            }
            catch (JsonException) { }
        }
        throw new HttpRequestException(text ?? $"团队服务暂未完成请求（{(int)response.StatusCode}），可重试。", null, response.StatusCode);
    }

    /// <summary>Read only persisted content; saving an identical snapshot may increment outer revision/timestamps.</summary>
    internal static async Task<string> ReadLocalFingerprintAsync(Guid projectId, string planningRoot, string shotRoot, CancellationToken token = default)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        async Task Add(string root, string name, bool required, bool shots)
        {
            var path = Path.Combine(root, name);
            PlanningImageStorage.RejectLinks(root, path);
            digest.AppendData(Encoding.UTF8.GetBytes(name + "\n"));
            if (!File.Exists(path) && !required) { digest.AppendData("null\n"u8); return; }
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                void Write(JsonElement node, int depth, bool shotItem = false, bool planningDocument = false)
                {
                    switch (node.ValueKind)
                    {
                        case JsonValueKind.Object:
                            writer.WriteStartObject();
                            foreach (var property in node.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                            {
                                if ((depth == 0 && property.Name is "Revision" or "UpdatedAt") ||
                                    (shotItem && property.Name == "UpdatedAt") || (planningDocument && property.Name == "UiLayout")) continue;
                                writer.WritePropertyName(property.Name);
                                Write(property.Value, depth + 1, shots && depth == 0 && property.Name == "Shots",
                                    !shots && depth == 0 && property.Name == "Document");
                            }
                            writer.WriteEndObject();
                            break;
                        case JsonValueKind.Array:
                            writer.WriteStartArray();
                            foreach (var item in node.EnumerateArray()) Write(item, depth + 1, shotItem);
                            writer.WriteEndArray();
                            break;
                        default: node.WriteTo(writer); break;
                    }
                }
                Write(doc.RootElement, 0);
            }
            digest.AppendData(output.GetBuffer().AsSpan(0, (int)output.Length));
            digest.AppendData("\n"u8);
        }
        await Add(planningRoot, $"{projectId:N}.planning.json", true, false);
        await Add(shotRoot, $"{projectId:N}.shots.json", false, true);
        return Convert.ToHexString(digest.GetHashAndReset());
    }

    internal async Task OpenOwnerAsync(PlanningTeamShareReceipt receipt, Window? desktop)
    {
        Check(receipt, receipt.ProjectId, receipt.DocumentId);
        if (_ownerWindow is { IsLoaded: true } window && _openPublication == receipt.PublicationId)
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate(); // Same live room: preserve unsaved web editor input.
            return;
        }
        // Never discard unsaved online edits when the owner opens a different project.
        var web = new WebView2();
        var owner = new Window { Title = "光镜 · 团队策划", Width = 1460, Height = 900, MinWidth = 760, MinHeight = 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = desktop, Content = web };
        _ownerWindow = owner; _openPublication = receipt.PublicationId;
        owner.Closed += (_, _) => { if (ReferenceEquals(_ownerWindow, owner)) { _ownerWindow = null; _openPublication = null; } web.Dispose(); };
        try
        {
            var profile = Path.Combine(_local, "owner-browser", receipt.PublicationId!.Value.ToString("N"));
            PlanningImageStorage.RejectLinks(AppDataPaths.Root, profile);
            owner.Show();
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            if (!owner.IsLoaded) return;
            await web.EnsureCoreWebView2Async(environment);
            if (!owner.IsLoaded) return;
            var browser = web.CoreWebView2;
            string prefix = $"/r/{receipt.PublicationId!.Value:N}/";
            bool Match(string url) => Uri.TryCreate(url, UriKind.Absolute, out var target) &&
                SameAuthority(target, _controlBase) && target.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            browser.Settings.AreDevToolsEnabled = false;
            browser.Settings.IsWebMessageEnabled = false;
            browser.NavigationStarting += (_, eventArgs) => { if (!Match(eventArgs.Uri)) eventArgs.Cancel = true; };
            browser.NewWindowRequested += (_, eventArgs) => eventArgs.Handled = true;
            browser.ServerCertificateErrorDetected += (_, eventArgs) =>
            {
                using var cert = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(eventArgs.ServerCertificate.ToPemEncoding());
                eventArgs.Action = Match(eventArgs.RequestUri) && DateTime.UtcNow >= cert.NotBefore.ToUniversalTime() &&
                    DateTime.UtcNow <= cert.NotAfter.ToUniversalTime() && string.Equals(cert.GetCertHashString(HashAlgorithmName.SHA256),
                        _config.CertificateSha256, StringComparison.OrdinalIgnoreCase)
                    ? CoreWebView2ServerCertificateErrorAction.AlwaysAllow : CoreWebView2ServerCertificateErrorAction.Cancel;
            };
            // Environment and profile creation may outlast the ticket in the publication receipt.
            // Get one immediately before navigating; it must still name the same ready publication.
            var fresh = await GetStatusAsync(receipt.ProjectId, receipt.DocumentId);
            if (!owner.IsLoaded) return;
            if (fresh.State != "ready" || fresh.PublicationId != receipt.PublicationId ||
                !string.Equals(fresh.PublishedFingerprint, receipt.PublishedFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("团队工作台的发布身份已改变，请重试。");
            web.Source = OwnerUri(fresh);
        }
        catch { if (owner.IsLoaded) owner.Close(); throw; }
    }
}
#endif
