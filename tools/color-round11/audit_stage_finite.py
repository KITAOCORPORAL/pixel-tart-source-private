"""Passive stage finite audit on existing6 proxies, natural pixels strictly local."""
from pathlib import Path
import argparse,os,json,sys,re,hashlib
import numpy as np
repo=Path(__file__).resolve().parents[2];sys.path.insert(0,str(repo/'tools/cross-device-diagnostic'));from compare_rows import compare
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('output',type=Path);a=p.parse_args();r=a.root;out=a.output
if out.exists():raise ValueError('Finite observation output exists')
out.mkdir();sys.path[:0]=[str(r/'source-patch/engine/tests'),str(r/'source-patch/engine/tools')];from spk_ctypes import Engine;from windows_lifecycle import OwnedResult
m=json.loads((r/'round11/natural-inputs-v1/manifest.json').read_text());resolved=json.loads((repo/'tools/cross-device-diagnostic/resolved-portraA.json').read_text());lib=r/'round11/build-natural-finite-trace/engine/spektrafilm_engine.dll';report={'version':'NaturalPassiveStageFiniteV1','traceLibrarySha256':hashlib.sha256(lib.read_bytes()).hexdigest(),'scope':'whole fixed1200px pipeline 12stages, passive readback; does not inspect every arithmetic instruction; stream final equals whole separately','privateStageFilesRemainLocal':True,'cases':[]}
for x in m['sources']:
 id=x['id'];image=np.load(r/'round11/natural-inputs-v1'/x['file']);h,w,_=image.shape;d=out/id;d.mkdir();os.environ['SPK_SYNTHETIC_STAGE_DIR']=str(d.resolve())
 try:
  with Engine(library=lib,resources=r/'resources-patch') as e:
   with e.open(image,resolved) as s:
    with OwnedResult(s,'full') as held:arr=held.pixels().copy()
 finally:os.environ.pop('SPK_SYNTHETIC_STAGE_DIR',None)
 path=d/'traced.rgba16';arr.astype('<u2').tofile(path);diff=compare(r/'round11/natural-coverage-run1'/id/'whole.rgba16',path,[h,w,4],'<u2')
 if diff['changedChannels']:raise ValueError('Finite trace changed final output '+id)
 stages=[]
 for fp in sorted(d.glob('*.f32')):
  match=re.match(r'\d+-(.+)-(\d+)-(\d+)-(\d+)\.f32$',fp.name)
  if not match:raise ValueError('Bad stage name')
  name,hh,ww,cc=match.groups();data=np.fromfile(fp,dtype='<f4')
  if data.size!=int(hh)*int(ww)*int(cc):raise ValueError('Bad stage length')
  nonfinite=int((~np.isfinite(data)).sum());stages.append({'stage':name,'shape':[int(hh),int(ww),int(cc)],'sha256':hashlib.sha256(fp.read_bytes()).hexdigest(),'nonfiniteSamples':nonfinite,'min':float(data.min()),'max':float(data.max()),'negativeSamples':int((data<0).sum()),'aboveOneSamples':int((data>1).sum()),'zeroSamples':int((data==0).sum()),'note':'range depends on stage domain; >1 negative density is not output clipping'})
  if nonfinite:raise ValueError('Nonfinite actual intermediate '+id+'/'+name)
 if len(stages)!=12:raise ValueError('Missing stage')
 report['cases'].append({'id':id,'tracedOutputExact':True,'stages':stages});(out/'report.json').write_text(json.dumps(report,indent=2,allow_nan=False))
print(json.dumps({'photos':len(report['cases']),'all12StagesFinite':True,'passiveFinalExact':True}))
