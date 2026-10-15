import sys,pathlib,json
sys.path.insert(0,str(pathlib.Path(__file__).parent/'python-deps'))
import tifffile,numpy as np
root=pathlib.Path(__file__).parent/'raw';rows=[]
for n in ['TNAN8886.CR3','DSCF0347.RAF']:
 a=tifffile.imread(root/(n+'.tif'));b=tifffile.imread(root/(n+'.configured.rt.tif')); assert a.shape==b.shape
 d=a.astype('float64')/65535-b.astype('float64')/65535
 pts=[(0,0),(a.shape[0]//2,a.shape[1]//2),(a.shape[0]-1,a.shape[1]-1)]
 rows.append(dict(file=n,stage='FINAL_ENCODED_RGB16',classification='CONFIGURATION_NOT_EQUIVALENT_NO_ACCURACY_PASS',shape=list(a.shape),maxAbs=float(np.max(np.abs(d))),meanAbs=float(np.mean(np.abs(d))),rmse=float(np.sqrt(np.mean(d*d))),points=[dict(y=y,x=x,libraw=a[y,x].tolist(),rawtherapee=b[y,x].tolist()) for y,x in pts],noCropping=True))
print(json.dumps(rows,indent=2))
