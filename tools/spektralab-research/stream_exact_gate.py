"""Small exact oracle gate before the single20MP attempt. No effect toggles."""
from pathlib import Path
import argparse,json,subprocess,sys
from natural_admission import sha,atomic_json
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('engine',type=Path);p.add_argument('--tag',default='');a=p.parse_args();base=Path(__import__('host_environment').environment()['smallRoot']);records=[]
for w,h in [(321,215),(641,429),(1200,801)]:
 for rows in ([1,17,37,113] if w==321 else [17,37,113]):
  dest=a.root/(f'exact-{w}-r{rows}'+a.tag);manifest=base/'admission-manifest-v2.json';cmd=[sys.executable,str(Path(__file__).with_name('stream_case.py')),str(base/f'whole-scene-{w}x{h}.npy'),str(dest),'--manifest',str(manifest),'--manifest-sha',sha(manifest),'--library',str(a.root/'build-stream-v1/engine/spektrafilm_engine.dll'),'--resources',str(Path(__import__('host_environment').environment()['resources'])),'--rows',str(rows),'--consumer-rows',str(1 if rows==17 else 7),'--oracle',str(base/f'{w}-whole/rgba16.bin')];proc=subprocess.run(cmd,capture_output=True,text=True);r=json.loads((dest/'report.json').read_text());records.append({'directory':dest.name,**r});atomic_json(a.root/('small-exact-results'+a.tag+'.json'),{'version':'StreamingSmallWholeOracleV1','cases':records});print(proc.stdout.strip(),flush=True)
  if not r['success']:raise RuntimeError('First exact mismatch/error retained. Stop before full.')
