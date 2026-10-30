"""Two-point reproduction independent of native/GPU runtime; no smoothing or clamp."""
from pathlib import Path
import argparse,json,numpy as np
from scipy.interpolate import make_interp_spline
p=argparse.ArgumentParser();p.add_argument('fixture',type=Path);p.add_argument('output',type=Path);a=p.parse_args();fixture=json.loads(a.fixture.read_text(encoding='utf-8-sig'));cases=[]
for c in fixture['cases']:
 x=np.array(c['x']);y=np.array(c['originalRawY']);q=np.array(c['queries']);left=np.interp(q,x,y);right=make_interp_spline(x,y,k=1)(q)
 if not (x[1]>x[0] and y[1]<y[0] and q[1]>q[0] and left[1]<left[0] and right[1]<right[0]):raise ValueError('Expected original-sample decline missing')
 cases.append({'film':c['film'],'channel':c['channel'],'rawResourceSlope':float(np.diff(y)[0]/np.diff(x)[0]),'queries':q.tolist(),'numpyRawDensity':left.tolist(),'scipyRawDensity':right.tolist(),'numpyDelta':float(np.diff(left)[0]),'scipyDelta':float(np.diff(right)[0]),'maxReferenceDifference':float(np.max(np.abs(left-right)))})
with a.output.open('x') as f:json.dump({'version':'ProfileTwoPointReproductionV1','cases':cases,'fixedResourceNoMutation':True},f,indent=2)
print(json.dumps(cases))
