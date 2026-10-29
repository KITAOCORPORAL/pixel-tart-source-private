import sys,json
from pathlib import Path
import numpy as np
from render_compare import display
from stage_validation import validate_stage,validate_candidate,sha,require
root=Path(sys.argv[1]);kind=sys.argv[2];ev=float(sys.argv[3]);require(kind in ('u16x4','f32'),'Unknown domain kind');require(np.isfinite(ev) and -10<=ev<=10,'Invalid exposure');actualpath=Path(sys.argv[4]);m=validate_stage(root);h=m['sizes']['IHeight'];w=m['sizes']['IWidth']
if kind=='u16x4': src=np.memmap(root/'matrix_after.u16',dtype='<u2',mode='r',shape=(h,w,4))
else:
 candidate=Path(sys.argv[5]);validate_candidate(candidate,m);src=np.memmap(candidate,dtype='<f4',mode='r',shape=(h,w,3))
require(actualpath.stat().st_size==h*w*24,'oracle length mismatch');actual=np.memmap(actualpath,dtype='<f8',mode='r',shape=(h,w,3));maximum=0.;total=0.;count=0
for y in range(0,h,32):
 a=src[y:y+32,:,:3].astype(np.float64);a=(a.astype(np.float32)/np.float32(65535)).astype(np.float64) if kind=='u16x4' else a;pred=display(a,ev);err=np.abs(pred-actual[y:y+32]);maximum=max(maximum,float(err.max()));total+=float(err.sum());count+=err.size
report={'status':'PASS' if maximum<=1e-12 else 'FAIL','pixels':w*h,'channels':count,'kind':kind,'ev':ev,'maxAbsoluteError':maximum,'meanAbsoluteError':total/count,'tolerance':1e-12,'actualProductOutputSha256':sha(actualpath)};print(json.dumps(report,indent=2));sys.exit(0 if report['status']=='PASS' else 1)
