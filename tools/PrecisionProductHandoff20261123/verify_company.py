"""Read-only comparison of the three explicitly named company zero-RAW observations."""
from pathlib import Path
import argparse,hashlib,json,struct,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--root',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();root=a.root.resolve();assert not a.output.exists();repo=Path(__file__).resolve().parents[2]
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def blocks(path):
 assert path.suffix.lower() in {'.tif','.tiff'}
 with path.open('rb') as f:
  head=f.read(8);assert head[:4]==b'II*\0';f.seek(struct.unpack_from('<I',head,4)[0]);n=struct.unpack('<H',f.read(2))[0];tags={}
  for _ in range(n):tag,kind,count,value=struct.unpack('<HHII',f.read(12));tags[tag]=(kind,count,value)
  def values(tag):
   kind,count,value=tags[tag];assert kind==4
   if count==1:return [value]
   f.seek(value);return struct.unpack('<'+'I'*count,f.read(count*4))
  for at,count in zip(values(273),values(279),strict=True):
   f.seek(at)
   while count:
    data=f.read(min(count,1048576));assert data;count-=len(data);yield data
company=read(root/'COMPANY_TASK.json');assert company['pixelSha256']=='0806D680ACC74FACD093DD2280E1C1A82CD18E40D0C27823D8C6C87CC7E3FC7E';payload=Path(company['payload']);assert payload.suffix=='.rgb48' and payload.stat().st_size==7752*5184*6 and sha(payload)==company['pixelSha256']
paths={'oracle':root/'company-points-oracle/oracle.tif'}
for name,folder in [('failed_cycle_first','company-processed-three-cycle'),('first_only','company-first-display-observation')]:
 files=list((root/folder/'export-first').glob('*.tif'));assert len(files)==1;paths[name]=files[0]
probeRoot=root/(a.output.stem+'-probes');probeRoot.mkdir(exist_ok=False);probes={}
for name,path in paths.items():
 target=probeRoot/(name+'.json');subprocess.run([sys.executable,str(repo/'tools/LegacyExportMemory20261122/probe_tiff.py'),str(path),str(target)],check=True,capture_output=True);probes[name]=read(target)
checks={key:len({json.dumps(v[key],sort_keys=True) for v in probes.values()})==1 for key in ['width','height','bits','orientation','pixelBytes','pixelSha256','icc']};assert all(checks.values());assert probes['oracle']['width']==7752 and probes['oracle']['height']==5184 and probes['oracle']['orientation']==1 and probes['oracle']['bits']==[16,16,16] and probes['oracle']['icc']['bytes']==3144 and probes['oracle']['icc']['sha256']==company['iccSha256']
comparisons={}
for name in ['failed_cycle_first','first_only']:
 count=0
 for left,right in zip(blocks(paths['oracle']),blocks(paths[name]),strict=True):assert left==right;count+=len(left)//2
 assert count==120559104;comparisons[name]={'uint16ChannelSamplesCompared':count,'differentSamples':0,'exact':True}
reference=read(root/'company-points-oracle/RESULT.json');first=read(root/'company-first-display-observation/RESULT.json');assert first['source']=='2.3.0+ac1c57e9689428cea3f6938e06ad26209c7fc3d5';assert first['firstPreviewSha']==reference['outputRgb24Sha256'];assert not first['pointProcessedPreviewExcluded']
resources={}
for folder in ['company-points-oracle','company-processed-three-cycle','company-first-display-observation']:
 case=root/folder;exit=read(case/'EXIT.json');launch=read(case/'LAUNCH.json');samples=[json.loads(x) for x in (case/'RESOURCE_SAMPLES.jsonl').read_text().splitlines()];assert samples and len(samples)==exit['samples'] and exit['guardBytes']==2**31 and exit['samplingMs']==50;assert launch['hostSha256']==sha(root/'stage-ac1/LegacyExportMemoryHost.dll').lower() or launch['hostSha256'].upper()==sha(root/'stage-ac1/LegacyExportMemoryHost.dll');assert launch['task']['pixelSha256']==company['pixelSha256']
 resources[folder]={'exit':exit,'privateHeadroomBytes':2**31-exit['peakPrivateBytes50ms'],'workingHeadroomBytes':2**31-exit['osPeakWorkingSetBytes'],'privateHeadroomMiB':(2**31-exit['peakPrivateBytes50ms'])/2**20,'workingHeadroomMiB':(2**31-exit['osPeakWorkingSetBytes'])/2**20,'expectedMode':launch['mode'],'samplesVerified':len(samples),'stopFilePresent':(case/'STOP.json').exists()}
failed=root/'company-processed-three-cycle';assert (failed/'STOP.json').is_file() and resources['company-processed-three-cycle']['exit']['exitCode']!=0
failedEvents=[json.loads(x) for x in (failed/'events.jsonl').read_text().splitlines()];firstEvents=[json.loads(x) for x in (root/'company-first-display-observation/events.jsonl').read_text().splitlines()];views=[x['holders']['viewport'] for x in firstEvents if x['stage']=='visible-100'];assert len(views)==1 and views[0]['zoom']==1 and views[0]['IsVisible'] and views[0]['IsLoaded'] and views[0]['matched']['width']==7752 and views[0]['matched']['height']==5184 and not views[0]['DisplayColorEnabled']
releases=[x for x in failedEvents if x['stage']=='release-tasks-observed'];assert len(releases)==1 and all(x['IsCompleted'] for x in releases[0]['holders']['tasks']);masters=[x['holders']['target.RawMaster'] for x in failedEvents if x['stage']=='visible-100'];assert len(masters)==1 and not masters[0]['floatMaterialized'] and masters[0]['bytes']==241118208
saved=read(failed/'saved.ptstudio.json');target=saved['targets'][0];recipe=target['stack'];assert recipe['processingVersion']==2 and len(recipe['nodes'])==2;assert recipe['nodes'][0]['numericParameters']=={'temperature':9,'tint':-4};assert recipe['nodes'][1]['numericParameters']=={'exposure':.5,'highlights':-20,'saturation':7}
result={'status':'PARTIAL_COMPANY_FIRST_CYCLE_EXACT_REOPEN_RESOURCE_GATE_FAILED','productSource':'ac1c57e9689428cea3f6938e06ad26209c7fc3d5','companyOriginalPixelSha256':company['pixelSha256'],'checks':checks,'sampleComparisons':comparisons,'probes':probes,'display':{'status':'PASS_SEPARATE_FIRST_ONLY_OBSERVATION','referenceRgb24Sha256':reference['outputRgb24Sha256'],'actualViewportRgb24Sha256':first['firstPreviewSha'],'bytesComparedBySha256':120559104,'exact':True,'viewport':views[0],'notFailedRunLateResult':True},'save':{'file':str(failed/'saved.ptstudio.json'),'sha256':sha(failed/'saved.ptstudio.json'),'savedExactPointRecipe':recipe,'originalHistoricalRecipe':'Neutral/no stack; retained unchanged in HISTORICAL_RECIPE_CHECK.json','configuredPointRecipeIsPriorApprovedProbeRecipe':True},'resources':resources,'firstCycleExportAndProcessed100':True,'repeatedReopenPassed':False,'ordinaryFullExeAcceptance':False,'runLimits':'No failed reopen retry; one additional existing first-only mode captured display digest missing after STOP. Three processes total; no further resource cases.','newFailureLocalization':{'lastCompleteStage':'before-ushort-allocation-2','newSecondUshortReadCompleted':False,'priorReleaseKnownTasksAllComplete':True,'priorMasterNotFullFloat':True,'notProofOfLeakOrNativeOwnership':True},'newRawDecodes':0,'oldBatchOrBalanceReused':False,'noNewProductPatch':True}
a.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n');print(json.dumps({'status':result['status'],'uint16SamplesPerOutput':120559104,'outputCount':2,'displayMatch':True,'resources':{n:{'privateMiB':v['exit']['peakPrivateBytes50ms']/2**20,'workingMiB':v['exit']['osPeakWorkingSetBytes']/2**20,'privateHeadroomMiB':v['privateHeadroomMiB'],'workingHeadroomMiB':v['workingHeadroomMiB']} for n,v in resources.items()}}))
