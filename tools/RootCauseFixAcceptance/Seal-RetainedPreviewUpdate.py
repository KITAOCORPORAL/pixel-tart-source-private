"""Seal the d07 continuation with explicit source/acceptance boundaries; private runtime is excluded."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil,subprocess,zipfile,xml.etree.ElementTree as ET
repo=Path(__file__).resolve().parents[2];base=Path('E:/Kitao-Deliveries/root-cause-fixes-20261117');run=base/'continuation-20261119';docs=repo/'docs/validation/root-cause-fixes-20261117';follow=docs/'retained-preview-followup-20261119';source='d07e4f4370c8e7d8505a46e3085cc6a069fb4bd4';package=base/'Guangjing-RootCause-d07e4f43-Full-win-x64';archive=base/(package.name+'-20261119.zip')
def load(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(p,x):Path(p).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(repo),*a],text=True,encoding='utf-8').strip()
assert not archive.exists() and not (package/'PACKAGE_FILES.json').exists(),'Do not replace sealed delivery'
assert git('diff','--name-only',source,'HEAD','--','src','tests')=='' and git('diff','--name-only',source,'--','src','tests')==''
bindings=load(follow/'COMPONENT_BINDINGS.json');assert len(bindings)==8
for r in bindings:assert r['version']=='2.3.0+'+source and sha(package/r['path'])==r['sha256']
assert load(follow/'REGRESSION.json')['passed']==41 and load(follow/'CONTROLLED_REPEAT_RESULT.json')['status']=='PASS'
resource=load(follow/'CONTROLLED_REPEAT_RESOURCE.json');assert resource['exitCode']==0 and not resource['stop'] and resource['peakPrivateBytes50ms']<2**31 and resource['osPeakWorkingSetBytes']<2**31
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
(package/'diagnostics').mkdir(exist_ok=True)
for name in ['Run-AppGuard.py','Checkpoint.py']:shutil.copyfile(repo/'tools/CompanyFunctionalIntegration'/name,package/'diagnostics'/name)
startup=(repo/'tools/CompanyFunctionalIntegration/Start-OnlineSelection-LocalTest.ps1').read_text('utf-8-sig').replace('App/GuangjingCompanyBatch.exe','App/KitaoPhotoSelector.exe');(package/'Start-OnlineSelection-LocalTest.ps1').write_text(startup,encoding='utf-8',newline='\n')
(package/'Start-FullProduct-LocalTest.cmd').write_text('@echo off\nPowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-OnlineSelection-LocalTest.ps1"\n',encoding='utf-8',newline='\r\n')
identity=dict(productSourceSha=source,previousProductSource='cb8c1adcde14c7092d0bd22ca692d6073d9efa09',fullModuleDiagnostic=True,setupInstaller=False,privateRuntimeBundled=False,affectedRegressionPassed=41,controlledRepeated40Mp='PASS_VIEWMODEL_HOST_NOT_ORDINARY',ordinaryTiff='NOT_LOADED_UI_AUTOMATION_TIMEOUT',priorOrdinaryJpeg='PASS_BOUND_TO433a1d8e',phone='LOCAL_SAME_SOURCE_READY_NEEDS_HUMAN_TAILSCALE_LOGIN',newRawDecodes=0,forcedGc=False,releaseReady=False)
save(package/'BUILD_IDENTITY.json',identity)
(package/'README.md').write_text('# Guangjing full product diagnostic continuation\n\nFrozen product source: '+source+'\n\nApp, SelectionService, ScheduleService, native-host and browser extension are included. Start-FullProduct-LocalTest.cmd starts desktop/local selection in an isolated source-specific root. No installer or browser registration is performed. Use App/KitaoPhotoSelector.exe for the ordinary desktop.\n\nThis revision releases retired full-resolution preview identity from bounded analysis caches and charges rendered cache entries by actual pixel format. Pixel processing and the cb8c1adc bounded exposure renderer are unchanged. 41 affected regressions passed; a controlled production ViewModel host completed repeated authorized40MP edits, undo/redo, save and a second ViewModel reopen under the unchanged2GiB/50ms guard. Peak PrivateBytes1321.35MiB. This is not ordinary desktop acceptance or independent-process reopen.\n\nThe cb8c1adc ordinary repeated-edit2GiB STOP is preserved. The new ordinary desktop has started but native automation timed out before opening its recovery workfile. Ordinary TIFF edits/save/close/independent-process reopen/two TIFF exports and ICC/depth/orientation remain pending. The433 JPEG full mainline remains verified at its own source. iPhone acceptance needs human Tailscale login; local same-source service and protected original synthetic booking/new credentials are prepared outside this package.\n\nPrivate images, databases, credentials, login URLs and memory dumps are excluded. Overall PARTIAL; see diagnostics-reports/root-cause-fixes-20261117/HANDOFF.md.\n',encoding='utf-8',newline='\n')
for p in docs.rglob('*'):
 if p.is_file() and p.suffix.lower() in {'.md','.txt','.json','.trx'}:
  data=p.read_bytes();normalized=data.replace(b'\r\n',b'\n')
  if data!=normalized:p.write_bytes(normalized)
entries=[]
for p in sorted(docs.rglob('*')):
 if p.is_file() and p.name not in {'DELIVERY.json','PACKAGE_VALIDATION.json','EVIDENCE_INDEX.json','REMOTE_READBACK.json'}:entries.append(dict(file=p.relative_to(docs).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
save(docs/'EVIDENCE_INDEX.json',dict(productSource=source,scope='Historical433 mainline/cb STOP retained; d07 analysis ownership/regression/controlled repeat; ordinary TIFF UI blocker and phone local preparation',entries=entries))
target=package/'diagnostics-reports'/docs.name;target.mkdir(parents=True)
for p in docs.rglob('*'):
 if p.is_file() and p.name not in {'DELIVERY.json','PACKAGE_VALIDATION.json','REMOTE_READBACK.json'}:
  t=target/p.relative_to(docs);t.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,t)
shutil.copyfile(repo/'AGENTS.md',package/'AGENTS.md')
secrets=[]
privatePhone=Path('E:/Kitao-Deliveries/mobile-schedule-company-20261112/runtime/PHONE_DEVICE_PRIVATE.json')
if privatePhone.exists():secrets.append(load(privatePhone)['token'].encode())
p=base/'acceptance-433a1d8e-20261117/PRIVATE_CONNECTION.json'
if p.exists():secrets.append(load(p)['ownerKey'].encode())
p=base/'acceptance-433a1d8e-20261117/CUSTOMER_URL_PRIVATE.txt'
if p.exists() and '#token=' in p.read_text('utf-8-sig'):secrets.append(p.read_text('utf-8-sig').split('#token=')[1].strip().encode())
files=[];texttypes={'.json','.jsonl','.md','.txt','.ps1','.cmd','.py','.cs','.csproj','.xml','.js','.mjs','.html','.css','.config','.trx'}
for p in sorted(package.rglob('*')):
 if not p.is_file() or p.name=='PACKAGE_FILES.json':continue
 rel=p.relative_to(package).as_posix();assert p.suffix.lower() not in {'.db','.sqlite','.sqlite3','.raf','.arw','.cr3','.dmp','.nettrace','.key'}
 assert not any(s in p.name.upper() for s in ['_PRIVATE','PHONE_URL','LOGIN_PRIVATE'])
 if p.suffix.lower() in texttypes:
  data=p.read_bytes();assert not re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}',data) and not any(s and len(s)>10 and s in data for s in secrets),'Secret detected: '+rel
 files.append(dict(path=rel,bytes=p.stat().st_size,sha256=sha(p)))
assert len(files)>1000
save(package/'PACKAGE_FILES.json',dict(productSource=source,fullModules=['App','SelectionService','ScheduleService','native-host','extension'],privateRuntimeBundled=False,files=files))
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in sorted(package.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(package))
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None and len(z.namelist())==len(files)+1
 for row in files:
  data=z.read(row['path']);assert len(data)==row['bytes'] and hashlib.sha256(data).hexdigest()==row['sha256']
validation=dict(productSource=source,archiveBytes=archive.stat().st_size,archiveSha256=sha(archive),manifestFiles=len(files),archiveFiles=len(files)+1,crcAndEveryFileHashVerified=True,compiledComponentsSameSource=True,ordinaryTiffPassed=False,phoneActualPassed=False,privateRuntimeBundled=False)
save(docs/'PACKAGE_VALIDATION.json',validation);save(follow/'PACKAGE_VALIDATION.json',validation)
delivery=dict(atUtc=datetime.now(timezone.utc).isoformat(),status='PARTIAL_CONTROLLED_REPEAT_PASS_ORDINARY_TIFF_UI_TIMEOUT_PHONE_LOGIN',branch='fix/root-cause-b0-b1-b2-20261117',productSource=source,previousProductSource='cb8c1adcde14c7092d0bd22ca692d6073d9efa09',package=str(package),exe=str(package/'App/KitaoPhotoSelector.exe'),entry='Start-FullProduct-LocalTest.cmd',archive=str(archive),archiveBytes=validation['archiveBytes'],archiveSha256=validation['archiveSha256'],manifestFiles=len(files),archiveFiles=len(files)+1,compiledComponentsSameSource=True,affectedRegressionPassed=41,controlledRepeated40Mp='PASS_SEPARATE_VIEWMODEL_REOPEN',controlledPeakPrivateBytes50ms=resource['peakPrivateBytes50ms'],ordinaryFullWorkflowPassed=False,priorOrdinaryJpegMainlinePassed=True,priorOrdinaryJpegEvidenceSource='433a1d8e',priorCbOrdinaryTiff='FAIL_2GIB_STOP',ordinaryRepaired40Mp='NOT_LOADED_NATIVE_UI_TIMEOUT',phone='NEEDSLOGIN_SAME_SOURCE_SERVICE_AND_LOCAL_DEVICE_READY',phoneServicePid=50824,newRawDecodes=0,forcedGc=False,installer=False,releaseReady=False,fullModules=5,oneCurrentProductLine=True,packageEvidenceSnapshot='Before package hash publication; PACKAGE_VALIDATION and DELIVERY are external post-seal receipts')
save(docs/'DELIVERY.json',delivery);save(base/'CURRENT_DELIVERY.json',delivery)
print(json.dumps(validation))
