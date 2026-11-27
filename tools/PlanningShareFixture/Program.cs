using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;

internal static class Program
{
    private static readonly (string File, string Title, string Sha)[] Photos =
    [
        ("01_Fashion_model.jpg", "白底样张 · 外套轮廓", "1C07F7B45E9E0593A6CDB9F0F95B60F9E2F983262DAF7E570B67249205A7CB46"),
        ("02_Autumn_fashion.jpg", "秋冬衣领 · 造型参考", "0884ECC7C547FA1F8C6505FAC4DC5EC18EF08E437B224E6937A17D124EBEA169"),
        ("03_Hair_in_wind.jpg", "风中发丝 · 姿态参考", "1212A14C1DC4DD873C81FD3FE76B370FAC4BE95F8A11D84EAEC68FC9F5400107")
    ];

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: ShareFixture <candidate delivery root> <three approved CC0 photos folder>");
        string delivery = Path.GetFullPath(args[0]), photos = Path.GetFullPath(args[1]);
        string privateRoot = Path.Combine(delivery, "_private");
        string root = Path.Combine(privateRoot, "desktop-data");
        string config = Path.Combine(privateRoot, "host.json");
        PlanningImageStorage.RejectLinks(delivery, config);
        PlanningImageStorage.RejectLinks(delivery, root);
        using (var document = JsonDocument.Parse(File.ReadAllBytes(config)))
            if (document.RootElement.GetProperty("purpose").GetString() != "guangjing-planning-share-candidate")
                throw new InvalidOperationException("Use the separately configured candidate only.");
        if (Directory.Exists(root)) throw new InvalidOperationException("The desktop-data root already exists. Use a new candidate or open the existing local plans.");
        var inputs = Photos.Select(spec =>
        {
            var path = Path.Combine(photos, spec.File);
            PlanningImageStorage.RejectLinks(photos, path);
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != spec.Sha)
                throw new InvalidDataException("The approved CC0 sample bytes do not match: " + spec.File);
            return path;
        }).ToArray();
        Directory.CreateDirectory(root);
        var db = new PixelTartDatabase(Path.Combine(root, "Data", "pixel-tart.db"));
        var result = new DatabaseMigrator(db, new DatabaseBackupService(db, Path.Combine(root, "Backups", "Database"))).MigrateAsync().GetAwaiter().GetResult();
        if (!result.Success) throw new InvalidOperationException("The native project DB could not initialize.");
        var plan = new PlanningProjectStore(Path.Combine(root, "Data", "ProjectPlanning"));
        var shot = new ProjectShotStore(Path.Combine(root, "Data", "ProjectShots"));
        var repository = new SqliteProjectRepository(db);
        File.WriteAllText(Path.Combine(root, "settings.json"), "{\"OnboardingLegacyUser\":true,\"OnboardingUpgradeOfferShown\":true}");
        var records = new[] { Create(false), Create(true) };
        File.WriteAllText(Path.Combine(privateRoot, "fixture.json"), JsonSerializer.Serialize(new { projects = records }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Console.WriteLine("Two independently saved native plans; same title, distinct project and document identities. Candidate only.");

        object Create(bool sameTitle)
        {
            const string title = "秋冬服装型录";
            var project = new PhotoProjectRecord { Name = title, Status = PhotoProjectStatus.Ready,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            repository.UpsertAsync(project).GetAwaiter().GetResult();
            var brief = MakeBrief(sameTitle);
            var doc = new PlanningDocument
            {
                Title = title, Subtitle = sameTitle ? "同名对照｜独立项目" : "首轮样例｜北窗衣领",
                Body = brief.Body, CustomBody = "交付使用 sRGB JPEG；演示不含客户文件。", Location = "北窗摄影棚", People = "摄影 A、造型 B",
                ShootDate = new DateTimeOffset(2026, 12, 2, 9, 0, 0, TimeSpan.Zero)
            }.EnsureIdentity(project.Id);
            var references = Photos.Select((value, index) => new PlanningDocumentReference(
                new ProjectShotReference(Guid.NewGuid(), ShotReferenceKind.Styling, ExternalReference: inputs[index], Title: value.Title),
                "造型", IsHero: index == 1, IsMoodboard: true)).ToArray();
            var now = DateTimeOffset.UtcNow;
            string[] names = ["开场 · 全身轮廓", "步行 · 正面动态", "细节 · 衣领纹理", "侧身 · 剪裁线条", "半身 · 面料叠穿", "收尾 · 背面轮廓"];
            var shots = names.Select((name, index) => new ProjectShot(Guid.NewGuid(), project.Id, index, name,
                ProjectShotStatus.NotStarted, null, null, index == 2 ? [references[1].Source] : [], now, now, EstimatedMinutes: 10 + index, Scene: "北窗 A 棚")
                { PlanningFieldsVersion = 1, ShotSize = index == 2 ? "特写" : "中景", CameraAndLens = index == 2 ? "85 mm" : "50 mm",
                    Action = index == 2 ? "贴近衣领保留卷边与暗部织纹" : "保持衣料轮廓", Lighting = "侧逆光" }).ToArray();
            var canvas = new CanvasDocument { ProjectId = project.Id, Name = "秋冬 · 侧光色组", PixelWidth = 864, PixelHeight = 540 };
            var canvasRevision = new PlanningCanvasRevision(Guid.NewGuid(), canvas.CanvasId, now, canvas, Snapshot(inputs));
            var rows = new PlanningResource[]
            {
                new PlanningScheduleSlot { Name = "试装与校灯", Date = new DateOnly(2026,12,2), StartLocal = "09:30", EndLocal = "10:00", Location = "A 棚", Owner = "造型 B", ShotIds = [shots[2].ShotId] },
                new PlanningScheduleSlot { Name = "衣领近景通告", Date = new DateOnly(2026,12,2), StartLocal = "10:00", EndLocal = "10:45", Location = "A 棚北窗", Owner = "摄影 A", ShotIds = [shots[2].ShotId] },
                new PlanningLight { Name = "北窗主光", X = .2, Y = .35, DirectionDegrees = 22, PowerPercent = 65, Modifier = "柔光布", ShotIds = [shots[2].ShotId] },
                new PlanningProp { Name = "棕色翻领外套", Category = "服装", Preparation = "已确认", Quantity = 2, ShotIds = [shots[2].ShotId] },
                new PlanningGearItem { Name = "全画幅 A", Model = "85 mm", Quantity = 1 },
                new PlanningCrewMember { Name = "造型 B", Role = "造型", ArrivalLocal = "09:30" },
                new PlanningVenue { Name = "北窗摄影棚", Address = "演示 A 棚", AvailableHours = "09:00–17:00", Date = new DateOnly(2026,12,2) },
                new PlanningBudgetLine { Name = "摄影棚", Planned = 450, Currency = "CNY" }
            };
            doc = doc with { Package = doc.Package! with { Body = brief.Body, RichTextPackageBase64 = brief.Rtf,
                    RichLayout = new(new PlanningPageLayout(), [new(0,"Heading1"), new(1), new(2) { RepeatHeader = true }], []),
                    ShotIds = shots.Select(s => s.ShotId).ToArray(), CanvasRevisions = [canvasRevision] },
                Resources = new() { Items = rows } };
            var saved = new PlanningProjectState(project.Id, Summary: new("为秋冬外套拍出衣领和织纹", ["侧逆光","深棕","留白"], "织纹要可见", "衣领近景", "仅授权样片", "摄影型录"),
                VisualLinks: [new(Guid.NewGuid(), PlanningVisualLinkKind.FreeCanvas, canvas.CanvasId, canvas.Name, PlanningCanvasRole.Styling, now)]) { Document = doc };
            plan.SaveAsync(saved).GetAwaiter().GetResult();
            plan.ImportProjectCopiesAsync(project.Id, references, []).GetAwaiter().GetResult();
            shot.SaveCatalogAsync(new(project.Id, shots)).GetAwaiter().GetResult();
            var reopened = plan.LoadAsync(project.Id).GetAwaiter().GetResult().Document!;
            var reopenedShots = shot.LoadAsync(project.Id).GetAwaiter().GetResult();
            if (reopened.Package?.DocumentId != doc.Package.DocumentId || reopened.References.Count != 3 || reopenedShots.Shots[2].References.Single().ReferenceId != references[1].Source.ReferenceId)
                throw new InvalidDataException("Native fixture did not round trip.");
            return new { projectId = project.Id, documentId = doc.Package.DocumentId, control = sameTitle, title,
                originalThirdShot = shots[2].ShotId, shotOrder = shots.Select(s => s.ShotId).ToArray(), collar = references[1].Source.ReferenceId,
                assets = reopened.References.Select(r => new { id = r.Source.ReferenceId, sha256 = r.ProjectCopy!.Sha256 }).ToArray(),
                timelineRow = rows[1].Id, fixedCanvas = canvasRevision.RevisionId };
        }
    }

    private static (string Rtf,string Body) MakeBrief(bool other)
    {
        var heading = new Paragraph(new Run(other ? "另一个独立型录" : "项目简报 · 秋冬服装型录")) { FontSize = 25, FontWeight = FontWeights.Bold };
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(other ? "这是同名对照项目。" : "本次拍摄保留衣料厚度与 "));
        paragraph.Inlines.Add(new Run("衣领织纹") { FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, Foreground = Brushes.DarkGreen });
        paragraph.Inlines.Add(new Run("，现场注意翻折处的侧逆光。"));
        var table = new Table(); for (int i = 0; i < 3; i++) table.Columns.Add(new());
        var group = new TableRowGroup(); table.RowGroups.Add(group);
        foreach (var values in new[] { new[] { "拍摄通告", "时间", "地点" }, new[] { "进场校灯", "09:30", "A 棚" }, new[] { "衣领近景", "10:00", "A 棚北窗" } })
        { var row = new TableRow(); foreach (string value in values) row.Cells.Add(new(new Paragraph(new Run(value)))); group.Rows.Add(row); }
        var doc = new FlowDocument(); doc.Blocks.Add(heading); doc.Blocks.Add(paragraph); doc.Blocks.Add(table);
        var range = new TextRange(doc.ContentStart, doc.ContentEnd); using var data = new MemoryStream(); range.Save(data, DataFormats.Rtf);
        return (Convert.ToBase64String(data.ToArray()), range.Text.TrimEnd());
    }

    private static string Snapshot(string[] paths)
    {
        var image = new DrawingVisual();
        using(var ctx=image.RenderOpen())
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(236,229,208)),null,new Rect(0,0,864,540));
            ctx.DrawText(new FormattedText("秋冬 · 北窗侧光", CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 35, new SolidColorBrush(Color.FromRgb(38,64,47)), 1),new Point(40,26));
            for(var i=0;i<paths.Length;i++)
            {
                var photo = new BitmapImage(); photo.BeginInit(); photo.UriSource=new Uri(paths[i]); photo.DecodePixelWidth=260; photo.CacheOption=BitmapCacheOption.OnLoad; photo.EndInit();
                var h=320d; var w=Math.Min(236, h * photo.PixelWidth / photo.PixelHeight); h=w*photo.PixelHeight/photo.PixelWidth;
                ctx.DrawImage(photo,new Rect(44+264*i,115,w,h));
            }
            for(var i=0;i<3;i++)ctx.DrawRectangle(new SolidColorBrush(new[]{Color.FromRgb(51,75,64),Color.FromRgb(132,100,78),Color.FromRgb(171,175,157)}[i]),null,new Rect(44+60*i,464,45,25));
        }
        var bitmap=new RenderTargetBitmap(864,540,96,96,PixelFormats.Pbgra32); bitmap.Render(image); var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var data=new MemoryStream();png.Save(data);return Convert.ToBase64String(data.ToArray());
    }
}