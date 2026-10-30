"""Read-only evidence: actual BT decode node inside full-engine render."""
from pathlib import Path
import os,sys,json,shutil,hashlib
import numpy as np
from run_matrix import sha,digest,require
root,complete,output=map(Path,sys.argv[1:4]);input_name=sys.argv[4] if len(sys.argv)>4 else 'join';output.mkdir(exist_ok=False);sys.path[:0]=[str(root/'source2/engine/tests'),str(root/'source2/engine/tools')]
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
library=root/'round5/build-decode-trace/engine/spektrafilm_engine.dll';report={'version':'BtActualDecodeTapV1','passiveTraceLibrarySha256':sha(library),'inputName':input_name,'cases':[]}
for variant in ['original','patched','original-os','patched-os']:
 resources=complete/('resources-'+variant)
 for space in ['ITU-R BT.709','ITU-R BT.2020']:
  key=space[-3:]+'-'+input_name+'-kodak_portra_400';folder=complete/variant/key;recipe=json.loads((folder/'recipe.json').read_text());image=np.load(folder/'input.npy');tap=output/(variant+'-'+space[-3:]+'.f32');os.environ['SPK_R5_DECODE_FILE']=str(tap)
  with Engine(library=library,resources=resources) as e:
   with e.open(image,recipe['resolved']) as session:
    with OwnedResult(session,'full') as result:out=result.pixels().copy()
  require(digest(out)==recipe['outputPixelSha256'],'Passive trace changed full output');decoded=np.fromfile(tap,dtype='<f4').reshape(image.shape);row=[]
  for i in (range(image.shape[1]) if input_name=='join' else [0,127,128,129,image.shape[1]-1]):
   v=float(image[0,i,0]);linear=v/4.5;power=np.sign(v)*abs((v+float(np.float32(1.099)-np.float32(1)))/float(np.float32(1.099)))**float(np.float32(1)/np.float32(.45))
   value=float(decoded[0,i,0]);branch='linear' if abs(value-linear)<abs(value-power) else 'power';row.append({'column':i,'input':v,'inputBits':hex(int(image[0,i,0].view(np.uint32))),'decoded':value,'decodedBits':hex(int(decoded[0,i,0].view(np.uint32))),'nearestAnalyticBranch':branch,'linearReference':linear,'powerReference':power})
  report['cases'].append({'variant':variant,'space':space,'decodeTapSha256':sha(tap),'fullOutputUnaffectedByTap':True,'samples':row})
os.environ.pop('SPK_R5_DECODE_FILE',None)
# Show exact decode influence vs final complete renderer's spatial spread.
for space in ['ITU-R BT.709','ITU-R BT.2020']:
 l=next(c for c in report['cases'] if c['variant']=='original-os' and c['space']==space);r=next(c for c in report['cases'] if c['variant']=='patched-os' and c['space']==space);report.setdefault('comparisons',[]).append({'space':space,'decodeChangedColumns':[x['column'] for x,y in zip(l['samples'],r['samples']) if x['decodedBits']!=y['decodedBits']],'decodeChangedInputBits':[x['inputBits'] for x,y in zip(l['samples'],r['samples']) if x['decodedBits']!=y['decodedBits']]})
(output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report['comparisons']))
