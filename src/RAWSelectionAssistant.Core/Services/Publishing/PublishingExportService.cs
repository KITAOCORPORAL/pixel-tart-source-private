using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Publishing;

public interface IPublishingRenderer
{
    Task RenderAsync(string sourcePath, string destinationPath, PublishingOptions options, CancellationToken cancellationToken = default);
    Task VerifyAsync(string imagePath, CancellationToken cancellationToken = default);
}

public interface IPublishingExportService
{
    Task<PublishingExportResult> ExportAsync(Guid taskId, PublishingExportRequest request, IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class PublishingExportService(IPublishingRenderer renderer) : IPublishingExportService
{
    public async Task<PublishingExportResult> ExportAsync(Guid taskId, PublishingExportRequest request, IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null, CancellationToken cancellationToken = default)
    {
        request.Validate();
        Directory.CreateDirectory(request.DestinationDirectory);
        var sourceFiles = request.SourceFiles.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        IReadOnlyList<ExportRecipe?> recipes = request.Recipes is { Count: > 0 } ? request.Recipes.Cast<ExportRecipe?>().ToArray() : [null];
        var total = sourceFiles.Length * recipes.Count;
        var results = new List<PublishingItemResult>(total);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sequence = 0;
        for (var recipeIndex = 0; recipeIndex < recipes.Count; recipeIndex++)
        {
            var recipe = recipes[recipeIndex];
            var recipeOptions = recipe is null ? request.Options : RecipeOptions(recipe, request.Options);
            for (var sourceIndex = 0; sourceIndex < sourceFiles.Length; sourceIndex++)
            {
            var source = sourceFiles[sourceIndex];
            var index = sequence++;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(source)) throw new FileNotFoundException("照片不可用。", source);
                if (!PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(source))) throw new InvalidDataException("暂不支持此图片格式；当前支持 JPG、JPEG、PNG、TIFF。");
                var before = await ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
                var destination = ResolveDestination(source, request.DestinationDirectory, recipeOptions, reserved, recipe?.FilenameTemplate, recipe?.Destination);
                reserved.Add(destination);
                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".publishing";
                try
                {
                    await renderer.RenderAsync(source, temporary, recipeOptions, cancellationToken).ConfigureAwait(false);
                    await renderer.VerifyAsync(temporary, cancellationToken).ConfigureAwait(false);
                    var after = await ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
                    if (!CryptographicOperations.FixedTimeEquals(before, after)) throw new IOException("源照片在发布处理期间发生变化，已停止输出。");
                    File.Move(temporary, destination, false);
                    results.Add(new(index, PublishingItemState.Completed, source, destination, new FileInfo(destination).Length));
                }
                finally { TryDelete(temporary); }
            }
            catch (OperationCanceledException)
            {
                results.Add(new(index, PublishingItemState.Cancelled, source, null, 0));
                for (var pendingRecipe = recipeIndex; pendingRecipe < recipes.Count; pendingRecipe++)
                {
                    var firstSource = pendingRecipe == recipeIndex ? sourceIndex + 1 : 0;
                    for (var pending = firstSource; pending < sourceFiles.Length; pending++)
                        results.Add(new(sequence++, PublishingItemState.Cancelled, sourceFiles[pending], null, 0));
                }
                break;
            }
            catch (Exception error)
            {
                results.Add(new(index, PublishingItemState.Failed, source, null, 0, UserMessage(error)));
            }
            var summary = Summarize(total, results);
            progress?.Report((results.Count * 100d / total, recipe is null ? Path.GetFileName(source) : $"{recipe.Name} · {Path.GetFileName(source)}", summary));
            }
            if (results.Any(item => item.State == PublishingItemState.Cancelled)) break;
        }
        var finalSummary = Summarize(total, results);
        var state = finalSummary.Succeeded == total ? TaskLifecycleState.Completed
            : finalSummary.Succeeded > 0 ? TaskLifecycleState.PartiallyCompleted
            : finalSummary.Cancelled > 0 && finalSummary.Failed == 0 ? TaskLifecycleState.Cancelled : TaskLifecycleState.Failed;
        return new(taskId, state, finalSummary, results.OrderBy(item => item.Sequence).ToArray());
    }

    public static string ResolveDestination(string sourcePath, string destinationDirectory, PublishingOptions options, ISet<string>? reserved = null, string? filenameTemplate = null, string? recipeDestination = null)
    {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        var root = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destinationRoot = root.TrimEnd(Path.DirectorySeparatorChar);
        if (!string.IsNullOrWhiteSpace(recipeDestination))
        {
            if (!IsSafeRecipePath(recipeDestination)) throw new ArgumentException("Recipe destination cannot escape the export root.", nameof(recipeDestination));
            destinationRoot = Path.GetFullPath(Path.Combine(destinationRoot, recipeDestination));
            if (!destinationRoot.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Recipe destination cannot escape the export root.", nameof(recipeDestination));
        }
        Directory.CreateDirectory(destinationRoot);
        var suffix = options.Suffix ?? string.Empty;
        if (string.Equals(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar), destinationRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(suffix)) suffix = PublishingDefaults.DefaultSuffix;
        var extension = options.OutputFormat switch { PublishingOutputFormat.Png => ".png", PublishingOutputFormat.Tiff => ".tif", _ => ".jpg" };
        var recipeName = string.IsNullOrWhiteSpace(recipeDestination) ? string.Empty : Path.GetFileName(recipeDestination);
        var template = string.IsNullOrWhiteSpace(filenameTemplate) ? "{name}" : filenameTemplate;
        if (template.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || template.Contains('/') || template.Contains('\\') || template.Contains("..", StringComparison.Ordinal)
            || System.Text.RegularExpressions.Regex.Replace(template, "\\{name\\}|\\{recipe\\}", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).IndexOfAny(['{', '}']) >= 0)
            throw new ArgumentException("FilenameTemplate must be a filename, not a path.", nameof(filenameTemplate));
        var stem = template.Replace("{name}", Path.GetFileNameWithoutExtension(sourcePath), StringComparison.OrdinalIgnoreCase)
            .Replace("{recipe}", recipeName, StringComparison.OrdinalIgnoreCase) + suffix;
        stem = SanitizeFilename(stem);
        var desired = Path.Combine(destinationRoot, stem + extension);
        var candidate = desired; var number = 2;
        while (File.Exists(candidate) || reserved?.Contains(candidate) == true) candidate = Path.Combine(destinationRoot, $"{stem}_{number++}{extension}");
        return candidate;
    }

    private static PublishingOptions RecipeOptions(ExportRecipe recipe, PublishingOptions fallback)
    {
        var dimensions = recipe.ResizeMode switch
        {
            ExportRecipeResizeMode.LongEdge => new PublishingDimensions(true, PublishingSizeMode.LongestEdge, recipe.LongEdge ?? 1, JpegQuality: recipe.Quality, PreserveMetadata: recipe.MetadataPolicy == ExportRecipeMetadataPolicy.Preserve, Dpi: recipe.Dpi),
            ExportRecipeResizeMode.ShortEdge => new PublishingDimensions(true, PublishingSizeMode.ShortestEdge, recipe.ShortEdge ?? 1, JpegQuality: recipe.Quality, PreserveMetadata: recipe.MetadataPolicy == ExportRecipeMetadataPolicy.Preserve, Dpi: recipe.Dpi),
            ExportRecipeResizeMode.Exact => new PublishingDimensions(true, PublishingSizeMode.Exact, Width: recipe.Width ?? 1, Height: recipe.Height ?? 1, JpegQuality: recipe.Quality, PreserveMetadata: recipe.MetadataPolicy == ExportRecipeMetadataPolicy.Preserve, Dpi: recipe.Dpi),
            _ => new PublishingDimensions(false, PublishingSizeMode.Original, JpegQuality: recipe.Quality, PreserveMetadata: recipe.MetadataPolicy == ExportRecipeMetadataPolicy.Preserve, Dpi: recipe.Dpi)
        };
        return fallback with
        {
            Dimensions = dimensions,
            OutputFormat = recipe.Format switch { ExportRecipeFormat.Png => PublishingOutputFormat.Png, ExportRecipeFormat.Tiff => PublishingOutputFormat.Tiff, _ => PublishingOutputFormat.Jpeg },
            Suffix = string.Empty,
            OutputBitDepth = recipe.BitDepth == ExportRecipeBitDepth.Sixteen ? PublishingOutputBitDepth.Sixteen : PublishingOutputBitDepth.Eight,
            ColorSpaceProfile = recipe.ColorSpaceProfile,
            MetadataPolicy = recipe.MetadataPolicy
        };
    }

    private static bool IsSafeRecipePath(string value)
    {
        if (Path.IsPathRooted(value) || value.Contains(':') || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
        var normalized = value.Replace('\\', '/');
        return !normalized.StartsWith('/') && !normalized.Contains("//", StringComparison.Ordinal)
            && normalized.Split('/').All(part => part.Length > 0 && part is not ("." or "..") && part.TrimEnd(' ', '.') == part && !IsReservedWindowsName(part));
    }

    private static string SanitizeFilename(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(ch => invalid.Contains(ch) || ch is '/' or '\\' ? '_' : ch).ToArray()).TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(safe)) safe = "export";
        if (IsReservedWindowsName(safe)) safe = "_" + safe;
        return safe;
    }

    private static bool IsReservedWindowsName(string value)
    {
        var stem = Path.GetFileNameWithoutExtension(value).TrimEnd(' ', '.');
        return new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase)
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9');
    }

    private static async Task<byte[]> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    private static TaskResultSummary Summarize(int total, IReadOnlyCollection<PublishingItemResult> results) => new(total, results.Count(item => item.State == PublishingItemState.Completed), results.Count(item => item.State == PublishingItemState.Failed), 0, results.Count(item => item.State == PublishingItemState.Cancelled), 0, 0, results.Sum(item => item.BytesWritten));
    private static string UserMessage(Exception error) => error switch { FileNotFoundException => "源照片不可用。", UnauthorizedAccessException => "无法写入输出目录。", InvalidDataException invalid => invalid.Message, _ => "发布输出失败，请检查照片和输出目录。" };
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}

public static class PublishingFolderInput
{
    public static IReadOnlyList<string> Scan(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
