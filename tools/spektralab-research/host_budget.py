import json,hashlib,os,time
from pathlib import Path
import psutil
class HostBudget:
 def __init__(self,cap,cancel=None,period=.005):
  import threading
  self.cap=int(cap);self.cancel=cancel or threading.Event();self.period=period;self.stop_event=threading.Event();self.thread=None;self.peak=0;self.os_peak=None;self.errors=[];self.samples=0;self.checks=[];self.started=time.perf_counter();self.sample()
 def sample(self):
  try:
   m=psutil.Process().memory_info();self.peak=max(self.peak,m.rss);v=getattr(m,'peak_wset',None)
   if v is not None:self.os_peak=max(self.os_peak or 0,v)
   self.samples+=1
   if self.peak>self.cap or (self.os_peak is not None and self.os_peak>self.cap):self.cancel.set()
  except BaseException as ex:self.errors.append(repr(ex));self.cancel.set()
 def start(self):
  import threading
  def loop():
   while not self.stop_event.wait(self.period):self.sample()
  self.thread=threading.Thread(target=loop,name='host-budget-monitor',daemon=False);self.thread.start();return self
 def finish(self):
  from native_wait import join_native
  self.stop_event.set();join_native(self.thread,self.cancel);self.sample();return self.report()
 def report(self):return {'sampledProcessPeakRssBytes':self.peak,'osProcessPeakWorkingSetBytes':self.os_peak,'hostCapBytes':self.cap,'samples':self.samples,'monitorErrors':list(self.errors),'monitorJoined':self.thread is None or not self.thread.is_alive(),'wholeCallerWithinCap':not self.errors and self.os_peak is not None and max(self.peak,self.os_peak)<=self.cap,'monitorWallMs':(time.perf_counter()-self.started)*1000,'scope':'process start OS high-water through SUCCESS_GROUP_COMMIT; excludes later console printing/process teardown','checks':list(self.checks)}
 def enforce(self,label=None):
  self.sample();r=self.report()
  if label:self.checks.append({'point':label,'sampledRss':self.peak,'osPeak':self.os_peak,'withinCap':r['wholeCallerWithinCap'],'atMs':(time.perf_counter()-self.started)*1000})
  if not r['wholeCallerWithinCap']:raise MemoryError('Whole caller sampled RSS or OS high-water exceeds cap or monitor unavailable: '+str(r))
def finalize_group(output,report,budget,write_report,renamer=None):
 """Fixed endpoint: successful data/report publication plus ONE OS high-water check.
 acceptance.json is a fixed-size preallocated receipt, not another moving endpoint.
 Readers require report + receipt, without FAILED/AUDITED_STATUS. Crash=>no valid receipt.
 """
 renamer=renamer or os.rename;done=[];prepared=output/'report.success-prepared.json';receipt=output/'acceptance.json';receipt_size=4096
 try:
  with receipt.open('xb') as f:f.write(b' '*receipt_size);f.flush();os.fsync(f.fileno())
  if budget.cancel.is_set():raise InterruptedError('Cancelled before commit')
  budget.enforce('cleanup_joined_before_report')
  report.update(budget.report());report.update(success=True,status='COMPLETE',acceptanceReceipt='acceptance.json',budgetBoundary='SUCCESS_GROUP_COMMIT',commitProtocol='provisional report serialize/fsync -> budget -> raw rename -> TIFF rename -> report rename LAST -> fixed OS peak check -> preallocated acceptance receipt')
  write_report(prepared,report);budget.enforce('report_serialized_before_data_commit')
  for source,target in [('rgba16.partial','rgba16.bin'),('display.partial.tif','display-rgba16.tif')]:renamer(output/source,output/target);done.append(target)
  budget.enforce('data_renamed_report_provisional');report.update(budget.report());write_report(prepared,report)
  report_hash=hashlib.sha256(prepared.read_bytes()).hexdigest();budget.enforce('final_budget_before_success_report_publish')
  if budget.cancel.is_set():raise InterruptedError('Cancelled during commit')
  os.rename(prepared,output/'report.json')
  budget.enforce('SUCCESS_GROUP_COMMIT')
  state=budget.report();payload={'version':'StreamCommitAcceptanceV1','success':True,'budgetBoundary':'SUCCESS_GROUP_COMMIT','reportSha256':report_hash,'outputPixelSha256':report['outputPixelSha256'],'sampledProcessPeakRssBytes':state['sampledProcessPeakRssBytes'],'osProcessPeakWorkingSetBytes':state['osProcessPeakWorkingSetBytes'],'capBytes':budget.cap,'monitorJoined':state['monitorJoined'],'checks':state['checks'],'boundaryDefinition':'OS peak taken once immediately after raw/TIFF/report group publication; receipt records this fixed event. Later printing and process teardown are not part of rendering acceptance.'};data=json.dumps(payload,separators=(',',':')).encode();
  if len(data)>receipt_size:raise ValueError('Acceptance receipt exceeds fixed allocation')
  with receipt.open('r+b') as f:f.write(data+b' '*(receipt_size-len(data)));f.flush();os.fsync(f.fileno())
  report['commitAcceptance']=payload
  return True
 except BaseException as ex:
  report.update(success=False,status='FAILED_COMMIT');report.setdefault('primaryError',repr(ex));report.setdefault('cleanupErrors',[])
  try:write_report(output/'FAILED.json',{'success':False,'primaryError':report['primaryError']})
  except BaseException as err:report['cleanupErrors'].append({'resource':'failure-marker','error':repr(err)})
  for target in [*reversed(done),'report.json',prepared.name,receipt.name]:
   path=output/target
   if path.exists():
    try:path.rename(path.with_name(path.name+'.failed'))
    except BaseException as err:report['cleanupErrors'].append({'resource':target,'error':repr(err)})
  report.update(budget.report());write_report(output/'report.json',report);return False

