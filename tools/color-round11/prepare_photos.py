"""Frozen production D65->D50 bridge then per-channel BOX proxy. Six explicit sources only."""
from pathlib import Path
import json,hashlib,sys
import numpy as np
from PIL import Image,ImageCms,ImageDraw
root=Path('N:/pixart/ColorResearchHome-20261030/round11');repo=Path(__file__).resolve().parents[2];admission=json.loads((Path(__file__).parent/'photo-preparation/sources-v1.json').read_text());bridge=json.loads((repo/'docs/research/color-engine-20261029/company-round7/new-preparation-manifest-v2.json').read_text())['bridge'];matrix=np.array(bridge['combinedMatrix'],np.float64);out=root/'natural-inputs-v1'
if out.exists():raise ValueError('Natural input preparation exists')
out.mkdir();entries=[];icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();tiles=[]
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for b in iter(lambda:f.read(4194304),b''):h.update(b)
 return h.hexdigest()
for source in admission['entries']:
 id=source['id'];prod=root/('production-'+id);r=json.loads((prod/'report.json').read_text());
 if r['status']!='DECODED' or r['sourceSha256']!=source['sha256'] or r['storedOrientation']!=1:raise ValueError('Unverified production '+id)
 h,w=r['height'],r['width'];paths=[prod/'production-linear-d65.f32',prod/'production-display-srgb.f32'];
 if sha(paths[0]).upper()!=r['productionLinearSha256'] or sha(paths[1]).upper()!=r['productionDisplaySha256']:raise ValueError('Production hash changed')
 raw=np.memmap(paths[0],dtype='<f4',mode='r',shape=(h,w,3));display=np.memmap(paths[1],dtype='<f4',mode='r',shape=(h,w,3));full=np.lib.format.open_memmap(out/(id+'-A-full.npy'),mode='w+',dtype='<f4',shape=(h,w,3));zero=np.zeros(3,np.int64);high=zero.copy()
 try:
  for y in range(0,h,32):
   block=np.asarray(raw[y:y+32]);
   if not np.isfinite(block).all():raise ValueError('Nonfinite master')
   zero+=(block==0).sum(axis=(0,1));high+=(block==1).sum(axis=(0,1));full[y:y+32]=(block.astype(np.float64)@matrix.T).astype(np.float32)
  full.flush();ph=round(h*min(1,1200/max(w,h)));pw=round(w*min(1,1200/max(w,h)));proxy=np.empty((ph,pw,3),np.float32);before=np.empty_like(proxy)
  for c in range(3):
   for data,target in [(full,proxy),(display,before)]:
    channel=np.empty((h,w),np.float32)
    for y in range(0,h,32):channel[y:y+32]=data[y:y+32,:,c]
    target[:,:,c]=np.asarray(Image.fromarray(channel,mode='F').resize((pw,ph),Image.Resampling.BOX));del channel
  if not np.isfinite(proxy).all() or not np.isfinite(before).all():raise ValueError('Nonfinite proxy')
  path=out/(id+'-A-proxy.npy');np.save(path,proxy);np.save(out/(id+'-display-proxy.npy'),before);Image.fromarray(np.rint(np.clip(before,0,1)*255).astype(np.uint8)).save(out/(id+'-before.png'),icc_profile=icc)
  entry={**source,'productionReportSha256':sha(prod/'report.json'),'productionLinearSha256':r['productionLinearSha256'],'productionDisplaySha256':r['productionDisplaySha256'],'originalSize':[w,h],'proxyShape':[ph,pw,3],'sourceOrientation':r['originalOrientation'],'storedOrientation':1,'inputDomain':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','bridge':bridge,'file':path.name,'fileSha256':sha(path),'pixelSha256':hashlib.sha256(proxy.tobytes()).hexdigest(),'proxyMin':proxy.min(axis=(0,1)).tolist(),'proxyMax':proxy.max(axis=(0,1)).tolist(),'fullMasterZeroFractionByChannel':(zero/(w*h)).tolist(),'fullMasterOneFractionByChannel':(high/(w*h)).tolist(),'priorLossScope':'final production master endpoints; no sensor saturation attribution','exposureEv':0,'resize':'Pillow F BOX independently per channel; full scene, no ROI stitch'};entries.append(entry)
  im=Image.open(out/(id+'-before.png'));im.thumbnail((360,240));tile=Image.new('RGB',(380,270),'#222222');tile.paste(im,((380-im.width)//2,25));ImageDraw.Draw(tile).text((10,5),id,fill='white');tiles.append(tile)
 finally:full._mmap.close();raw._mmap.close();display._mmap.close()
manifest={'version':'NaturalProxyAdmissionV1','sourcesAdmissionSha256':sha(Path(__file__).parent/'photo-preparation/sources-v1.json'),'maxLongEdge':1200,'sources':entries,'privatePhotosLocalOnly':True,'originalCanonWhitelistUntouched':True};(out/'manifest.json').write_text(json.dumps(manifest,indent=2));contact=Image.new('RGB',(1140,540),'#222222')
for i,tile in enumerate(tiles):contact.paste(tile,((i%3)*380,(i//3)*270))
contact.save(out/'before-contact.png');print(json.dumps({'photos':len(entries),'out':str(out)}))
