using System.Text;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public enum NextCaptureAdjustment { None, PreviousRatingAndLabel, CurrentColorScheme, ProjectDefaultColorScheme }
public sealed record NextCaptureRule(string ProjectName="Project",bool IncludeProject=true,bool IncludeDate=true,string CustomPrefix="",int Counter=1,string? TargetFolder=null,NextCaptureAdjustment Adjustment=NextCaptureAdjustment.None)
{
    public string Example(DateOnly date){var parts=new List<string>();var prefix=Safe(CustomPrefix);var project=Safe(ProjectName);if(prefix.Length>0)parts.Add(prefix);if(IncludeProject&&project.Length>0)parts.Add(project);if(IncludeDate)parts.Add(date.ToString("yyyyMMdd"));parts.Add(Math.Max(1,Counter).ToString("D4"));return string.Join('_',parts);}
    private static string Safe(string? value){var invalid=Path.GetInvalidFileNameChars();var builder=new StringBuilder();foreach(var character in value?.Trim()??string.Empty)if(!invalid.Contains(character)&&character is not '_' and not ' ')builder.Append(character);return builder.ToString();}
}
