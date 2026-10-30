"""Home same-width height check. Original pipeline and exact whole oracle, no full20MP."""
from pathlib import Path
import argparse,json,sys,subprocess,copy
import numpy as np
from natural_admission import sha,npy_info,scan,atomic_json,load_json,output_path,need,DOMAINS
from host_environment import environment
p=argparse.ArgumentParser();p.add_argument('--prefix',required=True);a=p.parse_args();host=environment();r=Path(host['researchRoot']);base=Path(host['smallRoot']);d=output_path(r/(a.prefix+'-input'));d.mkdir();original=np.load(base/'whole-scene-321x215.npy');image=np.concatenate([original,original[::-1]],axis=0);inp=d/'height430.npy';np.save(inp,image);m=copy.deepcopy(load_json(base/'admission-manifest-v2.json'));i=npy_info(inp);s=scan(inp,i);m['inputRoot']=str(d);m['files']=[{'path':inp.name,'fileSha256':sha(inp),'shape':i['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':'ExistingResearchD65ToD50V1','pixelSha256':s['pixelSha256'],'bytes':i['fileBytes']}];m['preparationIdentity']['mode']='Height diagnostic, original small complete scene plus vertical reverse; not a new scene';atomic_json(d/'manifest.json',m);sys.path[:0]=[str(r/'source-patch/engine/tests'),str(r/'source-patch/engine/tools')]
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
resolved=load_json(r/'exact-321-r37/report.json')['resolved']
with Engine(library=host['wholeLibrary'],resources=host['resources']) as engine:
 with engine.open(image,resolved) as session:
  with OwnedResult(session,'full') as out:out.pixels().copy().astype('<u2').tofile(d/'whole.bin')
dest=r/(a.prefix+'-stream');cmd=[sys.executable,str(Path(__file__).with_name('stream_case.py')),str(inp),str(dest),'--manifest',str(d/'manifest.json'),'--manifest-sha',sha(d/'manifest.json'),'--library',host['streamLibrary'],'--resources',host['resources'],'--rows','37','--oracle',str(d/'whole.bin')];proc=subprocess.run(cmd,capture_output=True,text=True);(r/'evidence'/(a.prefix+'-console.txt')).write_text(proc.stdout+proc.stderr);actual=load_json(dest/'report.json');small=load_json(r/'exact-321-r37/report.json');checks={k:[small['native'][k],actual['native'][k]] for k in ['devicePeakBytes','stagingPeakBytes','hostPeakBudgetedBytes']};need(actual['success'] and all(x[0]==x[1] for x in checks.values()),'Height bound or exactness failed');report={'version':'HomeStreamingHeightProofV1','sameWidth':321,'heights':[215,430],'rows':37,'peaks':checks,'wholeExact':actual['oracle'],'sameDevice':True,'no20MPrender':True,'success':True};atomic_json(r/'evidence'/(a.prefix+'.json'),report);print(json.dumps(report))
