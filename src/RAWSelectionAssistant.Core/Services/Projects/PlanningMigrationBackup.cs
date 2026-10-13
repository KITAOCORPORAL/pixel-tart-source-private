using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;

internal static class PlanningMigrationBackup
{
    // Preserve the original byte stream as well as typed extension data. Never replace an existing backup.
    public static void PreserveLegacy(string path, bool shots)
    {
        if (!File.Exists(path)) return;
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = json.RootElement;
        var legacy = shots
            ? root.TryGetProperty("Shots", out var items) && items.EnumerateArray().Any(s => !s.TryGetProperty("PlanningFieldsVersion", out var v) || v.GetInt32() == 0)
            : root.TryGetProperty("Document", out var doc) && doc.ValueKind == JsonValueKind.Object && (!doc.TryGetProperty("FoundationVersion", out var version) || version.GetInt32() == 0);
        var backup = path + ".foundation-v0.bak";
        if (legacy && !File.Exists(backup)) File.Copy(path, backup, overwrite: false);
    }
}
