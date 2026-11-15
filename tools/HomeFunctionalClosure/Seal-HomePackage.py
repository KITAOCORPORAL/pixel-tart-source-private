from pathlib import Path
import json,hashlib,shutil,sys
repo=Path(__file__).resolve().parents[2];artifacts=repo/'artifacts/home-20261115';context=json.loads((artifacts/'CONTEXT.json').read_text());package=Path(sys.argv[1]);source=sys.argv[2]
context['previousPackage']=context.get('previousPackage',context['package']);context['package']=str(package);context['productSource']=source;context['privateConnectionPath']=str(artifacts/'PRIVATE_CONNECTION.json')
(artifacts/'CONTEXT.json').write_text(json.dumps(context,ensure_ascii=False,indent=2),encoding='utf-8')
(package/'home-context.json').write_text(json.dumps(context,ensure_ascii=False,indent=2),encoding='utf-8')
for name in ['Start-Home.ps1','Start-HomeSchedule.ps1']:shutil.copyfile(repo/'tools/HomeFunctionalClosure'/name,package/name)
for name,args in [('Start-FullProduct-LocalTest.cmd','-ResumeStudio'),('Start-Guangjing-Diagnostic.cmd','-ResumeStudio'),('Start-SelectionService.cmd','-Service')]:
 (package/name).write_text('@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Home.ps1" '+args+'\n',encoding='utf-8')
(package/'Start-ScheduleService.cmd').write_text('@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-HomeSchedule.ps1"\n',encoding='utf-8')
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
(package/'BUILD_IDENTITY.json').write_text(json.dumps(dict(productSourceSha=source,base='bdc913c9463573247b9b6460f9a1102e40e74a31',functionalFix='be2f9c70',sourceFix='a9d1bb96',memoryOriginalCommits=['edd6d25e','e28074f1'],memoryIntegratedCommits=['17a95b5a','14eda5c7'],fullModuleDiagnostic=True,uiRedesign=False,setupInstaller=False,runtimePackaged=False),indent=2),encoding='utf-8')
files=[]
for path in sorted(package.rglob('*')):
 if not path.is_file() or path.name=='PACKAGE_FILES.json':continue
 assert path.suffix.lower() not in ['.key','.db','.sqlite'] and path.name not in ['PHONE_URL.txt','PRIVATE_CONNECTION.json']
 files.append(dict(path=path.relative_to(package).as_posix(),bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
(package/'PACKAGE_FILES.json').write_text(json.dumps(dict(source=source,package=str(package),files=files),ensure_ascii=False,indent=2),encoding='utf-8')
print('Full module package:',package,'files',len(files),'source',source)
