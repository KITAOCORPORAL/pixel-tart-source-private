using System.IO;using System.Text.Json;using RAWSelectionAssistant.Core.Utilities;
namespace RAWSelectionAssistant.Services;
public sealed record ViewingPreferences(bool LeftOpen=false,bool RightOpen=false,bool Sound=true,bool ReduceMotion=false)
{
 private static string PathName=>Path.Combine(AppDataPaths.Root,"viewing-user-preferences.json");
 public static ViewingPreferences Load(){try{return File.Exists(PathName)?JsonSerializer.Deserialize<ViewingPreferences>(File.ReadAllText(PathName))??new():new();}catch(Exception e)when(e is IOException or JsonException){return new();}}
 public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);var tmp=PathName+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(this));File.Move(tmp,PathName,true);}
}
