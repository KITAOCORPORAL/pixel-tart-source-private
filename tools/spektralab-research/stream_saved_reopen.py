"""Read and hash saved pixels in a new process. This is NOT a rerender."""
from pathlib import Path
import argparse,hashlib,time,json
from natural_admission import output_path,load_json,atomic_json,need
from host_budget import HostBudget
import tifffile

def audit(result,output):
 output=output_path(output);need(not (result/'FAILED.json').exists() and not (result/'AUDITED_STATUS.json').exists(),'Saved group has explicit failure or incomplete audit marker');saved=load_json(result/'report.json');need(saved.get('success') is True and saved.get('status')=='COMPLETE','Saved group not COMPLETE');h,w=saved['height'],saved['width'];
 if saved.get('acceptanceReceipt'):
  receipt=load_json(result/saved['acceptanceReceipt']);need(receipt.get('success') is True and receipt['reportSha256']==__import__('natural_admission').sha(result/'report.json') and receipt['outputPixelSha256']==saved['outputPixelSha256'] and max(receipt['sampledProcessPeakRssBytes'],receipt['osProcessPeakWorkingSetBytes'])<=512*1048576,'Commit receipt mismatch')
 budget=HostBudget(512*1048576).start();start=time.perf_counter();report={'version':'StreamSavedReopenHomeV2','success':False,'renderCalls':0,'newProcess':True,'operation':'saved raw/TIFF byte and pixel audit only; no native rerender','width':w,'height':h,'primaryError':None,'cleanupErrors':[]}
 try:
  rawhash=hashlib.sha256();tiffhash=hashlib.sha256();count=0
  with (result/'rgba16.bin').open('rb') as f:
   for y in range(h):
    budget.enforce();row=f.read(w*8);need(len(row)==w*8,'Short raw row');rawhash.update(row);count+=len(row)
   need(not f.read(1),'Raw trailer')
  with tifffile.TiffFile(result/'display-rgba16.tif') as tf:
   need(len(tf.pages)==1,'Unexpected TIFF page count');page=tf.pages[0];need(page.shape==(h,w,4) and page.compression==1 and page.rowsperstrip==1 and len(page.dataoffsets)==len(page.databytecounts)==h,'Unexpected TIFF layout')
   with (result/'display-rgba16.tif').open('rb') as f:
    for offset,size in zip(page.dataoffsets,page.databytecounts):
     budget.enforce();need(size==w*8,'Wrong row bytecount');f.seek(offset);row=f.read(size);need(len(row)==size,'Short TIFF row');tiffhash.update(row)
  need(rawhash.hexdigest()==tiffhash.hexdigest()==saved['outputPixelSha256'],'Complete pixel hash mismatch');report.update(rawPixelHash=rawhash.hexdigest(),tiffPixelHash=tiffhash.hexdigest(),savedExpectedHash=saved['outputPixelSha256'],rowsChecked=h,rgba16PixelBytes=count,allExact=True)
 except BaseException as ex:report['primaryError']=repr(ex)
 finally:
  try:report.update(budget.finish())
  except BaseException as ex:report['cleanupErrors'].append(repr(ex))
 report['wallMs']=(time.perf_counter()-start)*1000;budget.sample();report.update(budget.report());report['success']=not report['primaryError'] and not report['cleanupErrors'] and report.get('wholeCallerWithinCap',False);atomic_json(output,report);return report
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('result',type=Path);p.add_argument('output',type=Path);a=p.parse_args();r=audit(a.result,a.output);print(json.dumps(r));raise SystemExit(0 if r['success'] else 3)
