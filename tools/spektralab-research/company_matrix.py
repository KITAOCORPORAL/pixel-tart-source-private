"""Company-only small deterministic matrix, no natural-scene or throughput claims."""
import argparse,sys,time,json,hashlib,subprocess
from pathlib import Path
import numpy as np
p=argparse.ArgumentParser();p.add_argument('checkout',type=Path);p.add_argument('build',type=Path);p.add_argument('resources',type=Path);p.add_argument('output',type=Path);p.add_argument('--reopen',action='store_true');p.add_argument('--trace',type=Path);a=p.parse_args();sys.path[:0]=[str(a.checkout/'engine/tests'),str(a.checkout/'engine/tools')]
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
from run_matrix import sha,digest
from PIL import Image,ImageCms
def require(ok,msg):
 if not ok:raise ValueError(msg)
def resources_hash(root):
 return hashlib.sha256(json.dumps([(str(p.relative_to(root)).replace('\\','/'),sha(p)) for p in sorted(root.rglob('*')) if p.is_file()],separators=(',',':')).encode()).hexdigest()
if a.reopen:
 saved=json.loads(a.resources.read_text());case=saved['case'];image=np.load(a.resources.parent/'input.npy');params=case['resolved'];
 require(sha(a.build/'engine/spektrafilm_engine.dll')==saved['dllSha256'],'Backend hash mismatch');require(digest(image)==case['inputHash'],'Input hash mismatch');require(resources_hash(Path(saved['resourceDirectory']))==saved['resourcesSha256'],'Resources mismatch')
 with Engine(library=a.build/'engine/spektrafilm_engine.dll',resources=Path(saved['resourceDirectory'])) as e:
  with e.open(image,params) as session:
   with OwnedResult(session) as result:out=result.pixels().copy()
 require(digest(out)==case['outputHash'],'Fresh-process render differs');print(json.dumps({'freshProcess':True,'renderExact':True}));raise SystemExit
require(subprocess.check_output(['git','-C',str(a.checkout),'rev-parse','HEAD'],text=True).strip()=='c1447c6ab618fa4b1cb11fd2fe12826855007059','Wrong engine revision');
require(not a.output.exists(),'Output exists');a.output.mkdir();report={'version':'CompanyDeterministicMatrixV1','device':'GTX1650 driver572.70','naturalScene':False,'randomGrain':False,'glare':False,'dllSha256':sha(a.build/'engine/spektrafilm_engine.dll'),'cases':[]}
ramp=np.geomspace(.00001,4,1025,dtype=np.float32)
inputs={'linear':np.broadcast_to(np.linspace(-.01,2,97,dtype=np.float32)[None,:,None],(41,97,3)).copy(),'display':np.broadcast_to(np.linspace(0,1,97,dtype=np.float32)[None,:,None],(41,97,3)).copy(),'neutral_log':np.broadcast_to(ramp[None,:,None],(17,1025,3)).copy()}
with Engine(library=a.build/'engine/spektrafilm_engine.dll',resources=a.resources) as e:
 for name,image in inputs.items():
  for film,paper in [('kodak_portra_400','kodak_portra_endura'),('kodak_ektar_100','kodak_ultra_endura'),('kodak_tri_x_400','ilford_multigrade_iv_rc')]:
   params={'film_stock':film,'print_stock':paper,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'input_color_space':'sRGB' if name=='display' else 'ProPhoto RGB','input_cctf_decoding':name=='display','output_color_space':'sRGB','output_cctf_encoding':True}
   if film=='kodak_tri_x_400':params.update(m_filter_neutral=0,y_filter_neutral=68,print_exposure=1,enlarger_illuminant='TH-KG3')
   start=time.perf_counter()
   if a.trace:
    import os
    td=a.trace/(name+'-'+film);td.mkdir(parents=True,exist_ok=False);os.environ['SPK_RESEARCH_TRACE']=str(td)
   with e.open(image,params) as s:
    with OwnedResult(s) as result:out=result.pixels().copy()
    with OwnedResult(s) as result:repeat=result.pixels().copy()
    resolved=s.get_params()
   require(digest(out)==digest(repeat),'Deterministic repeat differs');key=name+'-'+film;np.save(a.output/(key+'.npy'),out)
   delta=np.diff(out[out.shape[0]//2,:,:3].astype(int),axis=0);indices=np.argwhere(delta<0)
   report['cases'].append({'input':name,'film':film,'paper':paper,'inputHash':digest(image),'shape':list(out.shape),'resolved':resolved,'outputHash':digest(out),'repeatExact':True,'elapsedMsIncludingTwoRenders':(time.perf_counter()-start)*1000,'declines':[{'index':int(i),'channel':int(c),'inputLeft':float(image[0,i,0]),'inputRight':float(image[0,i+1,0]),'left':int(out[out.shape[0]//2,i,c]),'right':int(out[out.shape[0]//2,i+1,c])} for i,c in indices]})
   # Persist resolved recipe plus original input, then ask a genuinely new process.
   saved=a.output/key;saved.mkdir();np.save(saved/'input.npy',image);case=report['cases'][-1];recipe=saved/'recipe.json';recipe.write_text(json.dumps({'case':case,'dllSha256':report['dllSha256'],'resourcesSha256':resources_hash(a.resources),'resourceDirectory':str(a.resources.resolve())}))
   child=subprocess.run([sys.executable,str(Path(__file__).resolve()),str(a.checkout),str(a.build),str(recipe),str(saved),'--reopen'],capture_output=True,text=True)
   require(child.returncode==0,child.stderr);case['freshProcessReopen']=json.loads(child.stdout)
   tiff=saved/'display-rgba16.tif'
   import tifffile
   tifffile.imwrite(tiff,out,photometric='rgb',extrasamples='unassalpha',iccprofile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes());read=tifffile.imread(tiff);require(np.array_equal(out,read),'TIFF16 differs');case['tiff16Exact']=True;case['tiff16Hash']=sha(tiff)
(a.output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({'cases':len(report['cases']),'repeatExact':all(c['repeatExact'] for c in report['cases'])}))
