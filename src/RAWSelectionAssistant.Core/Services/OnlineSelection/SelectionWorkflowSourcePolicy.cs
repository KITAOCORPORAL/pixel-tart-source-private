namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

/// <summary>Rendered selection intermediates must not become an extra original source.</summary>
public static class SelectionWorkflowSourcePolicy
{
    public static List<string> Resolve(IEnumerable<string> existingSources, IEnumerable<string> selectedDirectories, string managedProjectPreparedRoot)
    {
        var prepared=Path.GetFullPath(managedProjectPreparedRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var original=existingSources.Where(p=>!string.IsNullOrWhiteSpace(p)).Where(p=>
        {
            var full=Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            return !string.Equals(full,prepared,StringComparison.OrdinalIgnoreCase)
                && !full.StartsWith(prepared+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return original.Count>0 ? original : selectedDirectories.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
