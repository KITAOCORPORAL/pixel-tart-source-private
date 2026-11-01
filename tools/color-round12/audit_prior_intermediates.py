"""Read-only provenance for selected prior endpoint/display intermediates."""
from pathlib import Path
import sys,json
import numpy as np
from PIL import Image
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'color-round11'))
from verify_saved_outputs import sha
from diagnostic.package_validation import need,read
root,output=map(Path,sys.argv[1:3]);repo=Path(__file__).resolve().parents[2];m=read(root/'round11/natural-inputs-v1/manifest.json');records=[]
for photo_id in ['sonynex5r','canonr6']:
 e=next(v for v in m['sources'] if v['id']==photo_id);width,height=e['originalSize'];h,w,_=e['proxyShape'];record={'id':photo_id}
 master=root/('round11/production-'+photo_id)/'production-linear-d65.f32';need(sha(master).upper()==e['productionLinearSha256'],'Master hash mismatch');record['masterHashMatches']=True
 data=np.memmap(master,dtype='<f4',mode='r',shape=(height,width,3));support=np.zeros((h,w),bool)
 for c in range(3):
  for endpoint in [0,1]:
   mask=(data[:,:,c]==endpoint).astype(np.float32);small=np.asarray(Image.fromarray(mask,mode='F').resize((w,h),Image.Resampling.BOX));support|=small>0
 old=root/('round11/natural-coverage-run1/'+photo_id+'/proxy-prior-endpoint-support.npy');need(np.array_equal(support,np.load(old)),'Saved footprint differs');record['supportMaskSha256']=sha(old);record['supportRecomputedExact']=True
 if photo_id=='canonr6':
  dp=root/'round11/production-canonr6/production-display-srgb.f32';need(sha(dp).upper()==e['productionDisplaySha256'],'Display master hash');disp=np.memmap(dp,dtype='<f4',mode='r',shape=(height,width,3));small=np.stack([np.asarray(Image.fromarray(np.array(disp[:,:,c]),mode='F').resize((w,h),Image.Resampling.BOX)) for c in range(3)],2);oldp=root/'round11/natural-inputs-v1/canonr6-display-proxy.npy';need(np.array_equal(small,np.load(oldp)),'Display proxy changed');record['displayProxySha256']=sha(oldp);record['displayProxyRecomputedExact']=True;record['fixedRoiPixelsWithPriorMasterEndpointSupport']=int(support[500:750,:300].sum())
 records.append(record)
with output.open('x',encoding='utf-8') as f:json.dump({'version':'Round12PriorIntermediateIdentityV1','rawDecodeCalls':0,'gpuCalls':0,'scope':'existing decoded master read only; does not revisit RAW or render20MP','records':records},f,indent=2)
print(json.dumps(records))
