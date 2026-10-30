"""Strict PortraA stream caller. Private pixels stay under authorized local root."""
from pathlib import Path
import argparse,ctypes,json,os,sys,time,threading,signal,hashlib
import numpy as np,psutil
from natural_admission import load_json,sha,npy_info,scan,LockedInput,output_path,atomic_json,resources,DOMAINS,need

def run(a):
 output=output_path(a.output);output.mkdir();report={'version':'StreamResearchCallerV1','success':False,'status':'FAILED','width':None,'height':None,'callbackErrors':[]};lib=None;lock=None;image=None;raw=None;reply=ctypes.c_void_p();cancel=threading.Event();peak=[psutil.Process().memory_info().rss];stop=threading.Event();call_counts={'read':0,'consumer':0,'poll':0};hasher=hashlib.sha256();returned=[];signal_old=None
 def monitor():
  while not stop.wait(.01):
   rss=psutil.Process().memory_info().rss;peak[0]=max(peak[0],rss)
   if rss>int(a.host_mib*1048576):cancel.set()
 thread=threading.Thread(target=monitor,daemon=True);thread.start();t0=time.perf_counter()
 try:
  need(sha(a.manifest)==a.manifest_sha,'Preparation seal hash changed');manifest=load_json(a.manifest);need(manifest['version']=='NaturalAdmissionManifestV2' and manifest['sourceSha256'].lower()=='b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401','Unknown preparation source');inputpath=a.input.resolve();relative=inputpath.relative_to(Path(manifest['inputRoot']).resolve()).as_posix();matches=[x for x in manifest['files'] if x['path']==relative];need(len(matches)==1,'Input not uniquely prepared');entry=matches[0];need(entry['route']=='A' and entry['domain']==DOMAINS['A'] and entry['orientation']==1,'Only PortraA linear D50 admitted');need(manifest['bridge']['version']=='ExistingResearchD65ToD50V1','Unknown bridge');info=npy_info(inputpath);need(info['shape']==entry['shape'] and info['fileBytes']==entry['bytes'],'Prepared dimensions differ');need(info['dataBytes']+psutil.Process().memory_info().rss+128*1048576<=a.host_mib*1048576,'Independent host map+callback working-set budget refused');lock=LockedInput(inputpath);need(sha(inputpath)==entry['fileSha256'],'Input modified');stats=scan(inputpath,info,16);need(stats['pixelSha256']==entry['pixelSha256'],'Prepared pixels changed');need(resources(a.resources)==manifest['resources'],'Resource collection changed');source_case=load_json(Path(__file__).resolve().parents[2]/'docs/research/color-engine-20261029/company-round6/natural-cases.json');resolved=next(c for c in source_case['cases'] if c['localDirectory']=='portra-A-proxy')['resolved'];resolved.update(striped=False,strip_rows=0,strip_budget_bytes=0)
  h,w,_=info['shape'];report.update(width=w,height=h,inputFileSha256=entry['fileSha256'],inputPixelSha256=stats['pixelSha256'],preparationManifestSha256=a.manifest_sha,sourceSha256=manifest['sourceSha256'],resolved=resolved,bridgeVersion=manifest['bridge']['version'],librarySha256=sha(a.library),resourceIdentity=manifest['resources'],requestedRows=a.rows,consumerRows=a.consumer_rows)
  gpu_csv=__import__('subprocess').check_output(['nvidia-smi','--query-gpu=memory.free','--format=csv,noheader,nounits'],text=True);free=int(gpu_csv.strip())*1048576;device_cap=min(int(a.device_mib*1048576),2*1024**3,max(0,free-256*1048576));host_cap=int(a.host_mib*1048576);staging_cap=int(a.staging_mib*1048576);need(host_cap<=512*1048576 and staging_cap<=256*1048576 and host_cap+info['dataBytes']<psutil.virtual_memory().available-1024**3,'Independent CPU/mapping budget refused');report['budgets']={'deviceBytes':device_cap,'stagingBytes':staging_cap,'hostBytes':host_cap,'currentFreeGpuBytes':free,'reserveBytes':256*1048576,'researchCapUnchanged':2*1024**3};image=np.load(inputpath,mmap_mode='r');resource_map={x['path']:x['sha256'] for x in manifest['resources']};resource_sha=hashlib.sha256(json.dumps(resource_map,sort_keys=True,separators=(',',':')).encode()).hexdigest();request={'version':'SpektraStreamingResearchV1','route':'A','domain':DOMAINS['A'],'orientation':1,'sourceSha256':manifest['sourceSha256'].lower(),'inputPixelSha256':entry['pixelSha256'],'backendSha256':sha(a.library),'resourceManifestSha256':resource_sha,'resolved':resolved,'negativeStore':str(output/'negative-store.partial')}
  if a.fault=='unknown':request['resolved']['grain_active']=True
  if a.fault=='unknown-key':request['extra']=1
  if a.fault=='wrong-route':request['route']='B'
  if a.fault=='bad-input-hash':request['inputPixelSha256']='0'*64
  if a.fault=='bad-backend':request['backendSha256']='0'*64
  raw=(output/'rgba16.partial').open('xb');lib=ctypes.CDLL(str(a.library));READ=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_float));CONSUME=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_uint16));POLL=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p)
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
    if psutil.Process().memory_info().rss>host_cap:report['callbackErrors'].append({'callback':'poll','error':'Process RSS host budget exceeded'});cancel.set()
    return int(cancel.is_set())
   except BaseException as e:report['callbackErrors'].append({'callback':'poll','error':repr(e)});return 1
  fn=lib.spk_stream_research_v1;fn.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint64,ctypes.c_uint64,ctypes.c_uint64,READ,CONSUME,POLL,ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_int;lib.spk_string_free.argtypes=[ctypes.c_void_p]
  def invoke():returned.append(fn(str(a.resources).encode(),json.dumps(request).encode(),w,h,a.rows,host_cap,device_cap,staging_cap,read,consume,poll,None,ctypes.byref(reply)))
  worker=threading.Thread(target=invoke);start=time.perf_counter();worker.start()
  try:
   while worker.is_alive():worker.join(.05)
  except BaseException:
   cancel.set()
   while worker.is_alive():
    try:worker.join(.05)
    except KeyboardInterrupt:continue
  report['nativeCallMs']=(time.perf_counter()-start)*1000;report['joinedBeforeRelease']=not worker.is_alive();need(bool(reply.value),'Native response missing');native=json.loads(ctypes.string_at(reply).decode());lib.spk_string_free(reply);reply=ctypes.c_void_p();report['native']=native;report['returncode']=returned[0];raw.close();raw=None;need(native['deviceLiveAfter']==0 and native['stagingLiveAfter']==0,'Live native allocations after call')
  if returned[0]!=0:raise RuntimeError(native.get('error','stream failed'))
  # Native has consumed the complete source; do not retain its file pages while
  # output mapping/TIFF validation is live. No rerender required.
  image._mmap.close();image=None;lock.close();lock=None
  need(not report['callbackErrors'] and nextrow[0]==h,'Incomplete consumer');report['outputPixelSha256']=hasher.hexdigest();need((output/'rgba16.partial').stat().st_size==w*h*8,'Output byte length differs');report['rawReadbackHash']=sha(output/'rgba16.partial');need(report['rawReadbackHash']==report['outputPixelSha256'],'Raw readback differs')
  import tifffile
  from PIL import ImageCms
  rgb=np.memmap(output/'rgba16.partial',dtype='<u2',mode='r',shape=(h,w,4));start=time.perf_counter();icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
  with tifffile.TiffWriter(output/'display.partial.tif') as writer:writer.write(data=(np.ascontiguousarray(rgb[y:y+1]) for y in range(h)),shape=(h,w,4),dtype='<u2',photometric='rgb',extrasamples='unassalpha',rowsperstrip=1,compression=None,iccprofile=icc)
  rgb._mmap.close();report['tiffWriteMs']=(time.perf_counter()-start)*1000;back=hashlib.sha256();start=time.perf_counter()
  with tifffile.TiffFile(output/'display.partial.tif') as tf:
   page=tf.pages[0];need(page.shape==(h,w,4),'TIFF size changed')
   need(page.compression==1 and page.rowsperstrip==1 and len(page.dataoffsets)==h,'Unexpected TIFF layout')
   with (output/'display.partial.tif').open('rb') as binary:
    for offset,count in zip(page.dataoffsets,page.databytecounts):
     need(count==w*8,'TIFF strip bytecount differs');binary.seek(offset);row=binary.read(count);need(len(row)==count,'TIFF truncated');back.update(row)
  report['tiffReadbackMs']=(time.perf_counter()-start)*1000;need(back.hexdigest()==report['outputPixelSha256'],'TIFF pixels differ');report['tiff16Exact']=True
  if a.oracle:
   need(a.oracle.stat().st_size==w*h*8,'Oracle byte length mismatch');oracle=np.memmap(a.oracle,dtype='<u2',mode='r',shape=(h,w,4));actual=np.memmap(output/'rgba16.partial',dtype='<u2',mode='r',shape=(h,w,4));changed=0;first=None;maxdelta=0
   for y in range(h):
    delta=actual[y].astype(np.int32)-oracle[y].astype(np.int32);changed+=int((delta!=0).sum());maxdelta=max(maxdelta,int(np.abs(delta).max()))
    if first is None and (delta!=0).any():
     x,c=map(int,np.argwhere(delta!=0)[0]);first={'xyChannel':[x,y,c],'stream':int(actual[y,x,c]),'whole':int(oracle[y,x,c])}
   report['oracle']={'wholeVsStreamExact':changed==0,'changedChannels':changed,'maxAbsU16':maxdelta,'firstDifference':first,'wholeHash':sha(a.oracle)};oracle._mmap.close();actual._mmap.close();need(changed==0,'Whole/stream mismatch; no tolerance change')
  need(not cancel.is_set() and peak[0]<=host_cap,'Host process budget exceeded before final commit');os.rename(output/'rgba16.partial',output/'rgba16.bin');os.rename(output/'display.partial.tif',output/'display-rgba16.tif');report['success']=True;report['status']='COMPLETE'
 except BaseException as e:report['primaryError']=repr(e)
 finally:
  stop.set();thread.join();report['sampledProcessPeakRssBytes']=peak[0];report['osProcessPeakWorkingSetBytes']=getattr(psutil.Process().memory_info(),'peak_wset',None);report['wallMs']=(time.perf_counter()-t0)*1000;report['callbacks']=call_counts
  cleanup_errors=[]
  for label,action in [('raw',lambda:raw.close() if raw else None),('reply',lambda:lib.spk_string_free(reply) if reply.value and lib else None),('input_mapping',lambda:image._mmap.close() if image is not None else None),('input_lock',lambda:lock.close() if lock else None)]:
   try:action()
   except BaseException as cleanup_error:cleanup_errors.append({'resource':label,'error':repr(cleanup_error)})
  report['cleanupErrors']=cleanup_errors
  if cleanup_errors:
   report['success']=False;report['status']='FAILED_CLEANUP';report.setdefault('primaryError','Cleanup failed after call')
   for final in ['rgba16.bin','display-rgba16.tif']:
    file=output/final
    if file.exists():
     try:file.rename(file.with_name(file.name+'.failed-cleanup'))
     except BaseException as rename_error:cleanup_errors.append({'resource':final,'error':'failed-output marking: '+repr(rename_error)})
  try:atomic_json(output/'report.json',report)
  except BaseException as reporting_error:
   print(json.dumps({'primaryError':report.get('primaryError'),'reportWriteError':repr(reporting_error),'success':False}),file=sys.stderr);raise
 print(json.dumps({'success':report['success'],'size':[report['width'],report['height']],'status':report['status'],'native':report.get('native',{}).get('error'),'hash':report.get('outputPixelSha256'),'oracle':report.get('oracle'),'peak':report.get('native',{}).get('devicePeakBytes')}))
 return report
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('input',type=Path);p.add_argument('output',type=Path);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--manifest-sha',required=True);p.add_argument('--library',type=Path,required=True);p.add_argument('--resources',type=Path,required=True);p.add_argument('--rows',type=int,default=37);p.add_argument('--consumer-rows',type=int,default=7);p.add_argument('--device-mib',type=float,default=256);p.add_argument('--staging-mib',type=float,default=64);p.add_argument('--host-mib',type=float,default=512);p.add_argument('--oracle',type=Path);p.add_argument('--fault',choices=['none','read','consumer','cancel','unknown','unknown-key','wrong-route','bad-input-hash','bad-backend'],default='none');a=p.parse_args();r=run(a);sys.exit(0 if r['success'] else 3)
