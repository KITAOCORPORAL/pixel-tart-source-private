import sys,json,os
from pathlib import Path
import numpy as np
from run_matrix import sha,digest,require
b=Path(os.environ['TEMP'])/'guangjing-spektra-research-20261029';sys.path.insert(0,str(b/'source2/engine/tests'));sys.path.insert(0,str(b/'source2/engine/tools'))
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
ramp=np.geomspace(.00001,4,1025,dtype=np.float32);image=np.broadcast_to(ramp[None,:,None],(17,1025,3)).copy();report={'version':'SpektraNeutralContinuityProbeV1','input':'synthetic neutral log ramp, linear standard ProPhoto D50, 0.00001..4','inputHash':digest(image),'physicalAccuracy':'NOT_MEASURED','cases':[]}
with Engine(library=b/'build-vulkan/engine/spektrafilm_engine.dll',resources=b/'resources-research') as e:
 for film,paper in [('kodak_portra_400','kodak_portra_endura'),('kodak_ektar_100','kodak_ultra_endura'),('kodak_tri_x_400','ilford_multigrade_iv_rc')]:
  params={'film_stock':film,'print_stock':paper,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'input_color_space':'ProPhoto RGB','input_cctf_decoding':False,'output_color_space':'sRGB','output_cctf_encoding':True}
  if film=='kodak_tri_x_400':params.update(m_filter_neutral=0,y_filter_neutral=68,print_exposure=1,enlarger_illuminant='TH-KG3')
  with e.open(image,params) as s:
   with OwnedResult(s) as x:
    a=x.pixels().copy();row=a[8,:,:3].astype(int);delta=np.diff(row,axis=0);report['cases'].append({'film':film,'paper':paper,'resolved':s.get_params(),'outputHash':digest(a),'zeroFractionPerChannel':(row==0).mean(axis=0).tolist(),'maximumFractionPerChannel':(row==65535).mean(axis=0).tolist(),'monotonicityViolationsPerChannel':(delta<0).sum(axis=0).tolist(),'maxPositiveStepU16':delta.max(axis=0).tolist(),'maxNegativeStepU16':delta.min(axis=0).tolist(),'maxNeutralChannelSpread':int((row.max(axis=1)-row.min(axis=1)).max()),'note':'Colored paper response need not be RGB-neutral; counts are continuity observations, not target calibration.'})
print(json.dumps(report,indent=2))
