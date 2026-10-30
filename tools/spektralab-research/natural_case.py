"""Bounded natural evaluator. Never lowers full dimensions; rejects before GPU upload."""
import argparse,ctypes,gc,hashlib,json,os,subprocess,sys,threading,time
from pathlib import Path
import numpy as np,psutil
from PIL import Image,ImageCms
from run_matrix import sha,digest,require

def resource_hashes(root):return {str(p.relative_to(root)).replace('\\','/'):sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def vram():
 p=subprocess.run(['nvidia-smi','--query-gpu=memory.total,memory.used,memory.free','--format=csv,noheader,nounits'],capture_output=True,text=True);values=list(map(int,p.stdout.strip().split(',')));return {'totalBytes':values[0]*1048576,'usedBytes':values[1]*1048576,'freeBytes':values[2]*1048576}
def admit(shape):
 h,w,c=shape;n=h*w;free=vram();estimate=n*76+96*1048576;limit=min(2*1024**3,max(0,free['freeBytes']-256*1048576));host=psutil.virtual_memory();return {'dimensions':[w,h],'pixels':n,'conservativeGpuEstimateBytes':estimate,'estimateRule':'76 bytes/pixel +96MiB = persistent RGB source+negative, three working RGB planes, RGBA16 result+staged/readback allowance; not a measured peak','availableBefore':free,'gpuReserveBytes':256*1048576,'maximumResearchGpuBytes':2*1024**3,'admissionLimitBytes':limit,'availableHostBytes':host.available,'hostEstimateBytes':n*44+512*1048576,'admitted':estimate<=limit and n*44+512*1048576<=host.available-1024**3,'fullResolutionNotResized':True}
class Monitor:
 def __enter__(self):
  self.done=threading.Event();self.samples=[];self.thread=threading.Thread(target=self.run,daemon=True);self.thread.start();return self
 def run(self):
  while not self.done.is_set():
   self.samples.append(psutil.Process().memory_info().rss);self.done.wait(.01)
 def __exit__(self,*args):self.done.set();self.thread.join()
 @property
 def peak(self):return max(self.samples,default=psutil.Process().memory_info().rss)
def timed(f):
 start=time.perf_counter();value=f();return value,(time.perf_counter()-start)*1000

def evaluate(source,library,resources,inputpath,output,route,film,paper,repeat=False,role='proxy',expected=None):
 import tifffile
 sys.path[:0]=[str(source/'engine/tests'),str(source/'engine/tools')]
 from spk_ctypes import Engine,SpkResult
 from windows_lifecycle import OwnedResult
 require(subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()=='c1447c6ab618fa4b1cb11fd2fe12826855007059','Wrong fixed external source');require(not subprocess.check_output(['git','-C',str(source),'status','--porcelain'],text=True).strip(),'Use clean original source for ABI bindings');require(not output.exists(),'Output exists');output.mkdir(parents=True);image=np.load(inputpath,mmap_mode='r');require(image.dtype==np.dtype('<f4') and bool(np.isfinite(image).all()),'Nonfinite/wrong input');budget=admit(image.shape);report={'version':'NaturalCaseV1','role':role,'route':route,'film':film,'paper':paper,'inputFile':str(inputpath),'inputFileSha256':sha(inputpath),'inputPixelSha256':digest(image),'inputShape':list(image.shape),'budget':budget,'status':'FAILED','librarySha256':sha(library),'resourceHashes':resource_hashes(resources)}
 if not budget['admitted']:
  report['status']='REFUSED_RESOURCE_BUDGET';(output/'report.json').write_text(json.dumps(report,indent=2));return report
 params={'film_stock':film,'print_stock':paper,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'exposure_compensation_ev':0,'input_color_space':'ProPhoto RGB' if route=='A' else 'sRGB','input_cctf_decoding':route=='B','output_color_space':'sRGB','output_cctf_encoding':True,'striped':False}
 if film=='kodak_tri_x_400':params.update(enlarger_illuminant='TH-KG3',c_filter_neutral=0,m_filter_neutral=0,y_filter_neutral=68,print_exposure=1)
 if expected:
  require(sha(library)==expected['librarySha256'] and report['resourceHashes']==expected['resourceHashes'] and report['inputPixelSha256']==expected['inputPixelSha256'],'Saved identity mismatch');params=expected['resolved']
 icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();engine=None;session=None;held=None
 with Monitor() as monitor:
  try:
   engine,report['initializationMs']=timed(lambda:Engine(library=library,resources=resources));session,report['openAndUploadMs']=timed(lambda:engine.open(image,params));resolved=session.get_params();report['resolved']=resolved
   for k,v in params.items():require(resolved[k]==v,'Parameter differs '+k)
   if expected:require(resolved==expected['resolved'],'Reopen resolved changed')
   report['memoryBeforeRender']=engine.memory_report();held,report['renderAndReadbackMs']=timed(lambda:OwnedResult(session,'full'));report['width']=int(held.result.width);report['height']=int(held.result.height);report['rowStridePixels']=int(held.result.row_stride_px);require([report['height'],report['width'],3]==list(image.shape),'Engine silently changed full size');result,report['cpuCopyMs']=timed(lambda:held.pixels().copy());held.close();held=None;report['memoryAfterRender']=engine.memory_report();report['outputPixelSha256']=digest(result)
   if repeat:
    retained,report['repeatRenderAndReadbackMs']=timed(lambda:OwnedResult(session,'full'));again=retained.pixels().copy();retained.close();report['repeatExact']=digest(again)==report['outputPixelSha256'];require(report['repeatExact'],'Repeated pixels differ');del again
   if expected:require(report['outputPixelSha256']==expected['outputPixelSha256'],'New process render differs')
   start=time.perf_counter();result.astype('<u2').tofile(output/'rgba16.bin');report['rawWriteMs']=(time.perf_counter()-start)*1000;start=time.perf_counter();tifffile.imwrite(output/'display-rgba16.tif',result,photometric='rgb',extrasamples='unassalpha',iccprofile=icc);report['tiff16WriteMs']=(time.perf_counter()-start)*1000;start=time.perf_counter();read=tifffile.imread(output/'display-rgba16.tif');require(np.array_equal(read,result),'TIFF16 differs');report['tiff16ReadbackMs']=(time.perf_counter()-start)*1000;report['tiff16Exact']=True
   rgb=result[:,:,:3];report['statistics']={'minRGB':rgb.min(axis=(0,1)).tolist(),'maxRGB':rgb.max(axis=(0,1)).tolist(),'zeroFractionRGB':(rgb==0).mean(axis=(0,1)).tolist(),'maximumFractionRGB':(rgb==65535).mean(axis=(0,1)).tolist(),'percentilesRGB':np.percentile(rgb,[.1,1,50,99,99.9],axis=(0,1)).tolist(),'alphaMinMax':[int(result[:,:,3].min()),int(result[:,:,3].max())]}
   Image.fromarray(np.rint(rgb/257).astype(np.uint8)).save(output/'observation.png',icc_profile=icc);report['status']='RENDERED_PROXY' if role=='proxy' else 'RENDERED_NATIVE_WINDOW' if role.startswith('crop') else 'RENDERED_FULL_RESOLUTION';report['outputFiles']={p.name:{'sha256':sha(p),'bytes':p.stat().st_size} for p in output.iterdir() if p.is_file()}
  except Exception as e:report['error']=repr(e)
  finally:
   if held:held.close()
   if session:session.close()
   if engine:report['memoryAfterSessionRelease']=engine.memory_report();engine.close()
 report['sampledProcessPeakRssBytes']=monitor.peak;report['osProcessPeakWorkingSetBytes']=getattr(psutil.Process().memory_info(),'peak_wset',None);report['memoryScope']='process RSS sampling10ms+OS peak; engine ledger allocation bytes not VRAM residency; no exclusive-GPU peak claim';report['timingScope']='spk_open includes upload; spk_render includes GPU execution and CPU readback; ABI cannot split internal upload/compute/readback';(output/'report.json').write_text(json.dumps(report,indent=2));return report

def main():
 p=argparse.ArgumentParser();p.add_argument('mode',choices=['case','reopen']);p.add_argument('source',type=Path);p.add_argument('library',type=Path);p.add_argument('resources',type=Path);p.add_argument('input',type=Path);p.add_argument('output',type=Path);p.add_argument('--route',choices=['A','B'],default='A');p.add_argument('--film',default='kodak_portra_400');p.add_argument('--paper',default='kodak_portra_endura');p.add_argument('--role',default='proxy');p.add_argument('--repeat',action='store_true');a=p.parse_args();saved=json.loads(a.input.read_text()) if a.mode=='reopen' else None;inputpath=Path(saved['inputFile']) if saved else a.input;report=evaluate(a.source,a.library,a.resources,inputpath,a.output,saved['route'] if saved else a.route,saved['film'] if saved else a.film,saved['paper'] if saved else a.paper,a.repeat,saved['role'] if saved else a.role,saved);print(json.dumps({'status':report['status'],'peakRss':report.get('sampledProcessPeakRssBytes'),'renderMs':report.get('renderAndReadbackMs'),'input':[report['inputShape'][1],report['inputShape'][0]],'outputHash':report.get('outputPixelSha256')}));sys.exit(0 if report['status'].startswith('RENDERED') else 3)
if __name__=='__main__':main()
