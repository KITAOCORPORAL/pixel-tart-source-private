"""Independent reopened full-result row audit; does NOT render again."""
from pathlib import Path
from natural_admission import output_path
import argparse,json,hashlib,time,threading
import psutil,tifffile
p=argparse.ArgumentParser();p.add_argument('result',type=Path);p.add_argument('output',type=Path);a=p.parse_args();a.output=output_path(a.output);saved=json.loads((a.result/'report.json').read_text());rawfile=a.result/('rgba16.initial-host-overbudget.bin' if (a.result/'AUDITED_STATUS.json').exists() else 'rgba16.bin');tifffilepath=a.result/('display-rgba16.initial-host-overbudget.tif' if (a.result/'AUDITED_STATUS.json').exists() else 'display-rgba16.tif');h,w=saved['height'],saved['width'];expected=saved['outputPixelSha256'];peak=[psutil.Process().memory_info().rss];stop=threading.Event()
def monitor():
 while not stop.wait(.005):peak[0]=max(peak[0],psutil.Process().memory_info().rss)
t=threading.Thread(target=monitor);t.start();start=time.perf_counter();rawhash=hashlib.sha256();count=0
with rawfile.open('rb') as f:
 for y in range(h):
  row=f.read(w*8)
  if len(row)!=w*8:raise ValueError('Short raw row')
  rawhash.update(row);count+=len(row)
 if f.read(1):raise ValueError('Raw trailer')
tiffhash=hashlib.sha256()
with tifffile.TiffFile(tifffilepath) as tf:
 page=tf.pages[0]
 if page.shape!=(h,w,4) or page.compression!=1 or page.rowsperstrip!=1:raise ValueError('Unexpected TIFF layout')
 with (tifffilepath).open('rb') as f:
  for offset,nbytes in zip(page.dataoffsets,page.databytecounts):
   if nbytes!=w*8:raise ValueError('Wrong row bytes')
   f.seek(offset);row=f.read(nbytes)
   if len(row)!=nbytes:raise ValueError('Short TIFF row')
   tiffhash.update(row)
stop.set();t.join();peak[0]=max(peak[0],psutil.Process().memory_info().rss);same=rawhash.hexdigest()==tiffhash.hexdigest()==expected
if not same:raise ValueError('Complete output pixel hash mismatch')
j={'version':'StreamFullIndependentSavedReopenV1','renderCalls':0,'newProcess':True,'width':w,'height':h,'rowsChecked':h,'rgba16PixelBytes':count,'rawPixelHash':rawhash.hexdigest(),'tiffPixelHash':tiffhash.hexdigest(),'savedExpectedHash':expected,'allExact':same,'rowBufferMaximumBytes':w*8,'sampledProcessPeakRssBytes':peak[0],'osPeakWorkingSetBytes':getattr(psutil.Process().memory_info(),'peak_wset',None),'cpuCapBytes':512*1048576,'withinCpuCap':peak[0]<=512*1048576,'wallMs':(time.perf_counter()-start)*1000,'whole20MPOracle':'NOT_RUN; small exact implementation + full saved-output consistency only'}
a.output.write_text(json.dumps(j,indent=2));print(json.dumps(j))
