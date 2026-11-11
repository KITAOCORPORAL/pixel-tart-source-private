"""Prepare a source-bound successor draft. It remains unauthorized and cannot launch."""
from pathlib import Path
import argparse,hashlib,json
p=argparse.ArgumentParser();p.add_argument('old_root',type=Path);p.add_argument('draft_root',type=Path);p.add_argument('source_sha');a=p.parse_args();old=a.old_root.resolve();root=a.draft_root.resolve()
def load(path):return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
 with Path(path).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def save(name,value):
 with (root/name).open('x',encoding='utf-8',newline='\n') as f:json.dump(value,f,indent=2,ensure_ascii=False);f.write('\n')
assert (root/'app/KitaoPhotoSelector.dll').is_file() and (root/'worker/XTransStableProductWorker.dll').is_file()
assert (old/'ledger/STOP.json').is_file() and len(list((old/'ledger').glob('*.consumed')))==1
original=load(old/'PLAN-A.json');task='RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111'
assert sha(root/'app/raw_r.dll')==original['DefaultNativeSha256']
assert sha(root/'worker/raw_r.dll')==original['CandidateNativeSha256']
assert sha(root/'worker/Sdcb.LibRaw.dll')==original['BindingSha256']
save('AUTHORIZATION.json',dict(task=task,status='PENDING_USER_AUTHORIZATION',authorizedHeavyDecodes=0,proposedHeavyDecodes=6,oldBatchStopped=True,memoryDiagnosticNewRawDecodes=0))
old_binding=[dict(Path=str(old/'ledger/STOP.json'),Sha256=sha(old/'ledger/STOP.json'))]
for f in sorted((old/'ledger').glob('*.consumed')):old_binding.append(dict(Path=str(f),Sha256=sha(f)))
save('OLD_STOPPED_BATCH_BINDING.json',old_binding)
files=[]
for directory in ['app','worker']:
 for file in sorted((root/directory).rglob('*')):
  if file.is_file():files.append(dict(Path=str(file),Sha256=sha(file)))
for name in ['AUTHORIZATION.json','OLD_STOPPED_BATCH_BINDING.json']:
 file=root/name;files.append(dict(Path=str(file),Sha256=sha(file)))
files.extend(old_binding)
plans={}
for group in ['A','B','control']:
 plan=dict(original);plan.update(Version=2,Task=task,AuthorizedForHeavyDecode=False,ProductSourceSha=a.source_sha,CacheRoot=str(root/('cache-'+group)),EvidenceRoot=str(root/'path-evidence'),LedgerRoot=str(root/'ledger'),WorkerDll=str(root/'worker/XTransStableProductWorker.dll'),Files=files)
 save('PLAN-'+group+'.json',plan);plans[group]=dict(sha256=sha(root/('PLAN-'+group+'.json')),cacheRoot='cache-'+group)
lock=load(old/'SOURCE_AND_RUN_LOCK.json');lock.update(featureProductSourceSha=a.source_sha,memoryFixCommit='a42cb7cd9633ad065f632172ee137614af151324',rawTask=task,authorizedHeavyDecodes=0,maximumHeavyDecodes=6,status='DRAFT_PENDING_USER_AUTHORIZATION',originalStoppedTask=original['Task'],oldBatchStopSha256=sha(old/'ledger/STOP.json'))
save('SOURCE_AND_RUN_LOCK.json',lock)
save('DRAFT_RECEIPT.json',dict(status='DRAFT_PENDING_USER_AUTHORIZATION',task=task,sourceSha=a.source_sha,version=2,authorizedHeavyDecodes=0,proposedHeavyDecodes=6,plannedSteps=original['Steps'],frozenFiles=len(files),plans=plans,oldBatchStopPreserved=True,nativeCandidateReused=True,newNativeBuilds=0,newRawDecodes=0,files=[dict(path=Path(f['Path']).relative_to(root).as_posix() if Path(f['Path']).is_relative_to(root) else 'OLD_STOPPED_BATCH/'+Path(f['Path']).name,sha256=f['Sha256']) for f in files]))
print(json.dumps(dict(status='DRAFT_PENDING_USER_AUTHORIZATION',files=len(files),authorized=0,proposed=6,newRaw=0)))
