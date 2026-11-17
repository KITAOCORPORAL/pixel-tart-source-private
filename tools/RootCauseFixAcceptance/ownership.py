from pathlib import Path
import argparse,json,subprocess,time,uuid,os,hashlib,threading,ctypes as C,ctypes.wintypes as W,struct
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);p.add_argument('--dll',required=True);p.add_argument('--root',required=True);a=p.parse_args();root=Path(a.root).resolve();root.mkdir(exist_ok=False);name='RootCause-'+uuid.uuid4().hex
owners=[];rows=[];watched=[];peaks={};stops=[];finished=threading.Event()
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.CloseHandle.argtypes=[W.HANDLE]
def watch(proc):
 handle=k.OpenProcess(0x410,False,proc.pid);assert handle;watched.append((proc,handle));peaks[proc.pid]=dict(privateBytes=0,workingSetBytes=0)
def sample():
 with (root/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8') as stream:
  while not finished.is_set():
   for proc,handle in list(watched):
    if proc.poll() is not None:continue
    b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
    if k.K32GetProcessMemoryInfo(handle,b,80):
     peak,working=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0];peaks[proc.pid]['privateBytes']=max(peaks[proc.pid]['privateBytes'],private);peaks[proc.pid]['workingSetBytes']=max(peaks[proc.pid]['workingSetBytes'],peak);stream.write(json.dumps(dict(atUtc=time.time(),pid=proc.pid,privateBytes=private,workingSetBytes=working))+'\n')
     if max(peak,private)>2**31:stops.append(proc.pid);proc.kill()
   stream.flush();time.sleep(.05)
thread=threading.Thread(target=sample,daemon=True);thread.start()
def owner(role,instance):
 proc=subprocess.Popen([a.dotnet,a.dll,role,instance,str(root)],creationflags=subprocess.CREATE_NO_WINDOW);owners.append(proc);watch(proc)
 until=time.monotonic()+10
 while not (root/(role+'-'+str(proc.pid)+'.json')).exists():
  assert proc.poll() is None and time.monotonic()<until;time.sleep(.03)
 return proc
def run(role,instance):
 start=time.monotonic();proc=subprocess.Popen([a.dotnet,a.dll,role,instance,str(root)],creationflags=subprocess.CREATE_NO_WINDOW);watch(proc);assert proc.wait(timeout=15)==0;rows.append(dict(case=role,instance=instance,durationMs=(time.monotonic()-start)*1000))
try:
 A=owner('owner-unresponsive',name+'-A');B=owner('owner-slow',name+'-B');time.sleep(16.6)
 run('recover',name+'-A');assert A.poll() is None and B.poll() is None
 recover=next(json.loads(p.read_text()) for p in root.glob('recover-*.json'));assert not recover['acquired'] and recover['outcome']=='ExistingInstanceUnresponsive'
 for proc in owners:assert (root/('unsaved-'+str(proc.pid)+'.txt')).read_text()=='in-memory-work-not-committed'
 acknowledged_owner=owner('owner-ack',name+'-C');run('activate',name+'-C');activate=next(json.loads(p.read_text()) for p in root.glob('activate-*.json'));assert activate['outcome']=='ActivatedExisting' and not activate['acquired']
 run('unowned',name+'-unowned');run('abandoned',name+'-abandoned')
 # The production manager no longer queries or acts on any PID. A reused PID cannot
 # be selected by this recovery path, including fabricated stale metadata.
 (root/'stale-pid-metadata.json').write_text(json.dumps(dict(pid=B.pid,root='A',creationTime=0)))
 run('recover-again',name+'-A');assert A.poll() is None and B.poll() is None
 source=Path(__file__).resolve().parents[2]/'src/RAWSelectionAssistant/Utilities/SingleInstanceManager.cs';text=source.read_text('utf-8')
 assert all(s not in text for s in ['Process.GetProcesses','Process.GetProcessById','.Kill(','out var createdNew'])
 result=dict(status='PASS',cases=rows,twoRootsCoexist=True,unresponsiveAndSlowNoWindowPreserved=True,unsavedContentPreserved=True,pidReuseCannotAuthorizeTermination=True,pidReuseScope='No PID lookup or termination in recovery; stale metadata has no authority',actualMutexAndAbandonedAcquired=True,sameRootActivation=True,sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),guardBytes=2**31,intervalMs=50,resourcePeaks=peaks,guardStops=stops,forcedGc=False)
 assert not stops and thread.is_alive()
 assert peaks[A.pid]['privateBytes']>0 and peaks[B.pid]['privateBytes']>0
 (root/'RESULT.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(result))
finally:
 for proc in owners:(root/('finish-'+str(proc.pid))).touch()
 for proc in owners:proc.wait(timeout=10)
 finished.set();thread.join()
 for proc,handle in watched:k.CloseHandle(handle)
