"""Synthetic-only backend runner; never changes natural source whitelist."""
import argparse,ctypes,json,os,sys,hashlib,time,re,subprocess
from pathlib import Path
import numpy as np
from generate_inputs import sha,VERSION
from compare_rows import compare

def need(c,m):
 if not c:raise ValueError(m)
def tree(r):return {p.relative_to(r).as_posix():sha(p) for p in sorted(r.rglob('*')) if p.is_file()}
def main():
 p=argparse.ArgumentParser();p.add_argument('--checkout',type=Path,required=True);p.add_argument('--library',type=Path,required=True);p.add_argument('--trace-library',type=Path,required=True);p.add_argument('--resources',type=Path,required=True);p.add_argument('--expected-resources',type=Path,required=True);p.add_argument('--fixtures',type=Path,required=True);p.add_argument('--resolved',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();need(not a.output.exists(),'Output exists');a.output.mkdir();manifest=json.loads((a.fixtures/'fixtures.json').read_text());need(manifest['version']==VERSION and manifest['naturalSource'] is None,'Synthetic identity required');resolved=json.loads(a.resolved.read_text());actualResources=tree(a.resources);expected=json.loads(a.expected_resources.read_text());need(actualResources==expected,'Resource/SPIRV mismatch');resourceSha=hashlib.sha256(json.dumps(actualResources,sort_keys=True,separators=(',',':')).encode()).hexdigest();syntheticSource=hashlib.sha256(b'SyntheticPortraCrossDeviceV1:PUBLIC_SYNTHETIC_NO_CAMERA').hexdigest()
 sys.path[:0]=[str(a.checkout/'engine/tests'),str(a.checkout/'engine/tools')]
 from spk_ctypes import Engine
 from windows_lifecycle import OwnedResult
 READ=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_float));WRITE=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_uint16));POLL=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p)
 def stream(libpath,image,path,rows,fixture,trace=False,natural=False):
  lib=ctypes.CDLL(str(libpath));fn=lib.spk_stream_research_v1 if natural else lib.spk_stream_synthetic_diag_v1;fn.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint64,ctypes.c_uint64,ctypes.c_uint64,READ,WRITE,POLL,ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_int;lib.spk_string_free.argtypes=[ctypes.c_void_p];h,w,_=image.shape;output=np.empty((h,w,4),'<u2');nextrow=[0];errors=[]
  @READ
  def read(_,y,n,p):
   try:np.copyto(np.ctypeslib.as_array(p,shape=(n*w*3,)).reshape(n,w,3),image[y:y+n]);return 0
   except BaseException as ex:errors.append(repr(ex));return 1
  @WRITE
  def write(_,y,n,p):
   try:need(y==nextrow[0],'Rows out of order');output[y:y+n]=np.ctypeslib.as_array(p,shape=(n*w*4,)).reshape(n,w,4);nextrow[0]+=n;return 0
   except BaseException as ex:errors.append(repr(ex));return 1
  @POLL
  def poll(_):return 0
  request={'version':'SyntheticPortraStreamingDiagnosticV1' if not natural else 'SpektraStreamingResearchV1','route':'A','domain':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','orientation':1,'sourceSha256':syntheticSource,'inputPixelSha256':fixture['pixelSha256'],'backendSha256':sha(libpath),'resourceManifestSha256':resourceSha,'resolved':resolved,'negativeStore':str(path.with_suffix('.negative.partial').resolve())};reply=ctypes.c_void_p();t=time.perf_counter();status=fn(str(a.resources.resolve()).encode(),json.dumps(request).encode(),w,h,rows,512*1048576,256*1048576,64*1048576,read,write,poll,None,ctypes.byref(reply));ms=(time.perf_counter()-t)*1000
  try:result=json.loads(ctypes.string_at(reply).decode()) if reply.value else {}
  finally:
   if reply.value:lib.spk_string_free(reply)
  if not natural:
   need(status==0 and nextrow[0]==h and not errors,'Synthetic native call failed '+str(result));need(result['deviceLiveAfter']==result['stagingLiveAfter']==0 and result['negativeStoreAbsentAfter'],'Synthetic cleanup failed');output.tofile(path)
  return {'status':status,'ms':ms,'native':result,'outputHash':sha(path) if path.exists() else None,'callbacksError':errors}
 report={'version':'SyntheticCrossDeviceRunV1','syntheticManifestHash':sha(a.fixtures/'fixtures.json'),'syntheticSourceIdentity':syntheticSource,'privatePhotosIncluded':False,'resources':actualResources,'resourceSetSha256':resourceSha,'resolved':resolved,'library':{'sha256':sha(a.library),'traceSha256':sha(a.trace_library)},'gpu':subprocess.check_output(['nvidia-smi','--query-gpu=name,driver_version','--format=csv,noheader'],text=True),'python':sys.version,'cases':[],'companyRun':'NOT_RUN_HOME_ONLY','harnessSha256':sha(Path(__file__)),'crossDeviceThreshold':'NONE_MEASURE_AND_LOCALIZE'}
 # Independent decoder diagnostic: fixed BT join vector, native mode3. This does
 # not change the PortraA input transfer flag or pretend its values are camera data.
 bt=np.asarray([np.asarray(int(v,16),dtype='<u4').view('<f4') for v in manifest['bt']['inputBits']],dtype='<f4');bt.tofile(a.output/'bt-input.f32');btout=np.empty_like(bt);lib=ctypes.CDLL(str(a.library));fn=lib.spk_synthetic_bt_probe_v1;fn.argtypes=[ctypes.c_char_p,ctypes.POINTER(ctypes.c_float),ctypes.c_uint32,ctypes.POINTER(ctypes.c_float)];fn.restype=ctypes.c_int;status=fn(str(a.resources.resolve()).encode(),bt.ctypes.data_as(ctypes.POINTER(ctypes.c_float)),bt.size,btout.ctypes.data_as(ctypes.POINTER(ctypes.c_float)));need(status==0,'BT diagnostic native read failed');btout.tofile(a.output/'bt-decode.f32');report['btDiagnostic']={'status':status,'mode':3,'shape':[1,int(bt.size),1],'inputBits':manifest['bt']['inputBits'],'outputBits':[format(int(v),'08x') for v in btout.view('<u4')],'inputHash':sha(a.output/'bt-input.f32'),'outputHash':sha(a.output/'bt-decode.f32'),'inputDomain':'BT encoded synthetic join vector','outputDomain':'linear BT transfer decoded float32; not PortraA film output','outputFile':'bt-decode.f32'}
 for f in manifest['fixtures']:
  image=np.load(a.fixtures/f['path'],allow_pickle=False);need(sha(a.fixtures/f['path'])==f['fileSha256'] and hashlib.sha256(image.tobytes()).hexdigest()==f['pixelSha256'],'Fixture modified');h,w,_=image.shape;folder=a.output/f['id'];folder.mkdir();case={'id':f['id'],'shape':[h,w,4],'stageWhole':[],'stageStream':[],'stream':[]};report['cases'].append(case)
  os.environ.pop('SPK_SYNTHETIC_STAGE_DIR',None)
  with Engine(library=a.library,resources=a.resources) as e:
   with e.open(image,resolved) as s:
    need(s.get_params()==resolved,'Whole defaults drift');
    with OwnedResult(s,'full') as result:whole=result.pixels().copy();case['wholeInfo']=result.info()
  whole.tofile(folder/'whole.rgba16')
  for rows in [1,7,17,64]:
   path=folder/f'stream-r{rows}.rgba16';record=stream(a.library,image,path,rows,f);d=compare(folder/'whole.rgba16',path,[h,w,4],'<u2');record.update(rows=rows,comparison=d);need(d['changedChannels']==0,'Same-device whole/stream mismatch');case['stream'].append(record)
  # A synthetic identity must be rejected by original natural-only entry.
  rejected=stream(a.library,image,folder/'must-not-exist.rgba16',7,f,natural=True);need(rejected['status']!=0,'Natural whitelist was weakened');case['naturalEntrySyntheticRejected']=True
  for kind in ['whole','stream']:
   trace=folder/('stages-'+kind);trace.mkdir();os.environ['SPK_SYNTHETIC_STAGE_DIR']=str(trace.resolve())
   if kind=='whole':
    with Engine(library=a.trace_library,resources=a.resources) as e:
     with e.open(image,resolved) as s:
      with OwnedResult(s,'full') as result:traced=result.pixels().copy()
    path=folder/'whole-traced.rgba16';traced.tofile(path)
   else:path=folder/'stream-traced-r7.rgba16';stream(a.trace_library,image,path,7,f)
   d=compare(folder/'whole.rgba16',path,[h,w,4],'<u2');need(d['changedChannels']==0,'Passive taps changed output');samples=[]
   for fp in sorted(trace.glob('*.f32')):
    match=re.match(r'(\d+)-(.+)-(\d+)-(\d+)-(\d+)\.f32$',fp.name);need(match is not None,'Bad stage file');serial,name,hh,ww,cc=match.groups();data=np.fromfile(fp,dtype='<f4').reshape(int(hh),int(ww),int(cc));samples.append({'file':str(fp.relative_to(a.output)).replace('\\','/'),'stage':name,'stageDomain':{'film_scale_and_expose':'exposed camera spectral-response RGB float','film_boost':'exposure RGB float','film_overscan':'exposure RGB float','film_blurs':'exposure RGB float','film_log_and_curves':'CMY negative density float','film_couplers':'CMY negative density float','film_grain':'CMY negative density float','print_spectral':'print scan tristimulus intermediate float, see fixed print_spectral function','print_glare':'print scan tristimulus intermediate float','print_linear':'output sRGB D65 linear float','print_scan_finish':'output sRGB D65 linear float after scanner filters','print_output':'output sRGB encoded float before final RGBA16 clamp/round'}.get(name,'fixed internal float intermediate'),'shape':list(data.shape),'sha256':sha(fp),'min':float(data.min()),'max':float(data.max()),'sample':data.reshape(-1,int(cc))[:8].tolist(),'note':'passive full small-stage readback; stream stage dimensions are band+halo and serial identifies execution order'})
   case['stageWhole' if kind=='whole' else 'stageStream']=samples;case[kind+'TraceUnchanged']=True
  os.environ.pop('SPK_SYNTHETIC_STAGE_DIR',None)
  (a.output/'run.json').write_text(json.dumps(report,indent=2,allow_nan=False))
 print(json.dumps({'syntheticCases':len(report['cases']),'sameDeviceExact':True,'company':'NOT_RUN','output':str(a.output)}))
if __name__=='__main__':main()

