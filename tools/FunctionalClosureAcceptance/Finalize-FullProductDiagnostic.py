from pathlib import Path
import argparse,json,subprocess,sys,shutil,hashlib
parser=argparse.ArgumentParser();parser.add_argument('--context',required=True);a=parser.parse_args()
c=json.loads(Path(a.context).read_text(encoding='utf-8-sig'));r=Path.cwd();package=Path(c['package']);base=Path(c['deliveryRoot']);mobile=json.loads(Path(c['mobileContext']).read_text(encoding='utf-8-sig'));product=c['currentProductSource']
if subprocess.check_output(['git','diff','--name-only',product,'--','src','extensions'],text=True).strip():raise RuntimeError('Republish any changed product inputs with a new functional SHA first')
if not(package/'SelectionService/PixelTart.SelectionApi.Server.exe').exists():raise RuntimeError('Missing self-contained selection backend')
subprocess.run([sys.executable,str(r/'tools/MobileScheduleAcceptance/Write-DiagnosticPackage.py'),str(package),mobile['runtimeRoot'],product,str(r),'--port','17662','--public-base-url',mobile['localBaseUrl'],'--dotnet',mobile['dotnetPath'],'--builder-source',c['startingSource']],check=True)
bootstrap=(r/'tools/online-http-v1/Start-LocalTest.ps1').read_text(encoding='utf-8-sig')
bootstrap=bootstrap.replace("$package=Split-Path -Parent $PSScriptRoot","$package=$PSScriptRoot").replace("'selection-service/PixelTart.SelectionApi.Server.exe'","'SelectionService/PixelTart.SelectionApi.Server.exe'")
bootstrap=bootstrap.replace("'OnlineHttpV1-c44f800a'","'FullProductFunctional-20261112'")
(package/'Start-OnlineSelection-LocalTest.ps1').write_text(bootstrap,encoding='utf-8')
(package/'BUILD_IDENTITY.json').write_text(json.dumps({'productSourceSha':product,'fullModuleDiagnostic':True,'setupInstaller':False,'uiRedesignApproved':False,'sourceInputsUnchanged':True},indent=2)+'\n',encoding='utf-8')

root=Path(c.get('fullProductLocalTestRoot',str(base/'full-product-localtest')))
for name,extra in [('Start-FullProduct-LocalTest.cmd',''),('Start-OnlineSelection-LocalTest.cmd',''),('Stop-OnlineSelection-LocalTest.cmd',' -Stop')]:
 text='@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-OnlineSelection-LocalTest.ps1" -DataRoot "'+str(root)+'"'+extra+'\n'
 (package/name).write_text(text,encoding='utf-8')
config={'productSource':product,'defaultFullProductEntry':'Start-FullProduct-LocalTest.cmd','workflowDataRoot':str(root),'existingNativeWorkflowRoot':c['workflowQaRuntime'],'phoneRuntimeRoot':mobile['runtimeRoot'],'phoneBookingId':mobile['bookingId'],'phoneMutatingProbesPaused':True,'separateSelectionBackendIncluded':True,'uiRedesignApproved':False}
(package/'full-product-runtime.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
files=[]
for f in sorted(package.rglob('*')):
 if not f.is_file()or f.name=='PACKAGE_FILES.json':continue
 if f.suffix.lower()in['.key','.db','.sqlite']or f.name.endswith('_PRIVATE.json')or f.name in['PHONE_URL.txt','AUTOMATION_URL.txt']:raise RuntimeError('Runtime data must remain outside the package')
 files.append({'path':f.relative_to(package).as_posix(),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
(package/'PACKAGE_FILES.json').write_text(json.dumps({'source':product,'fullModuleDiagnostic':True,'selectionServiceIncluded':True,'uiRedesignApproved':False,'files':files},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'package':str(package),'productSource':product,'files':len(files),'defaultEntry':'Start-FullProduct-LocalTest.cmd','runtimeDataNotPackaged':True},ensure_ascii=False))
