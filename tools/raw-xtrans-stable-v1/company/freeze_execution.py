from pathlib import Path
import json,hashlib,os,sys
root=Path(sys.argv[1]);read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest().upper();identity=lambda p:dict(Path=str(Path(p).resolve()),Sha256=sha(p))
m=read(root/'NO_RAW_MANIFEST_02.json');selftest=root/'no-raw-selftest-02/NO_RAW_SELF_TEST.json';s=read(selftest);assert s['status']=='NATIVE_NO_RAW_SELF_TEST_COMPLETED' and s['zeroRaw'];assert s['nativeSha256']==m['Native']['Sha256'] and s['preparationSha256']==m['PreparationSha256'];assert s['nativeSelfTestEvidence']['syntheticExportReturnCode']==s['nativeSelfTestEvidence']['lifecycleExportReturnCode']==0;assert s['counts']=={'Initialize':1,'ManagedDispose':1};assert s['lifecycleEvidence']['managedDisposeReturned'] and s['lifecycleEvidence']['postDisposeAccessRejected'];assert s['runtime']['debuggerAttached'] is False
for f in m['Files']+[m['Native'],m['Binding'],m['Contract']]:assert sha(f['Path'])==f['Sha256']
for f in s['loadedDependencies']:
 assert sha(f['Path'])==f['Sha256']
 if not any(x['Path'].lower()==f['Path'].lower() for x in m['Files']):m['Files'].append(f)
company=Path(__file__).resolve().parent
for n in ['Program.cs','RawStableCandidateHost.csproj','CandidateBatch.mjs','Run-CandidateBatch.ps1','freeze_execution.py']:m['Files'].append(identity(company/n))
# Every historical consumed receipt stays unchanged; candidate uses a different ledger/task.
oldpaths=[Path('E:/Guangjing-RawReopen001-20261106/DELIVERY_BUDGET.json'),*[Path('E:/Guangjing-RawReopen001-NoRaw-20261109/validation-batch/ledger')/(s+'.consumed') for s in ['first','second']],*[Path('E:/Guangjing-RawReopen001-H1Window-20261109/batch-01/ledger')/(s+'.consumed') for s in ['first','second']]]
auth=root/'AUTHORIZATION.txt';assert auth.exists();batch=dict(task='RAW-REOPEN-001-STABLE-INPUT-V1-20261109',maximum=2,ledgerRoot=str(root/'ledger'),authorization=dict(path=str(auth),sha256=sha(auth).lower()),oldEvidence=[dict(path=str(p),sha256=sha(p).lower()) for p in oldpaths],failedAttemptsConsume=True,automaticRetry=False)
assert not (root/'ledger').exists(),'Do not reset or replace any candidate ledger'
config=root/'BATCH_CONFIG.json'
with config.open('x',encoding='utf-8',newline='\n') as f:json.dump(batch,f,indent=2);f.write('\n')
for p in [config,auth,*oldpaths]:m['Files'].append(identity(p))
oldmanifest=read('E:/Guangjing-RawReopen001-NoRaw-20261109/VALIDATION_MANIFEST01.json');inp=oldmanifest['Input'];assert sha(inp['Path'])==inp['Sha256']=='52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865'
m.update(Input=inp,NativeSelfTest=identity(selftest),ValidationBatchId=batch['task'],Purpose='EXPLICIT_ISOLATED_CANDIDATE_TWO_PROCESS_NO_DEBUGGER')
with (root/'EXECUTION_MANIFEST.json').open('x',encoding='utf-8',newline='\n') as f:json.dump(m,f,indent=2);f.write('\n')
# Bind already-stored P6 files by exact content; never create a new old-baseline decode.
base=Path('E:/Guangjing-RawReopen001-NoRaw-20261109/validation-batch/pair01');baseline=[]
for side in ['first','second']:
 rows=[json.loads(l) for l in (base/side/'native-events.jsonl').read_text().splitlines()];payload=next(r for r in rows if r.get('kind')=='payload' and r.get('id')=='P6');p=base/side/'P6.rgb.u16';assert p.exists() and p.stat().st_size==7752*5184*6;assert sha(p).lower()==payload['sha256'];baseline.append(dict(process=side,path=str(p),sha256=sha(p),width=7752,height=5184,channels=3))
(root/'STORED_BASELINE_IDENTITIES.json').write_text(json.dumps(baseline,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(nativeSelfTest='PASS',frozenFiles=len(m['Files']),oldConsumedFiles=len(oldpaths),oldP6PayloadsVerified=2,newRawCalls=0,debugger=False)))
