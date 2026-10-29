import ctypes,json,os,sys
from pathlib import Path
import numpy as np
from run_matrix import require,digest
b=Path(os.environ['TEMP'])/'guangjing-spektra-research-20261029';sys.path.insert(0,str(b/'source2/engine/tests'));sys.path.insert(0,str(b/'source2/engine/tools'))
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
base={'grain_active':True,'glare_active':False,'halation_active':False,'auto_exposure':False,'input_color_space':'ProPhoto RGB','input_cctf_decoding':False,'output_color_space':'sRGB'};image=np.ones((129,193,3),np.float32)*.18;image[0:8,0:8]=4
with Engine(library=b/'build-vulkan/engine/spektrafilm_engine.dll',resources=b/'resources-research') as e:
 with e.open(image,base) as s:
  hashes=[];arrays=[]
  for i in range(3):
   with OwnedResult(s) as r:hashes.append(r.digest());arrays.append(r.pixels().copy())
 report={'sameSessionGrainHashes':hashes,'sameSessionExact':len(set(hashes))==1,'freshSessionSeedNote':'Each Pipeline starts from same rng_state constant; repeated new sessions may repeat initial realization. C ABI has no arbitrary grain seed setter.'}
 for rows in (1,7,17):
  with e.open(image,{**base,'striped':True,'strip_rows':rows}) as s:
   with OwnedResult(s) as r:
    delta=r.pixels().astype(int)-arrays[0].astype(int);report[f'firstSeedStripe{rows}']={'changedChannels':int((delta!=0).sum()),'maxU16':int(abs(delta).max())}
 print(json.dumps(report,indent=2))
