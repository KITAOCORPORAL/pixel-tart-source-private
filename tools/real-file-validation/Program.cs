using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.BatchCompression;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: <raw|batch|local-split|collage> <output> <report.json> <source files...>");
    return 2;
}

var mode = args[0].ToLowerInvariant();
var outputArgument = Path.GetFullPath(args[1]);
var reportPath = Path.GetFullPath(args[2]);
var sources = args.Skip(3).Select(Path.GetFullPath).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
var isolationRoot = Path.Combine(Path.GetDirectoryName(reportPath)!, ".runtime-" + Guid.NewGuid().ToString("N"));
Environment.SetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT", isolationRoot);

object report;
var exitCode = 1;
try
{
    (report, exitCode) = mode switch
    {
        "raw" => await ValidateRawAsync(outputArgument, sources),
        "batch" => await ValidateBatchAsync(outputArgument, sources),
        "local-split" => await ValidateLocalSplitAsync(outputArgument, sources),
        "collage" => await ValidateCollageAsync(outputArgument, sources),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unknown validation mode.")
    };
}
catch (Exception exception)
{
    report = new
    {
        protocol = "pixel-tart-real-file-validation/v1",
        mode,
        final_state = "Failed",
        error_type = exception.GetType().Name,
        error_message = MediaTaskFailurePayload.SanitizeTechnical(exception.Message),
        source_files = sources.Select(UserPath).ToArray(),
        generated_at = DateTimeOffset.UtcNow
    };
}

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(reportPath, json, new UTF8Encoding(false));
Console.WriteLine(json);
return exitCode;

static async Task<(object Report, int ExitCode)> ValidateRawAsync(string outputDirectory, IReadOnlyList<string> sources)
{
    if (sources.Count != 1) throw new ArgumentException("RAW validation requires exactly one source file.");
    Directory.CreateDirectory(outputDirectory);
    var source = sources[0];
    var proof = await SourceProof.CreateAsync(source);
    var decoder = new LibRawDecoder();
    var options = new RawToJpegOptions(JpegQuality: 90, UseCameraWhiteBalance: true,
        VerifySha256: true, PreserveExif: true, AutoRotate: true);
    var stopwatch = Stopwatch.StartNew();
    var decoded = await decoder.DecodeAsync(source, options);
    var capability = decoder.GetCapability();
    var root = await ApplicationCompositionRoot.CreateAsync();
    var taskId = await root.RawToJpegCoordinator.StartAsync(new([source], outputDirectory, options));
    await root.RawToJpegCoordinator.WaitForCompletionAsync(taskId);
    var terminal = await root.RawToJpegCoordinator.GetTaskStateAsync(taskId);
    var outputs = Directory.GetFiles(outputDirectory, "*.jpg", SearchOption.TopDirectoryOnly);
    var decodedOutputs = new List<object>();
    foreach (var output in outputs)
    {
        using var stream = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        decodedOutputs.Add(new { path = ValidationPath(output), width = frame.PixelWidth, height = frame.PixelHeight, bytes = new FileInfo(output).Length });
    }
    stopwatch.Stop();
    var sourceUnchanged = await proof.IsUnchangedAsync();
    var passed = terminal?.State == TaskLifecycleState.Completed && terminal.Progress == 100 && outputs.Length == 1 &&
                 sourceUnchanged && decodedOutputs.Count == 1;
    return (new
    {
        protocol = "pixel-tart-real-file-validation/v1",
        mode = "raw",
        source = UserPath(source),
        extension = Path.GetExtension(source).ToUpperInvariant(),
        camera_make = decoded.Metadata.CameraMake,
        camera_model = decoded.Metadata.CameraModel,
        raw_dimensions = $"{decoded.Width}x{decoded.Height}",
        color_space = decoded.Metadata.ColorSpace,
        orientation = decoded.Metadata.Orientation,
        decoder = capability.DecoderName,
        libraw_runtime_version = capability.Version,
        wrapper_version = typeof(Sdcb.LibRaw.RawContext).Assembly.GetName().Version?.ToString(),
        decode_result = "Completed",
        jpeg_quality = options.JpegQuality,
        jpeg_result = outputs.Length == 1 ? "Completed" : "Failed",
        output_decode_verification = decodedOutputs.Count == 1,
        outputs = decodedOutputs,
        source_length_unchanged = sourceUnchanged,
        source_last_write_time_unchanged = sourceUnchanged,
        source_sha256_unchanged = sourceUnchanged,
        task_id = taskId,
        task_state = terminal?.State.ToString(),
        task_progress = terminal?.Progress,
        duration_ms = stopwatch.ElapsedMilliseconds,
        final_state = passed ? "Completed" : "Failed",
        generated_at = DateTimeOffset.UtcNow
    }, passed ? 0 : 1);
}

static async Task<(object Report, int ExitCode)> ValidateBatchAsync(string outputDirectory, IReadOnlyList<string> sources)
{
    if (sources.Count < 3) throw new ArgumentException("Batch validation requires at least three source images.");
    Directory.CreateDirectory(outputDirectory);
    var proofs = await Task.WhenAll(sources.Select(SourceProof.CreateAsync));
    var root = await ApplicationCompositionRoot.CreateAsync();
    var options = new BatchCompressionOptions(JpegQuality: 75, LongestEdge: 2400);
    var taskId = await root.BatchCompressionCoordinator.StartAsync(new(sources, outputDirectory, options));
    await root.BatchCompressionCoordinator.WaitForCompletionAsync(taskId);
    var terminal = await root.BatchCompressionCoordinator.GetTaskStateAsync(taskId);
    var outputFiles = Directory.GetFiles(outputDirectory, "*.jpg", SearchOption.TopDirectoryOnly);
    var outputs = new List<object>();
    foreach (var output in outputFiles)
    {
        using var stream = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        outputs.Add(new { path = ValidationPath(output), width = frame.PixelWidth, height = frame.PixelHeight, bytes = new FileInfo(output).Length });
    }
    var sourceUnchanged = (await Task.WhenAll(proofs.Select(proof => proof.IsUnchangedAsync()))).All(value => value);
    var passed = terminal?.State == TaskLifecycleState.Completed && terminal.Progress == 100 &&
                 outputFiles.Length == sources.Count && sourceUnchanged;
    return (new
    {
        protocol = "pixel-tart-real-file-validation/v1",
        mode = "batch",
        sources = sources.Select(UserPath).ToArray(),
        input_count = sources.Count,
        output_count = outputFiles.Length,
        jpeg_quality = options.JpegQuality,
        longest_edge = options.LongestEdge,
        outputs,
        output_decode_verification = outputs.Count == sources.Count,
        source_unchanged = sourceUnchanged,
        task_id = taskId,
        task_state = terminal?.State.ToString(),
        task_progress = terminal?.Progress,
        final_state = passed ? "Completed" : "Failed",
        generated_at = DateTimeOffset.UtcNow
    }, passed ? 0 : 1);
}

static async Task<(object Report, int ExitCode)> ValidateLocalSplitAsync(string outputDirectory, IReadOnlyList<string> sources)
{
    if (sources.Count < 6 || sources.Count % 2 != 0) throw new ArgumentException("Local split validation requires JPG/RAW pairs.");
    Directory.CreateDirectory(outputDirectory);
    var pairs = sources.Chunk(2).Select(pair => (Jpg: pair[0], Raw: pair[1])).ToArray();
    var proofs = await Task.WhenAll(sources.Select(SourceProof.CreateAsync));
    var log = new FileLogService(Path.Combine(outputDirectory, "logs"));
    var normalizer = new FileNameNormalizer();
    var sourceDirectories = sources.Select(Path.GetDirectoryName).Where(path => path is not null)
        .Distinct(StringComparer.OrdinalIgnoreCase).Select((path, priority) => new SourceDirectoryEntry
        { Path = path!, DirectoryType = SourceDirectoryType.Mixed, Priority = priority }).ToArray();
    var index = await new MediaIndexService(normalizer, log, cacheFilePath: Path.Combine(outputDirectory, "validation-index.json"))
        .ScanAsync(sourceDirectories, MediaExtensionPolicy.DefaultJpegExtensions.Concat(MediaExtensionPolicy.DefaultRawExtensions), null, CancellationToken.None);
    var selections = pairs.Select(pair => new MediaSelectionItem { OriginalInput = Path.GetFileName(pair.Jpg) }).ToArray();
    var decisions = await new MediaMatchService(normalizer).MatchAsync(selections, index,
        MediaMatchOptions.Default(CollectionCategory.JpegAndRaw), CancellationToken.None);
    foreach (var decision in decisions) selections.Single(item => item.Id == decision.ItemId).ApplyMatch(decision);

    var filesRoot = Path.Combine(outputDirectory, "files");
    var database = new PixelTartDatabase(Path.Combine(outputDirectory, "pixel-tart.db"));
    var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, Path.Combine(outputDirectory, "backups"))).MigrateAsync();
    if (!migration.Success) throw new InvalidOperationException(migration.ErrorMessage);
    var executor = new FileOperationExecutor(new FileOperationValidator(), new FileVerificationService(),
        new SqliteUndoJournalRepository(database), database);
    var summary = await new MediaCopyService(log, executor, new FileConflictResolver())
        .CopyAsync(selections, filesRoot, OutputMode.ByFileCategory, null, CancellationToken.None);
    foreach (var outcome in summary.Outcomes)
    {
        var item = selections.Single(selection => selection.Id == outcome.ItemId);
        var result = item.FormatResults.Single(format => format.Key == outcome.FormatKey);
        result.Status = outcome.Status;
        result.OutputPath = outcome.DestinationPath;
        result.ErrorMessage = outcome.ErrorMessage;
        result.OperationTime = outcome.OperationTime;
        item.RefreshOverallStatus();
    }
    await new MediaReportService(log).ExportAsync(filesRoot, CollectionCategory.JpegAndRaw, selections,
        CancellationToken.None, ReportExportOptions.Pro);
    var project = new PhotoProjectRecord
    {
        Name = "PixelTart_Validation", Status = PhotoProjectStatus.Completed,
        Category = CollectionCategory.JpegAndRaw, OutputMode = OutputMode.ByFileCategory,
        OutputDirectory = filesRoot, SelectionCount = selections.Length,
        MatchedFileCount = selections.Sum(item => item.MatchedFileCount), CopiedFileCount = summary.CopiedCount,
        ExportReports = true, ExportCsvReport = true, ExportJsonReport = true, ExportLogReport = true
    };
    project.SourceDirectories.AddRange(sourceDirectories.Select(source => source.Path));
    project.SelectionInputs.AddRange(selections.Select(item => item.OriginalInput));
    var projects = new SqliteProjectRepository(database);
    await projects.UpsertAsync(project);
    var restored = (await new SqliteProjectRepository(database).ListAsync()).SingleOrDefault(item => item.Id == project.Id);
    var diskOutputCount = Directory.GetFiles(filesRoot, "*", SearchOption.AllDirectories).Count(path =>
        !Path.GetFileName(path).StartsWith("匹配报告", StringComparison.Ordinal) &&
        !string.Equals(Path.GetFileName(path), "操作日志.txt", StringComparison.Ordinal));
    var sourceUnchanged = (await Task.WhenAll(proofs.Select(proof => proof.IsUnchangedAsync()))).All(value => value);
    var passed = summary.CopiedCount == sources.Count && summary.FailedCount == 0 && diskOutputCount == sources.Count &&
                 restored is not null && sourceUnchanged;
    return (new
    {
        protocol = "pixel-tart-real-file-validation/v1",
        mode = "local-split",
        selection_count = selections.Length,
        matched_file_count = decisions.Sum(decision => decision.MatchedFileCount),
        executor_success_count = summary.CopiedCount,
        executor_failed_count = summary.FailedCount,
        disk_output_count = diskOutputCount,
        report_csv = File.Exists(Path.Combine(filesRoot, "匹配报告.csv")),
        report_json = File.Exists(Path.Combine(filesRoot, "匹配报告.json")),
        report_log = File.Exists(Path.Combine(filesRoot, "操作日志.txt")),
        project_restored = restored is not null,
        restored_selection_count = restored?.SelectionInputs.Count,
        source_unchanged = sourceUnchanged,
        final_state = passed ? "Completed" : "Failed",
        generated_at = DateTimeOffset.UtcNow
    }, passed ? 0 : 1);
}

static async Task<(object Report, int ExitCode)> ValidateCollageAsync(string outputPath, IReadOnlyList<string> sources)
{
    if (sources.Count < 2) throw new ArgumentException("Collage validation requires at least two source images.");
    var proofs = await Task.WhenAll(sources.Select(SourceProof.CreateAsync));
    var project = new CollageProject { TemplateId = sources.Count >= 4 ? "4-grid" : "3-left-right-stack" };
    project.Export.PixelWidth = 1800;
    project.Export.PixelHeight = 1800;
    project.Export.JpegQuality = 90;
    project.Export.Format = "JPG";
    for (var index = 0; index < sources.Count; index++)
        project.Images.Add(new CollageImageState { SourcePath = sources[index], SlotId = (index + 1).ToString() });
    var result = await new CollageExportService().ExportAsync(project, outputPath);
    using var stream = new FileStream(result.OutputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
    var sourceUnchanged = (await Task.WhenAll(proofs.Select(proof => proof.IsUnchangedAsync()))).All(value => value);
    var passed = File.Exists(result.OutputPath) && result.FileSizeBytes > 0 && frame.PixelWidth == 1800 &&
                 frame.PixelHeight == 1800 && sourceUnchanged;
    return (new
    {
        protocol = "pixel-tart-real-file-validation/v1",
        mode = "collage",
        input_count = sources.Count,
        output = ValidationPath(result.OutputPath),
        output_bytes = result.FileSizeBytes,
        output_dimensions = $"{frame.PixelWidth}x{frame.PixelHeight}",
        output_decode_verification = true,
        source_unchanged = sourceUnchanged,
        final_state = passed ? "Completed" : "Failed",
        generated_at = DateTimeOffset.UtcNow
    }, passed ? 0 : 1);
}

static string UserPath(string path) => $"<USER_PATH>\\{Path.GetFileName(path)}";
static string ValidationPath(string path) => $"<VALIDATION_PATH>\\{Path.GetFileName(path)}";

file sealed record SourceProof(string Path, long Length, DateTime LastWriteTimeUtc, string Sha256)
{
    public static async Task<SourceProof> CreateAsync(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The local validation source is unavailable.");
        return new(path, info.Length, info.LastWriteTimeUtc, await HashAsync(path));
    }

    public async Task<bool> IsUnchangedAsync()
    {
        var info = new FileInfo(Path);
        return info.Exists && info.Length == Length && info.LastWriteTimeUtc == LastWriteTimeUtc &&
               string.Equals(await HashAsync(Path), Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
