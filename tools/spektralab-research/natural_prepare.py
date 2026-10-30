"""Prepare A/B from actual production buffers. Source images stay in explicit local root."""
import argparse,json,hashlib,sys,time
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms
from run_matrix import sha,digest,require
p=argparse.ArgumentParser();p.add_argument('production',type=Path);p.add_argument('output',type=Path);p.add_argument('--edge',type=int,default=1200);a=p.parse_args();require(not a.output.exists(),'Output exists');a.output.mkdir();r=json.loads((a.production/'report.json').read_text());require(r['status']=='DECODED','Not an accepted production decode');w,h=r['width'],r['height'];require(sha(a.production/'production-linear-d65.f32').upper()==r['productionLinearSha256'],'Linear input changed');require(sha(a.production/'production-display-srgb.f32').upper()==r['productionDisplaySha256'],'Display input changed')
P=np.array([[.529317,.330092,.140588],[.098368,.873465,.028169],[.016879,.117663,.865457]]);S=np.array([[.4124,.3576,.1805],[.2126,.7152,.0722],[.0193,.1192,.9505]]);B=np.array([[.8951,.2664,-.1614],[-.7502,1.7135,.0367],[.0389,-.0685,1.0296]]);E=np.array([[.7977,.1352,.0313],[.288,.7119,.0001],[0,0,.8249]])
def white(x,y):return np.array([x/y,1,(1-x-y)/y])
adapt=np.linalg.inv(B)@np.diag((B@white(.3457,.3585))/(B@white(.3127,.329)))@B;bridge=np.linalg.inv(E)@adapt@S@np.linalg.inv(P)
raw=np.memmap(a.production/'production-linear-d65.f32',dtype='<f4',mode='r',shape=(h,w,3));display=np.memmap(a.production/'production-display-srgb.f32',dtype='<f4',mode='r',shape=(h,w,3));start=time.perf_counter();master=np.lib.format.open_memmap(a.output/'A-linear-d50-full.npy',mode='w+',dtype='<f4',shape=(h,w,3))
minimum=np.full(3,np.inf);maximum=-minimum;negative=np.zeros(3,np.int64);above=np.zeros(3,np.int64)
for y in range(0,h,64):
 block=(np.asarray(raw[y:y+64],np.float64)@bridge.T).astype(np.float32);require(bool(np.isfinite(block).all()),'Bridge nonfinite');master[y:y+64]=block;minimum=np.minimum(minimum,block.min(axis=(0,1)));maximum=np.maximum(maximum,block.max(axis=(0,1)));negative+=(block<0).sum(axis=(0,1));above+=(block>1).sum(axis=(0,1))
master.flush();del master;creative=np.lib.format.open_memmap(a.output/'B-display-srgb-full.npy',mode='w+',dtype='<f4',shape=(h,w,3));creative[:]=display;creative.flush();del creative;preparems=(time.perf_counter()-start)*1000
# Explicit box-integrated proxies; A resamples linear scene domain, B resamples encoded creative domain.
scale=min(1,a.edge/max(w,h));pw,ph=round(w*scale),round(h*scale)
def proxy(path):
 f=np.load(path,mmap_mode='r');out=np.empty((ph,pw,3),np.float32)
 for c in range(3):out[:,:,c]=np.asarray(Image.fromarray(np.asarray(f[:,:,c],np.float32),mode='F').resize((pw,ph),Image.Resampling.BOX))
 return out
np.save(a.output/'A-linear-d50-proxy.npy',proxy(a.output/'A-linear-d50-full.npy'));np.save(a.output/'B-display-srgb-proxy.npy',proxy(a.output/'B-display-srgb-full.npy'))
# Registered source-coordinate windows at native sampling. No full-frame claim for ROI renders.
regions={'edge_people':{'left':2050,'top':1370,'width':640,'height':640},'shadow_vehicle':{'left':0,'top':1980,'width':640,'height':640},'sky_wire':{'left':3050,'top':380,'width':640,'height':640}}
for name,box in regions.items():
 for route in ['A-linear-d50','B-display-srgb']:
  f=np.load(a.output/(route+'-full.npy'),mmap_mode='r');np.save(a.output/(route+'-crop-'+name+'.npy'),f[box['top']:box['top']+box['height'],box['left']:box['left']+box['width']].copy())
icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();im=Image.fromarray(np.rint(np.clip(np.load(a.output/'B-display-srgb-proxy.npy'),0,1)*255).astype(np.uint8));im.save(a.output/'production-display-proxy.png',icc_profile=icc)
manifest={'version':'NaturalSpektraInputsV1','sourceSha256':r['sourceSha256'],'productionContract':r['Contract'],'fullDimensions':[w,h],'proxyDimensions':[pw,ph],'orientation':1,'exposureEv':r['definition']['ExposureEv'],'bridge':{'version':'ExistingResearchD65ToD50V1','P':P.tolist(),'sRGBtoXYZD65':S.tolist(),'Bradford':B.tolist(),'sourceWhiteXY':[.3127,.329],'destinationWhiteXY':[.3457,.3585],'engineProPhotoD50toXYZ':E.tolist(),'combinedMatrix':bridge.tolist(),'order':'LibRawProPhotoD65 inverse -> linear sRGB D65 -> XYZ D65 -> Bradford D50 -> standard engine ProPhoto D50','extraWB':False,'displayMappingOnA':False},'AStatistics':{'min':minimum.tolist(),'max':maximum.tolist(),'negative':negative.tolist(),'aboveOne':above.tolist()},'B':'Unchanged production RenderOutputPixel EV0 once; sRGB encoded, no display repeated before engine','prepareFullAndWriteMs':preparems,'proxyMethod':'Pillow F BOX; explicit proxy, not full output; channel-domain box averaging','regions':regions,'files':[{'path':f.name,'bytes':f.stat().st_size,'sha256':sha(f)} for f in sorted(a.output.glob('*.npy'))]};(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps({'full':[w,h],'proxy':[pw,ph],'A':manifest['AStatistics']}))
