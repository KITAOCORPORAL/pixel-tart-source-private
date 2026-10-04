namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public sealed record VisualHistogram(uint[] R, uint[] G, uint[] B, uint[] Luma, ElevenZoneDistribution Zones);
