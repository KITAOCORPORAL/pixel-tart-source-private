$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot 'references/polaroid-lab-print-corrector'
New-Item -ItemType Directory -Force -Path "$target/js" | Out-Null
$base = 'https://raw.githubusercontent.com/Kunkles/polaroid-lab-print-corrector/25ffdfa4c27dbbccb70d7a0037613f17d7d8bea2/'
foreach ($name in @('LICENSE','js/pipeline.js','js/lut.js')) {
    Invoke-WebRequest -Uri ($base + $name) -OutFile (Join-Path $target $name)
}
Write-Output 'Pinned MIT reference fetched. Read LICENSE. No presets were imported into the product.'
