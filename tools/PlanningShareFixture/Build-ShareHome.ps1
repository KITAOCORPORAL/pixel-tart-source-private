#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Delivery,
    [string]$Dotnet = 'dotnet.exe',
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
$repo = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '../..')).FullName
$root = [IO.Path]::GetFullPath($Delivery)
if (Test-Path -LiteralPath $root) { throw 'Choose a new delivery directory; an existing candidate will not be replaced.' }
if (!(Test-Path -LiteralPath (Split-Path -Parent $root) -PathType Container)) { throw 'The parent of the new delivery directory must exist.' }
$prefix = $repo.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($root.Equals($repo, [StringComparison]::OrdinalIgnoreCase) -or $root.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Place the delivery outside the checkout.' }
$dotnetPath = (Get-Command -Name $Dotnet -CommandType Application -ErrorAction Stop).Source
Push-Location -LiteralPath $repo
try {
    $version = (& $dotnetPath --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $version -ne '10.0.302') { throw "Expected SDK 10.0.302 from the repository root; got '$version'. Use -Dotnet with that SDK's complete path." }
    $head = (& git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[a-f0-9]{40}$') { throw 'Unable to verify source HEAD.' }
    $dirty = @(& git status --porcelain=v1 --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to verify checkout status.' }
    if ($dirty.Count -gt 0 -and !$AllowDirty) { throw 'Use a clean checkout. -AllowDirty produces an explicitly unfrozen diagnostic only.' }
    New-Item -ItemType Directory -Path $root | Out-Null
    function Publish([string]$Project, [string]$Part, [string[]]$Extra = @()) {
        & $dotnetPath publish $Project -c Release -r win-x64 --self-contained true -o (Join-Path $root $Part) @Extra
        if ($LASTEXITCODE -ne 0) { throw "$Part did not build; preserve the incomplete directory and use a new one after fixing the build." }
    }
    Publish 'tools/PlanningReview/PlanningReview.csproj' 'Service'
    Publish 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj' 'Desktop' @('-p:PlanningReviewPilot=true','-p:WholeAppDiagnosticBuild=true')
    Publish 'tools/PlanningShareFixture/ShareFixture.csproj' 'Fixture'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ApprovedSamples') -Destination $root -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'tools/PlanningReview/Start-ShareCandidate.ps1') -Destination $root
    Copy-Item -LiteralPath (Join-Path $repo 'tools/PlanningReview/Start-PlanningShare.cmd') -Destination $root
    $artifacts = [ordered]@{}
    foreach ($file in @('Service/PlanningReview.exe','Service/PlanningReview.dll','Desktop/KitaoPhotoSelector.exe','Desktop/KitaoPhotoSelector.dll','Fixture/ShareFixture.exe')) {
        $artifacts[$file] = (Get-FileHash -LiteralPath (Join-Path $root $file) -Algorithm SHA256).Hash
    }
    [ordered]@{ sdk = $version; sourceSha = $head; sourceState = $(if ($dirty.Count) { 'UNFROZEN_WORKTREE' } else { 'CLEAN' }); files = $artifacts } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'build-receipt.json') -Encoding utf8
    Write-Host "Built $root. Next: start the host only, then run the fixture before opening the desktop. See HOME_HANDOFF.md."
} finally { Pop-Location }
