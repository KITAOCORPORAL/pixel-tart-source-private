[CmdletBinding()]
param([string]$RepositoryRoot = '')

$ErrorActionPreference = 'Stop'
$root = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path } else { (Resolve-Path $RepositoryRoot).Path }
$generatedFromHead = (& git -C $root rev-parse HEAD).Trim()
$branch = (& git -C $root branch --show-current).Trim()
$generatedAt = [DateTimeOffset]::UtcNow.ToString('o')

$definitionPath = Join-Path $root 'docs/current/capabilities.definition.json'
$definition = Get-Content -LiteralPath $definitionPath -Raw | ConvertFrom-Json
$validImplementation = @('IMPLEMENTED','PARTIAL','BLOCKED','DEFERRED','SPEC_ONLY','NOT_IMPLEMENTED')
$validVerification = @('PASS','FAIL','NOT_RUN','NOT_APPLICABLE')
foreach ($capability in $definition.capabilities) {
  if ($capability.implementation_status -notin $validImplementation) { throw "Invalid implementation_status for $($capability.id)" }
  if ($capability.verification_status -notin $validVerification) { throw "Invalid verification_status for $($capability.id)" }
  foreach ($path in @($capability.evidence,$capability.required_tests,$capability.acceptance_artifacts)) {
    foreach ($relative in $path) {
      if (-not (Test-Path -LiteralPath (Join-Path $root $relative))) { throw "Missing evidence path for $($capability.id): $relative" }
    }
  }
  if ($capability.implementation_status -eq 'IMPLEMENTED' -and $capability.verification_status -eq 'PASS' -and @($capability.evidence).Count -eq 0) { throw "Implemented capability has no evidence: $($capability.id)" }
}

$manifest = [ordered]@{ generated_at=$generatedAt; generated_from_head=$generatedFromHead; branch=$branch; capabilities=$definition.capabilities }
$current = Join-Path $root 'docs/current'
New-Item -ItemType Directory -Force -Path $current | Out-Null
$json = $manifest | ConvertTo-Json -Depth 8
Set-Content -LiteralPath (Join-Path $current 'PIXEL_TART_CURRENT_CAPABILITIES.json') -Value $json -Encoding utf8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Pixel Tart current capability state')
$lines.Add('')
$lines.Add(('Generated: {0}' -f $generatedAt))
$lines.Add(('Generated from source HEAD: {0}' -f $generatedFromHead))
$lines.Add(('Branch: {0}' -f $branch))
$lines.Add('')
$lines.Add('| Capability | Implementation | Verification | Evidence | Next gate |')
$lines.Add('|---|---|---|---|---|')
foreach ($entry in $definition.capabilities) {
  $evidence = (@($entry.evidence) -join '<br>')
  $lines.Add(('| {0} | {1} | {2} | {3} | {4} |' -f $entry.id, $entry.implementation_status, $entry.verification_status, $evidence, $entry.next_gate))
}
$lines.Add('')
$lines.Add('This file is generated from the explicit mapping in `scripts/generate-current-capabilities.ps1`. Historical acceptance and roadmap documents remain preserved evidence, not the current source of truth.')
Set-Content -LiteralPath (Join-Path $current 'PIXEL_TART_CURRENT_STATE.md') -Value $lines -Encoding utf8
