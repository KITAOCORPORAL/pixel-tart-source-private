"""At most two new, separately reserved RAW processes; no automatic retry/refund."""
from pathlib import Path
import json,hashlib,subprocess,sys,os,uuid,datetime
ROOT=Path(sys.argv[1]);HERE=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
gate=read(ROOT/'GATE.json');assert gate['admitted'] and gate['maximumRawProcesses']==2
for r in gate['frozenFiles']:assert sha(r['path'])==r['sha256'],'Gate identity drift'
plan=read(ROOT/'PLAN.json');assert len(plan['watch'])==4 and plan['maxEvents']==256
batch=ROOT/'batch-01';batch.mkdir(exist_ok=False);ledger=batch/'ledger';ledger.mkdir()
old=gate['oldBatchEvidence'];assert len(old)==3
for r in old:assert sha(r['path'])==r['sha256'],'Old consumed evidence changed'
# The batch directory is exclusive; reserving happens before debugger/child creation.
host=ROOT/'host-final';mods=read('E:/Guangjing-RawReopen001-NoRaw-20261109/no-raw-self-test01/loaded-dependencies.json');sdk=Path(next(x['Path'] for x in mods if x['Path'].endswith('hostfxr.dll'))).parents[3]/'dotnet.exe'
inputpath=read('E:/Guangjing-RawReopen001-NoRaw-20261109/VALIDATION_MANIFEST01.json')['Input']['Path']
receipts=[]
for slot in ['first','second']:
 for r in gate['frozenFiles']+old:assert sha(r['path'])==r['sha256'],'Frozen file drift before reservation'
 folder=batch/slot;folder.mkdir();reservation=ledger/(slot+'.consumed')
 manifest={'nativePath':str(host/'raw_r.dll'),'inputPath':inputpath,'reservationPath':str(reservation),'files':gate['frozenFiles']}
 manifestpath=folder/'HOST_MANIFEST.json';manifestpath.write_text(json.dumps(manifest,indent=2)+'\n')
 config={**{k:plan[k] for k in ['sites','watch','readyRva','readyByte','stopRva','stopByte','maxEvents']},'mode':'raw','argv':[str(sdk),str(host/'H1Host.dll'),'--once',str(manifestpath),str(folder/'host')],'executableSha256':sha(sdk).lower(),'nativeSha256':plan['nativeDllSha256'],'workingDirectory':str(host),'timeoutSeconds':600,'memoryLimitBytes':2147483648}
 configpath=folder/'WATCH_CONFIG.json';configpath.write_text(json.dumps(config,indent=2)+'\n')
 ticket={'task':'RAW-REOPEN-001-H1-WINDOW-20261109','maximum':2,'slot':slot,'reservationId':str(uuid.uuid4()),'hostManifestSha256':sha(manifestpath),'watchConfigSha256':sha(configpath),'gateSha256':sha(ROOT/'GATE.json'),'authorizationSha256':sha(ROOT/'AUTHORIZATION.txt'),'status':'reserved-before-spawn','failedAttemptStillConsumed':True,'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 with reservation.open('x',encoding='utf-8') as f:json.dump(ticket,f,indent=2);f.write('\n')
 with (folder/'DEBUGGER.log').open('w') as log:run=subprocess.run([sys.executable,str(HERE/'debug_watch.py'),str(configpath),str(folder/'debug')],stdout=log,stderr=subprocess.STDOUT,timeout=620)
 record={'slot':slot,'debuggerExitCode':run.returncode,'reservationId':ticket['reservationId']};receipts.append(record)
 if run.returncode!=0:
  (batch/'BATCH_RECEIPT.json').write_text(json.dumps({'status':'FAILED_STOP_NO_RETRY','processes':receipts,'consumed':len(receipts),'remaining':2-len(receipts)},indent=2)+'\n');sys.exit(1)
 debug=read(folder/'debug/RECEIPT.json');actual=read(folder/'host/result.json');assert debug['status']=='completed' and debug['readyHit'] and debug['windowClosedAfterJoin'] and not debug['truncated'],'Observation incomplete; no retry'
 assert actual['status']=='completed' and actual['error'] is None and actual['ProcessId']==debug['pid']
 assert actual['counts']=={'OpenFile':1,'Unpack':1,'DcrawProcess':1,'MakeDcrawMemoryImage':1}
 for name in ['C1-lower','C2-lower']:
  ev=[e for e in debug['events'] if e['watch']['id']==name and e['consumerValueKnown']];assert any(e['phase']=='final-G-store' for e in ev) and any('LIM-lower' in e['phase'] for e in ev),'Actual store/load coverage absent; no retry'
 record.update({'processId':debug['pid'],'events':len(debug['events']),'unknownAccesses':debug['unknownAccesses'],'counts':actual['counts'],'debugReceiptSha256':sha(folder/'debug/RECEIPT.json'),'hostReceiptSha256':sha(folder/'host/result.json')})
 (batch/'BATCH_RECEIPT.json').write_text(json.dumps({'status':'FIRST_COMPLETED' if slot=='first' else 'COMPLETED_TWO_NO_RETRY','processes':receipts,'consumed':len(receipts),'remaining':2-len(receipts),'oldBatchEvidence':old,'wholeP5P6Comparison':False},indent=2)+'\n')
for r in old:assert sha(r['path'])==r['sha256'],'Old batch evidence must remain unchanged'
print(json.dumps(read(batch/'BATCH_RECEIPT.json'),indent=2))
