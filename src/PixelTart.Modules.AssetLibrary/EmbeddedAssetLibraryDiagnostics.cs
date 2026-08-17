using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace PixelTart.Modules.AssetLibrary;

public sealed class PreviewImportDiagnostics
{
    [JsonPropertyName("picker_accepted")] public bool PickerAccepted { get; set; }
    [JsonPropertyName("selected_file_count")] public int SelectedFileCount { get; set; }
    [JsonPropertyName("import_command_entered")] public bool ImportCommandEntered { get; set; }
    [JsonPropertyName("import_service_entered")] public bool ImportServiceEntered { get; set; }
    [JsonPropertyName("imported_count")] public int ImportedCount { get; set; }
    [JsonPropertyName("skipped_count")] public int SkippedCount { get; set; }
    [JsonPropertyName("failed_count")] public int FailedCount { get; set; }
    [JsonPropertyName("repository_asset_count_before")] public int RepositoryAssetCountBefore { get; set; }
    [JsonPropertyName("repository_asset_count_after")] public int RepositoryAssetCountAfter { get; set; }
    [JsonPropertyName("current_query_count")] public int CurrentQueryCount { get; set; }
    [JsonPropertyName("view_model_item_count")] public int ViewModelItemCount { get; set; }
    [JsonPropertyName("asset_grid_item_count")] public int AssetGridItemCount { get; set; }
    [JsonPropertyName("items_source_instance")] public string ItemsSourceInstance { get; set; } = string.Empty;
    [JsonPropertyName("items_source_is_view_model_collection")] public bool ItemsSourceIsViewModelCollection { get; set; }
    [JsonPropertyName("collection_changed_count")] public int CollectionChangedCount { get; set; }
    [JsonPropertyName("data_context_type")] public string DataContextType { get; set; } = string.Empty;
    [JsonPropertyName("selected_collection")] public string SelectedCollection { get; set; } = string.Empty;
    [JsonPropertyName("thumbnail_queue_count")] public int ThumbnailQueueCount { get; set; }
    [JsonPropertyName("thumbnail_failure_count")] public int ThumbnailFailureCount { get; set; }
    [JsonPropertyName("source_kind")] public string SourceKind { get; set; } = string.Empty;
    [JsonPropertyName("scanned_extension_counts")] public Dictionary<string, int> ScannedExtensionCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}
internal sealed class PreviewImportDiagnosticsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string? _outputPath;
    private readonly object _gate = new();
    private readonly PreviewImportDiagnostics _snapshot = new();

    public PreviewImportDiagnosticsWriter(string? acceptanceRoot)
    {
        if (!string.IsNullOrWhiteSpace(acceptanceRoot))
            _outputPath = Path.Combine(Path.GetFullPath(acceptanceRoot), "InputDiagnostics", "asset-library-import.json");
    }

    public PreviewImportDiagnostics Snapshot => _snapshot;

    public void Save()
    {
        if (_outputPath is null) return;
        lock (_gate)
        {
            _snapshot.UpdatedAt = DateTimeOffset.UtcNow;
            var directory = Path.GetDirectoryName(_outputPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _outputPath + ".tmp";
            var json = JsonSerializer.Serialize(_snapshot, JsonOptions);
            File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, _outputPath, overwrite: true);
        }
    }

    public void SetSource(string kind, int selectedFileCount, IReadOnlyDictionary<string, int>? extensionCounts = null)
    {
        _snapshot.SourceKind = kind;
        _snapshot.SelectedFileCount = selectedFileCount;
        _snapshot.ScannedExtensionCounts = extensionCounts is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(extensionCounts, StringComparer.OrdinalIgnoreCase);
        Save();
    }

    public void SetViewState(int currentQueryCount, int viewModelItemCount, int assetGridItemCount)
    {
        _snapshot.CurrentQueryCount = currentQueryCount;
        _snapshot.ViewModelItemCount = viewModelItemCount;
        _snapshot.AssetGridItemCount = assetGridItemCount;
        _snapshot.ThumbnailQueueCount = AsyncThumbnail.PendingRequestCount;
        _snapshot.ThumbnailFailureCount = AsyncThumbnail.FailureCount;
        Save();
    }

    public void RecordCollectionChanged() => _snapshot.CollectionChangedCount++;

    public void SetBindingState(int assetGridItemCount, string itemsSourceInstance, bool itemsSourceIsViewModelCollection, string dataContextType, string selectedCollection)
    {
        _snapshot.AssetGridItemCount = assetGridItemCount;
        _snapshot.ItemsSourceInstance = itemsSourceInstance;
        _snapshot.ItemsSourceIsViewModelCollection = itemsSourceIsViewModelCollection;
        _snapshot.DataContextType = dataContextType;
        _snapshot.SelectedCollection = selectedCollection;
        _snapshot.ThumbnailQueueCount = AsyncThumbnail.PendingRequestCount;
        _snapshot.ThumbnailFailureCount = AsyncThumbnail.FailureCount;
        Save();
    }
}
