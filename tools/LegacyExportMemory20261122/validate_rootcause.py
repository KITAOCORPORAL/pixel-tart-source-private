from pathlib import Path
import argparse,json,subprocess,sys,struct
p=argparse.ArgumentParser();p.add_argument('--output',required=True,type=Path);a=p.parse_args();assert not a.output.exists();repo=Path(__file__).resolve().parents[2];r=repo/'artifacts/precision-rootcause-20261122';old=repo.parent/'guangjing-legacy-precision-lifetime-20261122/artifacts/precision-followup-20261122';older=repo.parent/'guangjing-legacy-export-memory-20261122/artifacts/legacy-export-memory-20261122'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def blocks(p):
 with p.open('rb') as f:
  header=f.read(8);assert header[:4]==b'II*\0';f.seek(struct.unpack_from('<I',header,4)[0]);n=struct.unpack('<H',f.read(2))[0];tags={}
  for _ in range(n):t,k,c,v=struct.unpack('<HHII',f.read(12));tags[t]=(k,c,v)
  def vals(tag):
   k,c,v=tags[tag];assert k==4
   if c==1:return [v]
   f.seek(v);return struct.unpack('<'+'I'*c,f.read(c*4))
  for at,count in zip(vals(273),vals(279),strict=True):
   f.seek(at)
   while count:
    b=f.read(min(count,1048576));assert b;count-=len(b);yield b
cases=[('processed40',r/'final-three-cycle',old/'cf5-40-display-oracle'),('sony8',r/'final-sony-cycle',old/'cf5-sony-display-oracle'),('neutral40',r/'final-neutral-cycle',older/'d07-neutral-oracle')];all_results={}
for name,run,oracle in cases:
 result=read(run/'RESULT.json');exit=read(run/'EXIT.json');assert exit['exitCode']==0 and exit['stop'] is None
 images={'original':oracle/'oracle.tif','first':Path(result['first']),'second':Path(result['second'])}
 if result.get('third'):images['third']=Path(result['third'])
 probes={};samples={}
 for label,file in images.items():
  target=r/(name+'-'+label+'-probe.json');subprocess.run([sys.executable,str(repo/'tools/LegacyExportMemory20261122/probe_tiff.py'),str(file),str(target)],stdout=subprocess.PIPE,check=True);probes[label]=read(target)
 checks={key:len({json.dumps(v[key],sort_keys=True) for v in probes.values()})==1 for key in ['width','height','bits','orientation','pixelBytes','pixelSha256','icc']};assert all(checks.values())
 for label,file in list(images.items())[1:]:
  count=0
  for x,y in zip(blocks(images['original']),blocks(file),strict=True):assert x==y;count+=len(x)//2
  samples[label]={'comparedUint16Samples':count,'differentSamples':0}
 original=read(oracle/'RESULT.json');display={}
 for key in ['firstPreviewSha','secondPreviewSha','thirdPreviewSha']:
  if result.get(key):
   if original.get('outputRgb24Sha256'):display[key]=result[key]==original['outputRgb24Sha256'];assert display[key]
 assert result['firstPreviewSha']==result['secondPreviewSha'] and (not result.get('thirdPreviewSha') or result['thirdPreviewSha']==result['firstPreviewSha'])
 events=[json.loads(x) for x in (run/'events.jsonl').read_text().splitlines()];visible=[x for x in events if x['stage']=='visible-100'];assert len(visible)==len(images)-1
 for event in visible:
  v=event['holders']['viewport'];assert v['zoom']==1 and v['IsVisible'] and v['IsLoaded'];assert not v['DisplayColorEnabled'];assert event['holders']['editor.mode']['enabled'];master=event['holders']['target.RawMaster'];assert not master['floatMaterialized'];assert master['bytes']==master['width']*master['height']*6
  if name=='processed40':assert v['Mode'] in ['仿色结果','仿色'] and v['matched']['width']==7752 and v['matched']['height']==5184
 assert not result['pointProcessedPreviewExcluded']
 cycle=1;stages={}
 for line in (run/'RESOURCE_SAMPLES.jsonl').read_text().splitlines():
  sample=json.loads(line)
  if sample['phase'].startswith('before-ushort-allocation-'):cycle=int(sample['phase'].rsplit('-',1)[1])
  entry=stages.setdefault(cycle,{'privatePeakBytes':0,'workingPeakBytes':0});entry['privatePeakBytes']=max(entry['privatePeakBytes'],sample['privateBytes']);entry['workingPeakBytes']=max(entry['workingPeakBytes'],sample['workingSetBytes'])
 all_results[name]={'status':'PASS','checks':checks,'exhaustiveSamples':samples,'displayVsOriginalCore':display,'resource':exit,'cyclePeaks':stages,'fullCycle':True,'result':result,'viewports':[x['holders']['viewport'] for x in visible],'backings':[x['holders']['target.RawMaster'] for x in visible],'probes':probes,'releaseTaskObservations':[x['holders'].get('tasks') for x in events if x['stage']=='release-tasks-observed']}
a.output.write_bytes((json.dumps(all_results,ensure_ascii=False,indent=2)+'\n').encode());print(json.dumps({k:{'status':v['status'],'cycles':len(v['cyclePeaks']),'privateMiB':round(v['resource']['peakPrivateBytes50ms']/1048576,2),'workingMiB':round(v['resource']['osPeakWorkingSetBytes']/1048576,2),'cyclePeaks':{n:{key:round(size/1048576,2) for key,size in vals.items()} for n,vals in v['cyclePeaks'].items()}} for k,v in all_results.items()}))
