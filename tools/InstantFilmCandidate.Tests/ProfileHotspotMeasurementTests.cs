using System.Diagnostics;
using System.Text.Json;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass][DoNotParallelize]
public sealed class ProfileHotspotMeasurementTests
{
 public TestContext TestContext {get;set;}=null!;
 [TestMethod] public void MeasureRepeatedRegistryResolutionAgainstTaskLocalPalette()
 {
  var recipe=CandidateRecipe.Create("modern-itype-v3").Freeze();const int count=250000;var palette=recipe.Profile.Palette;double a=0,b=0;
  // Both paths intentionally executed; relative speed is evidence, never a machine-specific correctness assertion.
  for(int warm=0;warm<1000;warm++)_=recipe.Profile.Palette;
  var timer=Stopwatch.StartNew();for(int i=0;i<count;i++)a+=recipe.Profile.Palette.Length;var registry=timer.Elapsed.TotalMilliseconds;
  timer.Restart();for(int i=0;i<count;i++)b+=palette.Length;var frozen=timer.Elapsed.TotalMilliseconds;
  Assert.AreEqual(a,b);TestContext.WriteLine(JsonSerializer.Serialize(new{count,repeatedRegistryMs=registry,taskLocalPaletteMs=frozen,identicalSemanticResult=true,scope="Measured lookup only; not full render speedup claim"}));
 }
}
