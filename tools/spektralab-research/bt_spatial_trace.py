"""Bounded stage comparison of the existing BT repair. No new engine pixel edits."""
from pathlib import Path
import os,sys,json
import numpy as np
from run_matrix import sha,digest,require
root,complete,output=map(Path,sys.argv[1:4]);input_name=sys.argv[4] if len(sys.argv)>4 else 'join';output.mkdir(exist_ok=False);sys.path[:0]=[str(root/'source2/engine/tests'),str(root/'source2/engine/tools')]
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
library=root/'build-trace/engine/spektrafilm_engine.dll';report={'version':'BtSpatialPropagationV1','traceLibrarySha256':sha(library),'inputName':input_name,'cases':[]}
for film in ['kodak_portra_400','kodak_ektar_100']:
 captures={}
 for variant in ['original-os','patched-os']:
  key='709-'+input_name+'-'+film;folder=complete/variant/key;recipe=json.loads((folder/'recipe.json').read_text());image=np.load(folder/'input.npy');d=output/(film+'-'+variant);d.mkdir();os.environ['SPK_RESEARCH_TRACE']=str(d)
  with Engine(library=library,resources=complete/('resources-'+variant)) as e:
   with e.open(image,recipe['resolved']) as session:
    with OwnedResult(session,'full') as result:out=result.pixels().copy()
  require(digest(out)==recipe['outputPixelSha256'],'Passive stage trace changed pixels');capture={}
  for f in sorted(d.glob('*.f32')):
   parts=f.stem.split('-');h,w,c=map(int,parts[-3:]);name='-'.join(parts[1:-3]);capture[name]=(np.fromfile(f,dtype='<f4').reshape(h,w,c),f)
  captures[variant]=capture
 stages=[]
 for name,(left,p) in captures['original-os'].items():
  right,r=captures['patched-os'][name];delta=right-left;mask=np.any(delta!=0,axis=2);ys,xs=np.nonzero(mask);stages.append({'stage':name,'leftHash':sha(p),'rightHash':sha(r),'changedPixels':int(mask.sum()),'maxAbsFloat':float(np.abs(delta).max()),'changedXColumns':np.unique(xs).tolist()})
 report['cases'].append({'film':film,'fullOutputTraceExact':True,'stages':stages})
os.environ.pop('SPK_RESEARCH_TRACE',None);(output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps([{'film':c['film'],'stages':[(s['stage'],s['changedXColumns']) for s in c['stages']]} for c in report['cases']]))
