param()
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$productSourceSha = (& git -C $repoRoot rev-parse HEAD).Trim()
$buildId = '2.3.0-dev.' + $productSourceSha.Substring(0,7)
if (@(& git -C $repoRoot status --porcelain -- src tests installer build_stage_v2_installable.ps1).Count -gt 0) { throw 'Product, tests and packaging must be committed before packaging.' }
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\stage-v2-installed-startup-failure\builds\$buildId"))
$publish = Join-Path $artifactRoot 'publish'
$installer = Join-Path $artifactRoot 'installer'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if ((Test-Path -LiteralPath $publish) -or (Test-Path -LiteralPath $installer)) { throw 'Immutable build already exists; preserve it and use a new committed build.' }
New-Item -ItemType Directory -Force -Path $publish,$installer | Out-Null
& $dotnet publish (Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj') -c Release -r win-x64 --self-contained true -p:DeveloperPreviewBuild=true -p:ContinuousIntegrationBuild=true -p:DebugType=None -p:DebugSymbols=false -p:SourceRevisionId=$productSourceSha -warnaserror -o $publish --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$forbidden = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object { $_.Name -like '*.Tests.dll' -or $_.Name -like '*TestHost*' -or $_.Name -like '*Acceptance.dll' -or $_.Extension -in '.pdb','.trx','.cs','.xaml' })
if ($forbidden.Count -gt 0) { throw ('Forbidden runtime files: ' + ($forbidden.Name -join ', ')) }
foreach ($required in 'PixelTart.exe','PixelTart.dll','PixelTart.deps.json','PixelTart.runtimeconfig.json','PresentationFramework.dll','PresentationCore.dll','Microsoft.Data.Sqlite.dll','e_sqlite3.dll','coreclr.dll','hostfxr.dll','wpfgfx_cor3.dll') {
    if (-not (Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object Name -EQ $required)) { throw "Missing runtime dependency: $required" }
}
@{ BuildId=$buildId; ProductSourceSha=$productSourceSha } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish 'build-info.json') -Encoding UTF8
$iscc = @((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),'C:\Program Files (x86)\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 6\ISCC.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup was not found.' }
$outputBaseName = "PixelTart-DeveloperPreview-$buildId-x64-Setup"
& $iscc "/DAppVersion=$buildId" "/DOutputBaseFilename=$outputBaseName" "/DPublishDir=$publish" "/O$installer" (Join-Path $repoRoot 'installer\PixelTartDeveloperPreview.iss')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$setup = Get-ChildItem -LiteralPath $installer -Filter '*Setup.exe' -File | Select-Object -First 1
if (-not $setup) { throw 'Installer was not created.' }
$manifest = [ordered]@{ ProductName='像素蛋挞 Pixel Tart 开发预览版'; BuildId=$buildId; Branch='integration/pixel-tart-developer-preview'; ProductSourceSha=$productSourceSha; InstallerPackagingSha=$productSourceSha; InstallerFileName=$setup.Name; InstallerSha256=(Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash; PublishMode='Release self-contained'; RuntimeIdentifier='win-x64'; SelfContained=$true; BuildTime=[DateTimeOffset]::Now.ToString('O'); ReleaseBuildResult='PASS (warnings are errors)'; CoreResult='See recorded TRX, not inferred by packaging'; WpfResult='See recorded TRX, not inferred by packaging'; StartupSmokeResult='PENDING REAL INSTALLED GATE'; InstallSmokeResult='PENDING REAL INSTALLED GATE' }
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $installer 'INSTALLER_PROVENANCE.json') -Encoding UTF8
$rows = foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) { [ordered]@{ RelativePath=$file.FullName.Substring($publish.Length+1).Replace('\','/'); Size=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash } }
@{ ProductSourceSha=$productSourceSha; Files=$rows } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $installer 'INSTALL_FILE_MANIFEST.json') -Encoding UTF8
"$($setup.Name)  $((Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash)" | Set-Content -LiteralPath (Join-Path $installer 'SHA256SUMS.txt') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repoRoot 'artifacts\stage-v2-human-acceptance\PixelTart-DeveloperPreview\MANUAL_UI_ACCEPTANCE.md') -Destination (Join-Path $artifactRoot 'README_安装测试.md') -Force
$readme = Join-Path $artifactRoot 'README_安装测试.md'
$instructions = Get-Content -LiteralPath $readme -Raw
"请确认安装包文件名：$($setup.Name)`nSHA256：$($manifest.InstallerSha256)`n不要使用旧 67362b1 安装器。当前验收状态请查看 STAGE_V2R2_REAL_INSTALLED_STARTUP_CLOSURE.md。`n`n$instructions" | Set-Content -LiteralPath $readme -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repoRoot 'artifacts\stage-v2-human-acceptance\PixelTart-DeveloperPreview\USER_FEEDBACK_TEMPLATE.md') -Destination (Join-Path $artifactRoot 'USER_FEEDBACK_TEMPLATE.md') -Force
Write-Output ($manifest | ConvertTo-Json -Depth 8)
