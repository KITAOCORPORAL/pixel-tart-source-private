using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using RAWSelectionAssistant.Core.Services.Projects;
using static PlanningReview.Rules;

namespace PlanningReview;

internal sealed record ShareIdentity(Guid DestinationId, Guid ProjectId, Guid DocumentId);
internal sealed record ShareFile(string Path, long Length, string Sha256);
internal sealed record ShareBegin(Guid DestinationId, Guid ProjectId, Guid DocumentId, string SourceFingerprint, ShareFile[] Files);
internal sealed record ShareSeal(Guid DestinationId, Guid ProjectId, Guid DocumentId, string? ManifestSha256);
internal sealed record ShareManifest(Guid PublicationId, Guid DestinationId, Guid ProjectId, Guid DocumentId,
    string SourceFingerprint, string ManifestSha256, ShareFile[] Files);
internal sealed record ShareReceipt(Guid DestinationId, Guid ProjectId, Guid DocumentId, Guid? PublicationId,
    string State, ShareFile[] Missing, string? ManifestSha256 = null, string? PublishedFingerprint = null,
    string? OwnerUrl = null, string? MemberUrl = null);

internal sealed class ShareRegistry
{
    const int MaxFiles = 2048;
    internal const long MaximumFileBytes = 64L * 1024 * 1024;
    const long MaxTotalBytes = 1024L * 1024 * 1024;
    internal const string MarkerFile = "share-candidate.json";
    static readonly string[] PublicRoomFiles = ["/", "/workbench.html", "/workbench.js", "/workbench.css", "/workbench-editors.js", "/workbench-editors.css"];
    static readonly string[] RoomApis = ["/api/join", "/api/state", "/api/presence", "/api/op", "/api/workbench/docx", "/api/export"];
    const string ImageRoute = "/api/image/";
    static readonly object RoomSlot = new();
    static readonly object EntryUrlSlot = new();
    readonly string storage;
    readonly byte[] secretHash;
    readonly Guid destination;
    readonly Uri publicBase;
    readonly int listenPort;
    readonly object catalogGate = new();
    readonly ConcurrentDictionary<Guid, Entry> byId = new();
    readonly Dictionary<(Guid Project, Guid Document), Entry> bySource = [];

    public ShareRegistry(string root, Guid destination, string publicBase, string secretFile, int listenPort)
    {
        if (destination == Guid.Empty || !Uri.TryCreate(publicBase, UriKind.Absolute, out var address) ||
            address.Scheme != Uri.UriSchemeHttps || address.AbsolutePath != "/" || address.Query.Length != 0 ||
            address.Fragment.Length != 0 || address.UserInfo.Length != 0 || address.Port != listenPort ||
            address.HostNameType == UriHostNameType.Unknown)
            throw new InvalidDataException("Share candidate requires its own destination ID and HTTPS origin on the listen port.");
        if (File.Exists(Path.Combine(root, "notebook.json")) || Directory.Exists(Path.Combine(root, "isolated")) ||
            File.Exists(Path.Combine(root, "identity.key")) || File.Exists(Path.Combine(root, "owner.local")))
            throw new InvalidDataException("Use a dedicated candidate data directory. Existing review data is unchanged.");
        this.destination = destination;
        this.publicBase = address;
        this.listenPort = listenPort;
        var controlSecret = File.ReadAllText(secretFile).Trim();
        if (controlSecret.Length != 64 || !controlSecret.All(Uri.IsHexDigit))
            throw new InvalidDataException("Share control secret file must contain 32 random bytes encoded as 64 hexadecimal characters.");
        secretHash = SHA256.HashData(Encoding.ASCII.GetBytes(controlSecret));
        var marker = Path.Combine(root, MarkerFile);
        var identity = new CandidateIdentity(1, destination, address.AbsoluteUri);
        PlanningImageStorage.RejectLinks(root, marker);
        storage = Path.Combine(root, "publications");
        if (File.Exists(marker))
        {
            if (Read<CandidateIdentity>(marker) != identity)
                throw new InvalidDataException("Share candidate destination cannot change for existing data.");
        }
        else
        {
            if (Directory.Exists(storage)) throw new InvalidDataException("Share candidate marker is missing; preserving existing data.");
            NativePlan.Atomic(marker, JsonSerializer.SerializeToUtf8Bytes(identity, Json));
        }
        Directory.CreateDirectory(storage);
        PlanningImageStorage.RejectLinks(root, storage);
        foreach (var dir in Directory.EnumerateDirectories(storage))
        {
            if (!Guid.TryParseExact(Path.GetFileName(dir), "N", out var id)) continue;
            PlanningImageStorage.RejectLinks(storage, dir);
            var path = Path.Combine(dir, "manifest.json");
            var draft = new Entry(dir);
            if (!File.Exists(path))
            {
                if (draft.Ready) throw new InvalidDataException("A ready share has lost its manifest; preserving existing data.");
                continue; // Process stopped between allocating a directory and writing its manifest.
            }
            PlanningImageStorage.RejectLinks(storage, path);
            var saved = Read<ShareManifest>(path);
            var validated = Manifest(new(saved.DestinationId, saved.ProjectId, saved.DocumentId, saved.SourceFingerprint, saved.Files), id);
            if (validated.ManifestSha256 != saved.ManifestSha256 || saved.PublicationId != id ||
                !byId.TryAdd(id, draft) || !bySource.TryAdd((saved.ProjectId, saved.DocumentId), draft))
                throw new InvalidDataException("Share binding is inconsistent; preserving existing data.");
            draft.Manifest = validated;
        }
    }

    sealed record CandidateIdentity(int Schema, Guid DestinationId, string PublicBase);
    sealed class Entry(string path)
    {
        public string Path { get; } = path;
        public string ReadyPath => System.IO.Path.Combine(Path, "ready");
        public string Blobs => System.IO.Path.Combine(Path, "blobs");
        public string BlobPath(string digest) => System.IO.Path.Combine(Blobs, digest);
        public bool Ready => Directory.Exists(ReadyPath);
        public ShareManifest Manifest { get; set; } = null!;
        public readonly SemaphoreSlim Gate = new(1, 1);
        public Room? Room { get; set; }
        public void Save(ShareManifest value)
        {
            NativePlan.Atomic(System.IO.Path.Combine(Path, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(value, Json));
            Manifest = value;
        }
    }

    static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)
        ?? throw new InvalidDataException("Share metadata is empty.");

    public Guid DestinationId => destination;
    internal static bool IsLoopback(HttpContext ctx) => ctx.Connection.RemoteIpAddress is {} remote && IPAddress.IsLoopback(remote) &&
        ctx.Connection.LocalIpAddress is {} local && IPAddress.IsLoopback(local);
    bool Control(HttpContext ctx, string? token) => IsLoopback(ctx) && !ctx.Request.Headers.ContainsKey("Origin") &&
        token is { Length: 64 } && token.All(Uri.IsHexDigit) &&
        CryptographicOperations.FixedTimeEquals(secretHash, SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    public static Room? RequestRoom(HttpContext ctx) => ctx.Items.TryGetValue(RoomSlot, out var item) ? (Room)item! : null;
    public static string? RequestMemberUrl(HttpContext ctx) => ctx.Items.TryGetValue(EntryUrlSlot, out var item) ? (string)item! : null;
    public async Task Scope(HttpContext ctx, RequestDelegate next, string? token)
    {
        var request = ctx.Request;
        var host = request.Host;
        var localHost = string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
        Need((host.Port ?? 443) == listenPort && (localHost || string.Equals(host.Host, publicBase.IdnHost, StringComparison.OrdinalIgnoreCase)),
            "此地址未配置为策划共享入口。", 400);
        if (request.Path.StartsWithSegments("/api/share", StringComparison.OrdinalIgnoreCase))
        {
            Need(Control(ctx, token), "仅本机获授权的发布端可以使用此入口。", 403);
            await next(ctx);
            return;
        }
        if (request.Path == "/health") { await next(ctx); return; }
        var segments = request.Path.Value?.Split('/', 4);
        Need(segments is { Length: >= 3 } && segments[1] == "r" && Guid.TryParseExact(segments[2], "N", out _),
            "策划入口不存在。", 404);
        var id = Guid.ParseExact(segments![2], "N");
        Need(byId.TryGetValue(id, out var entry) && entry.Ready, "策划入口不存在。", 404);
        var remaining = segments.Length == 4 ? "/" + segments[3] : "/";
        Need(PublicRoomFiles.Contains(remaining, StringComparer.Ordinal) || RoomApis.Contains(remaining, StringComparer.Ordinal) ||
            (remaining.StartsWith(ImageRoute, StringComparison.Ordinal) && Guid.TryParseExact(remaining.AsSpan(ImageRoute.Length), "D", out _)),
            "策划入口不存在。", 404);
        var room = await GetRoom(entry!);
        Need(IsLoopback(ctx) || !room.IsOwnerCredential(token), "负责人入口仅可在发布电脑上打开。", 403);
        ctx.Items[RoomSlot] = room;
        ctx.Items[EntryUrlSlot] = MemberUrl(id);
        request.PathBase = request.PathBase.Add(new PathString("/r/" + segments[2]));
        request.Path = remaining;
        await next(ctx);
    }

    public void Map(WebApplication app)
    {
        var control = app.MapGroup("/api/share").AddEndpointFilter((call, next) =>
        {
            var ctx = call.HttpContext;
            var authorization = ctx.Request.Headers.Authorization.ToString();
            var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : null;
            Need(!ctx.Request.PathBase.HasValue && Control(ctx, token), "仅本机获授权的发布端可以使用此入口。", 403);
            return next(call);
        });
        control.MapPost("/begin", async (HttpContext ctx, CancellationToken _) => await Begin(await Body<ShareBegin>(ctx)));
        control.MapPost("/status", async (HttpContext ctx, CancellationToken _) => await Status(await Body<ShareIdentity>(ctx)));
        control.MapPut("/{id:guid}/files/{sha256}", async (HttpContext ctx, Guid id, string sha256) =>
        {
            var limit = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = MaximumFileBytes;
            await Upload(id, sha256, ctx.Request);
            return Results.NoContent();
        });
        control.MapPost("/{id:guid}/seal", async (HttpContext ctx, Guid id) => await Seal(id, await Body<ShareSeal>(ctx)));
    }

    static async Task<T> Body<T>(HttpContext ctx) => await JsonSerializer.DeserializeAsync<T>(ctx.Request.Body, Json, ctx.RequestAborted)
        ?? throw new Problem(400, "缺少发布请求正文。");
    void Identity(Guid target, Guid project, Guid document)
    {
        Need(target == destination, "策划共享目的地身份不匹配。", 409);
        Need(project != Guid.Empty && document != Guid.Empty, "必须提供真实项目和策划文档身份。");
    }
    static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    ShareManifest Manifest(ShareBegin request, Guid id)
    {
        Identity(request.DestinationId, request.ProjectId, request.DocumentId);
        Need(Digest(request.SourceFingerprint), "缺少有效的已保存策划指纹。");
        Need(request.Files is { Length: > 0 and <= MaxFiles }, "策划共享文件清单为空或超过上限。");
        var files = new Dictionary<string, ShareFile>(StringComparer.OrdinalIgnoreCase);
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        long sum = 0;
        foreach (var file in request.Files!)
        {
            Need(file != null && SharePackage.ValidPath(file.Path, request.ProjectId, request.DocumentId) &&
                file.Length is > 0 and <= MaximumFileBytes && Digest(file.Sha256), "策划共享文件路径、大小或摘要无效。");
            var canonical = file! with { Sha256 = file!.Sha256.ToUpperInvariant() };
            Need(files.TryAdd(canonical.Path, canonical), "策划共享文件路径重复。");
            Need(sizes.TryAdd(canonical.Sha256, canonical.Length) || sizes[canonical.Sha256] == canonical.Length, "同一摘要对应不同的文件大小。");
            sum += canonical.Length;
            Need(sum <= MaxTotalBytes, "策划共享大小超过 1 GiB 上限。");
        }
        Need(files.ContainsKey(SharePackage.Plan(request.ProjectId)), "缺少已保存的原生策划文件。");
        var ordered = files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
        var fingerprint = request.SourceFingerprint.ToUpperInvariant();
        var manifestHash = Hash(JsonSerializer.SerializeToUtf8Bytes(new { fingerprint, files = ordered }, Json));
        return new(id, destination, request.ProjectId, request.DocumentId, fingerprint, manifestHash, ordered);
    }

    async Task<ShareReceipt> Begin(ShareBegin request)
    {
        Identity(request.DestinationId, request.ProjectId, request.DocumentId);
        Entry entry;
        lock (catalogGate)
        {
            var key = (request.ProjectId, request.DocumentId);
            if (!bySource.TryGetValue(key, out entry!))
            {
                var id = Guid.NewGuid();
                var manifest = Manifest(request, id);
                var path = Path.Combine(storage, id.ToString("N"));
                Directory.CreateDirectory(path);
                entry = new Entry(path);
                entry.Save(manifest);
                byId[id] = entry;
                bySource.Add(key, entry);
            }
        }
        await entry.Gate.WaitAsync();
        try
        {
            if (!entry.Ready)
            {
                var next = Manifest(request, entry.Manifest.PublicationId);
                if (next.ManifestSha256 != entry.Manifest.ManifestSha256) { entry.Save(next); DropUnneededBlobs(entry, next.Files); }
            }
            return await Receipt(entry);
        }
        finally { entry.Gate.Release(); }
    }

    async Task<ShareReceipt> Status(ShareIdentity request)
    {
        Identity(request.DestinationId, request.ProjectId, request.DocumentId);
        Entry? entry;
        lock (catalogGate) bySource.TryGetValue((request.ProjectId, request.DocumentId), out entry);
        if (entry == null) return new(destination, request.ProjectId, request.DocumentId, null, "notPublished", []);
        await entry.Gate.WaitAsync();
        try { return await Receipt(entry); }
        finally { entry.Gate.Release(); }
    }

    Entry Find(Guid id)
    {
        Need(byId.TryGetValue(id, out var entry), "发布记录不存在。", 404);
        return entry!;
    }
    async Task Upload(Guid id, string digest, HttpRequest request)
    {
        Need(Digest(digest), "文件摘要无效。");
        digest = digest.ToUpperInvariant();
        var entry = Find(id);
        var cancellation = request.HttpContext.RequestAborted;
        await entry.Gate.WaitAsync(cancellation);
        try
        {
            if (entry.Ready) return;
            var file = entry.Manifest.Files.FirstOrDefault(f => f.Sha256 == digest) ?? throw new Problem(409, "文件不属于当前发布清单，请重试共享。");
            if (request.ContentLength is {} length) Need(length == file.Length, "文件大小与发布清单不符。");
            var path = entry.BlobPath(digest);
            var parent = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(parent);
            PlanningImageStorage.RejectLinks(entry.Path, path);
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Need(await CopyVerified(request.Body, tmp, file, cancellation), "文件字节或摘要与发布清单不符。");
                cancellation.ThrowIfCancellationRequested();
                File.Move(tmp, path, true);
            }
            finally { File.Delete(tmp); }
        }
        finally { entry.Gate.Release(); }
    }

    async Task<ShareReceipt> Seal(Guid id, ShareSeal request)
    {
        Identity(request.DestinationId, request.ProjectId, request.DocumentId);
        var entry = Find(id);
        await entry.Gate.WaitAsync();
        try
        {
            var m = entry.Manifest;
            Need(request.ProjectId == m.ProjectId && request.DocumentId == m.DocumentId, "发布记录与策划项目不匹配。", 409);
            if (entry.Ready) return await Receipt(entry);
            Need(Digest(request.ManifestSha256) && string.Equals(request.ManifestSha256, m.ManifestSha256, StringComparison.OrdinalIgnoreCase),
                "发布清单已变化，请重试当前已保存的策划。", 409);
            if (Missing(entry).Length > 0) return await Receipt(entry);
            var stage = Path.Combine(entry.Path, "build");
            RemoveBuild(entry, stage);
            Directory.CreateDirectory(stage);
            try
            {
                var isolated = Path.Combine(stage, "isolated");
                Directory.CreateDirectory(isolated);
                foreach (var file in m.Files)
                {
                    var source = entry.BlobPath(file.Sha256);
                    PlanningImageStorage.RejectLinks(entry.Path, source);
                    var material = Path.Combine(isolated, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(material)!);
                    bool valid;
                    using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                        valid = await CopyVerified(input, material, file, CancellationToken.None);
                    if (!valid)
                    {
                        File.Delete(source);
                        return await Receipt(entry);
                    }
                }
                try
                {
                    await SharePackage.Validate(isolated, m);
                    await Room.Load(stage, m.ProjectId, workbench: true, publishedDocument: m.DocumentId);
                }
                catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException or InvalidOperationException or NullReferenceException or KeyNotFoundException or Problem)
                {
                    throw new Problem(422, "未能从这份已保存策划建立在线工作台。核对项目、正文和资源后重试。");
                }
                Directory.Move(stage, entry.ReadyPath); // Only this rename makes the room accessible. Never replaces a ready room.
            }
            finally { RemoveBuild(entry, stage); }
            DropUnneededBlobs(entry, []);
            return await Receipt(entry);
        }
        finally { entry.Gate.Release(); }
    }

    static void RemoveBuild(Entry entry, string stage)
    {
        if (!Directory.Exists(stage)) return;
        var expected = Path.GetFullPath(Path.Combine(entry.Path, "build"));
        if (!Path.GetFullPath(stage).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected share build path.");
        PlanningImageStorage.RejectLinks(entry.Path, stage);
        Directory.Delete(stage, recursive: true);
    }
    static void DropUnneededBlobs(Entry entry, ShareFile[] files)
    {
        var keep = files.Select(f => f.Sha256).ToHashSet(StringComparer.Ordinal);
        var path = entry.Blobs;
        if (!Directory.Exists(path)) return;
        PlanningImageStorage.RejectLinks(entry.Path, path);
        // Post-commit housekeeping must not affect the durable publication receipt.
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
                if (!keep.Contains(Path.GetFileName(file))) File.Delete(file);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static async Task<bool> CopyVerified(Stream source, string path, ShareFile file, CancellationToken cancellation)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellation);
            if (read == 0) break;
            total += read;
            if (total > file.Length) return false;
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
        }
        if (total != file.Length || Convert.ToHexString(hash.GetHashAndReset()) != file.Sha256) return false;
        await output.FlushAsync(cancellation);
        output.Flush(true);
        return true;
    }
    static ShareFile[] Missing(Entry entry) => entry.Manifest.Files.Where(file =>
    {
        var path = entry.BlobPath(file.Sha256);
        PlanningImageStorage.RejectLinks(entry.Path, path);
        var info = new FileInfo(path);
        return !info.Exists || info.Length != file.Length;
    }).ToArray();
    string MemberUrl(Guid id) => new Uri(publicBase, $"r/{id:N}/workbench.html").AbsoluteUri;
    async Task<ShareReceipt> Receipt(Entry entry)
    {
        var m = entry.Manifest;
        if (!entry.Ready) return new(destination, m.ProjectId, m.DocumentId, m.PublicationId, "partial", Missing(entry), m.ManifestSha256);
        var room = await GetRoom(entry);
        var member = MemberUrl(m.PublicationId);
        var owner = $"https://127.0.0.1:{listenPort}/r/{m.PublicationId:N}/workbench.html#code={Uri.EscapeDataString(await room.IssueOwnerTicket())}&route=overview";
        return new(destination, m.ProjectId, m.DocumentId, m.PublicationId, "ready", [], m.ManifestSha256, m.SourceFingerprint, owner, member);
    }
    async Task<Room> GetRoom(Entry entry)
    {
        if (entry.Room is {} running) return running;
        // A distinct in-process gate ensures concurrent first requests share one Room/gate.
        var initialization = roomLoads.GetOrAdd(entry.Manifest.PublicationId, _ => new SemaphoreSlim(1, 1));
        await initialization.WaitAsync();
        try
        {
            PlanningImageStorage.RejectLinks(entry.Path, entry.ReadyPath);
            var m = entry.Manifest;
            return entry.Room ??= await Room.Load(entry.ReadyPath, m.ProjectId, workbench: true,
                publishedDocument: m.DocumentId, existingOnly: true);
        }
        finally { initialization.Release(); }
    }
    readonly ConcurrentDictionary<Guid, SemaphoreSlim> roomLoads = new();
}
