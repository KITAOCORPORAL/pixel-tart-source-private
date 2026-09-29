[CmdletBinding()]
param([string]$RepositoryRoot = '')

$ErrorActionPreference = 'Stop'
$root = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path } else { (Resolve-Path $RepositoryRoot).Path }
$gitSha = (& git -C $root rev-parse HEAD).Trim()
$branch = (& git -C $root branch --show-current).Trim()
$generatedAt = [DateTimeOffset]::UtcNow.ToString('o')

# This table is intentionally explicit. The generator does not infer product state from prose.
$modules = [ordered]@{
  Photography = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs','tests/RAWSelectionAssistant.Tests/PhotographerWorkflowFoundationTests.cs'); nextGate='Native 2-Up walkthrough and action-target closure' }
  AssetLibrary = @{ status='IMPLEMENTED'; evidence=@('src/PixelTart.Modules.AssetLibrary/','tests/PixelTart.ModularHarness.Tests/'); nextGate='Existing acceptance maintenance' }
  Tether = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs','src/RAWSelectionAssistant/Views/TetherCaptureView.xaml'); nextGate='Native WPF review' }
  Compare = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Models/Photography/PhotographyCompareModels.cs','src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs','tests/RAWSelectionAssistant.Tests/PhotographerWorkflowFoundationTests.cs'); nextGate='Independent viewport/action integration' }
  Publishing = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/Publishing/','src/RAWSelectionAssistant/ViewModels/PublishingExportViewModel.cs','tests/RAWSelectionAssistant.Tests/PublishingCoreTests.cs'); nextGate='Live snapshot UI acceptance' }
  ColorStudio = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs','docs/color-studio/COLOR_STUDIO_CURRENT_STATUS.md'); nextGate='Historical closure gates' }
  MatchV3 = @{ status='IMPLEMENTED'; evidence=@('src/RAWSelectionAssistant.Core/Services/Projects/','docs/color-studio/'); nextGate='No new gate in this phase' }
  MatchV4 = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/Projects/','docs/color-studio/COLOR_MATCH_GPU_CURRENT_STATE_2026-09-28.md'); nextGate='Product correctness closure' }
  GPU = @{ status='PARTIAL'; evidence=@('src/PixelTart.MatchV4.Dx12/','docs/color-studio/COLOR_MATCH_GPU_CURRENT_STATE_2026-09-28.md'); nextGate='Full product integration' }
  RAW = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/RawToJpeg/','src/RAWSelectionAssistant.Core/Services/Raw/'); nextGate='Modern LibRaw and real corpus' }
  TIFF16 = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/Export/TiffExport.cs','src/RAWSelectionAssistant/Services/Publishing/WpfPublishingRenderer.cs'); nextGate='ICC/metadata/read-back closure' }
  Preset = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/Publishing/','src/RAWSelectionAssistant/ViewModels/PublishingExportViewModel.cs'); nextGate='Preset product consolidation' }
  Film = @{ status='SPEC_ONLY'; evidence=@('docs/design-system/19_FILM_CONTROLS.md','docs/research/OPEN_SOURCE_COLOR_ENGINE_PHASE0.md'); nextGate='Film Lab architecture' }
  ICC = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/Color/ColorProfileRegistry.cs','docs/color-studio/ICC_PIPELINE_AUDIT.md'); nextGate='Validated profile conversion' }
  '3DColorSpace' = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioColorSpace.cs','tests/RAWSelectionAssistant.Tests/ColorSpaceDataModelTests.cs'); nextGate='WPF renderer acceptance' }
  Planning = @{ status='IMPLEMENTED'; evidence=@('src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.cs'); nextGate='Roadmap maintenance' }
  OnlineSelection = @{ status='PARTIAL'; evidence=@('src/RAWSelectionAssistant/ViewModels/OnlineSelectionViewModels.cs'); nextGate='Product closure' }
  BrowserExtension = @{ status='DEFERRED'; evidence=@('docs/PIXEL_TART_PHOTOGRAPHER_OS_ROADMAP.md'); nextGate='Deferred by workflow correctness phase' }
}

$manifest = [ordered]@{ generated_at=$generatedAt; git_sha=$gitSha; branch=$branch; modules=$modules }
$current = Join-Path $root 'docs/current'
New-Item -ItemType Directory -Force -Path $current | Out-Null
$json = $manifest | ConvertTo-Json -Depth 8
Set-Content -LiteralPath (Join-Path $current 'PIXEL_TART_CURRENT_CAPABILITIES.json') -Value $json -Encoding utf8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Pixel Tart current capability state')
$lines.Add('')
$lines.Add(('Generated: {0}' -f $generatedAt))
$lines.Add(('HEAD: {0}' -f $gitSha))
$lines.Add(('Branch: {0}' -f $branch))
$lines.Add('')
$lines.Add('| Module | Status | Evidence | Next gate |')
$lines.Add('|---|---|---|---|')
foreach ($entry in $modules.GetEnumerator()) {
  $evidence = ($entry.Value.evidence -join '<br>')
  $lines.Add(('| {0} | {1} | {2} | {3} |' -f $entry.Key, $entry.Value.status, $evidence, $entry.Value.nextGate))
}
$lines.Add('')
$lines.Add('This file is generated from the explicit mapping in `scripts/generate-current-capabilities.ps1`. Historical acceptance and roadmap documents remain preserved evidence, not the current source of truth.')
Set-Content -LiteralPath (Join-Path $current 'PIXEL_TART_CURRENT_STATE.md') -Value $lines -Encoding utf8
