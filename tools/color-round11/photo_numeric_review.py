"""Describe quantization/relative dark response, no algorithm tuning."""
from pathlib import Path
import json
import numpy as np
r=Path('N:/pixart/ColorResearchHome-20261030/round11');inp=r/'natural-inputs-v1';run=r/'natural-coverage-run1';manifest=json.loads((inp/'manifest.json').read_text());result=[]
def linear(v):return np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
for entry in manifest['sources']:
 id=entry['id'];a=np.load(inp/(id+'-display-proxy.npy')).astype(float);h,w,_=a.shape;b=np.memmap(run/id/'whole.rgba16',dtype='<u2',mode='r',shape=(h,w,4))[:,:,:3].astype(float)/65535;ya=linear(a)@np.array([.2126,.7152,.0722]);yb=linear(b)@np.array([.2126,.7152,.0722]);dark=(ya>1e-5)&(ya<.01);record={'id':id,'midDarkPixelCount':int(dark.sum()),'medianDarkYRatio':float(np.median(yb[dark]/ya[dark])) if dark.any() else None,'darkOutputNearConstantCodeBins':int(np.unique(np.rint(b[dark]*65535)).size),'note':'Natural image value histograms are not a monotonic gradient oracle; no smoothness proof or noise estimate.'};record['conditionalEndpointCounts']={'inputDisplayInteriorPixels':int(((a>0)&(a<1)).all(axis=2).sum()),'outputAnyZeroGivenAllInputInterior':int(((b==0).any(axis=2)&((a>0)&(a<1)).all(axis=2)).sum())};record['spatialJumpObservations']={}
 for label,x in [('before',a),('after',b)]:
  lum=linear(x)@np.array([.2126,.7152,.0722]);diff=np.abs(np.diff(lum,axis=0));seams=np.arange(17,h,17)-1;record['spatialJumpObservations'][label]={'medianAllVerticalDeltaY':float(np.median(diff)),'maxAllVerticalDeltaY':float(diff.max()),'medianAt17rowSeams':float(np.median(diff[seams])),'maxAt17rowSeams':float(diff[seams].max()),'interpretation':'actual image edges confound absolute row jumps; decisive seam test is exact whole/stream pixel comparison at all rows'}
 result.append(record)
print(json.dumps({'version':'NaturalEndpointDarkReviewV1','records':result,'absoluteAccuracy':'NOT_RUN','smoothGradientBanding':'NOT_ESTABLISHED_FROM_NATURAL_HISTOGRAM'},indent=2))
