"""Trace endpoint support through the SAME per-channel BOX proxy, no RAW re-decode."""
from pathlib import Path
import json,numpy as np
from PIL import Image
r=Path('N:/pixart/ColorResearchHome-20261030/round11');manifest=json.loads((r/'natural-inputs-v1/manifest.json').read_text());records=[]
for entry in manifest['sources']:
 id=entry['id'];h,w=entry['originalSize'][1],entry['originalSize'][0];ph,pw,_=entry['proxyShape'];source=np.memmap(r/('production-'+id)/'production-linear-d65.f32',dtype='<f4',mode='r',shape=(h,w,3));output=np.memmap(r/'natural-coverage-run1'/id/'whole.rgba16',dtype='<u2',mode='r',shape=(ph,pw,4));supports={}
 try:
  for label,threshold in [('zero',0),('one',1)]:
   proxy=np.empty((ph,pw,3),np.float32)
   for c in range(3):
    mask=np.empty((h,w),np.float32)
    for y in range(0,h,32):mask[y:y+32]=(source[y:y+32,:,c]==threshold).astype(np.float32)
    proxy[:,:,c]=np.asarray(Image.fromarray(mask,mode='F').resize((pw,ph),Image.Resampling.BOX));del mask
   supports[label]=proxy
 finally:source._mmap.close()
 lo=output[:,:,:3]==0;hi=output[:,:,:3]==65535;priorAny=np.any((supports['zero']>0)|(supports['one']>0),axis=2);newZero=lo.any(axis=2)&~priorAny
 records.append({'id':id,'shape':[ph,pw,3],'priorEndpointSupportPixelFraction':float(priorAny.mean()),'outputZeroPixelsWithPriorSupport':int((lo.any(axis=2)&priorAny).sum()),'outputZeroPixelsWithoutAnyPriorEndpointSupport':int(newZero.sum()),'outputMaxPixelsWithoutPriorEndpointSupport':int((hi.any(axis=2)&~priorAny).sum()),'allOutputZeroPixels':int(lo.any(axis=2).sum()),'classification':'Same proxy footprint support of production integer-master endpoints; no raw sensor claim; working-space/gamut changes can still create zeros. Independent source masks do not isolate which internal film node clipped.'})
 # Persist only local spatial masks for follow-up, no source or image bytes into Git.
 np.save(r/'natural-coverage-run1'/id/'proxy-prior-endpoint-support.npy',priorAny)
print(json.dumps({'version':'PriorEndpointFootprintAuditV1','method':'Pillow F BOX of zero/one indicator per source master channel; any positive endpoint coverage counts contaminated footprint','records':records},indent=2))
