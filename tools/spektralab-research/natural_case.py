"""Manifest-bound V2 ABI caller. Full-size output processed in bounded rows."""
import argparse,ctypes,json,os,signal,sys,time,traceback,threading
from pathlib import Path
import numpy as np,psutil
from natural_admission import *
from run_matrix import timed

def gpu_budget(shape,striped=False,rows=127):
 import subprocess
 p=subprocess.run(['nvidia-smi','--query-gpu=memory.free','--format=csv,noheader,nounits'],capture_output=True,text=True,check=True);free=int(p.stdout.strip())*1048576;n=shape[0]*shape[1]
 estimate=n*76+96*1048576 # retained heuristic until source-measured model is approved
 return {'model':'Round6HeuristicV1_UNCHANGED','estimatedBytes':estimate,'currentFreeBytes':free,'limitBytes':min(2*1024**3,max(0,free-256*1048576)),'admitted':estimate<=min(2*1024**3,max(0,free-256*1048576)),'striped':striped,'rows':rows}
def cleanup(objects):
 errors=[]
 for label,obj in objects:
  if obj is not None:
   try:obj.close()
   except BaseException as e:errors.append({'resource':label,'error':repr(e)})
 return errors
def render_joined(factory,session,engine):
 box={}
 def work():
  try:box['result']=factory(session,'full')
  except BaseException as ex:box['error']=ex
 worker=threading.Thread(target=work);worker.start();interrupted=None
 try:
  while worker.is_alive():worker.join(.05)
 except BaseException as ex:
  interrupted=ex
  engine._lib.spk_cancel.argtypes=[ctypes.c_void_p,ctypes.c_char_p]
  try:engine._lib.spk_cancel(session._handle,None)
  except BaseException as cancel_error:box['cancelError']=repr(cancel_error)
  finally:
   while worker.is_alive():
    try:worker.join(.05)
    except KeyboardInterrupt:continue
 if interrupted:
  if box.get('result') is not None:
   try:box['result'].close()
   except BaseException as cleanup_error:interrupted.add_note('Result cleanup failed: '+repr(cleanup_error))
  if 'cancelError' in box:interrupted.add_note('Cancel request failed: '+box['cancelError'])
  raise interrupted
 if 'error' in box:raise box['error']
 return box['result']
def stream_result(held,output,expected=None):
 import tifffile
 from PIL import ImageCms
 r=held.result;w,h=int(r.width),int(r.height);pitch=int(r.row_stride_px);array=held.pixels();hp=__import__('hashlib').sha256();lo=np.full(3,65535);hi=np.zeros(3);zero=np.zeros(3,np.int64);full=zero.copy();write=time.perf_counter()
 with (output/'rgba16.bin').open('xb') as f:
  for y in range(h):
   row=np.ascontiguousarray(array[y],dtype='<u2');hp.update(memoryview(row).cast('B'));f.write(memoryview(row).cast('B'));rgb=row[:,:3];lo=np.minimum(lo,rgb.min(axis=0));hi=np.maximum(hi,rgb.max(axis=0));zero+=(rgb==0).sum(axis=0);full+=(rgb==65535).sum(axis=0)
 digest=hp.hexdigest();need(expected is None or digest==expected,'Output hash mismatch');rawms=(time.perf_counter()-write)*1000;icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();start=time.perf_counter()
 # TIFF iterator consumes rows; no entire output clone or entire TIFF readback.
 with tifffile.TiffWriter(output/'display-rgba16.tif') as writer:writer.write(data=(np.ascontiguousarray(array[y:y+1]) for y in range(h)),shape=(h,w,4),dtype='<u2',photometric='rgb',extrasamples='unassalpha',rowsperstrip=1,compression=None,iccprofile=icc)
 tiffms=(time.perf_counter()-start)*1000;read=__import__('hashlib').sha256()
 with tifffile.TiffFile(output/'display-rgba16.tif') as tf:
  page=tf.pages[0];need(page.shape==(h,w,4),'TIFF shape mismatch')
  for segment,_,_ in page.segments(maxworkers=1):read.update(memoryview(np.ascontiguousarray(segment)).cast('B'))
 need(read.hexdigest()==digest,'TIFF row readback mismatch');return {'width':w,'height':h,'rowStridePixels':pitch,'outputPixelSha256':digest,'rawWriteMs':rawms,'tiff16WriteMs':tiffms,'tiff16Exact':True,'statistics':{'minRGB':lo.tolist(),'maxRGB':hi.tolist(),'zeroSamples':zero.tolist(),'maximumSamples':full.tolist()},'resultOwnership':'No full NumPy clone; result retained through sequential TIFF encode/readback, then freed'}

def evaluate(a,engine_factory=None,result_factory=None,budget_override=None):
 output=output_path(a.output,getattr(a,'output_root',AUTHORIZED_OUTPUT));report={'version':'NaturalCaseV2','status':'REJECTED','success':False,'engineCreated':False,'uploaded':False,'cleanupErrors':[]};engine=session=held=None;original=None;image=None;input_lock=None
 try:
  saved=load_json(a.input) if a.mode=='reopen' else None;inputpath=Path(saved['inputFile']) if saved else a.input;route=saved['route'] if saved else a.route
  input_lock=LockedInput(inputpath);info=npy_info(inputpath);budget=budget_override if budget_override is not None else gpu_budget(info['shape'],a.striped,a.rows);report['gpuBudget']=budget
  m,entry,info,stats,cpu=validate(a.manifest,a.manifest_sha,inputpath,route,a.library,a.resources,psutil.virtual_memory().available,budget)
  need(saved is None or (saved['manifestSha256']==a.manifest_sha and saved['admittedEntry']==entry and saved['sourceSha256']==m['sourceSha256']),'Reopen preparation identity mismatch')
  report.update({'manifestSha256':a.manifest_sha,'manifestFile':str(a.manifest),'sourceSha256':m['sourceSha256'],'admittedEntry':entry,'inputFile':str(inputpath),'inputPixelSha256':stats['pixelSha256'],'inputShape':info['shape'],'boundedValidation':stats,'cpuBudget':cpu,'route':route,'librarySha256':m['backend']['sha256'],'resourceIdentity':m['resources'],'bridge':m['bridge']})
  params={'film_stock':a.film,'print_stock':a.paper,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'exposure_compensation_ev':0,'input_color_space':'ProPhoto RGB' if route=='A' else 'sRGB','input_cctf_decoding':route=='B','output_color_space':'sRGB','output_cctf_encoding':True,'striped':a.striped,'strip_rows':a.rows if a.striped else 0,'strip_budget_bytes':a.strip_budget}
  if saved:params=saved['resolved']
  need(params['film_stock']=='kodak_portra_400' and params['print_stock']=='kodak_portra_endura','V2 round7 caller restricted to PortraA contract; new stock requires separate evaluation')
  need(not params.get('grain_active',True) and not params.get('glare_active',True) and not params.get('halation_active',True) and not params.get('dir_couplers_active',True) and not params.get('auto_exposure',True),'Unknown stochastic/spatial configuration')
  need(not saved or saved['librarySha256']==m['backend']['sha256'],'Reopen backend identity mismatch')
  if engine_factory is None:
   sys.path[:0]=[str(a.source/'engine/tests'),str(a.source/'engine/tools')];from spk_ctypes import Engine;from windows_lifecycle import OwnedResult;engine_factory=Engine;result_factory=OwnedResult
  need(__import__('subprocess').check_output(['git','-C',str(a.source),'rev-parse','HEAD'],text=True).strip()=='c1447c6ab618fa4b1cb11fd2fe12826855007059','Wrong engine source')
  output.mkdir();image=np.load(inputpath,mmap_mode='r',allow_pickle=False);start=time.perf_counter();engine,report['initializationMs']=timed(lambda:engine_factory(library=a.library,resources=a.resources));report['engineCreated']=True;report['memoryAfterEngineCreate']=engine.memory_report();session,report['openAndUploadMs']=timed(lambda:engine.open(image,params));report['uploaded']=True;report['resolved']=session.get_params();need(all(report['resolved'][k]==v for k,v in params.items()),'Resolved mismatch');report['memoryAfterOpen']=engine.memory_report();held,report['renderAndReadbackMs']=timed(lambda:render_joined(result_factory,session,engine));need([int(held.result.height),int(held.result.width),3]==info['shape'],'Engine output dimension changed');report['memoryAfterRender']=engine.memory_report();report.update(stream_result(held,output,saved['outputPixelSha256'] if saved else None));
  if getattr(a,'repeat',False):
   held.close();held=None;repeat_result,report['repeatRenderMs']=timed(lambda:render_joined(result_factory,session,engine));held=repeat_result;repeat_hash=__import__('hashlib').sha256()
   for y in range(int(held.result.height)):repeat_hash.update(memoryview(np.ascontiguousarray(held.pixels()[y],dtype='<u2')).cast('B'))
   report['repeatExact']=repeat_hash.hexdigest()==report['outputPixelSha256'];need(report['repeatExact'],'Repeated output differs')
  report['status']='COMPLETE';report['success']=True
 except BaseException as e:original=e;report['primaryError']={'type':type(e).__name__,'message':str(e),'traceback':traceback.format_exc()};report['status']='REFUSED_BUDGET' if 'budget refused' in str(e).lower() else 'REJECTED_OR_FAILED'
 finally:
  errors=cleanup([('result',held),('session',session),('engine',engine)]);report['cleanupErrors']=errors
  if image is not None:
   try:image._mmap.close()
   except BaseException as e:errors.append({'resource':'input_mapping','error':repr(e)})
  if input_lock is not None:
   try:input_lock.close()
   except BaseException as e:errors.append({'resource':'input_lock','error':repr(e)})
  if errors:report['success']=False;report['status']='FAILED_CLEANUP';report.setdefault('primaryError',{'type':'CleanupError','message':'cleanup failed after render'})
  # Failed validation may create only a failure directory/report, never an engine or success flag.
  report['processPeakRssBytes']=getattr(psutil.Process().memory_info(),'peak_wset',psutil.Process().memory_info().rss);
  try:output.mkdir(exist_ok=True);atomic_json(output/'report.json',report)
  except BaseException as report_error:
   report['reportWriteError']=repr(report_error);report['success']=False;print(json.dumps(report),file=sys.stderr)
   if original is not None:original.add_note('Failure report write failed: '+repr(report_error));raise original
   raise
 return report

def main():
 p=argparse.ArgumentParser();p.add_argument('mode',choices=['case','reopen']);p.add_argument('source',type=Path);p.add_argument('library',type=Path);p.add_argument('resources',type=Path);p.add_argument('input',type=Path);p.add_argument('output',type=Path);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--manifest-sha',required=True);p.add_argument('--route',default='A');p.add_argument('--film',default='kodak_portra_400');p.add_argument('--paper',default='kodak_portra_endura');p.add_argument('--striped',action='store_true');p.add_argument('--rows',type=int,default=127);p.add_argument('--strip-budget',type=int,default=0);p.add_argument('--role',default='research');p.add_argument('--repeat',action='store_true');a=p.parse_args();signal.signal(signal.SIGTERM,lambda *args:(_ for _ in ()).throw(KeyboardInterrupt('termination requested')));r=evaluate(a);print(json.dumps({'status':r['status'],'success':r['success'],'engineCreated':r['engineCreated'],'hash':r.get('outputPixelSha256')}));sys.exit(0 if r['success'] else 3)
if __name__=='__main__':main()
