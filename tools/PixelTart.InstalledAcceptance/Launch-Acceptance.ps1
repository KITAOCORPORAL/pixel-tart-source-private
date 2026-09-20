param([switch]$PreflightOnly)
# Windows PowerShell 5.1 compatible; packaging emits this file as UTF-8 BOM.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$kitRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$result = Join-Path $kitRoot 'acceptance-result'
$log = Join-Path $result 'acceptance-launch.log'
$runner = Join-Path $kitRoot 'PixelTart.InstalledAcceptance.exe'
$exitCode = 1
$runnerCode = 'NOT_STARTED'
$launchTime = Get-Date
function Write-Launch([string]$Message) {
    Write-Host $Message
    Add-Content -LiteralPath $log -Value (('[{0:O}] ' -f (Get-Date)) + $Message) -Encoding UTF8
}
function Safe-Path([string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw "非法相对路径：$Relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $kitRoot $Relative))
    if (-not $path.StartsWith($kitRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "路径越界：$Relative" }
    for ($part = $path; $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "不允许链接目录或文件：$Relative" }
    }
    return $path
}
function Check-Files([string[]]$Paths) {
    foreach ($relative in $Paths) {
        $path = Safe-Path $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "未找到文件：$relative，请解压完整 ZIP，不要单独运行 BAT。" }
        $entry = @($script:manifest | Where-Object { $_.RelativePath -eq $relative })
        if ($entry.Count -ne 1) { throw "KIT_MANIFEST.json 缺少或重复记录：$relative" }
        if ((Get-Item -LiteralPath $path).Length -ne $entry[0].Size -or (File-Hash $path) -ne $entry[0].SHA256) { throw "文件完整性校验失败：$relative，请重新解压原始 ZIP。" }
    }
}
function File-Hash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','') }
    finally { $hash.Dispose(); $stream.Dispose() }
}
function Check-Gate([string]$Label, [scriptblock]$Check) {
    try { & $Check; Write-Launch "$Label PASS" }
    catch { Write-Launch "$Label FAIL：$($_.Exception.Message)"; throw }
}
function Archive-Results {
    $zip = Join-Path $kitRoot 'acceptance-result.zip'
    if (Test-Path -LiteralPath $zip) {
        $previous = Join-Path $kitRoot ('acceptance-result.previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6) + '.zip')
        Move-Item -LiteralPath $zip -Destination $previous
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($result, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host "诊断包：$zip（不会自动上传）"
}
try {
    New-Item -ItemType Directory -Path $result -Force | Out-Null
    Write-Launch "Time=$($launchTime.ToString('O')); KIT_ROOT=$kitRoot"
    Write-Launch "Windows Version=$([Environment]::OSVersion.VersionString); Runner Exists=$(Test-Path -LiteralPath $runner)"
    $installerPaths = @('installer/PixelTart-DeveloperPreview-2.3.0-dev.8cb95e6-x64-Setup.exe','installer/PixelTart-DeveloperPreview-2.3.0-dev.8729d17-x64-Setup.exe')
    foreach ($path in $installerPaths) { Write-Launch "Installer Exists=$(Test-Path -LiteralPath (Join-Path $kitRoot $path)); $path" }
    $manifestPath = Safe-Path 'KIT_MANIFEST.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw '未找到 KIT_MANIFEST.json，请重新解压完整 ZIP。' }
    $script:manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Check-Gate '[1/7] Runner' { Check-Files @('PixelTart.InstalledAcceptance.exe') }
    Check-Gate '[2/7] 依赖文件' {
        $required = @('PixelTart.InstalledAcceptance.dll','PixelTart.InstalledAcceptance.runtimeconfig.json','PixelTart.InstalledAcceptance.deps.json','hostfxr.dll','hostpolicy.dll','coreclr.dll','System.Private.CoreLib.dll','poppler/pdfinfo.exe','poppler/pdftoppm.exe','Launch-Acceptance.ps1','运行安装版验收.bat')
        Check-Files $required
        Check-Files @($script:manifest | Where-Object { $_.RelativePath -notmatch '(^installer[/\\]|\.plan\.json$)' } | ForEach-Object { $_.RelativePath })
    }
    Check-Gate '[3/7] Full Plan' {
        Check-Files @('planning-full.plan.json')
        $raw = Get-Content -LiteralPath (Join-Path $kitRoot 'planning-full.plan.json') -Raw -Encoding UTF8
        if ($raw -match '(?i)D:\\|AI AGENT|worktrees|pixel-tart-developer-preview-rc6') { throw 'PORTABILITY BUG：计划包含开发机路径。' }
        $script:fresh = $raw | ConvertFrom-Json
        if (-not $script:fresh.FullPlan -or $script:fresh.Mode -ne 'fresh') { throw 'Full Plan 模式错误。' }
    }
    Check-Gate '[4/7] Upgrade Plan' {
        Check-Files @('upgrade-full.plan.json')
        $raw = Get-Content -LiteralPath (Join-Path $kitRoot 'upgrade-full.plan.json') -Raw -Encoding UTF8
        if ($raw -match '(?i)D:\\|AI AGENT|worktrees|pixel-tart-developer-preview-rc6') { throw 'PORTABILITY BUG：计划包含开发机路径。' }
        $script:upgrade = $raw | ConvertFrom-Json
        if (-not $script:upgrade.FullPlan -or $script:upgrade.Mode -ne 'upgrade') { throw 'Upgrade Plan 模式错误。' }
    }
    Check-Gate '[5/7] Installer' {
        foreach ($path in $installerPaths) { if (-not (Test-Path -LiteralPath (Safe-Path $path))) { throw "未找到 Pixel Tart Developer Preview 安装包。预期位置：$path" } }
        Check-Files @($script:fresh.InstallerPath, $script:upgrade.InstallerPath, $script:upgrade.OldInstallerPath)
        foreach ($pair in @(@($script:fresh.InstallerPath,$script:fresh.InstallerSha256),@($script:upgrade.OldInstallerPath,$script:upgrade.OldInstallerSha256))) {
            if ((File-Hash (Safe-Path $pair[0])) -ne $pair[1]) { throw "安装器 SHA256 不符：$($pair[0])" }
        }
    }
    Check-Gate '[6/7] 输出目录' { $null = Safe-Path 'acceptance-result/acceptance-launch.log'; Add-Content -LiteralPath $log -Value 'Output write probe PASS' -Encoding UTF8 }
    while (@(Get-Process -Name PixelTart -ErrorAction SilentlyContinue).Count -gt 0) {
        Write-Launch '[7/7] Pixel Tart 运行状态 WAIT：检测到 Pixel Tart 正在运行。请先正常关闭正在使用的 Pixel Tart，避免干扰自动验收。不会强制关闭任何实例。'
        if ($PreflightOnly) { throw '仅预检检测到冲突实例；未开始验收，请关闭后重试。' }
        Write-Host '[重试] 关闭窗口后按任意键重新检查；Ctrl+C 取消。'
        $null = [Console]::ReadKey($true)
    }
    Write-Launch '[7/7] Pixel Tart 运行状态 PASS：未发现冲突实例。'
    $probe = @(& $runner --kit $kitRoot --preflight-only 2>&1)
    $probeCode = $LASTEXITCODE
    foreach ($line in $probe) { Write-Launch ([string]$line) }
    Write-Launch "Runner preflight ExitCode=$probeCode"
    if ($probeCode -ne 0) { throw 'Runner 预检启动失败，详情见启动日志。' }
    if ($PreflightOnly) {
        $runnerCode = $probeCode
        Write-Launch 'PORTABLE PREFLIGHT PASS：已在 UI 自动化前停止。未安装、未启动 Pixel Tart、未提升权限。'
        $exitCode = 0
    } else {
        Write-Launch '准备完成。接下来自动：1.安装隔离版本 2.启动 Pixel Tart 3.操作策划中心 4.截图与 PDF 5.重启 6.升级。'
        Write-Launch '测试过程中请暂时不要操作鼠标和键盘。当前原始安装器要求管理员权限，下一步才请求提升。'
        Write-Host '按任意键开始（Ctrl+C 取消）...'
        $null = [Console]::ReadKey($true)
        $arguments = '--kit "' + $kitRoot + '"'
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            $process = Start-Process -FilePath $runner -ArgumentList $arguments -WorkingDirectory $kitRoot -Wait -PassThru -NoNewWindow
        } else {
            $process = Start-Process -FilePath $runner -ArgumentList $arguments -WorkingDirectory $kitRoot -Verb RunAs -Wait -PassThru
        }
        $runnerCode = $process.ExitCode
        if ($runnerCode -ne 0) {
            $latest = Get-ChildItem -LiteralPath $result -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($latest) {
                foreach ($mode in @('fresh','upgrade')) {
                    $evidence = Join-Path $latest.FullName "$mode\acceptance.json"
                    if (Test-Path -LiteralPath $evidence) {
                        $run = Get-Content -LiteralPath $evidence -Raw -Encoding UTF8 | ConvertFrom-Json
                        $failed = @($run.Steps | Where-Object Status -eq BLOCKED)
                        if ($failed.Count) {
                            Write-Launch "验收在第 $(@($run.Steps).Count) 个已记录步骤（$($failed[0].Step)）停止。"
                            Write-Launch ('失败原因：' + (($failed[0].Error -split "`r?`n")[0]))
                            Write-Launch "详细日志：$(Join-Path $latest.FullName "$mode\uia.jsonl")"
                        }
                    }
                }
            }
            throw "验收未通过（Runner 返回 $runnerCode）。请回传诊断包，不需要手工补点或补截图。"
        }
        Write-Launch '========================================'
        Write-Launch '验收运行完成；技术流程通过，截图与 PDF 视觉审查仍待完成。'
        $exitCode = 0
    }
} catch {
    $message = $_.Exception.Message
    if ($_.Exception.NativeErrorCode -eq 1223) { $message = '管理员授权已取消；尚未开始正式验收。' }
    Write-Host "[FAIL] $message"
    try { Add-Content -LiteralPath $log -Value "Error=$message`r`n$($_.ToString())" -Encoding UTF8 } catch {
        $log = Join-Path ([IO.Path]::GetTempPath()) ('PixelTart-acceptance-launch-' + [Guid]::NewGuid().ToString('N') + '.log')
        "Time=$(Get-Date -Format O); KIT_ROOT=$kitRoot; Windows Version=$([Environment]::OSVersion); Runner Exists=$(Test-Path -LiteralPath $runner); Installer Exists=UNKNOWN; Runner ExitCode=$runnerCode; Error=$message" | Set-Content -LiteralPath $log -Encoding UTF8
        Write-Host "原目录不可写，备用日志：$log"
    }
} finally {
    try {
        Add-Content -LiteralPath $log -Value "Runner ExitCode=$runnerCode; Launcher ExitCode=$exitCode" -Encoding UTF8
        Write-Host "结果目录：$result"
        Archive-Results
    } catch { Write-Host "[FAIL] 诊断包压缩失败：$($_.Exception.Message)。请保留结果目录。" }
}
exit $exitCode
