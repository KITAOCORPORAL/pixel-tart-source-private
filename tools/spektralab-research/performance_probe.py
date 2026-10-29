import json,sys,time,threading,subprocess,ctypes,os
from pathlib import Path
import numpy as np
from run_matrix import timed,digest,require,sha
b=Path(os.environ['TEMP'])/'guangjing-spektra-research-20261029';sys.path.insert(0,str(b/'source2/engine/tests'));sys.path.insert(0,str(b/'source2/engine/tools'))
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
x=np.linspace(.005,.8,1281,dtype=np.float32)[None,:];y=np.linspace(.01,.6,853,dtype=np.float32)[:,None];img=np.empty((853,1281,3),np.float32);img[:,:,0]=x;img[:,:,1]=y;img[:,:,2]=.18;img[:50,:50]=3
samples=[];stop=threading.Event()
def poll():
 while not stop.is_set():
  try:samples.append({'time':time.perf_counter(),'gpuUsedMiB':int(subprocess.check_output(['nvidia-smi','--query-gpu=memory.used','--format=csv,noheader,nounits'],text=True).strip())})
  except Exception as e:samples.append({'error':str(e)})
  stop.wait(.05)
t=threading.Thread(target=poll);t.start();e,init=timed(lambda:Engine(library=b/'build-vulkan/engine/spektrafilm_engine.dll',resources=b/'resources-research'));p={'grain_active':False,'glare_active':False,'halation_active':True,'auto_exposure':False,'input_color_space':'ProPhoto RGB','input_cctf_decoding':False,'output_color_space':'sRGB','output_cctf_encoding':True};s,upload=timed(lambda:e.open(img,p));rows=[]
for i in range(5):
 r,ms=timed(lambda:OwnedResult(s));copy,copyms=timed(lambda:r.pixels().copy());rows.append({'kind':'first' if i==0 else 'warm','renderReadbackMs':ms,'copyMs':copyms,'info':r.info()});r.close()
report={'version':'SpektraLocalPerformanceV1','syntheticSize':[1281,853],'inputHash':digest(img),'params':s.get_params(),'initMs':init,'openUploadMs':upload,'renders':rows,'memoryLedger':e.memory_report()};s.close();e.close();stop.set();t.join();vals=[x['gpuUsedMiB'] for x in samples if 'gpuUsedMiB'in x];report.update(gpuGlobalUsedSamples=samples,gpuGlobalMinMaxMiB=[min(vals),max(vals)] if vals else None,vramLimitation='global nvidia-smi polling includes other applications, not process VRAM attribution or guaranteed peak');print(json.dumps(report,indent=2))
