"""Seal the frozen full product and explicit acceptance gaps. No runtime credentials/databases are admitted."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil,subprocess,zipfile
repo=Path(__file__).resolve().parents[2];base=Path('E:/Kitao-Deliveries/root-cause-fixes-20261117');docs=repo/'docs/validation/root-cause-fixes-20261117';frozen=json.loads((base/'FROZEN_SOURCE.json').read_text('utf-8'));source=frozen['source'];package=Path(frozen['package']);archive=base/(package.name+'-20261117.zip')
def load(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(repo),*args],text=True,encoding='utf-8').strip()
assert not archive.exists(),'Never replace a sealed archive'
assert git('diff','--name-only',source,'HEAD','--','src','tests')=='' and git('diff','--name-only',source,'--','src','tests')==''
bindings=load(docs/'COMPONENT_BINDINGS.json');assert len(bindings)==8
for row in bindings:assert row['version']=='2.3.0+'+source and row['sha256']==sha(package/row['path'])
assert load(docs/'FINAL_REGRESSION.json')['passed']==52 and load(docs/'B1_FINAL_RESULT.json')['passCount']==17 and load(docs/'B0_GUARDED_RESULT.json')['status']=='PASS'
assert (package/'App/KitaoPhotoSelector.exe').read_bytes()==(package/'App/GuangjingRootCauseQA.exe').read_bytes()
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
startup=(repo/'tools/CompanyFunctionalIntegration/Start-OnlineSelection-LocalTest.ps1').read_text('utf-8-sig').replace('App/GuangjingCompanyBatch.exe','App/KitaoPhotoSelector.exe')
(package/'Start-OnlineSelection-LocalTest.ps1').write_text(startup,encoding='utf-8',newline='\n')
(package/'Start-FullProduct-LocalTest.cmd').write_text('@echo off\nPowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-OnlineSelection-LocalTest.ps1"\n',encoding='utf-8',newline='\r\n')
save(package/'BUILD_IDENTITY.json',dict(productSourceSha=source,reviewBase='f6655aff201b157a09ee37c46eb82596ff59028f',B0='f55825bb',B1='057d58ff',B2='eef3b35bd530ad060548ee1bde1a38f312e2e100',counterexampleRefinement=source,fullModuleDiagnostic=True,setupInstaller=False,privateRuntimeBundled=False,newRawDecodes=0,uiRedesign=False,ordinaryExeAcceptance='INCOMPLETE_USER_STOPPED_COMPUTER_USE',actualPhone='NOT_RUN_NEEDS_LOGIN',regressionChecks=52,lifecycleChecks=17))
(package/'README.md').write_text('''# Guangjing full-product root-cause repair diagnostic

Frozen product source: '''+source+'''

Start-FullProduct-LocalTest.cmd starts the full desktop and its local selection service in a new isolated data root under LOCALAPPDATA. The desktop executable is App/KitaoPhotoSelector.exe. To select a different empty or owned test root, invoke Start-OnlineSelection-LocalTest.ps1 -DataRoot <absolute path>. Existing identity-checked service/desktop instances are reused. Do not point multiple writers at a shared root. All four compiled components plus the browser extension are included; no installer or browser/system registration is performed.

B0 protects root ownership without terminating unknown processes. B1 coordinates close/save and waits for owned preview work. B2 requires a project name before rendering/creation and propagates explicit business results.

52 targeted regressions,17 lifecycle scenarios and guarded B0 ownership probes passed. An ordinary preceding candidate (eef3b35b) and its same-source service started, but its project/browser/export/reopen cycle was interrupted at the system folder picker when Computer Use reported physical Escape. The final433a1d8e binaries include the later counterexample refinements and were not started through Computer Use after the stop. This package does not claim the final ordinary cycle, ordinary40MP closing/reopen, or iPhone acceptance passed. See diagnostics-reports/root-cause-fixes-20261117/REPORT.md and ORDINARY_EXE_STATUS.json. The prior40MP pixel/ICC receipts remain bound to their original source; rendering and cache contracts are retained.

This is a diagnostic preview, not a release-ready installer. Private credentials, source images, runtime databases and raw traces are excluded. The existing QA session and private phone setup remain outside this package. Phone acceptance still needs personal Tailscale login. No new RAW batch was decoded.
''',encoding='utf-8',newline='\n')
# Canonical text ensures Git evidence and the packaged report have identical bytes.
for folder in [docs,repo/'tools/RootCauseFixAcceptance']:
 for p in folder.rglob('*'):
  if p.is_file() and not any(x in p.parts for x in ['bin','obj']) and (p.suffix.lower() in {'.md','.json','.txt','.trx','.cs','.csproj','.py','.ps1'} or p.name=='.gitattributes'):
   data=p.read_bytes();canonical=data.replace(b'\r\n',b'\n');
   if data!=canonical:p.write_bytes(canonical)
entries=[]
for p in sorted(docs.rglob('*')):
 if p.is_file() and p.name not in ['DELIVERY.json','PACKAGE_VALIDATION.json','EVIDENCE_INDEX.json','REMOTE_READBACK.json']:
  entries.append(dict(file=p.relative_to(docs).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
save(docs/'EVIDENCE_INDEX.json',dict(productSource=source,entries=entries))
reportTarget=package/'diagnostics-reports'/docs.name;reportTarget.mkdir(parents=True,exist_ok=True)
for p in docs.rglob('*'):
 if p.is_file() and p.name not in ['DELIVERY.json','PACKAGE_VALIDATION.json','REMOTE_READBACK.json']:
  target=reportTarget/p.relative_to(docs);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
shutil.copyfile(repo/'AGENTS.md',package/'AGENTS.md')
helper=package/'diagnostics/RootCauseFixAcceptance';helper.mkdir(parents=True,exist_ok=True)
for p in (repo/'tools/RootCauseFixAcceptance').iterdir():
 if p.is_file():shutil.copyfile(p,helper/p.name)
for name in ['Run-AppGuard.py','Checkpoint.py','Probe-JpegExports.py']:shutil.copyfile(repo/'tools/CompanyFunctionalIntegration'/name,package/'diagnostics'/name)
secrets=[]
for p in [base/'ordinary-qa/PRIVATE_CONNECTION.json']:
 if p.exists():secrets.append(load(p)['ownerKey'].encode())
for c in ['E:/Kitao-Deliveries/mobile-schedule-company-20261112/MOBILE_COMPANY_CONTEXT.json']:
 if Path(c).exists():
  phone=load(c);r=Path(phone['runtimeRoot']);
  for p,key in [(r/'PHONE_DEVICE_PRIVATE.json','token')]:
   if p.exists():secrets.append(load(p)[key].encode())
  if (r/'schedule-owner.key').exists():secrets.append((r/'schedule-owner.key').read_bytes().strip())
texttypes={'.json','.jsonl','.md','.txt','.ps1','.cmd','.py','.cs','.csproj','.xml','.js','.mjs','.html','.css','.config','.trx'}
files=[]
for p in sorted(package.rglob('*')):
 if not p.is_file() or p.name=='PACKAGE_FILES.json':continue
 rel=p.relative_to(package).as_posix();assert p.suffix.lower() not in {'.db','.sqlite','.sqlite3','.raf','.arw','.cr3','.dmp','.nettrace','.key'}
 assert not any(s in p.name.upper() for s in ['_PRIVATE','PHONE_URL','LOGIN_PRIVATE'])
 if p.suffix.lower() in texttypes:
  data=p.read_bytes();assert not re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}',data) and not any(s and len(s)>10 and s in data for s in secrets),'Secret detected in '+rel
 files.append(dict(path=rel,bytes=p.stat().st_size,sha256=sha(p)))
assert len(files)>1000
save(package/'PACKAGE_FILES.json',dict(productSource=source,fullModules=['App','SelectionService','ScheduleService','native-host','extension'],privateRuntimeBundled=False,files=files))
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in sorted(package.rglob('*')):
  if p.is_file():z.write(p,package.name+'/'+p.relative_to(package).as_posix())
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None and len(z.infolist())==len(files)+1
 for row in files:
  with z.open(package.name+'/'+row['path']) as f:assert hashlib.file_digest(f,'sha256').hexdigest()==row['sha256']
delivery=dict(atUtc=datetime.now(timezone.utc).isoformat(),status='REPAIRS_IMPLEMENTED_CONTROLLED_CHECKS_PASS_ORDINARY_ACCEPTANCE_INCOMPLETE',branch='fix/root-cause-b0-b1-b2-20261117',productSource=source,package=str(package),exe=str(package/'App/KitaoPhotoSelector.exe'),entry='Start-FullProduct-LocalTest.cmd',archive=str(archive),archiveBytes=archive.stat().st_size,archiveSha256=sha(archive),manifestFiles=len(files),archiveFiles=len(files)+1,compiledComponentsSameSource=True,checksPassed=52,lifecycleScenarios=17,ordinaryFullWorkflowPassed=False,ordinaryBlocker='Computer Use physical Escape stopped; system folder picker unconfirmed',phone='NeedsLogin',phoneActual=False,newRawDecodes=0,forcedGc=False,installer=False,releaseReady=False)
for p in [docs/'DELIVERY.json',base/'CURRENT_DELIVERY.json']:save(p,delivery)
save(docs/'PACKAGE_VALIDATION.json',dict(source=source,crcPassed=True,everyMemberHashPassed=True,componentVersionsPassed=True,apphostAliasEqual=True,knownCredentialsExcluded=True,privateDatabasesAndInputsExcluded=True,fullModules=5,fileCount=len(files)+1,archiveSha256=delivery['archiveSha256'],ordinaryAcceptance='INCOMPLETE',realDeviceAcceptance='NOT_RUN'))
print(json.dumps(delivery))
