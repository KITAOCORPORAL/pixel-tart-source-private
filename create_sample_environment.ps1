param(
    [string]$RootPath = (Join-Path $PSScriptRoot 'artifacts\acceptance\TestRaw')
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($RootPath)
$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
if ($root -eq [System.IO.Path]::GetPathRoot($root)) {
    throw '不能把磁盘根目录作为示例环境目录。'
}

$files = @(
    'ProjectA\DSC01234.ARW',
    'ProjectA\DSC01235.ARW',
    'ProjectB\DSC01236.ARW',
    'ProjectC\DSC01236.ARW',
    'ProjectC\IMG_3288.CR3'
)
foreach ($relativePath in $files) {
    $path = Join-Path $root $relativePath
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    if (-not (Test-Path $path)) { New-Item -ItemType File -Path $path | Out-Null }
}

$customerFile = Join-Path (Split-Path $root) '客户选片.txt'
@('DSC01234.JPG', '1235', '1236', 'IMG_3288.JPG', '9999') | Set-Content -LiteralPath $customerFile -Encoding utf8
Write-Output "RAW 示例目录：$root"
Write-Output "客户选片清单：$customerFile"
