param([string]$V1 = 'D:\PixelTart-Installed-Acceptance-Kit-8cb95e6\acceptance-result\20260920-135509-417cc2')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$target = Join-Path $repo 'artifacts\installed-acceptance-history'
$sources = [ordered]@{
    v1 = $V1
    v2 = 'D:\PixelTart-Installed-Acceptance-Kit-8cb95e6\acceptance-result'
    v3 = (Join-Path $repo 'artifacts\acceptance-kit-v3\PixelTart-Installed-Acceptance-Kit-8cb95e6-v3\acceptance-result')
}
foreach ($version in $sources.Keys) {
    $destination = Join-Path $target $version
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $source = $sources[$version]
    if (!(Test-Path -LiteralPath $source)) {
        @{ Version=$version; Status='SOURCE_NOT_PRESENT'; Source=$source } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'preservation-status.json') -Encoding utf8
        continue
    }
    $rows = foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
        $copy = Join-Path $destination $relative
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if (Test-Path -LiteralPath $copy) {
            if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash) { throw "History collision: $copy" }
        } else {
            New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $copy
        }
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash) { throw "History copy mismatch: $copy" }
        [ordered]@{ Path=$relative; Bytes=$file.Length; SHA256=$hash }
    }
    @{ Version=$version; Status='HASH_VERIFIED_COPY'; Source=$source; Files=@($rows) } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'preservation-status.json') -Encoding utf8
    Write-Host "$version preserved: $(@($rows).Count) files"
}
