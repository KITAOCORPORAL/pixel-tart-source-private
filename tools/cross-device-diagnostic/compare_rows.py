"""Rows-only comparison, first differences plus exact histogram quantiles for U16.
Float stages report numeric deltas; no cross-device pass threshold is invented.
"""
from pathlib import Path
import argparse,json
import numpy as np
def compare(left,right,shape,dtype):
 dtype=np.dtype(dtype)
 if len(shape)!=3 or any(type(n) is not int or n<=0 for n in shape) or dtype not in (np.dtype('<u2'),np.dtype('<f4')):raise ValueError('Unsupported comparison domain/shape')
 n=int(np.prod(shape));size=n*dtype.itemsize
 if Path(left).stat().st_size!=size or Path(right).stat().st_size!=size:raise ValueError('Length/shape mismatch')
 a=np.memmap(left,dtype=dtype,mode='r',shape=tuple(shape));b=np.memmap(right,dtype=dtype,mode='r',shape=tuple(shape));changed=0;first=None;maximum=0.;total=0.;hist=np.zeros(65536,np.int64) if dtype==np.dtype('<u2') else None;values=[];nonfinite=0
 for y in range(shape[0]):
  x=np.asarray(a[y]);z=np.asarray(b[y]);finite=np.isfinite(x)&np.isfinite(z);nonfinite+=int((~finite).sum());
  if not finite.all():raise ValueError('Nonfinite comparison artifact; no numeric acceptance claimed')
  d=np.abs(x.astype(np.float64)-z.astype(np.float64));neq=(x!=z);changed+=int(neq.sum())
  if first is None and neq.any():
   xc=np.argwhere(neq)[0].tolist();xx,cc=map(int,xc);first={'x':xx,'y':y,'channel':cc,'left':float(x[xx,cc]),'right':float(z[xx,cc]),'absDelta':float(d[xx,cc])}
  valid=d[finite];maximum=max(maximum,float(valid.max(initial=0)));total+=float(valid.sum())
  if hist is not None:hist+=np.bincount(valid.astype(np.uint16).ravel(),minlength=65536)
  else:values.append(valid.ravel())
 if hist is not None:
  cumulative=np.cumsum(hist);qs={str(q):int(np.searchsorted(cumulative,q*(n-1)+1)) for q in [0,.5,.9,.95,.99,.999,1]}
 else:qs={str(q):float(np.quantile(np.concatenate(values),q)) for q in [0,.5,.9,.95,.99,.999,1]} if values else {}
 return {'shape':shape,'dtype':dtype.str,'channelValues':n,'changedChannels':changed,'exact':changed==0 and nonfinite==0,'firstDifference':first,'maxAbsoluteDelta':maximum,'meanAbsoluteDelta':total/max(1,n-nonfinite),'absoluteDeltaQuantiles':qs,'quantileMethod':'higher exact integer histogram' if hist is not None else 'linear NumPy quantile, all small finite stage samples','nonfinitePairs':nonfinite}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('left');p.add_argument('right');p.add_argument('--shape',nargs=3,type=int,required=True);p.add_argument('--dtype',choices=['<u2','<f4'],default='<u2');p.add_argument('--output',type=Path);a=p.parse_args();r=compare(a.left,a.right,a.shape,a.dtype);text=json.dumps(r,indent=2,allow_nan=False)
 if a.output:
  with a.output.open('x') as f:f.write(text)
 print(text)
