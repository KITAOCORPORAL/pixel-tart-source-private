using System.Net;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.HttpResults;
using PixelTart.SelectionApi.Contracts;

namespace PixelTart.SelectionApi.Server;

public partial class Program
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AssetMutationLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, MediaSession> MediaSessions = new(StringComparer.Ordinal);
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configureBuilder?.Invoke(builder);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        var root = Path.GetFullPath(builder.Configuration["PixelTartSelection:Root"]
            ?? Environment.GetEnvironmentVariable("PIXELTART_SELECTION_LOCALDEV_ROOT")
            ?? Path.Combine(Path.GetTempPath(), "PixelTart_OnlineSelection_LocalDev_Preview", "Server"));
        var port = int.TryParse(builder.Configuration["PixelTartSelection:Port"]
            ?? Environment.GetEnvironmentVariable("PIXELTART_SELECTION_LOCALDEV_PORT"), out var configuredPort)
            ? configuredPort
            : 5127;
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Parse("127.0.0.1"), port));

        builder.Services.AddSingleton(new LocalDevSelectionRepository(Path.Combine(root, "Data", "selection-localdev.db")));
        builder.Services.AddSingleton<ISelectionObjectStorage>(new LocalSelectionObjectStorage(Path.Combine(root, "Objects")));
        builder.Services.AddSingleton<LocalDevJpegVariantService>();
        builder.Services.AddSingleton(new LocalDevFaultPolicy(builder.Environment.IsDevelopment()));

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (SelectionApiException exception)
            {
                context.Response.StatusCode = exception.StatusCode;
                await context.Response.WriteAsJsonAsync(new ApiProblem(exception.Code, exception.Message, context.TraceIdentifier)
                {
                    CurrentSelectionVersion = exception.CurrentSelectionVersion,
                    CurrentRevision = exception.CurrentRevision
                }).ConfigureAwait(false);
            }
        });
        app.UseMiddleware<LocalDevFaultMiddleware>();
        MapRoutes(app);
        return app;
    }

    public static async Task Main(string[] args)
    {
        var app = Build(args);
        await app.Services.GetRequiredService<LocalDevSelectionRepository>().InitializeAsync().ConfigureAwait(false);
        await app.RunAsync().ConfigureAwait(false);
    }

    private static void MapRoutes(WebApplication app)
    {
        app.MapGet("/health/ready", () => Results.Ok(new { ready = true, mode = "LocalDev", production = false }));

        app.MapPost(SelectionApiRouteNames.Projects, async (CreateSelectionProjectRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var created = await repository.CreateProjectAsync(request, token).ConfigureAwait(false);
            return Results.Created($"{SelectionApiRouteNames.Projects}/{created.Project.Id}",
                new LocalDevCreateProjectResponse(ToProjectResponse(created.Project), created.Token));
        });

        app.MapGet(SelectionApiRouteNames.Projects + "/{projectId:guid}", async (Guid projectId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
            Results.Ok(ToProjectResponse(await repository.GetProjectAsync(projectId, AccessToken(request), token).ConfigureAwait(false))));

        app.MapPut(SelectionApiRouteNames.ProjectRule, async (Guid projectId, UpsertSelectionRuleRequest rule, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
            Results.Ok(await repository.UpsertRuleAsync(projectId, AccessToken(request), rule, token).ConfigureAwait(false)));

        app.MapPost(SelectionApiRouteNames.ProjectAssetProxy, async (Guid projectId, Guid assetId, HttpRequest request, LocalDevSelectionRepository repository, ISelectionObjectStorage storage, LocalDevJpegVariantService jpegVariants, CancellationToken token) =>
        {
            var uploadGate = AssetMutationLocks.GetOrAdd($"{projectId:N}/{assetId:N}", static _ => new SemaphoreSlim(1, 1));
            await uploadGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
            var access = AccessToken(request);
            await repository.AuthorizeAssetUploadAsync(projectId, assetId, access, token).ConfigureAwait(false);
            if (!string.Equals(request.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                throw new SelectionApiException(415, "ProxyJpegRequired", "Only sanitized proxy JPEG content is accepted.");
            var originalName = Header(request, "X-Original-File-Name", "image.jpg");
            var proxyName = Header(request, "X-Proxy-File-Name", "proxy.jpg");
            var sourceAssetId = Guid.TryParse(Header(request, "X-Source-Asset-Id", string.Empty), out var source) ? source : (Guid?)null;
            var variants = await jpegVariants.CreateAsync(request.Body, token).ConfigureAwait(false);
            var writes = new Dictionary<SelectionObjectVariant, SelectionObjectWriteResult>();
            try
            {
                foreach (var item in new[]
                {
                    (SelectionObjectVariant.Thumb, variants.Thumb),
                    (SelectionObjectVariant.Preview, variants.Preview),
                    (SelectionObjectVariant.Proxy, variants.Proxy)
                })
                {
                    var objectKey = $"{projectId:N}/{assetId:N}/{item.Item1.ToString().ToLowerInvariant()}.jpg";
                    if (await storage.ExistsAsync(objectKey, token).ConfigureAwait(false))
                        throw new SelectionApiException(409, "AssetProxyAlreadyExists", "Delete the existing cloud copy before uploading a replacement.");
                    await using var image = new MemoryStream(item.Item2, writable: false);
                    writes[item.Item1] = await storage.PutImageAsync(projectId, assetId, item.Item1, proxyName, image, token).ConfigureAwait(false);
                }
                var asset = await repository.AddAssetAsync(projectId, access, assetId, sourceAssetId, originalName, variants.Proxy.LongLength,
                    writes[SelectionObjectVariant.Proxy].ObjectKey, writes[SelectionObjectVariant.Thumb].ObjectKey,
                    writes[SelectionObjectVariant.Preview].ObjectKey, token).ConfigureAwait(false);
                var updatedProject = await repository.GetProjectAsync(projectId, access, token).ConfigureAwait(false);
                return Results.Ok(new LocalDevAssetUploadResponse(asset.ProjectId, asset.SelectionAssetId, asset.SourceAssetId,
                    asset.OriginalFileName, asset.Status, writes[SelectionObjectVariant.Proxy].Bytes,
                    writes[SelectionObjectVariant.Thumb].Bytes, writes[SelectionObjectVariant.Preview].Bytes,
                    updatedProject.SelectionVersion, updatedProject.Revision));
            }
            catch
            {
                foreach (var write in writes.Values)
                    await storage.DeleteAsync(write.ObjectKey, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            }
            finally
            {
                uploadGate.Release();
            }
        });

        app.MapPost(SelectionApiRouteNames.ProjectPublish, async (Guid projectId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
            Results.Ok(await repository.PublishAsync(projectId, AccessToken(request), token).ConfigureAwait(false)));

        app.MapPost(SelectionApiRouteNames.ProjectUnpublish, async (Guid projectId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            await repository.UnpublishAsync(projectId, AccessToken(request), token).ConfigureAwait(false);
            return Results.NoContent();
        });

        app.MapPost(SelectionApiRouteNames.ProjectReopen, async (Guid projectId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
            Results.Ok(await repository.ReopenAsync(projectId, AccessToken(request), token).ConfigureAwait(false)));

        app.MapGet(SelectionApiRouteNames.ProjectProgress, async (Guid projectId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
            Results.Ok(await repository.GetProgressAsync(projectId, AccessToken(request), false, token).ConfigureAwait(false)));

        app.MapGet(SelectionApiRouteNames.ProjectFinalSelection, async (Guid projectId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var snapshot = await repository.GetLatestSnapshotAsync(projectId, AccessToken(request), token).ConfigureAwait(false);
            return Results.Ok(ToSnapshotResponse(snapshot));
        });

        app.MapDelete(SelectionApiRouteNames.ProjectAssetCloudCopy, async (Guid projectId, Guid assetId, HttpRequest request, LocalDevSelectionRepository repository, ISelectionObjectStorage storage, CancellationToken token) =>
        {
            var mutationGate = AssetMutationLocks.GetOrAdd($"{projectId:N}/{assetId:N}", static _ => new SemaphoreSlim(1, 1));
            await mutationGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
            var access = AccessToken(request);
            var asset = await repository.GetAssetAsync(projectId, assetId, access, false, token).ConfigureAwait(false);
            await repository.DeleteAssetAsync(projectId, assetId, access, token).ConfigureAwait(false);
            await storage.DeleteAsync(asset.ProxyObjectKey, token).ConfigureAwait(false);
            await storage.DeleteAsync(asset.ThumbObjectKey, token).ConfigureAwait(false);
            await storage.DeleteAsync(asset.PreviewObjectKey, token).ConfigureAwait(false);
            return Results.NoContent();
            }
            finally { mutationGate.Release(); }
        });

        app.MapGet(SelectionApiRouteNames.ClientProjects + "/{publicId}", async (string publicId, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var project = await repository.GetPublicProjectAsync(publicId, AccessToken(request), token).ConfigureAwait(false);
            var rule = await repository.GetRuleAsync(project.Id, AccessToken(request), token).ConfigureAwait(false);
            var progress = await repository.GetProgressAsync(project.Id, AccessToken(request), true, token).ConfigureAwait(false);
            return Results.Ok(new LocalDevPublicProjectResponse(ToProjectResponse(project), rule, progress, project.IsLocked));
        });

        app.MapGet(SelectionApiRouteNames.ClientAssets, async (string publicId, string? cursor, int? limit, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var access = AccessToken(request);
            var project = await repository.GetPublicProjectAsync(publicId, access, token).ConfigureAwait(false);
            var page = await repository.GetAssetsAsync(project.Id, access, cursor, limit ?? 50, true, token).ConfigureAwait(false);
            return Results.Ok(new SelectionAssetPageResponse(page.Items.Select(asset => new SelectionAssetResponse(
                asset.SelectionAssetId, asset.SourceAssetId, asset.OriginalFileName,
                $"{SelectionApiRouteNames.ClientProjects}/{publicId}/assets/{asset.SelectionAssetId}/thumb",
                $"{SelectionApiRouteNames.ClientProjects}/{publicId}/assets/{asset.SelectionAssetId}/preview",
                asset.Status, asset.SortOrder)).ToArray(), page.NextCursor, Math.Clamp(limit ?? 50, 1, 100)));
        });

        app.MapPost(SelectionApiRouteNames.ClientMediaSession, async (string publicId, HttpRequest request, HttpResponse response, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var project = await repository.GetPublicProjectAsync(publicId, AccessToken(request), token).ConfigureAwait(false);
            var mediaToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var expires = DateTimeOffset.UtcNow.AddMinutes(15);
            MediaSessions[mediaToken] = new(project.Id, publicId, expires);
            response.Cookies.Append("PixelTartLocalDevMedia", mediaToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Expires = expires,
                Path = $"{SelectionApiRouteNames.ClientProjects}/{publicId}/assets"
            });
            return Results.Ok(new LocalDevMediaSessionResponse(mediaToken, expires));
        });

        app.MapGet(SelectionApiRouteNames.ClientAssetThumb, (string publicId, Guid assetId, HttpRequest request, LocalDevSelectionRepository repository, ISelectionObjectStorage storage, CancellationToken token) =>
            ReadObjectAsync(publicId, assetId, request, repository, storage, SelectionObjectVariant.Thumb, token));
        app.MapGet(SelectionApiRouteNames.ClientAssetPreview, (string publicId, Guid assetId, HttpRequest request, LocalDevSelectionRepository repository, ISelectionObjectStorage storage, CancellationToken token) =>
            ReadObjectAsync(publicId, assetId, request, repository, storage, SelectionObjectVariant.Preview, token));

        app.MapPut(SelectionApiRouteNames.ClientChoices, async (string publicId, Guid assetId, SelectionChoiceRequest body, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var access = AccessToken(request);
            var project = await repository.GetPublicProjectAsync(publicId, access, token).ConfigureAwait(false);
            return Results.Ok(await repository.SetChoiceAsync(project.Id, assetId, access, body, token).ConfigureAwait(false));
        });
        app.MapPut(SelectionApiRouteNames.ClientFavorites, async (string publicId, Guid assetId, SelectionChoiceRequest body, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var access = AccessToken(request);
            var project = await repository.GetPublicProjectAsync(publicId, access, token).ConfigureAwait(false);
            return Results.Ok(await repository.SetFavoriteAsync(project.Id, assetId, access, body, token).ConfigureAwait(false));
        });
        app.MapPut(SelectionApiRouteNames.ClientComments, async (string publicId, Guid assetId, SelectionCommentRequest body, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var access = AccessToken(request);
            var project = await repository.GetPublicProjectAsync(publicId, access, token).ConfigureAwait(false);
            return Results.Ok(await repository.SetCommentAsync(project.Id, assetId, access, body, token).ConfigureAwait(false));
        });
        app.MapPost(SelectionApiRouteNames.ClientConfirm, async (string publicId, ConfirmSelectionRequest body, HttpRequest request, LocalDevSelectionRepository repository, CancellationToken token) =>
        {
            var access = AccessToken(request);
            var project = await repository.GetPublicProjectAsync(publicId, access, token).ConfigureAwait(false);
            return Results.Ok(ToSnapshotResponse(await repository.ConfirmAsync(project.Id, access, body, token).ConfigureAwait(false)));
        });
    }

    private static async Task<IResult> ReadObjectAsync(
        string publicId,
        Guid assetId,
        HttpRequest request,
        LocalDevSelectionRepository repository,
        ISelectionObjectStorage storage,
        SelectionObjectVariant variant,
        CancellationToken token)
    {
        var mediaToken = request.Headers["X-PixelTart-Media-Token"].FirstOrDefault()
            ?? request.Cookies["PixelTartLocalDevMedia"]
            ?? request.Query["media_token"].FirstOrDefault()
            ?? string.Empty;
        if (!MediaSessions.TryGetValue(mediaToken, out var session) || session.ExpiresAtUtc <= DateTimeOffset.UtcNow || !string.Equals(session.PublicId, publicId, StringComparison.Ordinal))
            throw new SelectionApiException(401, "InvalidMediaSession", "A short-lived LocalDev media session is required.");
        var asset = await repository.GetAssetByMediaSessionAsync(session.ProjectId, assetId, token).ConfigureAwait(false);
        var key = variant == SelectionObjectVariant.Thumb ? asset.ThumbObjectKey : asset.PreviewObjectKey;
        var stream = await storage.OpenReadAsync(key, token).ConfigureAwait(false)
            ?? throw new SelectionApiException(404, "ObjectNotFound", "Image object was not found.");
        return Results.Stream(stream, "image/jpeg", enableRangeProcessing: true);
    }

    private static string AccessToken(HttpRequest request) =>
        request.Headers["X-PixelTart-Dev-Token"].FirstOrDefault()
        ?? string.Empty;

    private static string Header(HttpRequest request, string name, string fallback) =>
        request.Headers[name].FirstOrDefault() is { Length: > 0 } value ? value : fallback;

    private static SelectionProjectResponse ToProjectResponse(LocalDevProjectRecord project) => new(
        project.Id, project.PublicId, project.Name, project.Status, project.TargetCount, project.DeadlineUtc)
    {
        ClientDisplayName = project.ClientDisplayName,
        SelectionVersion = project.SelectionVersion,
        Revision = project.Revision
    };

    private static FinalSelectionSnapshotResponse ToSnapshotResponse(LocalDevSnapshotRecord snapshot) =>
        new(snapshot.ProjectId, snapshot.SelectionVersion, snapshot.Revision, snapshot.Items, snapshot.ConfirmedAtUtc, snapshot.IsLocked);
}

public sealed record MediaSession(Guid ProjectId, string PublicId, DateTimeOffset ExpiresAtUtc);

public sealed class LocalDevFaultPolicy(bool enabled)
{
    public bool Enabled { get; } = enabled;
}

public sealed class LocalDevFaultMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, LocalDevFaultPolicy policy)
    {
        if (policy.Enabled)
        {
            var delay = context.Request.Headers["X-PixelTart-Dev-Delay"].FirstOrDefault() switch
            {
                "300" => 300,
                "2000" => 2000,
                _ => 0
            };
            if (delay > 0) await Task.Delay(delay, context.RequestAborted).ConfigureAwait(false);
            if (context.Request.Headers["X-PixelTart-Dev-Random-Failure"] == "1" && Random.Shared.NextDouble() < 0.10)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new ApiProblem("InjectedDevelopmentFailure", "Development-only injected failure.", context.TraceIdentifier)).ConfigureAwait(false);
                return;
            }
        }
        await next(context).ConfigureAwait(false);
    }
}
