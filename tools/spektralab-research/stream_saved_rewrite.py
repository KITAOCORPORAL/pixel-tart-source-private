"""Corrected full-size file I/O exercise using saved pixels only, no GPU rerender."""
from pathlib import Path
from natural_admission import output_path
import argparse,json,time,threading,hashlib,os
import numpy as np,psutil,tifffile
from PIL import ImageCms
p=argparse.ArgumentParser();p.add_argument('saved',type=Path);p.add_argument('output',type=Path);a=p.parse_args();a.output=output_path(a.output);a.output.mkdir(exist_ok=False);r=json.loads((a.saved/'report.json').read_text());rawfile=a.saved/('rgba16.initial-host-overbudget.bin' if (a.saved/'AUDITED_STATUS.json').exists() else 'rgba16.bin');h,w=r['height'],r['width'];expected=r['outputPixelSha256'];peak=[psutil.Process().memory_info().rss];stop=threading.Event();thread=threading.Thread(target=lambda:None)
def monitor():
 while not stop.wait(.005):peak[0]=max(peak[0],psutil.Process().memory_info().rss)
thread=threading.Thread(target=monitor);thread.start();t0=time.perf_counter();datahash=hashlib.sha256()
def rows():
 with rawfile.open('rb') as f:
  for y in range(h):
   b=f.read(w*8)
   if len(b)!=w*8:raise ValueError('Short source row')
   datahash.update(b);yield np.frombuffer(b,dtype='<u2').reshape(1,w,4)
start=time.perf_counter();icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
with tifffile.TiffWriter(a.output/'display.partial.tif') as tw:tw.write(data=rows(),shape=(h,w,4),dtype='<u2',photometric='rgb',extrasamples='unassalpha',rowsperstrip=1,compression=None,iccprofile=icc)
write_ms=(time.perf_counter()-start)*1000;start=time.perf_counter();back=hashlib.sha256()
with tifffile.TiffFile(a.output/'display.partial.tif') as tf:
 page=tf.pages[0]
 if page.shape!=(h,w,4) or page.compression!=1 or page.rowsperstrip!=1:raise ValueError('Unexpected TIFF layout')
 with (a.output/'display.partial.tif').open('rb') as f:
  for offset,count in zip(page.dataoffsets,page.databytecounts):
   if count!=w*8:raise ValueError('Unexpected row bytecount')
   f.seek(offset);block=f.read(count)
   if len(block)!=count:raise ValueError('Truncated TIFF')
   back.update(block)
read_ms=(time.perf_counter()-start)*1000;stop.set();thread.join();peak[0]=max(peak[0],psutil.Process().memory_info().rss);same=back.hexdigest()==datahash.hexdigest()==expected
if not same or peak[0]>512*1048576:raise ValueError('Full file hash/budget gate failed')
os.rename(a.output/'display.partial.tif',a.output/'display-rgba16.tif');report={'version':'CorrectedFullRowIOV1','renderCalls':0,'width':w,'height':h,'sourcePixelHash':datahash.hexdigest(),'tiffPixelHash':back.hexdigest(),'expectedHash':expected,'allExact':same,'maxRowBytes':w*8,'hostCapBytes':512*1048576,'sampledPeakRssBytes':peak[0],'osPeakWorkingSetBytes':getattr(psutil.Process().memory_info(),'peak_wset',None),'writeMs':write_ms,'readbackMs':read_ms,'wallMs':(time.perf_counter()-t0)*1000,'success':True,'scope':'Full-size corrected output writer+reader on already rendered pixels only. Does not rerun GPU or prove corrected combined native+IO full process under cap.'};(a.output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
