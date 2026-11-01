"""Additional small-ROI relative findings; operates on existing proxy outputs only."""
from pathlib import Path
import json,numpy as np
r=Path('N:/pixart/ColorResearchHome-20261030/round11');run=r/'natural-coverage-run1';m=json.loads((r/'natural-inputs-v1/manifest.json').read_text());support=json.loads((r/'prior-endpoint-footprint-audit.json').read_text());records=[]
def eotf(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
for x in m['sources']:
 id=x['id'];h,w,_=x['proxyShape'];a=np.load(r/'natural-inputs-v1'/(id+'-A-proxy.npy'));b=np.memmap(run/id/'whole.rgba16',dtype='<u2',mode='r',shape=(h,w,4))[:,:,:3];prior=np.load(run/id/'proxy-prior-endpoint-support.npy');outputZero=(b==0).any(axis=2);inputOog=(a<=0).any(axis=2)|(a>=1).any(axis=2);clean=~prior&~inputOog
 first=np.argwhere(outputZero&clean);firstxy=[int(first[0,1]),int(first[0,0])] if len(first) else None
 records.append({'id':id,'firstCleanInputOutputZeroProxyXY':firstxy,'outputAnyZeroPixels':int(outputZero.sum()),'outputZeroWithAllProxyChannelsStrictInteriorAndNoPriorEndpointFootprint':int((outputZero&clean).sum()),'interpretation':'Processing endpoint evidence after admission, not proof of sensor clipping or which internal film node caused it; input standard ProPhoto D50 interior and underlying final-master endpoint-free support only.'})
# Source aligned shadow and skin regions, not calibrated references.
roi=[]
for id,label,x,y,w,h in [('canonr6','vehicle_shadow',0,500,300,250),('nikond5100','skin_arm_small',310,390,35,60),('sonynex5r','flower_detail',270,400,300,250)]:
 before=np.load(r/'natural-inputs-v1'/(id+'-display-proxy.npy'));hh,ww,_=before.shape;after=np.memmap(run/id/'whole.rgba16',dtype='<u2',mode='r',shape=(hh,ww,4))[:,:,:3]/65535;a=before[y:y+h,x:x+w].astype(float);b=after[y:y+h,x:x+w];ya=eotf(a)@np.array([.2126,.7152,.0722]);yb=eotf(b)@np.array([.2126,.7152,.0722]);roi.append({'id':id,'label':label,'boxXYWH':[x,y,w,h],'linearYBeforeQuantiles':np.quantile(ya,[0,.05,.5,.95,1]).tolist(),'linearYAfterQuantiles':np.quantile(yb,[0,.05,.5,.95,1]).tolist(),'linearYStdBefore':float(ya.std()),'linearYStdAfter':float(yb.std()),'stdRatio':float(yb.std()/ya.std()),'medianYRatio':float(np.median(yb)/np.median(ya)),'meanDisplayRgbBefore':a.mean(axis=(0,1)).tolist(),'meanDisplayRgbAfter':b.mean(axis=(0,1)).tolist(),'domain':'display sRGB EOTF luma comparison; not film density or physical accuracy','roiSelection':'manual development review after seeing images; not holdout or optimization target'})
print(json.dumps({'version':'NaturalConcreteFindingsV1','endpointRecords':records,'relativeRoi':roi,'algorithmChanged':False},indent=2))

