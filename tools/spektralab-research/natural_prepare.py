"""Prepare A/B from actual production buffers. Source images stay in explicit local root."""
import argparse,json,hashlib,sys,time
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms
from run_matrix import sha,digest,require
from natural_admission import output_path,atomic_json,npy_info,scan,resources,DOMAINS,AUTHORIZED_OUTPUT,SOURCE_ROOT
import psutil
p=argparse.ArgumentParser();p.add_argument('production',type=Path);p.add_argument('output',type=Path);p.add_argument('--edge',type=int,default=1200);p.add_argument('--library',type=Path,required=True);p.add_argument('--resources',type=Path,required=True);a=p.parse_args();a.output=output_path(a.output);a.output.mkdir()
try:
 require(a.production.resolve().is_relative_to(AUTHORIZED_OUTPUT),'Production outside authorized research root');r=json.loads((a.production/'report.json').read_text());require(r['Contract']['OutputSpace']=='LibRaw Linear ProPhoto D65' and r['Contract']['WhiteBalanceApplied'] and r['Contract']['CameraMatrixApplied'] and r['storedOrientation']==1,'Unverified production domain/metadata');require(r['sourceSha256']=='B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401' and sha(SOURCE_ROOT/'TNAN8886.CR3').upper()==r['sourceSha256'],'Explicit authorized source identity mismatch');require(r['status']=='DECODED','Not an accepted production decode');w,h=r['width'],r['height'];require(type(w) is int and type(h) is int and w>0 and h>0 and w<=2**31//h and w*h*12<=512*1048576,'Invalid/over-budget preparation dimensions');require((a.production/'production-linear-d65.f32').stat().st_size==w*h*12 and (a.production/'production-display-srgb.f32').stat().st_size==w*h*12,'Production byte length mismatch');require(w*h*24+256*1048576<psutil.virtual_memory().available-1024**3,'CPU preparation budget refused');require(sha(a.production/'production-linear-d65.f32').upper()==r['productionLinearSha256'],'Linear input changed');require(sha(a.production/'production-display-srgb.f32').upper()==r['productionDisplaySha256'],'Display input changed')
 P=np.array([[.529317,.330092,.140588],[.098368,.873465,.028169],[.016879,.117663,.865457]]);S=np.array([[.4124,.3576,.1805],[.2126,.7152,.0722],[.0193,.1192,.9505]]);B=np.array([[.8951,.2664,-.1614],[-.7502,1.7135,.0367],[.0389,-.0685,1.0296]]);E=np.array([[.7977,.1352,.0313],[.288,.7119,.0001],[0,0,.8249]])
 def white(x,y):return np.array([x/y,1,(1-x-y)/y])
 adapt=np.linalg.inv(B)@np.diag((B@white(.3457,.3585))/(B@white(.3127,.329)))@B;bridge=np.linalg.inv(E)@adapt@S@np.linalg.inv(P)
 raw=np.memmap(a.production/'production-linear-d65.f32',dtype='<f4',mode='r',shape=(h,w,3));display=np.memmap(a.production/'production-display-srgb.f32',dtype='<f4',mode='r',shape=(h,w,3));start=time.perf_counter();master=np.lib.format.open_memmap(a.output/'A-linear-d50-full.npy',mode='w+',dtype='<f4',shape=(h,w,3))
 minimum=np.full(3,np.inf);maximum=-minimum;negative=np.zeros(3,np.int64);above=np.zeros(3,np.int64)
 for y in range(0,h,32):
  require(bool(np.isfinite(raw[y:y+32]).all()) and bool(np.isfinite(display[y:y+32]).all()),'Nonfinite production input');block=(np.asarray(raw[y:y+32],np.float64)@bridge.T).astype(np.float32);require(bool(np.isfinite(block).all()),'Bridge nonfinite');master[y:y+32]=block;minimum=np.minimum(minimum,block.min(axis=(0,1)));maximum=np.maximum(maximum,block.max(axis=(0,1)));negative+=(block<0).sum(axis=(0,1));above+=(block>1).sum(axis=(0,1))
 master.flush();del master;creative=np.lib.format.open_memmap(a.output/'B-display-srgb-full.npy',mode='w+',dtype='<f4',shape=(h,w,3));
 for y in range(0,h,32):creative[y:y+32]=display[y:y+32]
 creative.flush();del creative;preparems=(time.perf_counter()-start)*1000
 # Explicit box-integrated proxies; A resamples linear scene domain, B resamples encoded creative domain.
 scale=min(1,a.edge/max(w,h));pw,ph=round(w*scale),round(h*scale)
 def proxy(path):
  # Preserve Pillow BOX exact per-channel behavior with one explicitly budgeted channel clone;
  # no simultaneous three-channel full-frame copy.
  # CPU operation budget explicitly includes one full channel (4*w*h), not GPU gate.
  require(w*h*4+pw*ph*12+64*1048576<psutil.virtual_memory().available-1024**3,'CPU proxy channel budget refused')
  f=np.load(path,mmap_mode='r');out=np.empty((ph,pw,3),np.float32)
  for c in range(3):
   channel=np.empty((h,w),np.float32)
   for y in range(0,h,32):channel[y:y+32]=f[y:y+32,:,c]
   out[:,:,c]=np.asarray(Image.fromarray(channel,mode='F').resize((pw,ph),Image.Resampling.BOX));del channel
  f._mmap.close();return out
 np.save(a.output/'A-linear-d50-proxy.npy',proxy(a.output/'A-linear-d50-full.npy'));np.save(a.output/'B-display-srgb-proxy.npy',proxy(a.output/'B-display-srgb-full.npy'))
 # Registered source-coordinate windows at native sampling. No full-frame claim for ROI renders.
 regions={'edge_people':{'left':2050,'top':1370,'width':640,'height':640},'shadow_vehicle':{'left':0,'top':1980,'width':640,'height':640},'sky_wire':{'left':3050,'top':380,'width':640,'height':640}}
 for name,box in regions.items():
  for route in ['A-linear-d50','B-display-srgb']:
   f=np.load(a.output/(route+'-full.npy'),mmap_mode='r');np.save(a.output/(route+'-crop-'+name+'.npy'),f[box['top']:box['top']+box['height'],box['left']:box['left']+box['width']].copy())
 icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();im=Image.fromarray(np.rint(np.clip(np.load(a.output/'B-display-srgb-proxy.npy'),0,1)*255).astype(np.uint8));im.save(a.output/'production-display-proxy.png',icc_profile=icc)
 manifest={'version':'NaturalSpektraInputsV1','sourceSha256':r['sourceSha256'],'productionContract':r['Contract'],'fullDimensions':[w,h],'proxyDimensions':[pw,ph],'orientation':1,'exposureEv':r['definition']['ExposureEv'],'bridge':{'version':'ExistingResearchD65ToD50V1','P':P.tolist(),'sRGBtoXYZD65':S.tolist(),'Bradford':B.tolist(),'sourceWhiteXY':[.3127,.329],'destinationWhiteXY':[.3457,.3585],'engineProPhotoD50toXYZ':E.tolist(),'combinedMatrix':bridge.tolist(),'order':'LibRawProPhotoD65 inverse -> linear sRGB D65 -> XYZ D65 -> Bradford D50 -> standard engine ProPhoto D50','extraWB':False,'displayMappingOnA':False},'AStatistics':{'min':minimum.tolist(),'max':maximum.tolist(),'negative':negative.tolist(),'aboveOne':above.tolist()},'B':'Unchanged production RenderOutputPixel EV0 once; sRGB encoded, no display repeated before engine','cpuPreparationBudget':{'mappedInputAndOutputBytes':w*h*24,'bridgeWorkRows':32,'proxyMaximumChannelCloneBytes':w*h*4,'independentOfGpu':True},'prepareFullAndWriteMs':preparems,'proxyMethod':'Pillow F BOX; explicit proxy, not full output; channel-domain box averaging','regions':regions,'files':[{'path':f.name,'bytes':f.stat().st_size,'sha256':sha(f)} for f in sorted(a.output.glob('*.npy'))]};(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps({'full':[w,h],'proxy':[pw,ph],'A':manifest['AStatistics']}))

 # A new preparation writes V2 directly. Existing round6 V1 bytes are sealed only
 # through the separate explicit frozen-evidence sealer; no implicit migration.
 entries=[]
 for f in sorted(a.output.glob('*.npy')):
  info=npy_info(f);stats=scan(f,info,32);route='A' if f.name.startswith('A-') else 'B'
  entries.append({'path':f.name,'fileSha256':sha(f),'shape':info['shape'],'route':route,'orientation':1,'domain':DOMAINS[route],'bridgeVersion':manifest['bridge']['version'],'pixelSha256':stats['pixelSha256'],'bytes':info['fileBytes']})
 seal={'version':'NaturalAdmissionManifestV2','inputRoot':str(a.output.resolve()),'sourceSha256':r['sourceSha256'],'productionReportSha256':sha(a.production/'report.json'),'orientation':1,'bridge':manifest['bridge'],'backend':{'path':str(a.library.resolve()),'sha256':sha(a.library)},'resources':resources(a.resources),'preparationIdentity':{'sourceSha256':r['sourceSha256'],'productionLinearSha256':r['productionLinearSha256'],'productionDisplaySha256':r['productionDisplaySha256'],'mode':'New bounded-row preparation with unchanged ExistingResearchD65ToD50V1 math'},'files':entries}
 atomic_json(a.output/'admission-manifest-v2.json',seal)
 print(json.dumps({'admissionManifestSha256':sha(a.output/'admission-manifest-v2.json'),'inputFiles':len(entries)}))
except BaseException as original:
 try:atomic_json(a.output/'failure.json',{'version':'PreparationFailureV2','success':False,'primaryError':repr(original)})
 except BaseException as reporting:original.add_note('Failure report error: '+repr(reporting))
 raise
