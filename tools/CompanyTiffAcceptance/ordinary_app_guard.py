from pathlib import Path
import argparse,ctypes as C,ctypes.wintypes as W,hashlib,json,os,subprocess,time,struct
p=argparse.ArgumentParser();p.add_argument('--app',required=True,type=Path);p.add_argument('--run',required=True,type=Path);p.add_argument('--runtime',required=True,type=Path);p.add_argument('--argument',action='append',default=[]);p.add_argument('--reopen-session',type=Path);p.add_argument('--scope',choices=['native-app','controlled-replay','readonly-probe'],default='native-app');p.add_argument('--phase-file',type=Path);a=p.parse_args()
a.run.mkdir(parents=True,exist_ok=False);a.runtime.mkdir(parents=True,exist_ok=True)
def save(name,value): (a.run/name).write_text(json.dumps(value,indent=2),encoding='utf-8')
env=os.environ.copy()
for k in list(env):
 if k.startswith('PIXEL_TART_') or k.startswith('KITAO_'):env.pop(k,None)
if a.reopen_session:
 assert a.reopen_session.is_file() and a.reopen_session.name.endswith('.ptstudio.json')
 env['KITAO_TIFF_REOPEN_SESSION']=str(a.reopen_session.resolve())
env['PIXEL_TART_ISOLATED_RUNTIME']='1';env['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=str(a.runtime.resolve())
out=(a.run/'stdout.log').open('w');err=(a.run/'stderr.log').open('w')
process=subprocess.Popen([str(a.app.resolve()),*a.argument],cwd=a.app.parent.resolve(),env=env,stdout=out,stderr=err)
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.K32GetProcessMemoryInfo.restype=W.BOOL;k.CloseHandle.argtypes=[W.HANDLE]
handle=k.OpenProcess(0x410,False,process.pid);assert handle
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
save('LAUNCH.json',dict(pid=process.pid,app=str(a.app.resolve()),exeSha256=sha(a.app),productAssemblySha256=sha(a.app.parent/'KitaoPhotoSelector.dll') if (a.app.parent/'KitaoPhotoSelector.dll').exists() else None,runtime=str(a.runtime.resolve()),guardBytes=2**31,intervalMs=50,newRawDecodeLimit=0,startedUtc=time.time(),authority=a.scope))
start=time.monotonic();count=0;peak_private=peak_working=0;reason=None;phases={}
with (a.run/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8') as journal:
 while process.poll() is None:
  b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
  if k.K32GetProcessMemoryInfo(handle,b,80):
   os_peak,working=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0]
   try:phase=(a.phase_file or a.run/'phase.txt').read_text('utf-8').strip()
   except OSError:phase='startup'
   sample=dict(elapsedMs=(time.monotonic()-start)*1000,privateBytes=private,workingSetBytes=working,osPeakWorkingSetBytes=os_peak,phase=phase)
   journal.write(json.dumps(sample)+'\n');journal.flush();count+=1;peak_private=max(private,peak_private);peak_working=max(os_peak,peak_working)
   q=phases.setdefault(phase,dict(samples=0,peakPrivateBytes=0,peakWorkingSetBytes=0));q['samples']+=1;q['peakPrivateBytes']=max(q['peakPrivateBytes'],private);q['peakWorkingSetBytes']=max(q['peakWorkingSetBytes'],working)
   if private>2**31 or os_peak>2**31:reason='2GiB resource guard'
  if (a.run/'FINISH_MONITOR').exists():break
  if reason:
   save('STOP.json',dict(reason=reason,sample=sample));subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);break
  time.sleep(.05)
k.CloseHandle(handle)
if reason is not None:
 try:process.wait(timeout=15)
 except subprocess.TimeoutExpired:process.kill();process.wait()
elif process.poll() is not None:process.wait()
save('EXIT.json',dict(pid=process.pid,exitCode=process.poll(),monitorEndedWhileAppAlive=process.poll() is None,stop=reason,peakPrivateBytes50ms=peak_private,osPeakWorkingSetBytes=peak_working,samples=count,phases=phases,forcedGcUsed=False,newRawDecodeLimit=0))
print(json.dumps(dict(pid=process.pid,exitCode=process.poll(),stop=reason)))

raise SystemExit(0 if process.poll()==0 and reason is None else 1)
