using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

/// <summary>
/// Deterministic, synthetic fixtures for the physical human-acceptance package.
/// Every file is written below AppDataPaths.Root, which is redirected by the
/// PixelTart.exe acceptance build to the package's DemoWorkspace.
/// </summary>
public static class PlanningHumanAcceptanceDemoSeeder
{
    public static readonly Guid ProjectId = Guid.Parse("9b338f05-bc81-4d68-88b6-7f68e2722a8b");
    public static readonly Guid BookingId = Guid.Parse("683cc235-d659-4b07-bc39-d923155e7c79");
    private static readonly Guid LookId = Guid.Parse("5e9a06bb-6d4c-4d5c-a2db-10d7bf6ca8c8");
    private static readonly byte[] DemoPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    public static async Task SeedAsync(SettingsService settingsService, CancellationToken token = default)
    {
        AppDataPaths.EnsureCreated();
        var root = AppDataPaths.Root;
        var demoAssets = Path.Combine(root, "SyntheticAssets");
        Directory.CreateDirectory(demoAssets);
        var now = DateTimeOffset.UtcNow;
        var files = new List<string>();
        for (var index = 0; index < 12; index++)
        {
            var path = Path.Combine(demoAssets, $"demo-shot-{index + 1:00}.png");
            if (!File.Exists(path)) await File.WriteAllBytesAsync(path, DemoPng, token).ConfigureAwait(false);
            files.Add(path);
        }

        var database = new PixelTartDatabase(AppDataPaths.DatabaseFile);
        await new SqliteProjectRepository(database).UpsertAsync(new PhotoProjectRecord
        {
            Id = ProjectId, Name = "Lumen Atelier · Autumn Campaign", Status = PhotoProjectStatus.Ready,
            CreatedAt = now.AddDays(-21), UpdatedAt = now, Summary = "人工验收用合成项目；不会写入生产素材。",
            SourceDirectories = [demoAssets]
        }, token).ConfigureAwait(false);
        await new SqliteShootBookingRepository(database).SaveAsync(new ShootBooking
        {
            Id = BookingId, ProjectId = ProjectId, Title = "Autumn Editorial Session",
            ClientDisplayName = "Lumen Atelier / 陈知夏", StartAtUtc = now.Date.AddDays(3).AddHours(2),
            EndAtUtc = now.Date.AddDays(3).AddHours(6), Status = ShootBookingStatus.Confirmed,
            Location = "Studio 07 · Shanghai", ShootingType = "Editorial", CreatedAtUtc = now.AddDays(-14), UpdatedAtUtc = now
        }, [], token).ConfigureAwait(false);

        var shotStore = new ProjectShotStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectShots"));
        var shots = Enumerable.Range(0, 12).Select(index =>
        {
            var shotId = Guid.Parse($"{index + 1:00000000}-1111-4111-8111-111111111111");
            var kinds = new[] { ShotReferenceKind.Lighting, ShotReferenceKind.Pose, ShotReferenceKind.Storyboard, ShotReferenceKind.Styling };
            var references = kinds.Select((kind, refIndex) => new ProjectShotReference(
                Guid.NewGuid(), kind, ExternalReference: files[(index + refIndex) % files.Count],
                Title: $"{kind switch { ShotReferenceKind.Lighting => "光线", ShotReferenceKind.Pose => "姿势", ShotReferenceKind.Storyboard => "分镜", _ => "造型" }}参考",
                Note: "合成参考图，仅用于验收布局。", IsPinned: refIndex == 0)).ToArray();
            return new ProjectShot(shotId, ProjectId, index, $"镜头 {index + 1:00} · {new[] { "入口全身", "窗边半身", "细节特写" }[index % 3]}",
                index == 0 ? ProjectShotStatus.InProgress : ProjectShotStatus.NotStarted,
                "保持主体与留白关系；验收时可拖动排序。", LookId, references, now, now, EstimatedMinutes: 15,
                Scene: index % 2 == 0 ? "Studio / Warm Window" : "Studio / Neutral Set");
        }).ToArray();
        await shotStore.SaveCatalogAsync(new ProjectShotCatalog(ProjectId, shots), token).ConfigureAwait(false);

        var planningStore = new PlanningProjectStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectPlanning"));
        var inspiration = new SqliteInspirationTrayService(Path.Combine(AppDataPaths.DataDirectory, "asset-library-v16.db"));
        var board = (await inspiration.ListCollectionsAsync(token).ConfigureAwait(false)).FirstOrDefault(item => item.ProjectId == ProjectId && item.Name == "Autumn Editorial · 灵感板")
            ?? await inspiration.CreateCollectionAsync("Autumn Editorial · 灵感板", ProjectId, token).ConfigureAwait(false);
        var canvasId = Guid.Parse("4d4d2cf4-88b9-4318-8f67-1b422dc66e14");
        await new CanvasDocumentStore(Path.Combine(AppDataPaths.DataDirectory, "FreeCanvas")).SaveAsync(new CanvasDocument
        {
            CanvasId = canvasId, ProjectId = ProjectId, Name = "Autumn Editorial · 画布", UpdatedAt = now
        }, token).ConfigureAwait(false);
        await planningStore.SaveAsync(new PlanningProjectState(ProjectId, BookingId,
            new PlanningSummary("秋季编辑拍摄验收", ["暖窗光", "留白", "低饱和"], "合成项目，不连接真实相机。", "入口全身、窗边半身、细节特写", "观察第一次打开和整理是否需要猜。", "RC12 人工验收"),
            shots[0].ShotId,
            [new PlanningVisualLink(Guid.NewGuid(), PlanningVisualLinkKind.InspirationBoard, board.CollectionId, board.Name, PlanningCanvasRole.Overview),
             new PlanningVisualLink(Guid.NewGuid(), PlanningVisualLinkKind.FreeCanvas, canvasId, "Autumn Editorial · 画布", PlanningCanvasRole.Styling)],
            Revision: 1, UpdatedAt: now), token).ConfigureAwait(false);

        var source = new ProjectVisualSource(Guid.Parse("7d70a428-e3f0-4cb9-a280-7593324a2b26"), Guid.Parse("0d4d41a6-9b44-4c9d-a5dc-85a4cf4cf9c1"), "synthetic-demo");
        var palette = new ProjectPaletteReference([new(new(196, 120, 82), new(0.53, 0.08, 0.07), 18, .42, .54, .55, "#C47852"), new(new(44, 54, 62), new(.23, .01, -.03), 210, .17, .21, .32, "#2C363E"), new(new(232, 216, 184), new(.88, .01, .04), 44, .21, .85, .13, "#E8D8B8")], [source], now, "visual-analysis-v4");
        var zones = new ElevenZoneDistribution([.02, .04, .08, .12, .18, .2, .16, .1, .06, .03, .01]);
        var tone = new ProjectToneTarget(zones, Enumerable.Range(0, 256).Select(i => i is > 70 and < 200 ? 2d : 1d).ToArray(), [source], now, "visual-analysis-v4");
        var visualStore = new ProjectVisualReferenceStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        await visualStore.SavePaletteAsync(ProjectId, palette, token).ConfigureAwait(false);
        await visualStore.SaveToneAsync(ProjectId, tone, token).ConfigureAwait(false);
        var lookStore = new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        await lookStore.SaveAsync(CreateLook(files[0], now), projectDefault: true, token).ConfigureAwait(false);
        await settingsService.SaveAsync(await settingsService.LoadAsync().ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static ReferenceLook CreateLook(string sourcePath, DateTimeOffset now)
    {
        var histogram = new uint[256]; histogram[128] = 1;
        var analysis = new AssetVisualAnalysisResult(Guid.Parse("0d4d41a6-9b44-4c9d-a5dc-85a4cf4cf9c1"), "synthetic-demo", AssetVisualAnalysisResult.CurrentVersion, 3, PaletteSortMode.Weight, VisualAnalysisSourceKind.RenderedProxy, "sRGB", "synthetic", [new(new(196, 120, 82), new(.53, .08, .07), 18, .42, .54, .55, "#C47852")], ColorHarmonyTendency.Analogous, histogram, histogram, histogram, histogram, new(.1, .15, .5, .2, .05), .5, .5, 0, 0, .5, ContrastTendency.Medium, .5, LuminanceSpanTendency.Medium, ToneKeyTendency.Mid, .42, 18, SaturationTendency.Medium, .5, WarmCoolTendency.Warm, now)
        { ZoneDistribution = new ElevenZoneDistribution([.02, .04, .08, .12, .18, .2, .16, .1, .06, .03, .01]), AverageLightness = .54 };
        var reference = new ReferenceLookSource(Guid.Empty, null, "合成暖窗光", sourcePath, "synthetic-demo", 1, analysis, "External");
        return new ReferenceLook(LookId, "Autumn Warm Window", ProjectId, [reference], new(), now, now);
    }
}
