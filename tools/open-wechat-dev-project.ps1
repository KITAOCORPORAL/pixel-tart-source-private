param([string]$CliPath)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\clients\wechat-mini-program'))
if (-not $CliPath) {
    $candidates = @(
        "$env:ProgramFiles(x86)\Tencent\微信web开发者工具\cli.bat",
        "$env:ProgramFiles\Tencent\微信开发者工具\cli.bat"
    )
    $CliPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CliPath -or -not (Test-Path -LiteralPath $CliPath)) {
    throw 'WeChat DevTools CLI was not found. Install/login is intentionally not automated.'
}
& $CliPath open --project $projectRoot
if ($LASTEXITCODE -ne 0) { throw "WeChat DevTools could not open the LocalDev mock project (exit $LASTEXITCODE)." }
