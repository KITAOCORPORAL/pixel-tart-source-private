"""Whole-process RSS+Windows high-water tracking. Never an allocation reservation."""
import threading,time
import psutil
class HostBudget:
 def __init__(self,cap,cancel=None,period=.005):
  self.cap=int(cap);self.cancel=cancel or threading.Event();self.period=period;self.stop_event=threading.Event();self.thread=None;self.peak=0;self.os_peak=None;self.errors=[];self.samples=0;self.started=time.perf_counter();self.sample()
 def sample(self):
  try:
   m=psutil.Process().memory_info();self.peak=max(self.peak,m.rss);v=getattr(m,'peak_wset',None)
   if v is not None:self.os_peak=max(self.os_peak or 0,v)
   self.samples+=1
   if self.peak>self.cap or (self.os_peak is not None and self.os_peak>self.cap):self.cancel.set()
  except BaseException as ex:self.errors.append(repr(ex));self.cancel.set()
 def start(self):
  def loop():
   while not self.stop_event.wait(self.period):self.sample()
  self.thread=threading.Thread(target=loop,name='host-budget-monitor',daemon=False);self.thread.start();return self
 def finish(self):
  self.stop_event.set()
  if self.thread:self.thread.join()
  self.sample();return self.report()
 def report(self):return {'sampledProcessPeakRssBytes':self.peak,'osProcessPeakWorkingSetBytes':self.os_peak,'hostCapBytes':self.cap,'samples':self.samples,'monitorErrors':self.errors,'monitorJoined':self.thread is None or not self.thread.is_alive(),'wholeCallerWithinCap':not self.errors and self.os_peak is not None and max(self.peak,self.os_peak)<=self.cap,'monitorWallMs':(time.perf_counter()-self.started)*1000,'scope':'whole caller process lifetime high-water including imports/preparation/native/raw/TIFF/readback/cleanup; sampled RSS separately from OS peak'}
 def enforce(self):
  self.sample();r=self.report()
  if not r['wholeCallerWithinCap']:raise MemoryError('Whole caller sampled RSS or OS high-water exceeds cap, or monitoring unavailable: '+str(r))

def finalize_group(output,report,budget,write_report,renamer=None):
 """Provisional raw/TIFF; success report committed LAST, rollback names on every error.
 write_report writes the prepared report to a NEW hidden path, never report.json.
 Serialization/fsync is done before final budget check and success publication.
 """
 import os
 renamer=renamer or os.rename;done=[];prepared=output/'report.success-prepared.json'
 def mark_failed(ex):
  report.update(success=False,status='FAILED_COMMIT');report.setdefault('primaryError',repr(ex));report.setdefault('cleanupErrors',[])
  try:write_report(output/'FAILED.json',{'success':False,'primaryError':report.get('primaryError'),'status':'FAILED_COMMIT'})
  except BaseException as err:report['cleanupErrors'].append({'resource':'failure-marker','error':repr(err)})
  for target in reversed(done):
   try:(output/target).rename(output/(target+'.failed'))
   except BaseException as err:report['cleanupErrors'].append({'resource':target,'error':repr(err)})
  for path in [prepared,output/'report.json']:
   if path.exists():
    try:path.rename(path.with_name(path.name+'.failed'))
    except BaseException as err:report['cleanupErrors'].append({'resource':path.name,'error':repr(err)})
  report.update(budget.report());write_report(output/'report.json',report)
 try:
  budget.enforce()
  # All expensive metadata serialization/report IO is measured before publication.
  report.update(budget.report());report.update(success=True,status='COMPLETE',commitProtocol='raw rename -> TIFF rename -> prepared success report rename LAST; rollback on any error; no complete group without matching report')
  write_report(prepared,report);budget.enforce()
  for source,target in [('rgba16.partial','rgba16.bin'),('display.partial.tif','display-rgba16.tif')]:
   renamer(output/source,output/target);done.append(target)
  budget.enforce();report.update(budget.report())
  # Refresh measured report after output renames, still at a provisional name.
  write_report(prepared,report);budget.enforce()
  os.rename(prepared,output/'report.json')
  # Catch any high-water increase in final metadata commit as a failed group.
  budget.enforce()
 except BaseException as ex:
  mark_failed(ex);return False
 return True
