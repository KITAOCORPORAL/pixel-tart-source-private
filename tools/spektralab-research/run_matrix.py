"""Isolated ABI caller. Requires the pinned external checkout/build; no vendored engine code."""
import argparse,ctypes,hashlib,json,os,sys,time,threading
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms

def sha(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as f:
  for block in iter(lambda:f.read(4194304),b''):h.update(block)
 return h.hexdigest()
def digest(a):return hashlib.sha256(np.ascontiguousarray(a).tobytes()).hexdigest()
def require(ok,message):
 if not ok:raise ValueError(message)
def timed(fn):
 t=time.perf_counter();v=fn();return v,(time.perf_counter()-t)*1000

def main():
 ap=argparse.ArgumentParser();ap.add_argument('checkout',type=Path);ap.add_argument('build',type=Path);ap.add_argument('resources',type=Path);ap.add_argument('stage',type=Path);ap.add_argument('candidate',type=Path);ap.add_argument('output',type=Path);args=ap.parse_args();args.output.mkdir(exist_ok=False)
 sys.path.insert(0,str(args.checkout/'engine/tests'));sys.path.insert(0,str(args.checkout/'engine/tools'))
 sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'color-matrix-probe'))
 import subprocess
 require(subprocess.check_output(['git','-C',str(args.checkout),'rev-parse','HEAD'],text=True).strip()=='c1447c6ab618fa4b1cb11fd2fe12826855007059','Wrong engine checkout')
 require(sha(args.build/'engine/spektrafilm_engine.dll')=='5b0bdc07275b14b08996fd9e26c918ce05e1314e88c06142df1104f421d36f0f','Unrecognized tested engine DLL')
 from spk_ctypes import Engine,EngineError,SpkImage,SpkResult
 from windows_lifecycle import OwnedResult
 from stage_validation import validate_stage,validate_candidate
 from render_compare import display,P
 meta=validate_stage(args.stage);side=validate_candidate(args.candidate,meta);sz=meta['sizes'];w,h=sz['IWidth'],sz['IHeight'];full=np.memmap(args.candidate,dtype='<f4',mode='r',shape=(h,w,3))
 # Only declared downsampled development image, not a new independent scene.
 hh,ww=427,641;yy=np.minimum((np.arange(hh)+.5)*h/hh,h-1).astype(int);xx=np.minimum((np.arange(ww)+.5)*w/ww,w-1).astype(int);raw=np.asarray(full[yy[:,None],xx[None,:],:],np.float64)
 # Engine blob standard ProPhoto is D50, unlike LibRaw's distinct D65 matrix.
 srgbxyz=np.array([[.4124,.3576,.1805],[.2126,.7152,.0722],[.0193,.1192,.9505]])
 B=np.array([[.8951,.2664,-.1614],[-.7502,1.7135,.0367],[.0389,-.0685,1.0296]])
 def white(x,y):return np.array([x/y,1,(1-x-y)/y])
 adapt=np.linalg.inv(B)@np.diag((B@white(.3457,.3585))/(B@white(.3127,.329)))@B
 engineXYZ=np.array([[.7977,.1352,.0313],[.288,.7119,.0001],[0,0,.8249]])
 adapter=np.linalg.inv(engineXYZ)@adapt@srgbxyz@np.linalg.inv(P)
 linear=(raw@adapter.T).astype(np.float32);creative=display(raw,0).astype(np.float32)
 # Deliberate synthetic edge/ramp and overshoot inputs, distinct from natural coverage.
 gx=np.linspace(-.05,2,193,dtype=np.float32);gy=np.linspace(0,1,129,dtype=np.float32);gradient=np.empty((129,193,3),np.float32);gradient[:,:,0]=gx;gradient[:,:,1]=gy[:,None];gradient[:,:,2]=np.clip(1-gx,0,1);gradient[32:48,80:96]=4
 inputs={'natural_linear_d50':(linear,False,'ProPhoto RGB'),'natural_srgb_creative':(creative,True,'sRGB'),'synthetic_gradient_linear':(gradient,False,'ProPhoto RGB')}
 resourceItems=[{'path':str(f.relative_to(args.resources)).replace('\\\\','/'),'bytes':f.stat().st_size,'sha256':sha(f)} for f in sorted(args.resources.rglob('*')) if f.is_file()]
 resourceRoot=hashlib.sha256(json.dumps(resourceItems,sort_keys=True,separators=(',',':')).encode()).hexdigest()
 library=args.build/'engine/spektrafilm_engine.dll';engine,initms=timed(lambda:Engine(library=library,resources=args.resources));report={'version':'SpektraWindowsResearchV1','targetCommit':'c1447c6ab618fa4b1cb11fd2fe12826855007059','dllSha256':sha(library),'resourceManifestSha256':resourceRoot,'resourceManifest':resourceItems,'harnessSha256':sha(Path(__file__)),'capabilities':engine.capabilities(),'initializationMs':initms,'inputs':{},'cases':[],'seedContract':{'deterministic':'grain/glare disabled','grain':'C ABI does not expose grain_sampler or fixed grain seed; stochastic trials not deterministic or Metal parity','halation':'deterministic, no RNG'},'adapter':adapter.tolist(),'sourceSha256':meta['sourceSha256'],'candidateSha256':side['sha256'],'licenseApprovedForProduct':False}
 srgbICC=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
 for name,(image,encoded,space) in inputs.items():
  report['inputs'][name]={'width':image.shape[1],'height':image.shape[0],'sha256':digest(image),'primaries':space,'white':'D50' if space=='ProPhoto RGB' else 'D65','transfer':'linear' if not encoded else 'sRGB encoded','min':float(image.min()),'max':float(image.max()),'exposureScale':'no metering; input relative 1 is scene reference; engine exposure_compensation_ev=0','icc':'explicit mathematical domain, no embedded input ICC','note':'sRGB is already display-mapped creative input, not native RAW film simulation' if encoded else 'LibRaw D65 -> linear sRGB -> XYZ D65 -> Bradford D50 -> engine rounded ProPhoto matrix'}
 pairs=[('kodak_portra_400','kodak_portra_endura'),('kodak_ektar_100','kodak_ultra_endura'),('kodak_tri_x_400','ilford_multigrade_iv_rc')]
 for film,paper in pairs:
  for inputname,(image,encoded,space) in inputs.items():
   entry={'film':film,'paper':paper,'input':inputname,'status':'FAILED'};report['cases'].append(entry);session=None;retained=None
   try:
    params={'film_stock':film,'print_stock':paper,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'exposure_compensation_ev':0,'input_color_space':space,'input_cctf_decoding':encoded,'output_color_space':'sRGB','output_cctf_encoding':True}
    if film=='kodak_tri_x_400':params.update(enlarger_illuminant='TH-KG3',c_filter_neutral=0,m_filter_neutral=0,y_filter_neutral=68,print_exposure=1)
    session,uploadms=timed(lambda:engine.open(image,params));actual=session.get_params();entry.update(paramsDelta=params,resolvedParams=actual,openUploadMs=uploadms)
    for k,v in params.items():require(actual[k]==v,'Parameter not applied '+k)
    retained,firstms=timed(lambda:OwnedResult(session,'full'));base=retained.pixels().copy();info=retained.info();entry.update(firstRenderCallMs=firstms,firstResult=info)
    warm=[];hashes=[]
    for i in range(3):
     r,ms=timed(lambda:OwnedResult(session,'full'));warm.append(ms);hashes.append(r.digest());r.close()
    require(all(x==digest(base) for x in hashes),'Deterministic repeat differs');entry.update(warmRenderCallMs=warm,deterministicExact=True)
    outfile=args.output/f'{film}_{inputname}.rgba16';t=time.perf_counter();base.astype('<u2').tofile(outfile);entry['fileOutputMs']=(time.perf_counter()-t)*1000
    t=time.perf_counter();_copy=retained.pixels().copy();entry['cpuCopyMs']=(time.perf_counter()-t)*1000;entry['outputHash']=sha(outfile);entry['alphaMinMax']=[int(base[:,:,3].min()),int(base[:,:,3].max())];entry['minMaxRgb']=[int(base[:,:,:3].min()),int(base[:,:,:3].max())]
    Image.fromarray(np.rint(base[:,:,:3]/257).astype(np.uint8)).save(args.output/f'{film}_{inputname}.png',icc_profile=srgbICC)
    entry['resourceFiles']={str(p.relative_to(args.resources)):sha(p) for p in [args.resources/'profiles'/f'{film}.json',args.resources/'profiles'/f'{paper}.json',args.resources/'neutral_print_filters.json',args.resources/'spektrafilm_constants.bin']}
    # Parameter error must not mutate the previously valid result.
    old=retained.digest()
    try:session.set_params({'scene_latitude_active':True});entry['unsupportedRequest']='unexpectedly accepted'
    except EngineError as e:entry['unsupportedRequest']=str(e)
    require(retained.digest()==old,'Earlier result mutated')
    session.close();session=None;require(retained.digest()==old,'Result invalid after session release');retained.close();entry['ownershipAfterSessionRelease']=True;entry['status']='EXECUTED_NOT_PRODUCT_APPROVED'
   except Exception as exc:entry['error']=str(exc)
   finally:
    if retained:retained.close()
    if session:session.close()
   (args.output/'report.json').write_text(json.dumps(report,indent=2,allow_nan=False))
 # alpha input is ignored by the engine's RGB uptake; check, do not assume preservation.
 a=creative[:129,:193];rgba=np.concatenate([a,np.full((*a.shape[:2],1),.125,np.float32)],axis=2);params={'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'input_color_space':'sRGB','input_cctf_decoding':True,'output_color_space':'sRGB'}
 with engine.open(rgba,params) as s:
  with OwnedResult(s) as r:report['alphaProbe']={'inputAlpha':.125,'outputAlphaMin':int(r.pixels()[:,:,3].min()),'outputAlphaMax':int(r.pixels()[:,:,3].max()),'stride':r.result.row_stride_px,'width':r.result.width}
 # Direct C ABI error controls (not only Python wrapper validation).
 invalid=np.zeros((3,3,2),np.float32);nativeImage=SpkImage(invalid.ctypes.data_as(ctypes.POINTER(ctypes.c_float)),3,3,2);nativeReply=ctypes.c_char_p();nativeHandle=engine._lib.spk_open(engine._handle,ctypes.byref(nativeImage),b'{}',ctypes.byref(nativeReply));require(not nativeHandle,'C ABI accepted invalid channel count');nativeMessage=engine._last_error()
 if nativeReply:engine._lib.spk_string_free(nativeReply)
 with engine.open(creative,params) as badTierSession:
  badResult=SpkResult();badStatus=engine._lib.spk_render(badTierSession._handle,b'unknown-tier',ctypes.byref(badResult));badMessage=engine._last_error();engine._lib.spk_result_free(ctypes.byref(badResult));require(badStatus!=0,'C ABI accepted invalid render tier')
 report['nativeErrorProbes']={'channels2Rejected':True,'channelsMessage':nativeMessage,'badTierStatus':badStatus,'badTierMessage':badMessage}
 # Repeated rendering memory ledger and retained-result lifecycle.
 with engine.open(linear,{'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'input_color_space':'ProPhoto RGB','input_cctf_decoding':False}) as s:
  ledgers=[]
  for i in range(10):
   with OwnedResult(s) as r:pass
   ledgers.append(engine.memory_report())
  report['memoryLedgers']=ledgers
 # cancellation from another thread, no invalid dereference or forced session release.
 lib=engine._lib;lib.spk_cancel.argtypes=[ctypes.c_void_p,ctypes.c_char_p]
 large=np.tile(linear,(2,2,1));s=engine.open(large,{'grain_active':True,'glare_active':False,'auto_exposure':False,'input_color_space':'ProPhoto RGB','input_cctf_decoding':False});result=SpkResult();resultcode=[]
 def run():resultcode.append(lib.spk_render(s._handle,b'full',ctypes.byref(result)))
 t=threading.Thread(target=run);t.start();time.sleep(.02);lib.spk_cancel(s._handle,None);t.join(60)
 require(not t.is_alive(),'Cancellation thread timeout');require(resultcode==[-7],'Cancellation did not return SPK_ERR_CANCELLED');report['cancelProbe']={'status':resultcode,'expectedCancelled':-7,'message':engine._last_error()};lib.spk_result_free(ctypes.byref(result));s.close()
 # Owned CPU result must outlive engine destruction.
 s=engine.open(creative,params);held=OwnedResult(s);old=held.digest();s.close();engine.close();require(held.digest()==old,'Result invalid after engine destruction');held.close();report['resultAfterEngineDestroyed']=True
 retention=[(x['total_bytes'],x['pool']['live_bytes'],x['pool']['pending_bytes']) for x in report['memoryLedgers']];require(len(set(retention))==1 and all(x[1:]==(0,0) for x in retention),'Repeated render ledger not stable');report['repeatMemoryLedgerStable']=True
 report['observedSourceContent']='D800 development crop/downsample: city buildings, church spire and blue sky/clouds; no people. Visual inspection is not color accuracy or independent scene assignment.'
 report['outputContract']={'space':'sRGB D65','transfer':'sRGB encoded','storage':'owned CPU RGBA16 native-endian','range':[0,65535],'alpha':'forced opaque; input alpha not preserved','clipping':'spk_to_rgba16 clamp [0,1] then *65535+.5 -> uint16; earlier spectral/scanner stages have their own nonlinear bounds','readbackTiming':'inside render call, cannot disaggregate with public ABI','vram':'allocation ledger only; no physical residency peak measured'}
 (args.output/'report.json').write_text(json.dumps(report,indent=2,allow_nan=False));print(json.dumps({'cases':len(report['cases']),'executed':sum(x['status']=='EXECUTED_NOT_PRODUCT_APPROVED' for x in report['cases']),'output':str(args.output)}))
if __name__=='__main__':main()
