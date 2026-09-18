namespace RAWSelectionAssistant.Core.Services.Projects;

public static class ReferenceLookResolver
{
    public static Guid? Resolve(Guid? shotLookId, Guid? projectDefaultLookId, Guid? sessionLookId) =>
        shotLookId ?? projectDefaultLookId ?? sessionLookId;
}
