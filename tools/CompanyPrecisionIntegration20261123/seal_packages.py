from pathlib import Path
import json,hashlib,zipfile,sys,shutil
root=Path(sys.argv[1]);docs=Path(sys.argv[2]);context=json.loads((root/'CURRENT_CONTEXT.json').read_text('utf-8-sig'));stable=root/'Guangjing-Company-f84f8b2d-StableFull-win-x64';candidate=Path(context['package']);deliveries=[]
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
for label,pkg,source in [('CURRENT_STABLE',stable,'f84f8b2d9f68d668c6387089832e6f52cdd24dab'),('HELD_CANDIDATE',candidate,context['source'])]:
 embedded=pkg/'diagnostics-reports/company-precision-integration-20261123';embedded.mkdir(parents=True,exist_ok=True)
 for p in docs.iterdir():
  if p.is_file() and p.suffix.lower() in ['.md','.json','.trx'] and p.name not in ['EVIDENCE_INDEX.json']:shutil.copyfile(p,embedded/p.name)
 files=[]
 for p in sorted(pkg.rglob('*')):
  if not p.is_file() or p.name=='PACKAGE_FILES.json':continue
  if p.suffix.lower() in ['.db','.sqlite','.sqlite3','.key','.raf','.arw','.rgb48','.dmp','.nettrace'] or p.name.upper().startswith('PRIVATE_') or '_PRIVATE.' in p.name.upper() or p.name.endswith('.ptstudio.json'):raise RuntimeError('Unexpected runtime/private file '+str(p))
  if p.suffix.lower() in ['.json','.md','.ps1','.cmd','.txt','.html','.js']:
   text=p.read_text('utf-8-sig',errors='ignore')
   if 'https://login.tailscale.com/a/' in text or '#token=' in text and '/qa/start/' in text:raise RuntimeError('Private authlink in package '+str(p))
  files.append({'path':p.relative_to(pkg).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)})
 manifest={'source':source,'role':label,'currentProduct':label=='CURRENT_STABLE','compiledComponentsSourceBound':True,'runtimeBundled':False,'files':files};(pkg/'PACKAGE_FILES.json').write_text(json.dumps(manifest,indent=2)+chr(10),encoding='utf-8')
 archive=root/(pkg.name+'-20261123.zip')
 with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED) as z:
  for p in sorted(pkg.rglob('*')):
   if p.is_file():z.write(p,pkg.name+'/'+p.relative_to(pkg).as_posix())
 with zipfile.ZipFile(archive) as z:
  assert z.testzip() is None
  for row in files:
   with z.open(pkg.name+'/'+row['path']) as f:assert hashlib.file_digest(f,'sha256').hexdigest()==row['sha256']
 deliveries.append({'role':label,'source':source,'package':str(pkg),'entry':str(pkg/'Start-FullProduct-LocalTest.cmd') if label=='CURRENT_STABLE' else None,'archive':str(archive),'archiveBytes':archive.stat().st_size,'archiveSha256':sha(archive),'manifestFiles':len(files),'photosAndRuntimeBundled':False,'newRawDecodes':0,'releaseReady':False})
(root/'PACKAGE_DELIVERIES.json').write_text(json.dumps({'status':'STABLE_F84_RETAINED_CANDIDATE_HELD','primaryEntryCount':1,'deliveries':deliveries},indent=2)+chr(10),encoding='utf-8');print(json.dumps(deliveries))
