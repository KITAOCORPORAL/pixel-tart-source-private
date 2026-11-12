param(
 [string]$RuntimeRoot='',
 [string]$PackageRoot='',
 [string]$ProductSourceSha='',
 [string]$DotNetPath='',
 [int]$ServicePort=17662,
 [string]$PublicBaseUrl=''
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $RuntimeRoot){$RuntimeRoot=Join-Path $taskRoot 'artifacts/mobile-schedule/final-runtime'}
$RuntimeRoot=[IO.Path]::GetFullPath($RuntimeRoot)
if(-not(Test-Path -LiteralPath (Join-Path $RuntimeRoot 'identity.json'))){throw 'Seed the owned synthetic runtime before publishing'}
if(-not $PackageRoot){$PackageRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Guangjing/Deliveries/MobileScheduleV1-20261111'}
$PackageRoot=[IO.Path]::GetFullPath($PackageRoot)
if(-not $DotNetPath){$DotNetPath=(Get-Command dotnet -ErrorAction Stop).Source}
$DotNetPath=[IO.Path]::GetFullPath($DotNetPath)
$head=(& git -C $taskRoot rev-parse HEAD).Trim()
if(-not $ProductSourceSha){$ProductSourceSha=$head}
$source=(& git -C $taskRoot rev-parse "${ProductSourceSha}^{commit}").Trim()
if($LASTEXITCODE -ne 0){throw 'Product source commit is unavailable'}
$productPaths=@('src','extensions','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.config')
$differences=@(& git -C $taskRoot diff --name-only $source -- @productPaths)
if($LASTEXITCODE -ne 0 -or $differences.Count -gt 0){throw 'Working product/build inputs differ from the requested frozen source'}
$untracked=@(& git -C $taskRoot ls-files --others --exclude-standard -- src extensions)
if($untracked.Count -gt 0){throw 'Untracked product inputs would invalidate the frozen-source binding'}
New-Item -ItemType Directory -Force -Path $PackageRoot | Out-Null
$env:DOTNET_ROOT=Split-Path -Parent $DotNetPath
$env:PATH="$env:DOTNET_ROOT$([IO.Path]::PathSeparator)$env:PATH"
$common=@('-c','Release','-r','win-x64','--self-contained','true','-p:IncludeSourceRevisionInInformationalVersion=true',"-p:SourceRevisionId=$source",'--disable-build-servers','-m:1','-nr:false','-v:q')
& $DotNetPath publish (Join-Path $taskRoot 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj') @common -p:Platform=x64 -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -o (Join-Path $PackageRoot 'App')
if($LASTEXITCODE -ne 0){throw 'Complete diagnostic App publish failed'}
& $DotNetPath publish (Join-Path $taskRoot 'src/Guangjing.Schedule.Server/Guangjing.Schedule.Server.csproj') @common -o (Join-Path $PackageRoot 'ScheduleService')
if($LASTEXITCODE -ne 0){throw 'Self-contained schedule service publish failed'}
& $DotNetPath publish (Join-Path $taskRoot 'src/Guangjing.BrowserCapture.NativeHost/Guangjing.BrowserCapture.NativeHost.csproj') @common -o (Join-Path $PackageRoot 'native-host')
if($LASTEXITCODE -ne 0){throw 'Preserved browser native host publish failed'}
$argsForBinding=@((Join-Path $PSScriptRoot 'Write-DiagnosticPackage.py'),$PackageRoot,$RuntimeRoot,$source,$taskRoot,'--port',"$ServicePort",'--dotnet',$DotNetPath,'--builder-source',$head)
if($PublicBaseUrl){$argsForBinding+=@('--public-base-url',$PublicBaseUrl)}
& python @argsForBinding
if($LASTEXITCODE -ne 0){throw 'Package binding generation failed'}
