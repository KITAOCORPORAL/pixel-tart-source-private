"""Home V2 caller, same frozen PortraA native wire and math; strict whole caller cap."""
from pathlib import Path
import argparse,ctypes,json,os,sys,time,threading,hashlib
import numpy as np,psutil
from natural_admission import load_json,sha,npy_info,scan,LockedInput,output_path,atomic_json,resources,DOMAINS,need
from host_environment import environment
from host_budget import HostBudget,finalize_group

def run(a):
 output=output_path(a.output);output.mkdir();report={'version':'StreamResearchHomeCallerV2','success':False,'status':'FAILED','width':None,'height':None,'callbackErrors':[],'cleanupErrors':[]};lib=None;lock=None;image=None;raw=None;reply=ctypes.c_void_p();cancel=threading.Event();host_cap=int(a.host_mib*1048576);need(0<host_cap<=512*1048576,'Caller cap cannot exceed512MiB');budget=HostBudget(host_cap,cancel).start();t0=time.perf_counter();call_counts={'read':0,'consumer':0,'poll':0};hasher=hashlib.sha256();returned=[];worker=None
 try:
  host=environment();report['environmentVersion']=host['version'];budget.enforce();need(sha(a.manifest)==a.manifest_sha,'Preparation seal hash changed');manifest=load_json(a.manifest);need(manifest['version']=='NaturalAdmissionManifestV2' and manifest['sourceSha256'].lower()=='b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401','Unknown preparation source');inputpath=a.input.resolve();relative=inputpath.relative_to(Path(manifest['inputRoot']).resolve()).as_posix();matches=[x for x in manifest['files'] if x['path']==relative];need(len(matches)==1,'Input not uniquely prepared');entry=matches[0];need(entry['route']=='A' and entry['domain']==DOMAINS['A'] and entry['orientation']==1,'Only PortraA linear D50 admitted');need(manifest['bridge']['version']=='ExistingResearchD65ToD50V1','Unknown bridge');info=npy_info(inputpath);need(info['shape']==entry['shape'] and info['fileBytes']==entry['bytes'],'Prepared dimensions differ');need(info['dataBytes']+psutil.Process().memory_info().rss+128*1048576<=host_cap,'Independent host map+callback working-set budget refused');lock=LockedInput(inputpath);need(sha(inputpath)==entry['fileSha256'],'Input modified');stats=scan(inputpath,info,16);need(stats['pixelSha256']==entry['pixelSha256'],'Prepared pixels changed');need(resources(a.resources)==manifest['resources'],'Resource collection changed');source_case=load_json(Path(__file__).resolve().parents[2]/'docs/research/color-engine-20261029/company-round6/natural-cases.json');resolved=next(c for c in source_case['cases'] if c['localDirectory']=='portra-A-proxy')['resolved'].copy();resolved.update(striped=False,strip_rows=0,strip_budget_bytes=0)
  h,w,_=info['shape'];report.update(width=w,height=h,inputFileSha256=entry['fileSha256'],inputPixelSha256=stats['pixelSha256'],preparationManifestSha256=a.manifest_sha,sourceSha256=manifest['sourceSha256'],resolved=resolved,bridgeVersion=manifest['bridge']['version'],librarySha256=sha(a.library),resourceIdentity=manifest['resources'],requestedRows=a.rows,consumerRows=a.consumer_rows)
  gpu_csv=__import__('subprocess').check_output(['nvidia-smi','--query-gpu=memory.free','--format=csv,noheader,nounits'],text=True);free=int(gpu_csv.strip())*1048576;device_cap=min(int(a.device_mib*1048576),2*1024**3,max(0,free-256*1048576));staging_cap=int(a.staging_mib*1048576);need(0<device_cap<=256*1048576 and 0<staging_cap<=64*1048576 and host_cap+info['dataBytes']<psutil.virtual_memory().available-1024**3,'Independent CPU/device/staging budget refused');report['budgets']={'deviceBytes':device_cap,'stagingBytes':staging_cap,'hostBytes':host_cap,'currentFreeGpuBytes':free,'reserveBytes':256*1048576,'researchCapUnchanged':2*1024**3};image=np.load(inputpath,mmap_mode='r',allow_pickle=False);resource_map={x['path']:x['sha256'] for x in manifest['resources']};resource_sha=hashlib.sha256(json.dumps(resource_map,sort_keys=True,separators=(',',':')).encode()).hexdigest();request={'version':'SpektraStreamingResearchV1','route':'A','domain':DOMAINS['A'],'orientation':1,'sourceSha256':manifest['sourceSha256'].lower(),'inputPixelSha256':entry['pixelSha256'],'backendSha256':sha(a.library),'resourceManifestSha256':resource_sha,'resolved':resolved,'negativeStore':str(output/'negative-store.partial')}
  if a.fault=='unknown':request['resolved']['grain_active']=True
  if a.fault=='unknown-key':request['extra']=1
  if a.fault=='wrong-route':request['route']='B'
  if a.fault=='bad-input-hash':request['inputPixelSha256']='0'*64
  if a.fault=='bad-backend':request['backendSha256']='0'*64
  need(a.consumer_rows>0 and a.rows>0,'Row counts must be positive');raw=(output/'rgba16.partial').open('xb');lib=ctypes.CDLL(str(a.library));READ=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_float));CONSUME=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_uint16));POLL=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p)
  @READ
  def read(_,y,count,ptr):
   try:
    call_counts['read']+=1
    if a.fault=='read' and call_counts['read']==2:raise IOError('injected read_rows failure')
    need(y+count<=h,'Native read out of range');dest=np.ctypeslib.as_array(ptr,shape=(count*w*3,)).reshape(count,w,3);np.copyto(dest,image[y:y+count]);return 0
   except BaseException as e:report['callbackErrors'].append({'callback':'read','error':repr(e)});return 1
  nextrow=[0]
  @CONSUME
  def consume(_,y,count,ptr):
   try:
    call_counts['consumer']+=1
    if a.fault=='consumer' and call_counts['consumer']==2:raise IOError('injected consumer failure')
    need(y==nextrow[0] and y+count<=h,'Consumer rows not ordered');arr=np.ctypeslib.as_array(ptr,shape=(count*w*4,)).reshape(count,w,4)
    for start in range(0,count,a.consumer_rows):
     block=arr[start:start+a.consumer_rows];view=memoryview(block).cast('B');raw.write(view);hasher.update(view)
    nextrow[0]+=count;return 0
   except BaseException as e:report['callbackErrors'].append({'callback':'consumer','error':repr(e)});return 1
  @POLL
  def poll(_):
   try:
    call_counts['poll']+=1
    if a.fault=='cancel' and call_counts['read']>=2:cancel.set()
    budget.sample();return int(cancel.is_set())
   except BaseException as e:report['callbackErrors'].append({'callback':'poll','error':repr(e)});return 1
  fn=lib.spk_stream_research_v1;fn.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint64,ctypes.c_uint64,ctypes.c_uint64,READ,CONSUME,POLL,ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_int;lib.spk_string_free.argtypes=[ctypes.c_void_p]
  def invoke():returned.append(fn(str(a.resources).encode(),json.dumps(request).encode(),w,h,a.rows,host_cap,device_cap,staging_cap,read,consume,poll,None,ctypes.byref(reply)))
  worker=threading.Thread(target=invoke);start=time.perf_counter();worker.start()
  try:
   while worker.is_alive():worker.join(.05)
  except BaseException:
   cancel.set();worker.join();raise
  report['nativeCallMs']=(time.perf_counter()-start)*1000;report['joinedBeforeRelease']=not worker.is_alive();need(bool(reply.value),'Native response missing');native=json.loads(ctypes.string_at(reply).decode());lib.spk_string_free(reply);reply=ctypes.c_void_p();report['native']=native;need(bool(returned),'Native result missing');report['returncode']=returned[0];raw.flush();os.fsync(raw.fileno());raw.close();raw=None;need(native['deviceLiveAfter']==0 and native['stagingLiveAfter']==0,'Live native allocations after call')
  if returned[0]!=0:raise RuntimeError(native.get('error','stream failed'))
  image._mmap.close();image=None;lock.close();lock=None;budget.enforce();need(not report['callbackErrors'] and nextrow[0]==h,'Incomplete consumer');report['outputPixelSha256']=hasher.hexdigest();need((output/'rgba16.partial').stat().st_size==w*h*8,'Output byte length differs');report['rawReadbackHash']=sha(output/'rgba16.partial');need(report['rawReadbackHash']==report['outputPixelSha256'],'Raw readback differs')
  import tifffile
  from PIL import ImageCms
  start=time.perf_counter();icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
  # Direct row reader: never map or assemble full output while writing TIFF.
  def rows():
   with (output/'rgba16.partial').open('rb') as f:
    for y in range(h):
     budget.enforce();row=f.read(w*8);need(len(row)==w*8,'Short raw row during TIFF output');yield np.frombuffer(row,dtype='<u2').reshape(1,w,4)
  with tifffile.TiffWriter(output/'display.partial.tif') as writer:writer.write(data=rows(),shape=(h,w,4),dtype='<u2',photometric='rgb',extrasamples='unassalpha',rowsperstrip=1,compression=None,iccprofile=icc)
  report['tiffWriteMs']=(time.perf_counter()-start)*1000;back=hashlib.sha256();start=time.perf_counter()
  with tifffile.TiffFile(output/'display.partial.tif') as tf:
   need(len(tf.pages)==1,'Unexpected page count');page=tf.pages[0];need(page.shape==(h,w,4) and page.compression==1 and page.rowsperstrip==1 and len(page.dataoffsets)==len(page.databytecounts)==h,'Unexpected TIFF layout')
   with (output/'display.partial.tif').open('rb') as binary:
    for offset,count in zip(page.dataoffsets,page.databytecounts):
     budget.enforce();need(count==w*8,'TIFF strip bytecount differs');binary.seek(offset);row=binary.read(count);need(len(row)==count,'TIFF truncated');back.update(row)
  report['tiffReadbackMs']=(time.perf_counter()-start)*1000;need(back.hexdigest()==report['outputPixelSha256'],'TIFF pixels differ');report['tiff16Exact']=True
  if a.oracle:
   need(a.oracle.stat().st_size==w*h*8,'Oracle byte length mismatch');changed=0;first=None;maxdelta=0
   with a.oracle.open('rb') as oracle,(output/'rgba16.partial').open('rb') as actual:
    for y in range(h):
     left=actual.read(w*8);right=oracle.read(w*8);need(len(left)==len(right)==w*8,'Short oracle row');x=np.frombuffer(left,dtype='<u2').reshape(w,4);z=np.frombuffer(right,dtype='<u2').reshape(w,4);delta=x.astype(np.int32)-z.astype(np.int32);changed+=int((delta!=0).sum());maxdelta=max(maxdelta,int(np.abs(delta).max()))
     if first is None and (delta!=0).any():
      xx,c=map(int,np.argwhere(delta!=0)[0]);first={'xyChannel':[xx,y,c],'stream':int(x[xx,c]),'whole':int(z[xx,c])}
   report['oracle']={'wholeVsStreamExact':changed==0,'changedChannels':changed,'maxAbsU16':maxdelta,'firstDifference':first,'wholeHash':sha(a.oracle)};need(changed==0,'Whole/stream mismatch; no tolerance change')
  need(not cancel.is_set(),'Cancelled or host budget exceeded');report['candidateComplete']=True
 except BaseException as e:report['primaryError']=repr(e)
 finally:
  if worker is not None and worker.is_alive():cancel.set();worker.join()
  for label,action in [('raw',lambda:raw.close() if raw else None),('reply',lambda:lib.spk_string_free(reply) if reply.value and lib else None),('input_mapping',lambda:image._mmap.close() if image is not None else None),('input_lock',lambda:lock.close() if lock else None)]:
   try:action()
   except BaseException as e:report['cleanupErrors'].append({'resource':label,'error':repr(e)})
  if a.fault=='cleanup':report['cleanupErrors'].append({'resource':'injected','error':'injected cleanup failure'})
  if report['cleanupErrors']:report['status']='FAILED_CLEANUP'
  try:report.update(budget.finish())
  except BaseException as e:report['cleanupErrors'].append({'resource':'host-monitor','error':repr(e)});report['status']='FAILED_CLEANUP'
  report['wallMs']=(time.perf_counter()-t0)*1000;report['callbacks']=call_counts
 if report.get('candidateComplete') and not report.get('primaryError') and not report['cleanupErrors']:
  finalize_group(output,report,budget,atomic_json)
 else:atomic_json(output/'report.json',report)
 print(json.dumps({'success':report['success'],'size':[report['width'],report['height']],'status':report['status'],'hash':report.get('outputPixelSha256'),'oracle':report.get('oracle'),'sampledRss':report.get('sampledProcessPeakRssBytes'),'osPeak':report.get('osProcessPeakWorkingSetBytes')}));return report
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('input',type=Path);p.add_argument('output',type=Path);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--manifest-sha',required=True);p.add_argument('--library',type=Path,required=True);p.add_argument('--resources',type=Path,required=True);p.add_argument('--rows',type=int,default=37);p.add_argument('--consumer-rows',type=int,default=7);p.add_argument('--device-mib',type=float,default=256);p.add_argument('--staging-mib',type=float,default=64);p.add_argument('--host-mib',type=float,default=512);p.add_argument('--oracle',type=Path);p.add_argument('--fault',choices=['none','read','consumer','cancel','cleanup','unknown','unknown-key','wrong-route','bad-input-hash','bad-backend'],default='none');a=p.parse_args();r=run(a);sys.exit(0 if r['success'] else 3)
