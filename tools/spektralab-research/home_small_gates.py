"""Home orchestration: same whole/stream oracle and immutable PortraA math."""
import argparse,json,subprocess,sys,os
from pathlib import Path
import numpy as np
from natural_admission import *
from host_environment import environment
p=argparse.ArgumentParser();p.add_argument('--whole',action='store_true');p.add_argument('--controls-only',action='store_true');p.add_argument('--prefix',default='');a=p.parse_args();host=environment();root=Path(host['researchRoot']);small=Path(host['smallRoot']);manifest=small/'admission-manifest-v2.json';repo=Path(__file__).resolve().parents[2];frozen=load_json(repo/'docs/research/color-engine-20261029/company-round6/natural-cases.json');resolved=next(c for c in frozen['cases'] if c['localDirectory']=='portra-A-proxy')['resolved'].copy();resolved.update(striped=False,strip_rows=0,strip_budget_bytes=0)
# External original bindings; do not copy GPL into product.
sys.path[:0]=[str(root/'source-patch/engine/tests'),str(root/'source-patch/engine/tools')]
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
if a.whole:
 records=[]
 for width,height,name in [(321,215,'whole-scene-321x215.npy'),(641,429,'whole-scene-641x429.npy'),(1200,801,'whole-scene-1200x801.npy'),(97,65,'synthetic.npy')]:
  dest=small/f'{width}-whole';need(not dest.exists(),'Whole oracle exists');dest.mkdir();data=np.load(small/name);start=__import__('time').perf_counter()
  with Engine(library=host['wholeLibrary'],resources=host['resources']) as engine:
   with engine.open(data,resolved) as session:
    with OwnedResult(session,'full') as result:arr=result.pixels().copy();info=result.info()
  arr.astype('<u2').tofile(dest/'rgba16.bin');records.append({'size':[width,height],'inputPixelHash':__import__('hashlib').sha256(data.tobytes()).hexdigest(),'outputHash':sha(dest/'rgba16.bin'),'result':info,'wallMs':(__import__('time').perf_counter()-start)*1000})
 atomic_json(root/'evidence/home-small-whole.json',{'version':'HomeSmallWholeOracleV1','cases':records,'libraryHash':sha(Path(host['wholeLibrary']))});raise SystemExit(0)
records=[];synthetic=[];script=Path(__file__).with_name('stream_case.py')
def call(name,inputname,rows,consumer,oracle=None,extra=()):
 dest=root/(a.prefix+name);cmd=[sys.executable,str(script),str(small/inputname),str(dest),'--manifest',str(manifest),'--manifest-sha',sha(manifest),'--library',host['streamLibrary'],'--resources',host['resources'],'--rows',str(rows),'--consumer-rows',str(consumer),*extra]
 if oracle:cmd+=['--oracle',str(oracle)]
 r=subprocess.run(cmd,capture_output=True,text=True);(root/'evidence'/f'{a.prefix}{name}-console.txt').write_text(r.stdout+r.stderr);need((dest/'report.json').exists(),'Caller failed without report '+r.stderr);record=load_json(dest/'report.json');print(name,record['success'],record.get('primaryError'),flush=True);return record
if not a.controls_only:
 for w,h in [(321,215),(641,429),(1200,801)]:
  for rows in ([1,17,37,113] if w==321 else [17,37,113]):
   r=call(f'exact-{w}-r{rows}',f'whole-scene-{w}x{h}.npy',rows,1 if rows==17 else 7,small/f'{w}-whole/rgba16.bin');records.append(r);atomic_json(root/(a.prefix+'small-exact-results.json'),{'version':'HomeSmallExactV2','cases':records,'syntheticPassed':False,'consumerPassed':False});need(r['success'],'Natural small gate failed')
 for rows in [1,7,17,64]:
  r=call(f'synthetic-r{rows}','synthetic.npy',rows,3,small/'97-whole/rgba16.bin');synthetic.append(r);need(r['success'],'Synthetic gate failed')
 for consumer in [1,3,7,11]:
  r=call(f'consumer-{consumer}','whole-scene-321x215.npy',37,consumer,small/'321-whole/rgba16.bin');need(r['success'],'Consumer gate failed')
 atomic_json(root/(a.prefix+'small-exact-results.json'),{'version':'HomeSmallExactV2','cases':records,'syntheticPassed':True,'consumerPassed':True,'syntheticCases':synthetic})
faults=[]
for fault in ['read','consumer','cancel','unknown','unknown-key','wrong-route','bad-input-hash','bad-backend','staging-budget','device-budget']:
 extra=['--fault',fault] if fault not in ['staging-budget','device-budget'] else (['--staging-mib','.001'] if fault=='staging-budget' else ['--device-mib','.001'])
 r=call('control-'+fault,'whole-scene-321x215.npy',17,7,extra=extra);need(not r['success'],'Fault unexpectedly succeeded');need(r.get('native',{}).get('deviceLiveAfter')==r.get('native',{}).get('stagingLiveAfter')==0,'Fault native live not zero');faults.append(r)
r=call('control-cleanup','whole-scene-321x215.npy',37,7,extra=['--fault','cleanup']);need(not r['success'] and r['cleanupErrors'],'Cleanup control failed')
atomic_json(root/(a.prefix+'control-results.json'),{'version':'HomeStreamControlsV2','cases':faults,'cleanupControl':r,'concurrencyPassed':False})
