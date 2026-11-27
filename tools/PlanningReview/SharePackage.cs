using System.Text.Json;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Services.Projects;
using static PlanningReview.Rules;

namespace PlanningReview;

internal static partial class SharePackage
{
    public static string Plan(Guid project) => $"ProjectPlanning/{project:N}.planning.json";
    static string Shots(Guid project) => $"ProjectShots/{project:N}.shots.json";
    static string Copies(Guid project, Guid document) => $"ProjectPlanning/project-copies/{project:N}/{document:N}/";
    static string Images(Guid document) => $"ProjectPlanning/images/{document:N}/";

    [GeneratedRegex(@"\A(?:reference|attachment)/[0-9a-f]{32}/[0-9a-f]{64}\.[0-9a-z]{1,12}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CopyPath();
    [GeneratedRegex(@"\A[0-9a-f]{32}\.png\z", RegexOptions.CultureInvariant)]
    private static partial Regex ImagePath();

    public static bool ValidPath(string? path, Guid project, Guid document)
    {
        if (path is not { Length: > 0 and <= 250 }) return false;
        if (path == Plan(project) || path == Shots(project) || path == PlanningShareRichSnapshot.FileName) return true;
        var copies = Copies(project, document);
        var images = Images(document);
        return path.StartsWith(copies, StringComparison.Ordinal) && CopyPath().IsMatch(path[copies.Length..]) ||
            path.StartsWith(images, StringComparison.Ordinal) && ImagePath().IsMatch(path[images.Length..]);
    }

    public static async Task Validate(string isolated, ShareManifest manifest)
    {
        var project = manifest.ProjectId;
        var document = manifest.DocumentId;
        var planPath = Plan(project);
        await using var input = File.OpenRead(Path.Combine(isolated, planPath));
        var state = await JsonSerializer.DeserializeAsync<PlanningProjectState>(input);
        Need(state?.ProjectId == project && state.Document?.Package?.ProjectId == project &&
            state.Document.Package.DocumentId == document, "已保存的策划身份与发布清单不符。");
        var doc = state!.Document!;
        var declared = manifest.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        declared.Remove(planPath);
        declared.Remove(Shots(project));
        bool rich = doc.Package!.RichTextPackageBase64 is not null;
        Need(declared.Remove(PlanningShareRichSnapshot.FileName) == rich, "策划正文的富文本投影与文件清单不符。");
        foreach (var group in PlanningReferenceLinks.Copies(doc).GroupBy(copy => copy.RelativePath, StringComparer.Ordinal))
        {
            foreach (var copy in group)
            {
                copy.Validate();
                Need(copy.ProjectId == project && copy.DocumentId == document &&
                    declared.TryGetValue("ProjectPlanning/" + copy.RelativePath, out var file) &&
                    file.Length == copy.Bytes && string.Equals(file.Sha256, copy.Sha256, StringComparison.OrdinalIgnoreCase),
                    "项目自有副本没有完整的匹配字节。");
            }
            declared.Remove("ProjectPlanning/" + group.Key);
        }
        foreach (var image in doc.Package.RichLayout?.Images ?? [])
        {
            var path = PlanningImageStorage.RelativePath(document, image.DocumentImageId);
            Need(image.DocumentId == document && image.CopyPath == path &&
                declared.Remove("ProjectPlanning/" + path, out var file) &&
                string.Equals(file.Sha256, image.CopyHash, StringComparison.OrdinalIgnoreCase),
                "正文自有图片没有完整的匹配字节。");
        }
        Need(declared.Count == 0, "文件清单包含不属于此策划的材料。");
    }
}
