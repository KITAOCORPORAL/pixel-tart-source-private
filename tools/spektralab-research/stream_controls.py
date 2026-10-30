"""Failure/lifetime controls on actual native ABI and explicit synthetic diagnostic input."""
from pathlib import Path
import argparse,subprocess,sys,json,hashlib,types,os
import numpy as np
from natural_admission import sha,scan,npy_info,atomic_json
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('engine',type=Path);a=p.parse_args();root=a.root;base=Path('E:/Kitao-Research/color-engine-round7-20261030/allocation-measurements-bound');library=root/'build-stream-v1/engine/spektrafilm_engine.dll';script=Path(__file__).with_name('stream_case.py');records=[]
def invoke(name,manifest,inputpath,extra,oracle=None):
 dest=root/(name+'-sealed');cmd=[sys.executable,str(script),str(inputpath),str(dest),'--manifest',str(manifest),'--manifest-sha',sha(manifest),'--library',str(library),'--resources',str(a.engine/'resources-patch'),'--rows','17']+extra
 if oracle:cmd+=['--oracle',str(oracle)]
 proc=subprocess.run(cmd,capture_output=True,text=True);r=json.loads((dest/'report.json').read_text());records.append({'directory':name,**r});atomic_json(root/'control-results.json',{'version':'StreamActualNativeControlsV1','cases':records});print(proc.stdout.strip(),flush=True);return r
manifest=base/'321-measurement-manifest.json';inp=base/'whole-scene-321x215.npy'
for fault in ['read','consumer','cancel','unknown','unknown-key','wrong-route','bad-input-hash','bad-backend']:
 r=invoke('control-'+fault,manifest,inp,['--fault',fault]);assert not r['success'] and r.get('native',{}).get('deviceLiveAfter',0)==0 and r.get('native',{}).get('stagingLiveAfter',0)==0;assert not (root/('control-'+fault+'-sealed')/'display-rgba16.tif').exists();assert r.get('native',{}).get('negativeStoreAbsentAfter',True)
 if fault in ['unknown','unknown-key','wrong-route','bad-backend']:assert r['callbacks']['read']==0
r=invoke('control-staging-budget',manifest,inp,['--staging-mib','0.125']);assert not r['success'];assert r['native']['deviceLiveAfter']==r['native']['stagingLiveAfter']==0
r=invoke('control-device-budget',manifest,inp,['--device-mib','1']);assert not r['success'] and r['callbacks']['read']==0
# Same native DLL called repeatedly in the same Python process: TLS accounting/lifetimes reset.
sys.path.insert(0,str(Path(__file__).parent));from stream_case import run
for k in range(2):
 args=types.SimpleNamespace(input=inp,output=root/f'repeat-process-sealed-{k}',manifest=manifest,manifest_sha=sha(manifest),library=library,resources=a.engine/'resources-patch',rows=37,consumer_rows=1 if k==0 else 11,device_mib=256,staging_mib=64,host_mib=512,oracle=base/'321-whole/rgba16.bin',fault='none');r=run(args);assert r['success'];records.append({'directory':args.output.name,**r})
# Explicit derived negative/>1 nearzero fixture retains same source-lineage metadata,
# but preparationIdentity says synthetic diagnostic, not an additional natural scene.
synthetic=root/'synthetic-fixture-sealed';synthetic.mkdir();source=np.load(inp);data=source[:65,:97].copy();data[0,:7]=np.array([[0,0,0],[-.1,0,.2],[2,.18,0],[1,0,0],[0,1,0],[0,0,1],[1e-8,-1e-7,4]],np.float32);data[32,48]=[4,-.2,2];inputpath=synthetic/'synthetic-domain-stress.npy';np.save(inputpath,data);m=json.loads(manifest.read_text());info=npy_info(inputpath);stats=scan(inputpath,info);m['inputRoot']=str(synthetic);m['files']=[{'path':inputpath.name,'fileSha256':sha(inputpath),'shape':info['shape'],'route':'A','orientation':1,'domain':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','bridgeVersion':'ExistingResearchD65ToD50V1','pixelSha256':stats['pixelSha256'],'bytes':info['fileBytes']}];m['preparationIdentity']['mode']='Synthetic domain stress diagnostic, generated values inserted; source hash denotes development-scene lineage only; NOT a natural sample';atomic_json(synthetic/'manifest.json',m)
# Whole oracle uses unchanged original public engine; isolated small dimensions only.
sys.path[:0]=[str(a.engine/'source2/engine/tests'),str(a.engine/'source2/engine/tools')];from spk_ctypes import Engine;from windows_lifecycle import OwnedResult
resolved=json.loads((root/'exact-321-r37/report.json').read_text())['resolved']
with Engine(library=a.engine/'build-patch/engine/spektrafilm_engine.dll',resources=a.engine/'resources-patch') as engine:
 with engine.open(data,resolved) as session:
  with OwnedResult(session,'full') as result:out=result.pixels().copy()
out.astype('<u2').tofile(synthetic/'whole-rgba16.bin')
for rows in [1,7,17,64]:
 r=invoke('synthetic-r'+str(rows),synthetic/'manifest.json',inputpath,['--rows',str(rows),'--consumer-rows','3'],synthetic/'whole-rgba16.bin');assert r['success']
atomic_json(root/'control-results.json',{'version':'StreamActualNativeControlsV1','cases':records,'syntheticManifest':m,'syntheticWholeHash':sha(synthetic/'whole-rgba16.bin')})
