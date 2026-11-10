"""No-RAW offline readback; explicitly unavailable unless both candidate outputs exist."""
from pathlib import Path
import json,hashlib,subprocess,sys
from metrics import compare
root,out=map(Path,sys.argv[1:3]);out.mkdir(exist_ok=False);read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
required=[root/'pair'/side/name for side in ['first','second'] for name in ['result.json','P5.image4.u16','XTRANS-serial-ready.image4.u16','P6.rgb.u16']];missing=[str(p) for p in required if not p.exists()];completed=all((root/'pair'/s/'result.json').exists() and read(root/'pair'/s/'result.json')['status']=='completed' for s in ['first','second'])
if missing or not completed:
 result=dict(status='UNAVAILABLE_NO_COMPLETED_CANDIDATE_PAIR',candidateP5Equal=None,candidateSerialInputEqual=None,candidateP6BitExact=None,candidateVsStoredBaselines=None,interpolationResourcePeaks=None,missing=missing,regressionDecision='DO_NOT_ENTER_PRODUCT_REGRESSION_WITHOUT_NATIVE_PAIR',rawCallsByAnalysis=0)
 (out/'RESULT.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2));sys.exit(0)
# This path never starts a decoder. The reviewed comparison consumer checks schema/identity/state.
helper=Path(__file__).with_name('ReviewedCompare.mjs');subprocess.run(['node',str(helper),str(root/'pair/first'),str(root/'pair/second'),str(out/'typed-pair')],check=True);typed=read(out/'typed-pair/FIRST_DIFFERENCE.json');records={r['id']:r for r in typed['results']}
meta=[]
for side in ['first','second']:
 folder=root/'pair'/side;rows=[json.loads(l) for l in (folder/'native-events.jsonl').read_text().splitlines()];p6=next(r for r in rows if r.get('kind')=='payload' and r.get('id')=='P6');res=read(folder/'result.json');assert res['candidate']=='XTransStableInputSeparateRgbV1' and res['debuggerAttached'] is False and res['runtime']['debuggerAttached'] is False;meta.append((folder,p6,res))
metrics=[];a,b=meta;pair=compare(a[0]/'P6.rgb.u16',b[0]/'P6.rgb.u16',7752,5184,a[1]['sha256'],b[1]['sha256'])
for side,(folder,p6,_) in zip(['first','second'],meta):
 for old in read(root/'STORED_BASELINE_IDENTITIES.json'):metrics.append(dict(candidate=side,baseline=old['process'],metrics=compare(folder/'P6.rgb.u16',old['path'],7752,5184,p6['sha256'],old['sha256'])))
processes=read(root/'pair/PROCESS_RECEIPTS.json');processes=processes if isinstance(processes,list) else [processes];assert len(processes)==2
expectedConfig=read(read(root/'CANDIDATE_CONTRACT.json')['expectedConfigurationPath']);parallel=True
for process,(folder,_,res) in zip(processes,meta):
 assert process['status']=='completed' and process['aborted'] is False and process['exitCode']==0 and process['sampleMs']==50 and process['elapsedGuardMs']<=600000
 assert max(process['peakWorkingSetOsObserved'],res['peakWorkingSetBytes'])<=2147483648 and process['peakPrivateBytes50ms']<=2147483648
 assert read(folder/'configured-output.json')==expectedConfig
 events=[json.loads(l) for l in (folder/'native-events.jsonl').read_text().splitlines()];teams={r['team'] for r in events if r.get('kind')=='tile'};parallel=parallel and bool(teams) and min(teams)>1
valid=records['P5']['effectiveInputEqual'] and records['XTRANS-serial-ready']['effectiveInputEqual'] and pair['bitExact'] and parallel;result=dict(status='NATIVE_CANDIDATE_PAIR_ANALYZED',candidateP5Equal=records['P5']['effectiveInputEqual'],candidateSerialInputEqual=records['XTRANS-serial-ready']['effectiveInputEqual'],candidateP6BitExact=pair['bitExact'],pairMetrics=pair,versusStoredBaselines=metrics,stageTimes=[m[2]['stageTimesMs'] for m in meta],regressionDecision='ELIGIBLE_FOR_TARGETED_PRODUCT_REGRESSION_NOT_PRODUCTION' if valid else 'NOT_ELIGIBLE',rawCallsByAnalysis=0);(out/'RESULT.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k not in ['pairMetrics','versusStoredBaselines','stageTimes']},indent=2))
