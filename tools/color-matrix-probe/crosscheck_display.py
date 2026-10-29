import json, subprocess, sys
from pathlib import Path
import numpy as np
from render_compare import display
from stage_validation import validate_stage,validate_candidate
root=Path(sys.argv[1]);candidate=Path(sys.argv[2]);oracle=Path(sys.argv[3]);m=validate_stage(root);validate_candidate(candidate,m);s=m['sizes'];w=s['IWidth'];h=s['IHeight'];a=np.memmap(root/'matrix_after.u16',dtype='<u2',mode='r',shape=(h,w,4));b=np.memmap(candidate,dtype='<f4',mode='r',shape=(h,w,3))
rng=np.random.default_rng(20261029);coords=np.stack((rng.integers(0,h,10000),rng.integers(0,w,10000)),axis=1);extra=np.array([[2328,1600],[83,6637],[0,0],[h-1,w-1]]);coords=np.vstack((coords,extra));cases=[]
for y,x in coords:
 for vec in ((a[y,x,:3].astype(np.float32)/np.float32(65535)).astype(np.float64),b[y,x].astype(np.float64)):
  for ev in (0.,-2.):cases.append([*map(float,vec),ev])
cases.extend([[0,0,0,0],[1,1,1,0],[-.2,.1,1.3,-2],[0,0,0,-2],[.75,.75,.75,0]])
text=''.join(json.dumps(v,separators=(',',':'))+'\n' for v in cases);r=subprocess.run(['dotnet',str(oracle)],input=text,text=True,capture_output=True,check=True);actual=np.array([json.loads(line) for line in r.stdout.splitlines()]);arr=np.array(cases);pred=np.empty_like(actual)
for ev in (0.,-2.):
 mask=arr[:,3]==ev;pred[mask]=display(arr[mask,:3],ev)
err=np.abs(actual-pred);maxerr=float(err.max());report={'status':'PASS' if maxerr<=1e-12 else 'FAIL','sampledNaturalCoordinates':len(coords),'cases':len(cases),'channelsCompared':int(err.size),'maxAbsoluteError':maxerr,'meanAbsoluteError':float(err.mean()),'tolerance':1e-12,'limitation':'sampled coordinate-by-coordinate comparison, not all 36M pixels; no export/reopen validation','ev':[0,-2],'product':'ProfessionalRawPipeline.ApplyExposure and Display'};print(json.dumps(report,indent=2));sys.exit(0 if report['status']=='PASS' else 1)
