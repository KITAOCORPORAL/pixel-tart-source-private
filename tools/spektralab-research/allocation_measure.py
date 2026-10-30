"""Few ascending whole/stripe measurements, preserving complete frame context at each size."""
import argparse,os,sys,json,subprocess,time
from pathlib import Path
import numpy as np
from PIL import Image
from natural_admission import sha,scan,npy_info,atomic_json,load_json,validate,resources,DOMAINS,output_path

def worker(a):
 # Validate the derived measurement input against its preparation seal before importing/creating an engine.
 import psutil
 m,e,info,stats,cpu=validate(a.manifest,a.manifest_sha,a.input,'A',a.local/'build-allocation-ledger/engine/spektrafilm_engine.dll',a.engine/'resources-patch',psutil.virtual_memory().available,__import__('natural_case').gpu_budget(npy_info(a.input)['shape']))
 os.environ['SPK_R7_ALLOCATIONS']='1';os.environ['SPEKTRAFILM_TRANSFER_TIMINGS']='1';sys.path[:0]=[str(a.engine/'source2/engine/tests'),str(a.engine/'source2/engine/tools')]
 from spk_ctypes import Engine
 from windows_lifecycle import OwnedResult
 from natural_case import stream_result,cleanup
 image=np.load(a.input,mmap_mode='r');e=s=r=None;report={'version':'WholeStripeAllocationMeasurementV1','shape':list(image.shape),'striped':a.striped,'rows':a.rows,'stripBudget':a.strip_budget,'inputHash':sha(a.input),'manifestSha256':a.manifest_sha,'admittedEntry':e,'cpuBudget':cpu,'boundedInputValidation':stats,'success':False,'snapshots':{}};a.output.mkdir();start=time.perf_counter()
 try:
  os.environ['SPK_R7_PHASE']='create';e=Engine(library=a.local/'build-allocation-ledger/engine/spektrafilm_engine.dll',resources=a.engine/'resources-patch');report['snapshots']['engine']=e.memory_report();os.environ['SPK_R7_PHASE']='open';s=e.open(image,{'film_stock':'kodak_portra_400','print_stock':'kodak_portra_endura','input_color_space':'ProPhoto RGB','input_cctf_decoding':False,'output_color_space':'sRGB','output_cctf_encoding':True,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'striped':a.striped,'strip_rows':a.rows,'strip_budget_bytes':a.strip_budget});report['resolved']=s.get_params();report['snapshots']['open']=e.memory_report();os.environ['SPK_R7_PHASE']='render';t=time.perf_counter();r=OwnedResult(s,'full');report['renderMs']=(time.perf_counter()-t)*1000;report['snapshots']['render_return']=e.memory_report();os.environ['SPK_R7_PHASE']='write';report.update(stream_result(r,a.output));r.close();r=None;os.environ['SPK_R7_PHASE']='session_release';s.close();s=None;report['snapshots']['released']=e.memory_report();report['success']=True
 except BaseException as ex:report['primaryError']=repr(ex)
 finally:
  os.environ['SPK_R7_PHASE']='destroy';report['cleanupErrors']=cleanup([('result',r),('session',s),('engine',e)]);image._mmap.close();report['wallMs']=(time.perf_counter()-start)*1000;atomic_json(a.output/'report.json',report)
 return report

def main(a):
 if a.worker:return worker(a)
 a.output=output_path(a.output);a.output.mkdir();full=np.load('E:/Kitao-Research/color-engine-round6-20261030/canon-inputs/A-linear-d50-full.npy',mmap_mode='r');cases=[]
 for w,h in [(321,215),(641,429),(1200,801)]:
  import psutil
  if full.nbytes+full.shape[0]*full.shape[1]*4+w*h*12+128*1048576>psutil.virtual_memory().available-1024**3:raise RuntimeError('Independent CPU preparation budget refused')
  # Maximum ascending GPU input is fixed below1MP; original76n+96MiB safety gate retained.
  from natural_case import gpu_budget
  if not gpu_budget((h,w,3))['admitted']:raise RuntimeError('Small measurement GPU budget refused')
  image=np.empty((h,w,3),np.float32)
  # Bounded row band integration from entire existing frame: never independent ROI stitching.
  for c in range(3):
   # At <=1MP explicit preparation cap; one source channel temporary is bounded by source242MB contract.
   image[:,:,c]=np.asarray(Image.fromarray(np.asarray(full[:,:,c],np.float32),mode='F').resize((w,h),Image.Resampling.BOX))
  inp=a.output/f'whole-scene-{w}x{h}.npy';np.save(inp,image)
  # Explicit derivative-preparation identity: entire source scene resized, no ROI stitching.
  frozen=load_json(a.local/'bounded-preparation-v2/admission-manifest-v2.json');sourceentry=next(x for x in frozen['files'] if x['path']=='A-linear-d50-full.npy');need=sha(Path(frozen['inputRoot'])/sourceentry['path'])==sourceentry['fileSha256']
  if not need:raise ValueError('Full preparation identity changed')
  info=npy_info(inp);stats=scan(inp,info);entry={'path':inp.name,'fileSha256':sha(inp),'shape':info['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':frozen['bridge']['version'],'pixelSha256':stats['pixelSha256'],'bytes':info['fileBytes']};seal={**frozen,'inputRoot':str(a.output.resolve()),'backend':{'sha256':sha(a.local/'build-allocation-ledger/engine/spektrafilm_engine.dll')},'files':[entry],'preparationIdentity':{**frozen['preparationIdentity'],'derivedFromInputFileSha256':sourceentry['fileSha256'],'method':'Complete frame F BOX explicit measured size'}};manifest=a.output/f'{w}-measurement-manifest.json';atomic_json(manifest,seal);manifestsha=sha(manifest)
  modes=[('whole',False,0,0),('stripe37',True,37,0)]
  if w==641:modes += [('stripe113',True,113,0),('stripe37budget1',True,37,1)]
  for label,striped,rows,budget in modes:
   folder=a.output/f'{w}-{label}';cmd=[sys.executable,str(Path(__file__).resolve()),str(a.local),str(a.engine),str(folder),'--worker','--manifest',str(manifest),'--manifest-sha',manifestsha,'--input',str(inp),'--rows',str(rows),'--strip-budget',str(budget)]+(['--striped'] if striped else []);p=subprocess.run(cmd,capture_output=True,text=True);(a.output/(folder.name+'-stderr.log')).write_text(p.stderr);r=json.loads((folder/'report.json').read_text());events=[]
   for line in p.stderr.splitlines():
    try:event=json.loads(line)
    except ValueError:continue
    if 'r7_event' in event:events.append(event)
   live={};peak={'gpuDeviceLocalBytes':0,'hostVisibleStagingBytes':0,'cpuResultBytes':0,'gpuAllocationBytes':0};phase={}
   for event in events:
    if event['r7_event']=='allocate':live[event['id']]=event
    else:live.pop(event['id'],None)
    totals={'gpuDeviceLocalBytes':sum(x['bytes'] for x in live.values() if x['role']!='cpu_result' and x['flags']&1),'hostVisibleStagingBytes':sum(x['bytes'] for x in live.values() if x['role'].endswith('staging')),'cpuResultBytes':sum(x['bytes'] for x in live.values() if x['role']=='cpu_result'),'gpuAllocationBytes':sum(x['bytes'] for x in live.values() if x['role']!='cpu_result')}
    for k,v in totals.items():peak[k]=max(peak[k],v)
    ph=phase.setdefault(event['phase'],dict.fromkeys(peak,0))
    for k,v in totals.items():ph[k]=max(ph[k],v)
   summary={'size':[w,h],'label':label,'success':r['success'],'outputHash':r.get('outputPixelSha256'),'renderMs':r.get('renderMs'),'resolved':r['resolved'],'snapshots':r['snapshots'],'allocationPeaks':peak,'phasePeaks':phase,'events':len(events),'liveAfterDestroy':list(live.values()),'sourceHash':sha(inp),'manifestSha256':manifestsha,'admittedEntry':entry,'diagnosticLibraryHash':sha(a.local/'build-allocation-ledger/engine/spektrafilm_engine.dll')};cases.append(summary);atomic_json(a.output/'summary.json',{'version':'AllocationAscendingMeasurementsV1','cases':cases})
   print(w,label,r['success'],peak,flush=True)
   if not r['success']:raise RuntimeError('Bounded measurement failed, do not increase size')
 # Row comparison includes ends/remainders and source edges in complete image, no tolerance.
 comparisons=[]
 for c in cases:
  if c['label']=='whole':continue
  w,h=c['size'];left=np.memmap(a.output/f'{w}-whole/rgba16.bin',dtype='<u2',mode='r',shape=(h,w,4));right=np.memmap(a.output/f'{w}-{c["label"]}/rgba16.bin',dtype='<u2',mode='r',shape=(h,w,4));bad=[]
  for y in range(h):
   if not np.array_equal(left[y],right[y]):bad.append(y)
  comparisons.append({'size':[w,h],'label':c['label'],'wholeVsStripeExact':not bad,'differentRows':bad,'firstAndLastIncluded':True,'lastStripRows':h%c['resolved']['strip_rows'],'defaultUnsharpKept':True})
 atomic_json(a.output/'summary.json',{'version':'AllocationAscendingMeasurementsV1','cases':cases,'comparisons':comparisons})
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('local',type=Path);p.add_argument('engine',type=Path);p.add_argument('output',type=Path);p.add_argument('--worker',action='store_true');p.add_argument('--manifest',type=Path);p.add_argument('--manifest-sha');p.add_argument('--input',type=Path);p.add_argument('--striped',action='store_true');p.add_argument('--rows',type=int,default=37);p.add_argument('--strip-budget',type=int,default=0);main(p.parse_args())
