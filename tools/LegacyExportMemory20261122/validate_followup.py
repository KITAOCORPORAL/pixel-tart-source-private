from pathlib import Path
import argparse,json,subprocess,sys,struct,array
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args();assert not a.output.exists();repo=Path(__file__).resolve().parents[2];root=repo/'artifacts/precision-followup-20261122';prior=repo.parent/'guangjing-legacy-export-memory-20261122/artifacts/legacy-export-memory-20261122'
def read(file):return json.loads(file.read_text('utf-8-sig'))
def blocks(file):
 with file.open('rb') as f:
  h=f.read(8);assert h[:4]==b'II*\0';f.seek(struct.unpack_from('<I',h,4)[0]);n=struct.unpack('<H',f.read(2))[0];tags={}
  for _ in range(n):
   t,k,c,v=struct.unpack('<HHII',f.read(12));tags[t]=(k,c,v)
  def vals(t):
   k,c,v=tags[t];assert k==4
   if c==1:return [v]
   f.seek(v);return list(struct.unpack('<'+'I'*c,f.read(c*4)))
  for offset,count in zip(vals(273),vals(279),strict=True):
   f.seek(offset)
   while count:
    b=f.read(min(count,1048576));assert b;count-=len(b);yield b
cases=[('40-neutral',prior/'d07-neutral-oracle',root/'verified-40-neutral-cycle'),('40-processed-points',root/'cf5-40-display-oracle',root/'verified-40-processed100-first'),('sony-neutral',root/'cf5-sony-display-oracle',root/'verified-sony-cycle')];results={}
for name,oracle,run in cases:
 result=read(run/'RESULT.json');exit=read(run/'EXIT.json');assert exit['exitCode']==0 and exit['stop'] is None;files={'before':oracle/'oracle.tif','first':Path(result['first'])}
 if result.get('second'):files['reopen']=Path(result['second'])
 probes={};samples={}
 for label,file in files.items():
  target=root/(name+'-'+label+'-probe.json');subprocess.run([sys.executable,str(repo/'tools/LegacyExportMemory20261122/probe_tiff.py'),str(file),str(target)],check=True,stdout=subprocess.PIPE);probes[label]=read(target)
 checks={k:len({json.dumps(v[k],sort_keys=True) for v in probes.values()})==1 for k in ['width','height','bits','orientation','pixelSha256','icc']};assert all(checks.values())
 for label,file in list(files.items())[1:]:
  count=0
  for x,y in zip(blocks(files['before']),blocks(file),strict=True):assert x==y;(None);count+=len(x)//2
  samples[label]={'uint16SamplesCompared':count,'differentSamples':0}
 events=[json.loads(x) for x in (run/'events.jsonl').read_text().splitlines()];viewports=[e['holders']['viewport'] for e in events if e['stage']=='visible-100'];assert viewports and all(v['zoom']==1 and v['IsVisible'] and v['IsLoaded'] and not v['DisplayColorEnabled'] for v in viewports)
 if name=='40-processed-points':assert all(v['Mode'] in ['仿色结果','仿色'] and v['matched']['width']==7752 and v['matched']['height']==5184 for v in viewports);assert not result['pointProcessedPreviewExcluded']
 oracle_result=read(oracle/'RESULT.json');display={}
 if 'outputRgb24Sha256' in oracle_result:
  for label in ['firstPreviewSha','secondPreviewSha']:
   if label in result:display[label]=result[label]==oracle_result['outputRgb24Sha256'];assert display[label]
 if 'secondPreviewSha' in result:assert result['firstPreviewSha']==result['secondPreviewSha']
 results[name]={'status':'PASS','checks':checks,'samples':samples,'displayAdapterVsOriginalCore':display,'resources':exit,'result':result,'viewports':viewports,'probes':probes,'completeSameProcessCycle':bool(result.get('second'))}
a.output.write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:{'status':v['status'],'fullCycle':v['completeSameProcessCycle'],'privateMiB':round(v['resources']['peakPrivateBytes50ms']/1048576,2),'workingMiB':round(v['resources']['osPeakWorkingSetBytes']/1048576,2),'display':v['displayAdapterVsOriginalCore']} for k,v in results.items()}))
