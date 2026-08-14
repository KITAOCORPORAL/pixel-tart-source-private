using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PixelTart.SelectionApi.Contracts;
using PixelTart.SelectionApi.Server;

namespace PixelTart.SelectionApi.Tests;

[TestClass]
public sealed class LocalDevSelectionApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task CreatePublishPublicPaginationAndVariants_RoundTrip()
    {
        await using var host = await ApiHost.CreateAsync();
        var created = await host.CreateProjectAsync();
        var first = await host.UploadAsync(created, Guid.NewGuid(), 3000, 1000);
        var second = await host.UploadAsync(created, Guid.NewGuid(), 1200, 800);

        Assert.IsGreaterThan(0, first.ThumbBytes);
        Assert.IsGreaterThan(0, first.PreviewBytes);
        Assert.IsGreaterThan(0, first.ProxyBytes);
        await host.PostAuthorizedAsync($"{SelectionApiRouteNames.Projects}/{created.Project.Id}/publish", created.DevAccessToken, null);
        var publicProject = await host.GetAuthorizedAsync<LocalDevPublicProjectResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}", created.DevAccessToken);
        Assert.AreEqual("Published", publicProject.Project.Status);
        var firstPage = await host.GetAuthorizedAsync<SelectionAssetPageResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/assets?limit=1", created.DevAccessToken);
        Assert.HasCount(1, firstPage.Items);
        Assert.IsNotNull(firstPage.NextCursor);
        var secondPage = await host.GetAuthorizedAsync<SelectionAssetPageResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/assets?limit=1&cursor={firstPage.NextCursor}", created.DevAccessToken);
        Assert.HasCount(1, secondPage.Items);
        Assert.AreNotEqual(first.SelectionAssetId, second.SelectionAssetId);

        Assert.DoesNotContain(created.DevAccessToken, firstPage.Items[0].ThumbUrl!);
        Assert.DoesNotContain(created.DevAccessToken, firstPage.Items[0].PreviewUrl!);
        var media = await host.PostAuthorizedAsync<LocalDevMediaSessionResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/media-session", created.DevAccessToken, null);
        var thumb = await host.GetMediaBytesAsync(firstPage.Items[0].ThumbUrl!, media.Token);
        var preview = await host.GetMediaBytesAsync(firstPage.Items[0].PreviewUrl!, media.Token);
        var queryMedia = await host.Client.GetAsync($"{firstPage.Items[0].ThumbUrl}?media_token={Uri.EscapeDataString(media.Token)}");
        Assert.AreEqual(HttpStatusCode.OK, queryMedia.StatusCode);
        var leakedMainToken = await host.Client.GetAsync($"{firstPage.Items[0].ThumbUrl}?media_token={Uri.EscapeDataString(created.DevAccessToken)}");
        Assert.AreEqual(HttpStatusCode.Unauthorized, leakedMainToken.StatusCode);
        var queryOnlyAdminAccess = await host.Client.GetAsync($"{SelectionApiRouteNames.Projects}/{created.Project.Id}?access_token={Uri.EscapeDataString(created.DevAccessToken)}");
        Assert.AreEqual(HttpStatusCode.Unauthorized, queryOnlyAdminAccess.StatusCode);
        Assert.AreEqual(480, LongEdge(thumb));
        Assert.AreEqual(1600, LongEdge(preview));
        AssertMetadataFree(thumb);
        AssertMetadataFree(preview);
    }

    [TestMethod]
    public async Task ChoiceFavoriteCommentConflictConfirmLockReopenAndResult_Work()
    {
        await using var host = await ApiHost.CreateAsync();
        var created = await host.CreateProjectAsync();
        var asset = await host.UploadAsync(created, Guid.NewGuid(), 1200, 800);
        await host.PostAuthorizedAsync($"{SelectionApiRouteNames.Projects}/{created.Project.Id}/publish", created.DevAccessToken, null);

        var choice = await host.PutAuthorizedAsync<SelectionMutationResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/choices/{asset.SelectionAssetId}", created.DevAccessToken,
            new SelectionChoiceRequest(true, false) { ExpectedSelectionVersion = 1, ExpectedRevision = 1, OperationId = "choice-1" });
        Assert.AreEqual(2, choice.Revision);
        var idempotent = await host.PutAuthorizedAsync<SelectionMutationResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/choices/{asset.SelectionAssetId}", created.DevAccessToken,
            new SelectionChoiceRequest(true, false) { ExpectedSelectionVersion = 1, ExpectedRevision = 1, OperationId = "choice-1" });
        Assert.AreEqual(choice.Revision, idempotent.Revision);

        var conflict = await host.PutAuthorizedRawAsync(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/favorites/{asset.SelectionAssetId}", created.DevAccessToken,
            new SelectionChoiceRequest(true, true) { ExpectedSelectionVersion = 1, ExpectedRevision = 1, OperationId = "stale" });
        Assert.AreEqual(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions);
        Assert.AreEqual(2, problem!.CurrentRevision);

        var favorite = await host.PutAuthorizedAsync<SelectionMutationResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/favorites/{asset.SelectionAssetId}", created.DevAccessToken,
            new SelectionChoiceRequest(true, true) { ExpectedSelectionVersion = 1, ExpectedRevision = 2, OperationId = "favorite-1" });
        var comment = await host.PutAuthorizedAsync<SelectionMutationResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/comments/{asset.SelectionAssetId}", created.DevAccessToken,
            new SelectionCommentRequest("保留这张") { ExpectedSelectionVersion = 1, ExpectedRevision = favorite.Revision, OperationId = "comment-1" });
        var confirmed = await host.PostAuthorizedAsync<FinalSelectionSnapshotResponse>(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/confirm", created.DevAccessToken,
            new ConfirmSelectionRequest(true, "confirm-1") { ExpectedSelectionVersion = 1, ExpectedRevision = comment.Revision });
        Assert.IsTrue(confirmed.IsLocked);
        Assert.HasCount(1, confirmed.Items);
        Assert.IsTrue(confirmed.Items[0].Selected);
        Assert.AreEqual("保留这张", confirmed.Items[0].CustomerNote);

        var locked = await host.PutAuthorizedRawAsync(
            $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/choices/{asset.SelectionAssetId}", created.DevAccessToken,
            new SelectionChoiceRequest(false, true) { ExpectedSelectionVersion = 1, ExpectedRevision = confirmed.Revision, OperationId = "locked" });
        Assert.AreEqual(HttpStatusCode.Conflict, locked.StatusCode);
        var reopened = await host.PostAuthorizedAsync<ReopenSelectionResponse>(
            $"{SelectionApiRouteNames.Projects}/{created.Project.Id}/reopen", created.DevAccessToken, null);
        Assert.AreEqual(2, reopened.SelectionVersion);
        Assert.IsFalse(reopened.IsLocked);
        var result = await host.GetAuthorizedAsync<FinalSelectionSnapshotResponse>(
            $"{SelectionApiRouteNames.Projects}/{created.Project.Id}/final-selection", created.DevAccessToken);
        Assert.AreEqual(1, result.SelectionVersion);
    }

    [TestMethod]
    public async Task InvalidTokenRawTraversalAndCrossProjectAssetId_AreRejected()
    {
        await using var host = await ApiHost.CreateAsync();
        var first = await host.CreateProjectAsync();
        var second = await host.CreateProjectAsync("项目二");
        var assetId = Guid.NewGuid();
        await host.UploadAsync(first, assetId, 900, 600);
        var unauthorized = await host.GetAuthorizedRawAsync($"{SelectionApiRouteNames.Projects}/{first.Project.Id}", "invalid");
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        var collision = await host.UploadRawAsync(second, assetId, 900, 600);
        Assert.AreEqual(HttpStatusCode.Conflict, collision.StatusCode);
        var storage = host.App.Services.GetRequiredService<ISelectionObjectStorage>();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => storage.PutImageAsync(first.Project.Id, Guid.NewGuid(), SelectionObjectVariant.Proxy, "capture.ARW", new MemoryStream([1])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.PutAsync("../escape.jpg", new MemoryStream([1])));
    }

    [TestMethod]
    public async Task LockedDeletePreservesObjectsAndRestartPersistsSnapshot()
    {
        using var root = new TestRoot();
        LocalDevCreateProjectResponse created;
        LocalDevAssetUploadResponse asset;
        await using (var host = await ApiHost.CreateAsync(root.Path))
        {
            created = await host.CreateProjectAsync();
            asset = await host.UploadAsync(created, Guid.NewGuid(), 900, 600);
            await host.PostAuthorizedAsync($"{SelectionApiRouteNames.Projects}/{created.Project.Id}/publish", created.DevAccessToken, null);
            var choice = await host.PutAuthorizedAsync<SelectionMutationResponse>(
                $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/choices/{asset.SelectionAssetId}", created.DevAccessToken,
                new SelectionChoiceRequest(true, false) { ExpectedSelectionVersion = 1, ExpectedRevision = 1, OperationId = "persist-choice" });
            await host.PostAuthorizedAsync<FinalSelectionSnapshotResponse>(
                $"{SelectionApiRouteNames.ClientProjects}/{created.Project.PublicId}/confirm", created.DevAccessToken,
                new ConfirmSelectionRequest(true, "persist-confirm") { ExpectedSelectionVersion = 1, ExpectedRevision = choice.Revision });
            var delete = await host.DeleteAuthorizedRawAsync(
                $"{SelectionApiRouteNames.Projects}/{created.Project.Id}/assets/{asset.SelectionAssetId}/cloud-copy", created.DevAccessToken);
            Assert.AreEqual(HttpStatusCode.Conflict, delete.StatusCode);
            Assert.HasCount(3, Directory.GetFiles(Path.Combine(root.Path, "Objects"), "*.jpg", SearchOption.AllDirectories));
        }
        await using (var restarted = await ApiHost.CreateAsync(root.Path))
        {
            var snapshot = await restarted.GetAuthorizedAsync<FinalSelectionSnapshotResponse>(
                $"{SelectionApiRouteNames.Projects}/{created.Project.Id}/final-selection", created.DevAccessToken);
            Assert.IsTrue(snapshot.IsLocked);
            Assert.IsTrue(snapshot.Items.Single().Selected);
        }
    }

    [TestMethod]
    public async Task LocalObjectStorageRejectsOverwriteUntilExplicitDelete()
    {
        await using var host = await ApiHost.CreateAsync();
        var created = await host.CreateProjectAsync();
        var assetId = Guid.NewGuid();
        await host.UploadAsync(created, assetId, 900, 600);
        var replacement = await host.UploadRawAsync(created, assetId, 900, 600);
        Assert.AreEqual(HttpStatusCode.Conflict, replacement.StatusCode);
        Assert.HasCount(3, Directory.GetFiles(Path.Combine(host.Root, "Objects"), "*.jpg", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task UnauthorizedAndConcurrentUpload_DoNotCreateOrMixObjects()
    {
        await using var host = await ApiHost.CreateAsync();
        var created = await host.CreateProjectAsync();
        var unauthorized = created with { DevAccessToken = "invalid" };
        var unauthorizedResponse = await host.UploadRawAsync(unauthorized, Guid.NewGuid(), 900, 600);
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.HasCount(0, Directory.GetFiles(Path.Combine(host.Root, "Objects"), "*.jpg", SearchOption.AllDirectories));

        var assetId = Guid.NewGuid();
        var pair = await Task.WhenAll(host.UploadRawAsync(created, assetId, 900, 600), host.UploadRawAsync(created, assetId, 1200, 800));
        Assert.AreEqual(1, pair.Count(response => response.IsSuccessStatusCode));
        Assert.AreEqual(1, pair.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.HasCount(3, Directory.GetFiles(Path.Combine(host.Root, "Objects"), "*.jpg", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task StandaloneKestrel_BindsLoopbackAndSurvivesRestart()
    {
        using var root = new TestRoot();
        var port = ReserveLoopbackPort();
        LocalDevCreateProjectResponse created;
        await using (var first = await KestrelHost.CreateAsync(root.Path, port))
        {
            var health = await first.Client.GetFromJsonAsync<JsonElement>("/health/ready");
            Assert.IsTrue(health.GetProperty("ready").GetBoolean());
            created = await first.CreateProjectAsync();
        }
        await using (var restarted = await KestrelHost.CreateAsync(root.Path, port))
        {
            var response = await restarted.Client.SendAsync(ApiHost.Authorized(HttpMethod.Get,
                $"{SelectionApiRouteNames.Projects}/{created.Project.Id}", created.DevAccessToken));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static int LongEdge(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        return Math.Max(frame.PixelWidth, frame.PixelHeight);
    }

    private static void AssertMetadataFree(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var metadata = frame.Metadata as BitmapMetadata;
        Assert.IsTrue(metadata is null || string.IsNullOrWhiteSpace(metadata.Comment));
    }

    private sealed class ApiHost : IAsyncDisposable
    {
        private ApiHost(WebApplication app, HttpClient client, TestRoot? ownedRoot, string root)
        {
            App = app;
            Client = client;
            _ownedRoot = ownedRoot;
            Root = root;
        }

        private readonly TestRoot? _ownedRoot;
        public WebApplication App { get; }
        public HttpClient Client { get; }
        public string Root { get; }

        public static async Task<ApiHost> CreateAsync(string? root = null)
        {
            var owned = root is null ? new TestRoot() : null;
            root ??= owned!.Path;
            var app = Program.Build([], builder =>
            {
                builder.Environment.EnvironmentName = "Testing";
                builder.Configuration["PixelTartSelection:Root"] = root;
                builder.WebHost.UseTestServer();
            });
            await app.Services.GetRequiredService<LocalDevSelectionRepository>().InitializeAsync();
            await app.StartAsync();
            return new ApiHost(app, app.GetTestClient(), owned, root);
        }

        public async Task<LocalDevCreateProjectResponse> CreateProjectAsync(string name = "本地开发项目")
        {
            var response = await Client.PostAsJsonAsync(SelectionApiRouteNames.Projects, new CreateSelectionProjectRequest(name, "测试客户", 1, null));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LocalDevCreateProjectResponse>(JsonOptions))!;
        }

        public async Task<LocalDevAssetUploadResponse> UploadAsync(LocalDevCreateProjectResponse created, Guid assetId, int width, int height)
        {
            var response = await UploadRawAsync(created, assetId, width, height);
            if (!response.IsSuccessStatusCode)
                throw new AssertFailedException($"Upload failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LocalDevAssetUploadResponse>(JsonOptions))!;
        }

        public Task<HttpResponseMessage> UploadRawAsync(LocalDevCreateProjectResponse created, Guid assetId, int width, int height)
        {
            using var image = CreateJpeg(width, height, "GPS/private/path");
            var request = Authorized(HttpMethod.Post, $"{SelectionApiRouteNames.Projects}/{created.Project.Id}/assets/{assetId}/proxy", created.DevAccessToken);
            request.Headers.Add("X-Original-File-Name", "IMG_0001.ARW");
            request.Headers.Add("X-Proxy-File-Name", "sanitized-proxy.jpg");
            request.Content = new ByteArrayContent(image.ToArray());
            request.Content.Headers.ContentType = new("image/jpeg");
            return Client.SendAsync(request);
        }

        public async Task<T> GetAuthorizedAsync<T>(string path, string token)
        {
            var response = await GetAuthorizedRawAsync(path, token);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
        }

        public Task<HttpResponseMessage> GetAuthorizedRawAsync(string path, string token) => Client.SendAsync(Authorized(HttpMethod.Get, path, token));
        public async Task<byte[]> GetAuthorizedBytesAsync(string path, string token)
        {
            var response = await GetAuthorizedRawAsync(path, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        public async Task<byte[]> GetMediaBytesAsync(string path, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-PixelTart-Media-Token", token);
            var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task PostAuthorizedAsync(string path, string token, object? body) =>
            (await SendJsonAsync(HttpMethod.Post, path, token, body)).EnsureSuccessStatusCode();
        public async Task<T> PostAuthorizedAsync<T>(string path, string token, object? body)
        {
            var response = await SendJsonAsync(HttpMethod.Post, path, token, body);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
        }
        public async Task<T> PutAuthorizedAsync<T>(string path, string token, object body)
        {
            var response = await PutAuthorizedRawAsync(path, token, body);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
        }
        public Task<HttpResponseMessage> PutAuthorizedRawAsync(string path, string token, object body) => SendJsonAsync(HttpMethod.Put, path, token, body);
        public Task<HttpResponseMessage> DeleteAuthorizedRawAsync(string path, string token) => Client.SendAsync(Authorized(HttpMethod.Delete, path, token));

        private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string token, object? body)
        {
            var request = Authorized(method, path, token);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            return Client.SendAsync(request);
        }

        internal static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-PixelTart-Dev-Token", token);
            return request;
        }

        private static MemoryStream CreateJpeg(int width, int height, string comment)
        {
            var pixels = new byte[checked(width * height * 3)];
            Array.Fill(pixels, (byte)124);
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, width * 3);
            var metadata = new BitmapMetadata("jpg") { Comment = comment };
            var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;
            return stream;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            _ownedRoot?.Dispose();
        }
    }

    private sealed class KestrelHost : IAsyncDisposable
    {
        private KestrelHost(WebApplication app, HttpClient client)
        {
            App = app;
            Client = client;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<KestrelHost> CreateAsync(string root, int port)
        {
            var app = Program.Build([], builder =>
            {
                builder.Environment.EnvironmentName = "Development";
                builder.Configuration["PixelTartSelection:Root"] = root;
                builder.Configuration["PixelTartSelection:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            });
            await app.Services.GetRequiredService<LocalDevSelectionRepository>().InitializeAsync();
            await app.StartAsync();
            return new KestrelHost(app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") });
        }

        public async Task<LocalDevCreateProjectResponse> CreateProjectAsync()
        {
            var response = await Client.PostAsJsonAsync(SelectionApiRouteNames.Projects,
                new CreateSelectionProjectRequest("Kestrel restart", "LocalDev", 1, null));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LocalDevCreateProjectResponse>(JsonOptions))!;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }

    private sealed class TestRoot : IDisposable
    {
        public TestRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.SelectionApi.LocalDevTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
