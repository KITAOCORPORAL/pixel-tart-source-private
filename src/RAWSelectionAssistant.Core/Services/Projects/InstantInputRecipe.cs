namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Persisted full-resolution source recipe; never a preview bitmap or UI cache.</summary>
public sealed record InstantInputRecipe(int Version,string Mode,Guid? OriginVersionId,
    ReferenceLook? Look=null,ColorAdjustmentStack? Stack=null,PixelTartFilmSettings? Film=null);

