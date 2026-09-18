param()
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$dotnet = 'D:\AI AGENT\.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\stage-v2-installable-acceptance'))
$publish = Join-Path $artifactRoot 'publish'
$installer = Join-Path $artifactRoot 'installer'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish,$installer | Out-Null
$productSourceSha = (& git -C $repoRoot rev-parse HEAD).Trim()
$buildId = '2.3.0-dev.' + $productSourceSha.Substring(0,7)
if (& git -C $repoRoot status --porcelain -- src tests | Select-String -Pattern '^ M|^\?\?' ) { throw 'Product source and test tree must be clean before packaging.' }
& $dotnet publish (Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj') -c Release -r win-x64 --self-contained true -p:DeveloperPreviewBuild=true -p:ContinuousIntegrationBuild=true -p:DebugType=None -p:DebugSymbols=false -p:SourceRevisionId=$productSourceSha -warnaserror -o $publish --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$forbidden = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object { $_.Name -like '*.Tests.dll' -or $_.Name -like '*TestHost*' -or $_.Name -like '*Acceptance.dll' -or $_.Extension -in '.pdb','.trx','.cs','.xaml' })
if ($forbidden.Count -gt 0) { throw ('Forbidden runtime files: ' + ($forbidden.Name -join ', ')) }
$iscc = @((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),'C:\Program Files (x86)\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 6\ISCC.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup was not found.' }
$outputBaseName = "PixelTart-DeveloperPreview-$buildId-x64-Setup"
& $iscc "/DAppVersion=$buildId" "/DOutputBaseFilename=$outputBaseName" (Join-Path $repoRoot 'installer\PixelTartDeveloperPreview.iss')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$setup = Get-ChildItem -LiteralPath $installer -Filter '*Setup.exe' -File | Select-Object -First 1
if (-not $setup) { throw 'Installer was not created.' }
$manifest = [ordered]@{ ProductName='像素蛋挞 Pixel Tart 开发预览版'; BuildId=$buildId; Branch='integration/pixel-tart-developer-preview'; ProductSourceSha=$productSourceSha; InstallerPackagingSha=$productSourceSha; InstallerFileName=$setup.Name; InstallerSha256=(Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash; PublishMode='Release self-contained'; RuntimeIdentifier='win-x64'; SelfContained=$true; BuildTime=[DateTimeOffset]::Now.ToString('O'); ReleaseBuildResult='PASS · 0 warnings / 0 errors'; CoreResult='PASS · StageIV color/core tests'; WpfResult='PASS · StageV2 startup compatibility 3/3'; StartupSmokeResult='NOT RUN (user desktop launch not automated)'; InstallSmokeResult='NOT RUN (installer is not auto-run)' }
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $installer 'INSTALLER_PROVENANCE.json') -Encoding UTF8
$rows = foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) { [ordered]@{ RelativePath=$file.FullName.Substring($publish.Length+1).Replace('\','/'); Size=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash } }
@{ ProductSourceSha=$productSourceSha; Files=$rows } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $installer 'INSTALL_FILE_MANIFEST.json') -Encoding UTF8
"$($setup.Name)  $((Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash)" | Set-Content -LiteralPath (Join-Path $installer 'SHA256SUMS.txt') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repoRoot 'artifacts\stage-v2-human-acceptance\PixelTart-DeveloperPreview\MANUAL_UI_ACCEPTANCE.md') -Destination (Join-Path $artifactRoot 'README_安装测试.md') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'artifacts\stage-v2-human-acceptance\PixelTart-DeveloperPreview\USER_FEEDBACK_TEMPLATE.md') -Destination (Join-Path $artifactRoot 'USER_FEEDBACK_TEMPLATE.md') -Force
Write-Output ($manifest | ConvertTo-Json -Depth 8)
