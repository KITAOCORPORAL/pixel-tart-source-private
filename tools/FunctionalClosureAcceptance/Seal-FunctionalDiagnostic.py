"""Seal owned diagnostics; keep runtime data and post-archive receipts outside the zip."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,shutil,subprocess,zipfile
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args()
cp=Path(a.context);c=json.loads(cp.read_text(encoding='utf-8-sig'));r=Path(c['worktree']);b=Path(c['deliveryRoot']);d=r/'docs/validation/product-functional-closure-20261112';package=Path(c['package'])
def save(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for chunk in iter(lambda:f.read(1048576),b''):h.update(chunk)
 return h.hexdigest()
changed=subprocess.check_output(['git','-C',str(r),'diff','--name-only',c['currentProductSource'],'--','src','extensions'],text=True)
if changed.strip():raise RuntimeError('Product changed: rebuild and retest before sealing')
# The old archive remains available; updated package receipts get a different archive name.
archive=b/'Guangjing-FullModule-Functional-Diagnostic-R2-win-x64-20261112.zip'
receipt=json.loads((d/'CURRENT_RECEIPT.json').read_text('utf-8-sig'));receipt['archiveFileName']=archive.name;save(d/'CURRENT_RECEIPT.json',receipt)
tools=[f for f in (r/'tools/FunctionalClosureAcceptance').iterdir() if f.is_file()]
tools += [r/'tools/MobileScheduleAcceptance/BrowserTimeZoneObserver.py',r/'tools/online-http-v1/Start-LocalTest.ps1']
save(d/'DIAGNOSTIC_TOOL_IDENTITY.json',{'productSource':c['currentProductSource'],'tools':[{'file':f.relative_to(r).as_posix(),'bytes':f.stat().st_size,'sha256':sha(f)} for f in sorted(tools)]})
post_seal={'EVIDENCE_INDEX.json','PACKAGE_DELIVERY.json','BUNDLE_VALIDATION.json','PUSH_READBACK.json'}
index={'sourceBase':c['startingSource'],'productSource':c['currentProductSource'],
 'scope':'Read-only current state, scoped real old receipts, and explicit automatic checks. No full new-project or phone acceptance inferred.',
 'artifacts':[], 'excluded':['runtime SQLite','owner keys','device tokens','Tailscale login links','unapproved UI drafts','post-seal package and push readback receipts']}
for f in sorted(d.rglob('*')):
 if f.is_file() and f.name not in post_seal and f.name!='.gitattributes':
  index['artifacts'].append({'file':f.relative_to(d).as_posix(),'bytes':f.stat().st_size,'sha256':sha(f)})
save(d/'EVIDENCE_INDEX.json',index)
reportdir=package/'diagnostics-reports/product-functional-closure-20261112';reportdir.mkdir(parents=True,exist_ok=True)
for f in d.rglob('*'):
 if f.is_file() and f.name not in {'PACKAGE_DELIVERY.json','BUNDLE_VALIDATION.json','PUSH_READBACK.json','.gitattributes'}:
  dest=reportdir/f.relative_to(d);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,dest)
tooldir=package/'diagnostics/functional-closure';tooldir.mkdir(parents=True,exist_ok=True)
for f in tools:
 if 'FunctionalClosureAcceptance' in f.parts:shutil.copyfile(f,tooldir/f.name)
shutil.copyfile(r/'tools/MobileScheduleAcceptance/BrowserTimeZoneObserver.py',package/'diagnostics/BrowserTimeZoneObserver.py')
build=json.loads((package/'BUILD_IDENTITY.json').read_text('utf-8-sig'));build.update(launcherCrossShellIdentityFix=True,observerHealthBindsRoundAndOrigin=True,diagnosticBuilderBase=c['startingSource']);save(package/'BUILD_IDENTITY.json',build)
entries=[]
for f in sorted(package.rglob('*')):
 if not f.is_file() or f.name=='PACKAGE_FILES.json':continue
 if f.suffix.lower() in ['.key','.db','.sqlite','.sqlite3'] or f.name.upper().endswith('_PRIVATE.JSON') or f.name in ['PRIVATE_CONNECTION.json','config-private.json','PHONE_URL.txt','AUTOMATION_URL.txt']:
  raise RuntimeError('Runtime/private data would enter package: '+str(f))
 entries.append({'path':f.relative_to(package).as_posix(),'bytes':f.stat().st_size,'sha256':sha(f)})
save(package/'PACKAGE_FILES.json',{'source':c['currentProductSource'],'builderBase':c['startingSource'],'fullModuleDiagnostic':True,'selectionServiceIncluded':True,'uiRedesignApproved':False,'files':entries})
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for f in sorted(package.rglob('*')):
  if f.is_file():z.write(f,f.relative_to(package.parent).as_posix())
result={'status':'SEALED_PARTIAL_FUNCTIONAL_DIAGNOSTIC','observedAtUtc':datetime.now(timezone.utc).isoformat(),
 'productSource':c['currentProductSource'],'startingSource':c['startingSource'],'package':str(package),
 'entry':'Start-FullProduct-LocalTest.cmd','archive':str(archive),'archiveBytes':archive.stat().st_size,'archiveSha256':sha(archive),
 'manifestListedFiles':len(entries),'archiveFiles':len(entries)+1,'uiDraftsApproved':0,'setupInstaller':False,
 'runtimeDataAndCredentialsIncluded':False,'fullNewProjectWorkflowCompleted':False,'phoneRealDeviceRoundtripCompleted':False,
 'postSealReceiptNotEmbeddedInItsOwnArchive':True}
save(d/'PACKAGE_DELIVERY.json',result);save(b/'CONTINUED_PACKAGE_ARCHIVE.json',result)
c.update(archive=str(archive),archiveSha256=result['archiveSha256'],packageFileCount=result['archiveFiles'],
 stage='CONTINUED_DIAGNOSTIC_SEALED_PHONE_AND_NATIVE_INPUT_PENDING');save(cp,c)
print(json.dumps(result,ensure_ascii=True))