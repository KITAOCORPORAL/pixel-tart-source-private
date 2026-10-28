namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Normalize effective legacy sidecar Film once, including inactive batch targets.</summary>
public static class ColorStudioEffectiveState
{
    public static ColorAdjustmentStack? Resolve(ReferenceLook? look, ColorAdjustmentStack? stack, PixelTartFilmSettings? film)
    {
        var settings = film ?? look?.Film;
        if (stack is { Nodes.Count: > 0 })
        {
            var saved = stack.DeepClone();
            if(saved.Graph is not null&&settings?.Enabled==true&&saved.Nodes.All(n=>n.Type!=ColorStudioNodeType.Film))throw new NotSupportedException("节点图不隐式追加旧胶片效果；请显式添加独立胶片节点。");
            return settings?.Enabled == true && saved.Nodes.All(n => n.Type != ColorStudioNodeType.Film)
                ? saved with { Nodes = [.. saved.Nodes, new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片", FilmSettings: settings)] }
                : saved;
        }
        if (settings?.Enabled != true) return null;
        return look is null ? new([new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片", FilmSettings: settings)])
            : ColorStudioLegacyMigration.Migrate(look with { Film = settings }).Stack;
    }
}
