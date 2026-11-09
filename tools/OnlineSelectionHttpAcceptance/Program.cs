using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Services.OnlineSelection;
using RAWSelectionAssistant.ViewModels;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly List<object> Receipts = [];
    private static string Root = "";
    private static Uri Endpoint = null!;
    private static HttpClient Client = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        Root = Path.GetFullPath(args[1]); Endpoint = new Uri(args[2]);
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME", "1");
        Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT", Path.Combine(Root, "IsolatedApp"));
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exit = 1;
        dispatcher.InvokeAsync(async () =>
        {
            try
            {
                Client = new HttpClient { BaseAddress = Endpoint, Timeout = TimeSpan.FromSeconds(20) };
                if (args[0] == "roundtrip") await RoundTrip();
                else if (args[0] == "reopen") await Reopen();
                else if (args[0] == "seed") await Seed();
                else if (args[0] == "collect") await Collect();
                else if (args[0] == "phone-status") await PhoneStatus();
                else throw new ArgumentException("Expected roundtrip or reopen.");
                exit = 0;
                await File.WriteAllTextAsync(Path.Combine(Root, args[0] + "-receipt.json"), JsonSerializer.Serialize(new { status = "PASS", processId = Environment.ProcessId, steps = Receipts }, Json));
                Console.WriteLine(args[0] + " PASS (" + Receipts.Count + " checks)");
            }
            catch (Exception error)
            {
                // URLs and credential bodies are deliberately excluded from diagnostic receipts.
                await File.WriteAllTextAsync(Path.Combine(Root, args[0] + "-failure.txt"), error.GetType().Name + ": " + error.Message + "\n" + error.StackTrace);
                Console.Error.WriteLine(error.GetType().Name + ": " + error.Message);
            }
            finally { Client?.Dispose(); dispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run(); return exit;
    }

    private static void Check(bool valid, string name)
    {
        if (!valid) throw new InvalidOperationException(name);
        Receipts.Add(new { check = name, pass = true });
    }
    private static async Task<(HttpStatusCode Status, JsonElement Body)> Request(HttpMethod method, string path, string? token = null, object? body = null, int expected = 200)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Add("X-PixelTart-Dev-Token", token);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await Client.SendAsync(request);
        Receipts.Add(new { method = method.Method, path, status = (int)response.StatusCode });
        if ((int)response.StatusCode != expected) throw new InvalidOperationException($"{method} {path}: expected {expected}, received {(int)response.StatusCode}");
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.StartsWith('{') ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }
    private static async Task<SqliteProjectRepository> Repository()
    {
        var db = new PixelTartDatabase(AppDataPaths.DatabaseFile);
        var migration = await new DatabaseMigrator(db, new DatabaseBackupService(db, Path.Combine(Root, "backups"))).MigrateAsync();
        Check(migration.Success, "collection database migrated"); return new SqliteProjectRepository(db);
    }
    private static OnlineSelectionViewModel Workspace(HttpOnlineSelectionProvider provider, SqliteProjectRepository repository)
    {
        var vm = new OnlineSelectionViewModel(provider, new JsonSelectionWorkspaceStore(AppDataPaths.OnlineSelectionWorkspaceFile),
            new SelectionResultSyncService(new FileNameNormalizer()), new SelectionProxyJpegService(new WpfSelectionProxyRenderer(null!)), Path.Combine(AppDataPaths.OnlineSelectionDirectory, "Proxies"));
        vm.ProjectPage.WriteCollectionProjectAsync = new SelectionCollectionProjectWriter(repository).WriteAsync;
        return vm;
    }
    private static HttpOnlineSelectionProvider Provider() => new(Endpoint, Path.Combine(AppDataPaths.OnlineSelectionDirectory, "http-access.bin"));
    private static void Fixture(string path)
    {
        var width = 1400; var height = 900; var pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 3; pixels[i] = (byte)(x * 180 / width + 50); pixels[i + 1] = (byte)(y * 140 / height + 60); pixels[i + 2] = (byte)(100 + (x / 100 % 2) * 60);
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3);
        var metadata = new BitmapMetadata("jpg") { Comment = "Synthetic acceptance fixture, private source metadata must not be uploaded" };
        var encoder = new JpegBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
        using var output = File.Create(path); encoder.Save(output);
    }
    private static async Task RoundTrip()
    {
        var source = Path.Combine(Root, "IMG_0001.JPG"); Fixture(source); var sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var repository = await Repository();
        var sentinel = new PhotoProjectRecord { Name = "unrelated project", SelectionInputs = ["DO_NOT_CHANGE.JPG"] };
        await repository.UpsertAsync(sentinel);
        using var provider = Provider(); var vm = Workspace(provider, repository);
        vm.ProjectName = "在线选片 HTTP 首版验收"; vm.ClientName = "测试客户"; vm.TargetCountText = "1";
        await vm.CreateProjectAsync([source]);
        Check(vm.ProjectPage.Assets.Single().ProxyJpegPath is not null, "actual WPF proxy renderer produced a JPEG");
        await vm.ProjectPage.PublishAsync();
        Check(vm.ProjectPage.Project?.Status == SelectionProjectStatus.Published, "actual desktop publish command completed: " + vm.ProjectPage.StatusText);
        var project = vm.ProjectPage.Project!; var asset = vm.ProjectPage.Assets.Single().ToModel();
        Check(asset.CloudAssetId is not null && asset.ProjectId == project.Id, "upload queue persisted server acknowledgement");
        Check(provider.TryGetAccess(project.Id, out var access), "desktop owner credential persisted");
        var url = vm.ProjectPage.ClientUrl!; Check(!string.IsNullOrWhiteSpace(url), "server issued protected URL");
        var clientToken = new Uri(url).Fragment.Split("token=")[1]; Check(clientToken != access.DevAccessToken, "customer token differs from owner token");
        // Local-only handoff: not part of committed receipts.
        await File.WriteAllTextAsync(Path.Combine(Root, "PHONE_URL.txt"), url);
        var stem = "/v1/selection-projects/" + project.Id;
        var clientStem = "/v1/client/selection/" + project.PublicId;
        for (var i = 0; i < 2; i++)
        {
            var replay = await Request(HttpMethod.Post, "/v1/selection-projects", body: new { project.Name, project.ClientDisplayName, project.TargetCount, project.DeadlineUtc, projectId = project.Id, ownerToken = access.DevAccessToken }, expected: 201);
            Check(replay.Body.GetProperty("project").GetProperty("id").GetGuid() == project.Id, "create replay retains project identity #" + i);
        }
        using (var content = File.OpenRead(asset.ProxyJpegPath!))
        {
            var replay = await provider.UploadAssetAsync(project.Id, asset, content);
            Check(replay.Success && replay.Value!.Id == asset.Id, "upload replay retains asset identity");
        }
        await Request(HttpMethod.Get, "/s/" + project.PublicId);
        await Request(HttpMethod.Get, "/selection.js"); await Request(HttpMethod.Get, "/selection.css");
        await Request(HttpMethod.Get, clientStem, expected: 401);
        await Request(HttpMethod.Get, stem, clientToken, expected: 401);
        await Request(HttpMethod.Post, stem + "/unpublish", clientToken, expected: 401);
        var other = SelectionProjectFactory.CreateDraft("other", "other", 1);
        Check((await provider.CreateProjectAsync(other)).Success, "created isolated second project");
        await Request(HttpMethod.Get, "/v1/selection-projects/" + other.Id, access.DevAccessToken, expected: 401);
        var view = (await Request(HttpMethod.Get, clientStem, clientToken)).Body;
        var version = view.GetProperty("project").GetProperty("selectionVersion").GetInt32();
        var revision = view.GetProperty("project").GetProperty("revision").GetInt64();
        var gallery = (await Request(HttpMethod.Get, clientStem + "/assets", clientToken)).Body;
        Check(gallery.GetProperty("items").GetArrayLength() == 1, "retries created exactly one remote asset");
        var media = (await Request(HttpMethod.Post, clientStem + "/media-session", clientToken)).Body.GetProperty("token").GetString();
        using (var request = new HttpRequestMessage(HttpMethod.Get, clientStem + "/assets/" + asset.Id + "/preview"))
        {
            request.Headers.Add("X-PixelTart-Media-Token", media);
            using var response = await Client.SendAsync(request); Check(response.IsSuccessStatusCode, "real protected JPEG HTTP response 200");
            using var image = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
            var frame = BitmapDecoder.Create(image, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            Check((frame.Metadata as BitmapMetadata)?.Comment is null or "", "remote proxy strips private metadata");
        }
        var operation = new { selected = true, favorite = false, expectedSelectionVersion = version, expectedRevision = revision, operationId = Guid.NewGuid().ToString("N") };
        var choice = (await Request(HttpMethod.Put, clientStem + "/choices/" + asset.Id, clientToken, operation)).Body;
        var choiceReplay = (await Request(HttpMethod.Put, clientStem + "/choices/" + asset.Id, clientToken, operation)).Body;
        Check(choice.GetProperty("revision").GetInt64() == choiceReplay.GetProperty("revision").GetInt64(), "choice retry did not increment revision twice");
        var confirm = new { confirmed = true, confirmationNonce = Guid.NewGuid().ToString("N"), expectedSelectionVersion = version, expectedRevision = choice.GetProperty("revision").GetInt64() };
        var confirmed = (await Request(HttpMethod.Post, clientStem + "/confirm", clientToken, confirm)).Body;
        var confirmReplay = (await Request(HttpMethod.Post, clientStem + "/confirm", clientToken, confirm)).Body;
        Check(confirmed.GetProperty("confirmedAtUtc").GetDateTimeOffset() == confirmReplay.GetProperty("confirmedAtUtc").GetDateTimeOffset(), "confirm retry retained one snapshot");
        await Request(HttpMethod.Put, clientStem + "/choices/" + asset.Id, clientToken, new { selected = false, favorite = false, expectedSelectionVersion = version, expectedRevision = choice.GetProperty("revision").GetInt64(), operationId = Guid.NewGuid().ToString("N") }, expected: 409);
        await vm.ProjectPage.RefreshResultsAsync();
        Check(vm.ProjectPage.FinalResult?.Items.Single().Selected == true, "desktop refresh read actual customer confirmation");
        Check(vm.ProjectPage.FinalResult!.Revision == confirmed.GetProperty("revision").GetInt64(), "desktop persisted exact server confirmation revision");
        Check(vm.ProjectPage.ConfirmationReceiptText.Contains("服务器回执"), "PC presents server receipt to photographer");
        var collection = (await repository.ListAsync()).Single(item => item.Id == project.CollectionProjectId);
        Check(collection.SelectionInputs.SequenceEqual(new[] { asset.OriginalFileName }), "confirmed filenames written to bound collection project");
        Check((await repository.ListAsync()).Single(item => item.Id == sentinel.Id).SelectionInputs.SequenceEqual(new[] { "DO_NOT_CHANGE.JPG" }), "unrelated collection project unchanged");
        var archive = await new SelectionResultSyncService(new FileNameNormalizer()).SynchronizeAsync(vm.ProjectPage.FinalResult!, [source], Path.Combine(Root, "Archives"));
        Check(archive.ArchivePath is not null && File.Exists(archive.ArchivePath), "existing result sync service wrote real archive");
        var invalid = vm.ProjectPage.FinalResult! with { SelectionProjectId = other.Id };
        var refused = false; try { SelectionHttpWorkflow.ValidateFinal(project, [asset], invalid); } catch (InvalidDataException) { refused = true; }
        Check(refused, "wrong project result refused before writeback");
        await vm.ProjectPage.UnpublishAsync();
        Check(vm.ProjectPage.Project!.Status == SelectionProjectStatus.Draft, "desktop unpublish command acknowledged");
        await Request(HttpMethod.Get, clientStem, clientToken, expected: 404);
        await Request(HttpMethod.Get, "/s/" + project.PublicId, expected: 404);
        using (var request = new HttpRequestMessage(HttpMethod.Get, clientStem + "/assets/" + asset.Id + "/preview"))
        {
            request.Headers.Add("X-PixelTart-Media-Token", media); using var response = await Client.SendAsync(request);
            Check(!response.IsSuccessStatusCode, "revocation invalidated an already issued image session");
        }
        // Republish for persistence checks: revocation must remain permanent for the old URL.
        await vm.ProjectPage.PublishAsync();
        Check(vm.ProjectPage.Project!.Status == SelectionProjectStatus.ClientConfirmed, "republish retains confirmed lock");
        await Request(HttpMethod.Get, clientStem, clientToken, expected: 404);
        var afterBytes = await File.ReadAllBytesAsync(source);
        Check(sourceHash.SequenceEqual(SHA256.HashData(afterBytes)), "source file byte identity preserved");
        await File.WriteAllTextAsync(Path.Combine(Root, "identity.json"), JsonSerializer.Serialize(new { projectId = project.Id, collectionId = project.CollectionProjectId, assetId = asset.Id, sourceSha256 = Convert.ToHexString(sourceHash) }, Json));
    }
    private static async Task Seed()
    {
        var source = Path.Combine(Root, "PHONE_TEST.JPG"); if (!File.Exists(source)) Fixture(source);
        var repository = await Repository(); using var provider = Provider(); var vm = Workspace(provider, repository);
        await vm.RefreshAsync();
        if (vm.Projects.Count > 0) await vm.OpenProjectAsync(vm.Projects.Single());
        else
        {
            vm.ProjectName = "手机 Safari 联网选片测试"; vm.ClientName = "测试客户"; vm.TargetCountText = "1";
            await vm.CreateProjectAsync([source]);
        }
        await vm.ProjectPage.PublishAsync();
        Check(vm.ProjectPage.ClientUrl is not null, "unconfirmed protected phone gallery ready");
        await File.WriteAllTextAsync(Path.Combine(Root, "PHONE_URL.txt"), vm.ProjectPage.ClientUrl);
        var issuedUrl = new Uri(vm.ProjectPage.ClientUrl!);
        var clientToken = issuedUrl.Fragment.Split("token=")[1];
        using var phone = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var page = await phone.GetAsync(issuedUrl.GetLeftPart(UriPartial.Path));
        Check(page.IsSuccessStatusCode, "phone HTTPS HTML entry returned 200");
        using var api = new HttpRequestMessage(HttpMethod.Get, new Uri(issuedUrl, "/v1/client/selection/" + vm.ProjectPage.Project!.PublicId));
        api.Headers.Add("X-PixelTart-Dev-Token", clientToken);
        using var response = await phone.SendAsync(api);
        Check(response.IsSuccessStatusCode, "phone HTTPS authorized gallery returned 200");
        var publicView = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Check(!publicView.GetProperty("isLocked").GetBoolean(), "phone album is unconfirmed and editable");
        var clientStem = "/v1/client/selection/" + vm.ProjectPage.Project.PublicId;
        using var mediaRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(issuedUrl, clientStem + "/media-session"));
        mediaRequest.Headers.Add("X-PixelTart-Dev-Token", clientToken);
        using var mediaResponse = await phone.SendAsync(mediaRequest);
        mediaResponse.EnsureSuccessStatusCode();
        var mediaToken = JsonDocument.Parse(await mediaResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
        using var imageRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(issuedUrl, clientStem + "/assets/" + vm.ProjectPage.Assets.Single().Id + "/preview"));
        imageRequest.Headers.Add("X-PixelTart-Media-Token", mediaToken);
        using var imageResponse = await phone.SendAsync(imageRequest);
        Check(imageResponse.IsSuccessStatusCode, "phone HTTPS protected preview returned 200");
        using var imageBytes = new MemoryStream(await imageResponse.Content.ReadAsByteArrayAsync());
        var preview = BitmapDecoder.Create(imageBytes, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Check(preview.PixelWidth > 0 && preview.PixelHeight > 0, "phone HTTPS preview is an actual decodable JPEG");
        await File.WriteAllTextAsync(Path.Combine(Root, "identity.json"), JsonSerializer.Serialize(new { projectId = vm.ProjectPage.Project!.Id, collectionId = vm.ProjectPage.Project.CollectionProjectId, assetId = vm.ProjectPage.Assets.Single().Id }, Json));
    }

    private static async Task Collect()
    {
        using var provider = Provider(); var repository = await Repository(); var vm = Workspace(provider, repository);
        await vm.RefreshAsync(); await vm.OpenProjectAsync(vm.Projects.Single());
        await vm.ProjectPage.RefreshResultsAsync();
        Check(vm.ProjectPage.FinalResult?.Items.Single().Selected == true, "actual customer JavaScript result collected by desktop command");
        var project = (await repository.ListAsync()).Single(item => item.Id == vm.ProjectPage.Project!.CollectionProjectId);
        Check(project.SelectionInputs.SequenceEqual(new[] { "PHONE_TEST.JPG" }), "web client result persisted to bound collection project");
    }

    private static async Task PhoneStatus()
    {
        using var provider = Provider();
        var store = new JsonSelectionWorkspaceStore(AppDataPaths.OnlineSelectionWorkspaceFile);
        var snapshot = await store.LoadAsync(); var project = snapshot.Projects.Single();
        var progress = await provider.GetSelectionProgressAsync(project.Id);
        var final = await provider.GetFinalSelectionAsync(project.Id);
        Console.WriteLine(JsonSerializer.Serialize(new { projectId = project.Id, progress = progress.Value,
            confirmed = final.Success, serverReceipt = final.Value,
            deviceResult = "Awaiting user report: iPhone 13 Safari; server confirmation alone does not establish client device identity." }, Json));
    }

    private static async Task Reopen()
    {
        using var provider = Provider(); var repository = await Repository(); var vm = Workspace(provider, repository);
        await vm.RefreshAsync(); Check(vm.Projects.Count == 1, "new process restored one project from disk");
        await vm.OpenProjectAsync(vm.Projects.Single());
        Check(vm.ProjectPage.FinalResult?.Items.Single().Selected == true && vm.ProjectPage.Assets.Single().IsSelected, "new process restored confirmed result and checkbox state");
        Check(provider.GetClientUrl(vm.ProjectPage.Project!.Id) is not null, "new process decrypted persisted service access");
        Check(vm.ProjectPage.FinalResult!.Revision > 0 && vm.ProjectPage.ConfirmationReceiptText.Contains("服务器回执"), "new process restored exact server receipt revision");
        await vm.ProjectPage.RefreshResultsAsync();
        Check(vm.ProjectPage.StatusText.Contains("写入对应归片项目"), "new process re-fetched and wrote collection via actual command");
        var projects = await repository.ListAsync();
        Check(projects.Count == 2, "writeback retry did not duplicate collection project");
        var collection = projects.Single(item => item.Id == vm.ProjectPage.Project!.CollectionProjectId);
        Check(collection.SelectionInputs.SequenceEqual(new[] { "IMG_0001.JPG" }), "SQLite collection inputs survived new process");
    }
}
