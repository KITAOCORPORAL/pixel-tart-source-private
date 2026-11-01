"""Public synthetic diagnostic identity, independent of any camera source whitelist."""
import json,hashlib,struct
from pathlib import Path
import numpy as np
VERSION='SyntheticPortraCrossDeviceV1'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def generate(root):
 root=Path(root);root.mkdir(exist_ok=False);fixtures=[];h,w=17,33
 gray=np.geomspace(np.float32(1e-9),np.float32(2),w,dtype=np.float32);a=np.broadcast_to(gray[None,:,None],(h,w,3)).copy();a[0,:8]=np.array([[-1,-.01,0],[0,1e-12,1e-8],[.18,.18,.18],[1,1,1],[1.5,2,4],[1,0,0],[0,1,0],[0,0,1]],np.float32)
 colors=np.array([[1,0,0],[0,1,0],[0,0,1],[1,1,0],[0,1,1],[1,0,1],[1,.1,0],[.5,0,1],[0,0,0],[4,-.1,2]],np.float32);b=np.empty((19,35,3),np.float32)
 for y in range(19):
  for x in range(35):b[y,x]=colors[(x//4+y//5)%len(colors)]*np.float32(.02+1.98*y/18)
 c=np.ones((21,37,3),np.float32)*.02;c[0:3,0:3]=3;c[-3:,-3:]=1;c[:,18:20]=.8;c[9:12,6:30]=[1,.01,.2]
 # BT boundary included as declared code values; separate direct mode3 probe validates decoder.
 bt=np.array([np.array(0x3da66555+i,dtype='<u4').view('<f4') for i in range(-6,7)],np.float32);a[-1,:13]=bt[:,None]
 for name,data,meaning in [('gray-boundaries',a,'neutral log ramp, near-zero, negatives/>1, BT code vector; ProPhoto linear when used by PortraA'),('saturated-colors',b,'primaries/secondaries and saturated RGB, not natural color accuracy'),('edge-small',c,'edge/corners and crossing stripes for finite-radius print finish')]:
  path=root/(name+'.npy');np.save(path,np.ascontiguousarray(data,dtype='<f4'));fixtures.append({'id':name,'path':path.name,'shape':list(data.shape),'dtype':'<f4','fileSha256':sha(path),'pixelSha256':hashlib.sha256(data.tobytes()).hexdigest(),'identity':VERSION+':'+name,'meaning':meaning})
 manifest={'version':VERSION,'naturalSource':None,'privatePhotosIncluded':False,'inputDomain':'standard ProPhoto D50 linear float32; negative/>1 allowed','orientation':1,'fixtures':fixtures,'bt':{'cpuFloatJoinBits':'3da66555','inputBits':[format(0x3da66555+i,'08x') for i in range(-6,7)],'testMode':3,'purpose':'direct transfer-function diagnostic, not a new film recipe'},'comparisonPolicy':{'sameDeviceWholeStream':'EXACT_0_RGBA16_CHANNEL_DIFFERENCES','crossDevice':'MEASURE_AND_LOCALIZE_ONLY; no post-hoc acceptance threshold','stages':'passive readbacks; must verify traced final equals untraced final on same device'}};(root/'fixtures.json').write_text(json.dumps(manifest,indent=2));return manifest
if __name__=='__main__':
 import sys
 print(json.dumps(generate(Path(sys.argv[1])),indent=2))
