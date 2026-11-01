"""<=1200 natural development coverage, fixed original PortraA, independent paired admission."""
from pathlib import Path
import argparse,json,ctypes,time,threading,sys,hashlib,os
import numpy as np,psutil
from PIL import Image,ImageCms,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/cross-device-diagnostic'))
from compare_rows import compare

def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for b in iter(lambda:f.read(4194304),b''):h.update(b)
 return h.hexdigest()
def need(c,m):
 if not c:raise ValueError(m)
def tree(root):return {p.relative_to(root).as_posix():sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();r=a.root;out=a.output.resolve();need(out.is_relative_to((r/'round11').resolve()) and not out.exists(),'New round11 output required');out.mkdir();inp=r/'round11/natural-inputs-v1';m=json.loads((inp/'manifest.json').read_text());sources=json.loads((Path(__file__).parent/'photo-preparation/sources-v1.json').read_text());need(len(m['sources'])==len(sources['entries'])==6,'Frozen max6 coverage');library=r/'round11/build-natural-v1/engine/spektrafilm_engine.dll';resources=r/'resources-patch';expected=json.loads((ROOT/'tools/cross-device-diagnostic/expected-resources.json').read_text());need(tree(resources)==expected,'Resources changed');resolved=json.loads((ROOT/'tools/cross-device-diagnostic/resolved-portraA.json').read_text());resourceHash=hashlib.sha256(json.dumps(expected,sort_keys=True,separators=(',',':')).encode()).hexdigest();sys.path[:0]=[str(r/'source-patch/engine/tests'),str(r/'source-patch/engine/tools')]
 from spk_ctypes import Engine
 from windows_lifecycle import OwnedResult
 lib=ctypes.CDLL(str(library));READ=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_float));WRITE=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_uint16));POLL=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p);fn=lib.spk_stream_natural_photo_v1;fn.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint64,ctypes.c_uint64,ctypes.c_uint64,READ,WRITE,POLL,ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_int;lib.spk_string_free.argtypes=[ctypes.c_void_p]
 report={'version':'NaturalPhotographyCoverageRunV1','admissionManifestSha256':sha(inp/'manifest.json'),'sourceContractSha256':sha(Path(__file__).parent/'photo-preparation/sources-v1.json'),'naturalLibrarySha256':sha(library),'wholeLibrarySha256':sha(r/'build-patch/engine/spektrafilm_engine.dll'),'originalNaturalWhitelistUnchanged':True,'device':'home RTX5060Ti','resourceSetSha256':resourceHash,'resolved':resolved,'maxLongEdge':1200,'role':'development exploration only','independentValidationOrHoldout':False,'photos':[],'profile':'linear standard ProPhoto D50 input -> encoded sRGB D65 RGBA16 output, opaque alpha','absoluteColorAccuracy':'NOT_RUN','preference':'NOT_RUN','physicalFilmFit':'NOT_RUN'}
 icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();contact=[]
 for entry,source in zip(m['sources'],sources['entries']):
  id=entry['id'];need(entry['sha256']==source['sha256'] and sha(Path(source['path'])).upper()==source['sha256'],'Source identity changed');path=inp/entry['file'];need(sha(path)==entry['fileSha256'],'Proxy file changed');image=np.load(path,allow_pickle=False);h,w,c=image.shape;need(image.shape==tuple(entry['proxyShape']) and max(h,w)<=1200 and np.isfinite(image).all(),'Proxy domain invalid');need(hashlib.sha256(image.tobytes()).hexdigest()==entry['pixelSha256'],'Proxy pixels changed');folder=out/id;folder.mkdir();stop=threading.Event();peak=[0];ospeak=[0]
  def sample():
   info=psutil.Process().memory_info();peak[0]=max(peak[0],info.rss);ospeak[0]=max(ospeak[0],getattr(info,'peak_wset',0))
  def monitor():
   while not stop.wait(.005):sample()
  thread=threading.Thread(target=monitor);sample();thread.start();record={'id':id,'sourceSha256':entry['sha256'],'inputPixelSha256':entry['pixelSha256'],'shape':[h,w,4],'streams':[]};report['photos'].append(record)
  try:
   t=time.perf_counter()
   with Engine(library=r/'build-patch/engine/spektrafilm_engine.dll',resources=resources) as engine:
    record['engineCreateMs']=(time.perf_counter()-t)*1000;t=time.perf_counter()
    with engine.open(image,resolved) as session:
     need(session.get_params()==resolved,'Whole params drift');record['openUploadMs']=(time.perf_counter()-t)*1000;t=time.perf_counter()
     with OwnedResult(session,'full') as held:whole=held.pixels().copy();record['wholeNativeAndCopyMs']=(time.perf_counter()-t)*1000;record['wholeInfo']=held.info()
    record['wholeMemoryLedgerAfterRelease']=engine.memory_report()
   whole.tofile(folder/'whole.rgba16')
   for rows in [17,37]:
    output=np.empty((h,w,4),np.uint16);nextrow=[0];errors=[]
    @READ
    def read(_,y,n,p):
     try:np.copyto(np.ctypeslib.as_array(p,shape=(n*w*3,)).reshape(n,w,3),image[y:y+n]);return 0
     except BaseException as ex:errors.append(repr(ex));return 1
    @WRITE
    def write(_,y,n,p):
     try:need(y==nextrow[0] and y+n<=h,'Consumer range');output[y:y+n]=np.ctypeslib.as_array(p,shape=(n*w*4,)).reshape(n,w,4);nextrow[0]+=n;return 0
     except BaseException as ex:errors.append(repr(ex));return 1
    @POLL
    def poll(_):return 0
    req={'version':'NaturalPhotographyStreamV1','route':'A','domain':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','orientation':1,'sourceSha256':entry['sha256'].lower(),'inputPixelSha256':entry['pixelSha256'],'backendSha256':sha(library),'resourceManifestSha256':resourceHash,'resolved':resolved,'negativeStore':str(folder/f'negative-r{rows}.partial')};reply=ctypes.c_void_p();t=time.perf_counter();status=fn(str(resources).encode(),json.dumps(req).encode(),w,h,rows,512*1048576,256*1048576,64*1048576,read,write,poll,None,ctypes.byref(reply));ms=(time.perf_counter()-t)*1000
    try:native=json.loads(ctypes.string_at(reply).decode())
    finally:
     if reply.value:lib.spk_string_free(reply)
    need(status==0 and nextrow[0]==h and not errors,'Natural stream failed '+str(native));need(native['deviceLiveAfter']==native['stagingLiveAfter']==0 and native['negativeStoreAbsentAfter'],'Native cleanup');output.tofile(folder/f'stream-r{rows}.rgba16');diff=compare(folder/'whole.rgba16',folder/f'stream-r{rows}.rgba16',[h,w,4],'<u2');need(diff['changedChannels']==0,'Natural whole/stream mismatch');record['streams'].append({'rows':rows,'nativeMs':ms,'native':native,'comparison':diff});sample()
   before=np.load(inp/(id+'-display-proxy.npy'));rgb=whole[:,:,:3];low=rgb==0;high=rgb==65535;beforeLow=before<=0;beforeHigh=before>=1
   record['numeric']={'finitePreparedInput':True,'proxyInputNegativeFraction':(image<0).mean(axis=(0,1)).tolist(),'proxyInputAboveOneFraction':(image>1).mean(axis=(0,1)).tolist(),'beforeDisplayZeroFraction':beforeLow.mean(axis=(0,1)).tolist(),'beforeDisplayOneFraction':beforeHigh.mean(axis=(0,1)).tolist(),'outputZeroFraction':low.mean(axis=(0,1)).tolist(),'outputMaxFraction':high.mean(axis=(0,1)).tolist(),'newZeroVsBeforeDisplayFraction':(low&~beforeLow).mean(axis=(0,1)).tolist(),'newMaxVsBeforeDisplayFraction':(high&~beforeHigh).mean(axis=(0,1)).tolist(),'priorFullMasterZeroFraction':entry['fullMasterZeroFractionByChannel'],'priorFullMasterOneFraction':entry['fullMasterOneFractionByChannel'],'classificationLimit':'New output endpoints versus display proxy are a processing-domain observation, not proof of sensor clipping/recovery; prior full master endpoints already contain decoder loss.'}
   # Source aligned small display observations, not output accuracy targets.
   linear=np.where(before<=.04045,before/12.92,((before+.055)/1.055)**2.4);luma=linear@np.array([.2126,.7152,.0722]);record['inputZones']={'darkFractionBelowLinearY001':float((luma<.01).mean()),'brightFractionAboveLinearY08':float((luma>.8).mean()),'saturatedFractionDisplayChromaOver05':float((before.max(axis=2)-before.min(axis=2)>.5).mean())}
   record['rowBoundaryChecks']={str(rows):{'changedAtSeamRows':0,'allRowsCompared':h,'firstDifference':None,'sameDeviceWholeExact':True} for rows in [17,37]};Image.fromarray(np.rint(rgb/257).astype(np.uint8)).save(folder/'after.png',icc_profile=icc);Image.open(inp/(id+'-before.png')).save(folder/'before.png',icc_profile=icc)
   left=Image.open(folder/'before.png');right=Image.open(folder/'after.png');left.thumbnail((360,240));right.thumbnail((360,240));tile=Image.new('RGB',(740,270),'#222');draw=ImageDraw.Draw(tile);draw.text((10,5),id+' BEFORE / PortraA',fill='white');tile.paste(left,(0,25));tile.paste(right,(380,25));contact.append(tile);record['status']='EXECUTED_EXACT_NOT_ACCURACY_APPROVED'
  except BaseException as ex:record['status']='FAILED';record['primaryError']=repr(ex);raise
  finally:stop.set();thread.join();sample();record.update(sampledProcessPeakRssBytes=peak[0],osProcessLifetimePeakBytes=ospeak[0],memoryScope='one sequential process; OS is lifetime cumulative, sampled peak tracked per-photo interval; not raw decoder RSS');(out/'report.json').write_text(json.dumps(report,indent=2,allow_nan=False))
 full=Image.new('RGB',(1480,810),'#222')
 for i,tile in enumerate(contact):full.paste(tile,((i%2)*740,(i//2)*270))
 full.save(out/'comparison-contact.png');print(json.dumps({'photos':len(report['photos']),'exactPhotos':sum(x['status'].startswith('EXECUTED') for x in report['photos']),'output':str(out)}))
if __name__=='__main__':main()
