from pathlib import Path
import sys,json,hashlib,shutil
package=Path(sys.argv[1]);runtime=Path(sys.argv[2]);source=sys.argv[3];repo=Path(sys.argv[4])
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
identity=json.loads((runtime/'identity.json').read_text(encoding='utf-8-sig'))
config=dict(packageRoot=str(package),runtimeRoot=str(runtime),bookingId=identity['bookingId'],servicePort=17662,publicBaseUrl='https://desktop-beiweiqu.taile9dfde.ts.net:8443',productSource=source)
(package/'mobile-schedule-runtime.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
app_script="""$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mobile-schedule-runtime.json') -Raw | ConvertFrom-Json
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=$config.runtimeRoot
& (Join-Path $PSScriptRoot 'App/KitaoPhotoSelector.exe') --mobile-schedule "--booking-id=$($config.bookingId)"
"""
service_script="""$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mobile-schedule-runtime.json') -Raw | ConvertFrom-Json
& dotnet (Join-Path $PSScriptRoot 'ScheduleService/Guangjing.Schedule.Server.dll') "--Schedule:Root=$($config.runtimeRoot)" "--Schedule:OwnerKeyFile=$($config.runtimeRoot)/schedule-owner.key" "--Schedule:Port=$($config.servicePort)" "--Schedule:PublicBaseUrl=$($config.publicBaseUrl)" "--contentRoot=$PSScriptRoot/ScheduleService"
"""
(package/'Start-MobileScheduleApp.ps1').write_text(app_script,encoding='utf-8')
(package/'Start-MobileScheduleService.ps1').write_text(service_script,encoding='utf-8')
for name,target in [('Start-Guangjing-Diagnostic.cmd','Start-MobileScheduleApp.ps1'),('Start-MobileScheduleApp.cmd','Start-MobileScheduleApp.ps1'),('Start-MobileScheduleService.cmd','Start-MobileScheduleService.ps1')]:
 (package/name).write_text('@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0'+target+'"\n',encoding='utf-8')
files=[dict(path=f.relative_to(package).as_posix(),bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in sorted(package.rglob('*')) if f.is_file() and f.name!='PACKAGE_FILES.json']
(package/'PACKAGE_FILES.json').write_text(json.dumps(dict(source=source,packageRoot=str(package),runtimeRoot=str(runtime),fullModuleDiagnostic=True,setupInstaller=False,browserRegistrationChanged=False,files=files),ensure_ascii=False,indent=2),encoding='utf-8')
print('Home diagnostic package:',package,'files:',len(files))
