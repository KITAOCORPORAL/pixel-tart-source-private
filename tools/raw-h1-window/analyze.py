"""Verify bounded architectural receipts and apply the predeclared H1 criteria."""
from pathlib import Path
import json,hashlib,collections,sys
root=Path(sys.argv[1]);read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
plan=read(root/'PLAN.json');gate=read(root/'GATE.json');batch=read(root/'batch-01/BATCH_RECEIPT.json')
assert batch['consumed']==2 and batch['remaining']==0 and batch['status']=='COMPLETED_TWO_NO_RETRY'
for f in gate['frozenFiles']+gate['oldBatchEvidence']:assert sha(f['path'])==f['sha256']
results=[];compact=[];evidence=[]
for proc in batch['processes']:
 side=proc['slot'];folder=root/'batch-01'/side;trace=read(folder/'debug/RECEIPT.json');host=read(folder/'host/result.json');loaded=read(folder/'host/loaded-native.json');ticket=read(root/'batch-01/ledger'/(side+'.consumed'));manifest=folder/'HOST_MANIFEST.json';config=folder/'WATCH_CONFIG.json'
 assert trace['status']=='completed' and trace['windowClosedAfterJoin'] and trace['readyHit'] and not trace['truncated'] and trace['unknownAccesses']==0
 assert host['status']=='completed' and host['error'] is None and host['ProcessId']==trace['pid']==proc['processId'];assert host['fullFramePayloads']==0
 assert ticket['hostManifestSha256']==sha(manifest) and ticket['watchConfigSha256']==sha(config) and ticket['gateSha256']==sha(root/'GATE.json')
 assert loaded['sha256'].lower()==plan['nativeDllSha256'];assert host['counts']=={'OpenFile':1,'Unpack':1,'DcrawProcess':1,'MakeDcrawMemoryImage':1}
 ev=trace['events'];assert len(ev)==86;assert all(e['consumerValueKnown'] and e['effectiveAddressVerified'] for e in ev)
 assert collections.Counter(e['phase'] for e in ev)=={'tile-copy-consumed-G':4,'initial-LIM-upper':16,'initial-LIM-lower':16,'recalculation-LIM-upper':24,'recalculation-LIM-lower':24,'final-G-store':2}
 for e in ev:
  site=next(s for s in plan['sites'] if s['rva']==e['rva']);assert site['nextRva']==e['nextRva'] and site['bytes']==e['bytes'];assert e['value']==(e['postRegisters'][site['register']]>>site.get('shift',0))&65535
  assert e['tileTop'] in [3,499] and e['tileLeft']==7443
 bypoint=[];witness={r['id']:r['serialWitnessOnly'] for r in trace['serialWitness']}
 for name,owner,reader in [('C1',499,3),('C2',3,499)]:
  lower=[e for e in ev if e['watch']['id']==name+'-lower'];upper=[e for e in ev if e['watch']['id']==name+'-upper-control'];writes=[e for e in lower if e['phase']=='final-G-store'];assert len(writes)==1 and writes[0]['tileTop']==owner
  assert len(upper)==20 and all(e['value']==witness[name+'-upper-control'] for e in upper)
  cross=[e for e in lower if e['tileTop']==reader and e['phase']!='final-G-store'];assert len(cross)==11
  positive=[e for e in cross if e['value']!=witness[name+'-lower'] and e['value']==writes[0]['value'] and e['threadId']!=writes[0]['threadId']]
  bypoint.append(dict(candidate=name,coordinate=[lower[0]['watch']['x'],lower[0]['watch']['y']],serialLower=witness[name+'-lower'],serialUpper=witness[name+'-upper-control'],actualWriterValue=writes[0]['value'],writerThread=writes[0]['threadId'],writerTile=[owner,7443],crossReaderTile=[reader,7443],crossActualConsumedValues=sorted(set(e['value'] for e in cross)),crossReads=11,matchedNonInitialConsumedLoads=len(positive),upperControlUnchanged=True,mechanismObserved=bool(positive)))
  if positive:
   keep=[writes[0],*positive];compact.extend([dict(process=side,sequence=e['sequence'],phase=e['phase'],coordinate=[e['watch']['x'],e['watch']['y']],slot=e['watch']['slot'],value=e['value'],threadId=e['threadId'],tile=[e['tileTop'],e['tileLeft']],rva=f"0x{e['rva']:x}",instructionBytes=e['bytes'],destinationOrStoreSourceRegister=e['register'],postRegisterValue=e['postRegisters'][e['register']],extractShift=e.get('shift',0),effectiveAddressVerified=True) for e in keep])
 results.append(dict(process=side,pid=trace['pid'],events=len(ev),unknownAccesses=0,truncated=False,actualObservedWorkerThreads=sorted(set(e['threadId'] for e in ev)),candidateResults=bypoint,counts=host['counts'],peakWorkingSetBytes=host['peakWorkingSetBytes'],elapsedSeconds=trace['elapsedSeconds'],verdict='H1_SUPPORTED_IN_OBSERVED_WINDOW' if any(r['mechanismObserved'] for r in bypoint) else 'NO_DISTINGUISHABLE_CONTAMINATION_IN_THIS_WINDOW_NOT_GLOBAL_FALSIFICATION'))
 for f in [folder/'debug/RECEIPT.json',folder/'host/result.json',folder/'host/startup.json',folder/'host/configured-output.json',folder/'host/loaded-native.json',config,manifest,root/'batch-01/ledger'/(side+'.consumed')]:evidence.append(dict(path=str(f),sha256=sha(f),bytes=f.stat().st_size))
supported=any(p['verdict']=='H1_SUPPORTED_IN_OBSERVED_WINDOW' for p in results)
verdict=dict(version='H1WindowVerdictV1',H1='SUPPORTED_IN_OBSERVED_EXECUTION_WINDOW' if supported else 'WEAKENED_FOR_THIS_WINDOW_AND_SCHEDULE_ONLY',rootCause='OPEN_NOT_FIXED',processes=results,newRawProcesses=2,newGPUCalls=0,wholeP5P6Comparison=False,oldBatchEvidenceUnchanged=True,caveats=['Hardware/software debugging substantially perturbs scheduling.','Delivery sequence is not memory happens-before.','No claim that all old output differences or O_first are explained.','No full-frame difference or cross-run P5 equality was measured in this new bounded batch.'],gateSha256=sha(root/'GATE.json'),planSha256=sha(root/'PLAN.json'),evidence=evidence)
(root/'ANALYSIS.json').write_text(json.dumps(verdict,indent=2)+'\n');(root/'POSITIVE_EVENT_WITNESSES.json').write_text(json.dumps(dict(events=compact,sequenceMeaning='Debugger delivery order, not a global happens-before claim',valuesFrom='Actual post-instruction GPR, not watched-memory peeks'),indent=2)+'\n')
print(json.dumps({k:v for k,v in verdict.items() if k!='evidence'},indent=2))
