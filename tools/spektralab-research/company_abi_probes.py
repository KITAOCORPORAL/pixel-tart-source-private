"""Bounded company C-ABI probes. No natural input, no random grain, no user seed."""
import sys,ctypes,threading,time,json
from pathlib import Path
import numpy as np
from run_matrix import sha,digest,require
root,build,resources,output=map(Path,sys.argv[1:]);sys.path[:0]=[str(root/'engine/tests'),str(root/'engine/tools')]
from spk_ctypes import Engine,EngineError,SpkResult
from windows_lifecycle import OwnedResult
report={'version':'CompanyAbiProbesV1','libraryHash':sha(build/'engine/spektrafilm_engine.dll'),'grainSeed':'UNSUPPORTED','params':{'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'input_color_space':'sRGB','input_cctf_decoding':True,'output_color_space':'sRGB','output_cctf_encoding':True}}
with Engine(library=build/'engine/spektrafilm_engine.dll',resources=resources) as e:
 a=np.full((17,31,4),.18,np.float32);a[:,:,3]=.125
 with e.open(a,report['params']) as s:
  with OwnedResult(s) as x:report['alpha']={'input':.125,'outputMin':int(x.pixels()[:,:,3].min()),'outputMax':int(x.pixels()[:,:,3].max()),'rowStride':x.result.row_stride_px,'width':x.result.width}
  before=s.get_params()
  try:s.set_params({'grain_seed':42});raise ValueError('Seed unexpectedly accepted')
  except EngineError as err:report['seedRejected']=str(err)
  require(before==s.get_params(),'Rejected parameter mutated session')
 image=np.broadcast_to(np.linspace(0,1,1025,dtype=np.float32)[None,:,None],(769,1025,3)).copy()
 with e.open(image,report['params']) as s:
  e._lib.spk_cancel.argtypes=[ctypes.c_void_p,ctypes.c_char_p];result=SpkResult();status=[]
  def work():status.append(e._lib.spk_render(s._handle,b'full',ctypes.byref(result)))
  t=threading.Thread(target=work);t.start();calls=0
  while t.is_alive():e._lib.spk_cancel(s._handle,None);calls+=1;time.sleep(.001)
  t.join();e._lib.spk_result_free(ctypes.byref(result));require(status==[-7],'Cancellation did not return -7')
  report['cancel']={'status':status[0],'cancelCalls':calls,'sessionReleasedOnlyAfterJoin':True,'grain':False,'glare':False,'granularity':'node boundary; not shader-preemptive'}
with output.open('x') as f:json.dump(report,f,indent=2)
print(json.dumps({'alphaOpaque':report['alpha']['outputMin']==65535,'seedRejected':True,'cancel':report['cancel']}))
