using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PixelTart.InstalledAcceptance;

internal static partial class Program
{
    private static readonly string[] SelectorFields = ["IdSelector", "Name", "ControlType", "AutomationId", "AncestorAutomationId", "AncestorName", "AncestorControlType", "ScopeAnchorName", "DescendantName", "HelpText", "ExternalDialog", "ScopePath", "ScopePreset", "RequiredPattern", "RequireFocusable"];
    internal static Plan LoadPlan(string file)
    {
        var root = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        JsonObject? catalog = null;
        if (root["SelectorCatalog"] is { } catalogPath)
            catalog = JsonNode.Parse(File.ReadAllText(Child(Path.GetDirectoryName(Path.GetFullPath(file))!, catalogPath.GetValue<string>())))!.AsObject();
        void Expand(JsonObject step)
        {
            if (step["SelectorRef"] is not { } reference) return;
            var key = reference.GetValue<string>();
            var selector = catalog?[key]?.AsObject() ?? throw new InvalidDataException("Unknown selector reference: " + key);
            foreach (var field in SelectorFields)
            {
                if (step.ContainsKey(field)) throw new InvalidDataException("Selector override forbidden: " + key + "/" + field);
                if (selector[field] is { } value) step[field] = value.DeepClone();
            }
        }
        foreach (var step in root["Steps"]!.AsArray()) Expand(step!.AsObject());
        if (root["AuditCheckpoints"] is JsonObject checkpoints)
            foreach (var group in checkpoints) foreach (var step in group.Value!.AsArray()) Expand(step!.AsObject());
        return root.Deserialize<Plan>(Json) ?? throw new InvalidDataException("Empty plan");
    }
}
