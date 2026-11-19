from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,subprocess,re,zipfile,xml.etree.ElementTree as ET
repo=Path('E:/Kitao-Worktrees/root-cause-fixes-20261117');base=Path('E:/Kitao-Deliveries/root-cause-fixes-20261117');run=base/'tiff-433a1d8e-20261119';docs=repo/'docs/validation/root-cause-fixes-20261117';follow=docs/'tiff-memory-followup-20261119';frozen=json.loads((run/'MEMORY_REPAIR_SOURCE.json').read_text('utf-8'));source=frozen['source'];package=Path(frozen['package']);archive=base/(package.name+'-20261119.zip')
def load(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(p,x):Path(p).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(repo),*args],text=True,encoding='utf-8').strip()
assert not archive.exists() and git('diff','--name-only',source,'HEAD','--','src','tests')=='' and git('diff','--name-only',source,'--','src','tests')==''
bindings=load(follow/'COMPONENT_BINDINGS.json');assert len(bindings)==8
for row in bindings:assert row['version']=='2.3.0+'+source and sha(package/row['path'])==row['sha256']
verified=load(follow/'CANDIDATE_VERIFICATION.json');assert sha(repo/verified['rendererFile'])==verified['rendererSha256']
trx=run/'frozen-cb-tests/affected-memory.trx';counter=ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters');assert counter is not None and int(counter.attrib['passed'])==34 and int(counter.attrib['failed'])==0
(follow/'frozen-affected-memory.trx').write_bytes(trx.read_bytes().replace(b'\r\n',b'\n'))
save(follow/'FROZEN_SOURCE_CHECK.json',dict(source=source,rendererSha256=verified['rendererSha256'],diagnosticFixturesMatchCommittedRendererBytes=True,frozenRegressionPassed=34,normalWindowRetest='PENDING',sameProductLine=True))
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
for name in ['Run-AppGuard.py','Checkpoint.py']:shutil.copyfile(repo/'tools/CompanyFunctionalIntegration'/name,package/'diagnostics'/name)
startup=(repo/'tools/CompanyFunctionalIntegration/Start-OnlineSelection-LocalTest.ps1').read_text('utf-8-sig').replace('App/GuangjingCompanyBatch.exe','App/KitaoPhotoSelector.exe');(package/'Start-OnlineSelection-LocalTest.ps1').write_text(startup,encoding='utf-8',newline='\n')
(package/'Start-FullProduct-LocalTest.cmd').write_text('@echo off\nPowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-OnlineSelection-LocalTest.ps1"\n',encoding='utf-8',newline='\r\n')
save(package/'BUILD_IDENTITY.json',dict(productSourceSha=source,previousProductSource='433a1d8e88d839839e4f21341e3e2776696d33a5',fullModuleDiagnostic=True,setupInstaller=False,privateRuntimeBundled=False,change='Bounded scratch for narrowly admitted16-bit single-exposure raster; existing Core math unchanged',priorJpegMainlineEvidenceSource='433a1d8e',ordinaryTiffAfterUpdate='NOT_RUN_OPEN_RECOVERY_PENDING',controlledFixtureProcesses=3,affectedRegressionPassed=34,releaseReady=False))
(package/'README.md').write_text('# Guangjing root-cause repair full-product update\n\nSource: '+source+'\n\nThis updates the same root-cause repair branch. Start-FullProduct-LocalTest.cmd starts the complete desktop and local selection service in a source-specific isolated root; App/KitaoPhotoSelector.exe is the desktop executable. App, SelectionService, ScheduleService, native-host and extension are all included. No installer is generated.\n\nThe433 ordinary JPEG flow remains verified at433. Its first40MP exposure edit exceeded the2GiB guard; that STOP is preserved. This update changes only the16-bit single-exposure adapter scratch layout, reuses existing numerical code, and passed exact old-reference/full5184-row comparisons plus34 affected regressions. The repaired ordinary TIFF flow is still pending: do not count these controlled probes as desktop acceptance. See diagnostics-reports/root-cause-fixes-20261117/tiff-memory-followup-20261119.\n\nThe old433 archive is immutable historical evidence. Private images, databases, dumps, credentials and personal login links remain outside this package. Product status is PARTIAL; the current ordinary recovery session is under the separate tiff-cb8c1adc-20261119 run root.\n',encoding='utf-8',newline='\n')
for p in docs.rglob('*'):
 if p.is_file() and p.suffix.lower() in {'.md','.txt','.json','.trx'}:
  b=p.read_bytes();normalized=b.replace(b'\r\n',b'\n')
  if b!=normalized:p.write_bytes(normalized)
entries=[]
for p in sorted(docs.rglob('*')):
 if p.is_file() and p.name not in {'DELIVERY.json','PACKAGE_VALIDATION.json','EVIDENCE_INDEX.json','REMOTE_READBACK.json'}:entries.append(dict(file=p.relative_to(docs).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
save(docs/'EVIDENCE_INDEX.json',dict(productSource=source,previousMainlineSource='433a1d8e',scope='433 ordinaryJPEGpass/TIFFexposureSTOP plus bounded-storage repair and separate controlled validation; repaired ordinary TIFF pending',entries=entries))
target=package/'diagnostics-reports'/docs.name;target.mkdir(parents=True)
for p in docs.rglob('*'):
 if p.is_file() and p.name not in {'DELIVERY.json','PACKAGE_VALIDATION.json','REMOTE_READBACK.json'}:
  t=target/p.relative_to(docs);t.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,t)
shutil.copyfile(repo/'AGENTS.md',package/'AGENTS.md')
secret=[]
for p in [base/'acceptance-433a1d8e-20261117/PRIVATE_CONNECTION.json']:
 if p.exists():secret.append(load(p)['ownerKey'].encode())
p=base/'acceptance-433a1d8e-20261117/CUSTOMER_URL_PRIVATE.txt'
if p.exists():secret.append(p.read_text('utf-8-sig').split('#token=')[1].strip().encode())
m=load('E:/Kitao-Deliveries/mobile-schedule-company-20261112/MOBILE_COMPANY_CONTEXT.json');r=Path(m['runtimeRoot']);secret.append(load(r/'PHONE_DEVICE_PRIVATE.json')['token'].encode())
files=[]
for p in sorted(package.rglob('*')):
 if not p.is_file() or p.name=='PACKAGE_FILES.json':continue
 rel=p.relative_to(package).as_posix();assert p.suffix.lower() not in {'.db','.sqlite','.sqlite3','.raf','.arw','.cr3','.dmp','.nettrace','.key'}
 assert not any(s in p.name.upper() for s in ['_PRIVATE','PHONE_URL','LOGIN_PRIVATE'])
 if p.suffix.lower() in {'.md','.txt','.json','.jsonl','.py','.ps1','.cmd','.cs','.xml','.js','.html','.css','.trx','.config'}:
  b=p.read_bytes();assert not re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}',b) and not any(s and s in b for s in secret),'Private value in '+rel
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
delivery=dict(atUtc=datetime.now(timezone.utc).isoformat(),status='MEMORY_UPDATE_CONTROLLED_EXACTNESS_PASS_ORDINARY_TIFF_RETEST_PENDING',branch='fix/root-cause-b0-b1-b2-20261117',productSource=source,previousProductSource='433a1d8e88d839839e4f21341e3e2776696d33a5',package=str(package),exe=str(package/'App/KitaoPhotoSelector.exe'),entry='Start-FullProduct-LocalTest.cmd',archive=str(archive),archiveBytes=archive.stat().st_size,archiveSha256=sha(archive),manifestFiles=len(files),archiveFiles=len(files)+1,compiledComponentsSameSource=True,controlledFixtureProcesses=3,affectedRegressionPassed=34,ordinaryFullWorkflowPassed=False,priorOrdinaryJpegMainlinePassed=True,priorOrdinaryJpegEvidenceSource='433a1d8e',priorOrdinary40MpExposure='FAIL_2GIB_STOP_ON433',ordinaryRepaired40Mp='NOT_RUN_OPEN_WORKFILE_PENDING',ordinaryBlocker='Open recovered workfile in new revision normal dialog',phone='NeedsLogin_EXPIRED_CREDENTIAL',newRawDecodes=0,forcedGc=False,installer=False,releaseReady=False,fullModules=5,oneCurrentProductLine=True)
for p in [docs/'DELIVERY.json',base/'CURRENT_DELIVERY.json']:save(p,delivery)
save(follow/'PACKAGE_VALIDATION.json',dict(source=source,crcPassed=True,everyArchivedFileDigestPassed=True,componentVersionsPassed=True,manifestCount=len(files),archiveCount=len(files)+1,privateDumpAndInputsExcluded=True,knownCredentialsExcluded=True,ordinaryRepairedTiffPassed=False,releaseReady=False,archiveSha256=delivery['archiveSha256']))
print(json.dumps(delivery))
