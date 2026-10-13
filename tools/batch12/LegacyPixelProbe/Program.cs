using System.Text.Json;
using RAWSelectionAssistant.Core.ValidationTools;
var results=LegacyPixelCorpus.Capture();
File.WriteAllText(args[0],JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"Captured {results.Count} reopened synthetic legacy recipe output hashes.");
