from pathlib import Path
import sys,json,hashlib,shutil,argparse
from urllib.parse import urlparse
parser=argparse.ArgumentParser(description="Bind the frozen diagnostic App and schedule service to explicit machine paths")
parser.add_argument("package");parser.add_argument("runtime");parser.add_argument("source");parser.add_argument("repo")
parser.add_argument("--port",type=int,default=17662);parser.add_argument("--public-base-url",default="");parser.add_argument("--dotnet",default="");parser.add_argument("--builder-source",default="")
a=parser.parse_args();package=Path(a.package).resolve();runtime=Path(a.runtime).resolve();repo=Path(a.repo).resolve();source=a.source
if not 1024<=a.port<=65535:raise ValueError("Service port must be a nonprivileged TCP port")
if runtime==package or package in runtime.parents:raise ValueError("Runtime databases and credentials must remain outside the distributable package")
identity=json.loads((runtime/'identity.json').read_text(encoding='utf-8-sig'))
existing={};config_file=package/'mobile-schedule-runtime.json'
if config_file.exists():existing=json.loads(config_file.read_text(encoding='utf-8-sig'))
public=a.public_base_url or existing.get('publicBaseUrl') or ('http://127.0.0.1:'+str(a.port))
uri=urlparse(public)
if not uri.hostname or uri.path not in ['', '/'] or uri.username or uri.password or uri.query or uri.fragment or (uri.scheme!='https'and not(uri.scheme=='http'and uri.hostname in['127.0.0.1','localhost'])):raise ValueError("Public base must be HTTPS, or explicit loopback while preflight is local")
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
(package/'diagnostics').mkdir(exist_ok=True)
shutil.copy2(repo/'tools/MobileScheduleAcceptance/BrowserTimeZoneObserver.py',package/'diagnostics/BrowserTimeZoneObserver.py')
shutil.copy2(repo/'tools/MobileScheduleAcceptance/Start-MobileScheduleObserver.ps1',package/'Start-MobileScheduleObserver.ps1')

service_self=(package/'ScheduleService/coreclr.dll').is_file()
config=dict(packageRoot=str(package),runtimeRoot=str(runtime),databasePath=str(runtime/'Data/pixel-tart.db'),bookingId=identity['bookingId'],servicePort=a.port,publicBaseUrl=public.rstrip('/'),productSource=source,builderSource=a.builder_source or source,dotnetExecutable=a.dotnet,serviceSelfContained=service_self,phoneHttpsConfigured=uri.scheme=='https',appointmentTimeZoneIana='Asia/Shanghai',pythonExecutable=sys.executable,browserObserverPort=17663)
config_file.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(package/'schedule-service-config.json').write_text(json.dumps({'Schedule':{'Root':str(runtime),'OwnerKeyFile':str(runtime/'schedule-owner.key'),'Port':a.port,'PublicBaseUrl':public.rstrip('/')},'ExpectedDatabase':config['databasePath'],'ProductSourceSha':source},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
app_script="""$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mobile-schedule-runtime.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$id=[Guid]::Parse($config.bookingId)
$start=New-Object Diagnostics.ProcessStartInfo
$start.FileName=Join-Path $PSScriptRoot 'App/KitaoPhotoSelector.exe'
$start.WorkingDirectory=Join-Path $PSScriptRoot 'App'
$start.UseShellExecute=$false
$start.EnvironmentVariables['PIXEL_TART_ISOLATED_RUNTIME']='1'
$start.EnvironmentVariables['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=$config.runtimeRoot
$start.Arguments='--mobile-schedule --booking-id='+$id.ToString()
$process=[Diagnostics.Process]::Start($start)
Write-Output ('Mobile schedule diagnostic App PID: '+$process.Id)
"""
service_script="""$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mobile-schedule-runtime.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$arguments=@("--Schedule:Root=$($config.runtimeRoot)","--Schedule:OwnerKeyFile=$($config.runtimeRoot)/schedule-owner.key","--Schedule:Port=$($config.servicePort)","--Schedule:PublicBaseUrl=$($config.publicBaseUrl)","--contentRoot=$PSScriptRoot/ScheduleService")
$exe=Join-Path $PSScriptRoot 'ScheduleService/Guangjing.Schedule.Server.exe'
if($config.serviceSelfContained -and (Test-Path -LiteralPath $exe)){& $exe @arguments}
else {
 if(-not $config.dotnetExecutable -or -not(Test-Path -LiteralPath $config.dotnetExecutable)){throw 'The bound .NET 10 host is missing; regenerate this machine configuration'}
 $env:DOTNET_ROOT=Split-Path -Parent $config.dotnetExecutable
 & $config.dotnetExecutable (Join-Path $PSScriptRoot 'ScheduleService/Guangjing.Schedule.Server.dll') @arguments
}
exit $LASTEXITCODE
"""
(package/'Start-MobileScheduleApp.ps1').write_text(app_script,encoding='utf-8')
(package/'Start-MobileScheduleService.ps1').write_text(service_script,encoding='utf-8')
for name,target in [('Start-Guangjing-Diagnostic.cmd','Start-MobileScheduleApp.ps1'),('Start-MobileScheduleApp.cmd','Start-MobileScheduleApp.ps1'),('Start-MobileScheduleService.cmd','Start-MobileScheduleService.ps1'),('Start-MobileScheduleObserver.cmd','Start-MobileScheduleObserver.ps1')]:
 (package/name).write_text('@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0'+target+'"\n',encoding='utf-8')
files=[]
for f in sorted(package.rglob('*')):
 if not f.is_file()or f.name=='PACKAGE_FILES.json':continue
 if f.suffix.lower()in['.key','.sqlite','.db']or f.name in['PHONE_URL.txt','AUTOMATION_URL.txt']:raise ValueError('Credential or runtime data leaked into package: '+f.name)
 files.append(dict(path=f.relative_to(package).as_posix(),bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
(package/'PACKAGE_FILES.json').write_text(json.dumps(dict(source=source,builderSource=a.builder_source or source,packageRoot=str(package),runtimeRoot=str(runtime),databasePath=config['databasePath'],fullModuleDiagnostic=True,serviceSelfContained=service_self,setupInstaller=False,browserRegistrationChanged=False,files=files),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Machine-bound diagnostic package:',package,'files:',len(files),'service self-contained:',service_self)
