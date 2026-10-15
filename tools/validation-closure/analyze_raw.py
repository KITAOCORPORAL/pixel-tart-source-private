import sys,pathlib,json,hashlib
sys.path.insert(0,str(pathlib.Path(__file__).parent/'python-deps'))
import tifffile,numpy as np
r=[]
for p in pathlib.Path(__file__).parent.joinpath('raw').glob('*.tif'):
 a=tifffile.imread(p);r.append(dict(file=p.name,shape=list(a.shape),dtype=str(a.dtype),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),pixelSha256=hashlib.sha256(a.tobytes()).hexdigest(),minimum=int(a.min()),maximum=int(a.max()),mean=a.mean(axis=(0,1)).tolist()))
print(json.dumps(r,indent=2))
