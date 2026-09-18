param([Parameter(Mandatory)][string]$BuildDirectory, [Parameter(Mandatory)][string]$InstallDirectory)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$installed = (Resolve-Path -LiteralPath $InstallDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $build 'installer\INSTALL_FILE_MANIFEST.json') -Raw | ConvertFrom-Json
$rows = foreach ($entry in $manifest.Files) {
    $publishPath = Join-Path (Join-Path $build 'publish') $entry.RelativePath
    $installedPath = Join-Path $installed $entry.RelativePath
    $publishHash = if (Test-Path -LiteralPath $publishPath) { (Get-FileHash -LiteralPath $publishPath).Hash } else { $null }
    $installedHash = if (Test-Path -LiteralPath $installedPath) { (Get-FileHash -LiteralPath $installedPath).Hash } else { $null }
    [ordered]@{ File=$entry.RelativePath; Publish=$publishHash; InstallerSource=$entry.SHA256; Installed=$installedHash; Status=$(if ($publishHash -eq $entry.SHA256 -and $installedHash -eq $entry.SHA256) {'MATCH'} elseif (-not $installedHash) {'PUBLISH_ONLY'} else {'HASH_MISMATCH'}) }
}
$extras = @(Get-ChildItem -LiteralPath $installed -File -Recurse | Where-Object { $_.FullName.Substring($installed.Length+1).Replace('\','/') -notin $manifest.Files.RelativePath } | ForEach-Object { [ordered]@{ File=$_.FullName.Substring($installed.Length+1); Status='INSTALLED_ONLY'; ExpectedUninstaller=$_.Name -match '^unins\d+\.(exe|dat|msg)$' } })
$deps = Get-Content -LiteralPath (Join-Path $installed 'PixelTart.deps.json') -Raw | ConvertFrom-Json -AsHashtable
$probes = foreach ($target in $deps.targets.Values) { foreach ($library in $target.Values) { foreach ($kind in 'runtime','native','resources','runtimeTargets') {
    if ($library.ContainsKey($kind)) { foreach ($relative in $library[$kind].Keys) {
        if ($relative.EndsWith('/_._')) { continue }
        $leaf = [IO.Path]::GetFileName($relative)
        $matches = @(Get-ChildItem -LiteralPath $installed -Recurse -File -Filter $leaf)
        [ordered]@{ Dependency=$relative; Kind=$kind; Present=$matches.Count -gt 0 }
    } }
} } }
$result = [ordered]@{ ProductSourceSha=$manifest.ProductSourceSha; Files=$rows; Extras=$extras; DependencyProbe=$probes; Pass=(@($rows | Where-Object Status -NE 'MATCH').Count -eq 0 -and @($extras | Where-Object ExpectedUninstaller -EQ $false).Count -eq 0 -and @($probes | Where-Object Present -EQ $false).Count -eq 0) }
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $build 'runtime-three-way-diff.json') -Encoding UTF8
if (-not $result.Pass) { throw 'Installed runtime differs from frozen publish/installer or dependencies are missing.' }
"Runtime manifest PASS: $($rows.Count) MATCH; dependency probes: $($probes.Count)."
