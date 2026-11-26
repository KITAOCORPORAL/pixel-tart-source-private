"""One diagnostic capture before a second RGB48 allocation. Never a resource PASS."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,json,subprocess,sys,time,hashlib,shutil
from company_preflight import observation_task
p=argparse.ArgumentParser();p.add_argument('--stage',required=True,type=Path);p.add_argument('--out',required=True,type=Path);p.add_argument('--dump-tool',required=True,type=Path);source=p.add_mutually_exclusive_group(required=True);source.add_argument('--task',type=Path);source.add_argument('--baseline-launch',type=Path);a=p.parse_args();stage=a.stage.resolve();out=a.out.resolve();tool=a.dump_tool.resolve();assert not out.exists() and tool.is_file()
def load(path):return json.loads(path.read_text('utf-8-sig'))
def sha(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
binding = None
if a.baseline_launch:
 # Recheck evidence and staged DLLs after publish, before launching the observer guard.
 # A changed input cannot be substituted while the small host was building.
 task, binding = observation_task(a.baseline_launch.resolve(), stage, out.parent)
 scope = 'company diagnostic; current inputs bound for this observation only'
else:
 task=load(a.task);scope='authorized household fixture; not company original or company binary'
 payload=Path(task['payload']);assert payload.is_file() and payload.stat().st_size==task['width']*task['height']*6;assert sha(payload).lower()==task['pixelSha256'].lower();assert Path(task['icc']).is_file()
assert shutil.disk_usage(out.parent).free>3*1024**3,'Keep old evidence; insufficient room for one private dump'
taskfile=out.parent/(out.name+'-task.json');assert not taskfile.exists();taskfile.write_text(json.dumps(task,indent=2),encoding='utf-8')
log=out.parent/(out.name+'-controller.log');assert not log.exists();handle=log.open('x',encoding='utf-8');guard=Path(__file__).with_name('guard.py');process=subprocess.Popen([sys.executable,str(guard),str(stage),str(taskfile),str(out),'processed-diagnostic','points'],stdout=handle,stderr=subprocess.STDOUT)
ready=out/'DIAGNOSTIC_READY.json';until=time.monotonic()+240
while not ready.exists() and process.poll() is None and time.monotonic()<until:time.sleep(.1)
if not ready.exists():raise RuntimeError('One capture did not reach its boundary; preserve run, do not repeat a known gate')
info=load(ready);mapping=load(out/'WEAK_HANDLE_MAP.json');assert len(mapping['subjects'])==7 and mapping['handleKind'].startswith('GCHandleType.Weak')
launch=load(out/'LAUNCH.json');assert launch['pid']==info['pid'] and launch['guardBytes']==2**31 and launch['appSha256']==sha(stage/'KitaoPhotoSelector.dll')
dump=out/'before-second-ushort.dmp';assert not dump.exists();collectorLog=out/'collector.log'
start=datetime.now(timezone.utc).isoformat();code=None;timedOut=False
with collectorLog.open('x',encoding='utf-8') as output:
 collector=subprocess.Popen([str(tool),'collect','--process-id',str(info['pid']),'--type','Heap','--output',str(dump)],stdout=output,stderr=subprocess.STDOUT)
 try:code=collector.wait(timeout=120)
 except subprocess.TimeoutExpired:timedOut=True;collector.terminate();code=collector.wait(timeout=10)
# Release the observation boundary immediately; do not wait for memory to fall or analyze while paused.
(out/'DIAGNOSTIC_CONTINUE').write_text('Capture attempt finished; observation only.\n',encoding='utf-8')
try:process.wait(timeout=45)
except subprocess.TimeoutExpired:raise RuntimeError('Diagnostic host did not finish after capture; keep guard/run evidence')
handle.close();exit=load(out/'EXIT.json');after=load(out/'HOLDER_CAPTURE_COMPLETE.json') if (out/'HOLDER_CAPTURE_COMPLETE.json').exists() else None
summary={'classification':'DIAGNOSTIC_ONLY_NO_RESOURCE_ADMISSION','scope':scope,'startedAtUtc':start,'collectorExitCode':code,'collectorTimedOut':timedOut,'dumpExists':dump.exists(),'dumpBytes':dump.stat().st_size if dump.exists() else 0,'dumpSha256':sha(dump) if dump.exists() and code==0 else None,'weakMapSha256':sha(out/'WEAK_HANDLE_MAP.json'),'gcCountsBefore':mapping['gcCounts'],'gcCountsAfter':after['gcCountsAfter'] if after else None,'gcCountsEqual':bool(after and mapping['gcCounts']==after['gcCountsAfter']),'targetExit':exit,'diagnosticExpectedEarlyEnd':(out/'OBSERVATION_RESULT.json').exists(),'secondAllocationSkippedForObservationOnly':bool(after and after['secondUshortAllocated']==False),'baselineLaunch':str(a.baseline_launch) if a.baseline_launch else None,'stableProductUnchanged':True,'forcedGc':False,'oldReleaseAndSettlingUnchanged':True}
if binding is not None: summary['inputPreflight'] = binding
(out/'CAPTURE_SUMMARY.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(json.dumps({k:summary[k] for k in ['classification','scope','collectorExitCode','dumpBytes','gcCountsEqual','diagnosticExpectedEarlyEnd']}))
if code!=0 or not dump.exists():raise SystemExit(2)
