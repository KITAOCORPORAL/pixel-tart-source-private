"""Rebuild only round7 fixed-size proxies from validated existing full A; no RAW decode."""
import copy
from natural_admission import *
from host_environment import environment
from PIL import Image
host=environment();full=Path(host['preparationRoot']);m=load_json(full/'admission-manifest-v2.json');entry=next(x for x in m['files'] if x['path']=='A-linear-d50-full.npy');need(sha(full/entry['path'])==entry['fileSha256'],'Full A changed');src=np.load(full/entry['path'],mmap_mode='r');h,w,_=src.shape;small=Path(host['smallRoot']);small=output_path(small);small.mkdir();entries=[]
try:
 for width,height in [(321,215),(641,429),(1200,801)]:
  image=np.empty((height,width,3),np.float32)
  for c in range(3):
   channel=np.empty((h,w),np.float32)
   for y in range(0,h,32):channel[y:y+32]=src[y:y+32,:,c]
   image[:,:,c]=np.asarray(Image.fromarray(channel,mode='F').resize((width,height),Image.Resampling.BOX));del channel
  path=small/f'whole-scene-{width}x{height}.npy';np.save(path,image);info=npy_info(path);st=scan(path,info);entries.append({'path':path.name,'fileSha256':sha(path),'shape':info['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':m['bridge']['version'],'pixelSha256':st['pixelSha256'],'bytes':info['fileBytes']})
finally:src._mmap.close()
syn=np.zeros((65,97,3),np.float32);syn[:,:,0]=np.linspace(-.2,2,97,dtype=np.float32);syn[:,:,1]=np.linspace(0,1,65,dtype=np.float32)[:,None];syn[:,:,2]=.18;syn[0,0]=[-1,1e-12,3];p=small/'synthetic.npy';np.save(p,syn);info=npy_info(p);st=scan(p,info);entries.append({'path':p.name,'fileSha256':sha(p),'shape':info['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':m['bridge']['version'],'pixelSha256':st['pixelSha256'],'bytes':info['fileBytes'],'synthetic':True});m.update(inputRoot=str(small.resolve()),files=entries);atomic_json(small/'admission-manifest-v2.json',m);print('REBUILT_SMALL',[(x['shape'],x['pixelSha256']) for x in entries])
