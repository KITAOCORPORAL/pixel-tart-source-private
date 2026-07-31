using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Models;

public sealed class ReportSettings
{
    [JsonPropertyName("defaultExportEnabled")]
    public bool DefaultExportEnabled { get; set; }

    [JsonPropertyName("defaultExportCsv")]
    public bool DefaultExportCsv { get; set; } = true;

    [JsonPropertyName("defaultExportJson")]
    public bool DefaultExportJson { get; set; }

    [JsonPropertyName("defaultExportLog")]
    public bool DefaultExportLog { get; set; }
}
