using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.Tethering;
public enum AnnotationConflictChoice { KeepLibrary, UsePhotographer, MergeNotes }
public sealed record AnnotationHandoffProposal(int Rating, string? ColorLabel, string PhotographerNote,
    string? ClientNote, IReadOnlyList<string> Conflicts);
public static class TetherAnnotationMerge
{
    // Customer fields remain product-owned. They are never folded into the library comment.
    public static AnnotationHandoffProposal Propose(AssetItem library, TetherAnnotationRecord capture,
        AnnotationConflictChoice? choice = null)
    {
        var conflicts = new List<string>();
        if (library.Rating != 0 && capture.Rating != 0 && library.Rating != capture.Rating) conflicts.Add("Rating");
        if (!string.IsNullOrWhiteSpace(library.ColorLabel) && !string.IsNullOrWhiteSpace(capture.ColorLabel) && library.ColorLabel != capture.ColorLabel) conflicts.Add("ColorLabel");
        if (!string.IsNullOrWhiteSpace(library.Comment) && !string.IsNullOrWhiteSpace(capture.PhotographerNote) && library.Comment != capture.PhotographerNote) conflicts.Add("PhotographerNote");
        var incoming = choice == AnnotationConflictChoice.UsePhotographer;
        var rating = incoming || library.Rating == 0 ? capture.Rating : library.Rating;
        var color = incoming || string.IsNullOrWhiteSpace(library.ColorLabel) ? capture.ColorLabel : library.ColorLabel;
        var note = incoming || string.IsNullOrWhiteSpace(library.Comment) ? capture.PhotographerNote ?? "" : library.Comment;
        if (choice == AnnotationConflictChoice.MergeNotes && !string.IsNullOrWhiteSpace(capture.PhotographerNote))
            note = string.Join(Environment.NewLine, new[] { library.Comment, capture.PhotographerNote }.SelectMany(x => x.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)).Distinct());
        return new(rating, color, note, capture.ClientNote, choice is null ? conflicts : []);
    }
}
