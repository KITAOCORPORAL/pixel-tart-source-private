"""Run one no-RAW memory replay under a 2 GiB process guard. Never edits old ledgers."""
from pathlib import Path
import argparse,ctypes as C,ctypes.wintypes as W,hashlib,json,os,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('app',type=Path);parser.add_argument('cache',type=Path);parser.add_argument('output',type=Path);parser.add_argument('mode');parser.add_argument('--trace',action='store_true');args=parser.parse_args()
root=args.cache.resolve().parent;task=json.loads((root/'MEMORY_TASK.json').read_text('utf-8'))
assert task['maximumNewRawDecodes']==0 and task['guardBytes']==2**31
with args.cache.open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest().upper()==task['pixelSha256']
assert not args.output.exists();guard_root=args.output.with_name(args.output.name+'-guard');guard_root.mkdir(exist_ok=False)
env=os.environ.copy()
for name in ['PIXEL_TART_XTRANS_STABLE_V1','PIXEL_TART_XTRANS_PLAN','PIXEL_TART_XTRANS_STEP','PIXEL_TART_HUMAN_ACCEPTANCE']:env.pop(name,None)
stdout=(guard_root/'stdout.log').open('w');stderr=(guard_root/'stderr.log').open('w')
process=subprocess.Popen([str(args.app.resolve()),str(args.cache.resolve()),str(args.output.resolve()),args.mode],cwd=args.app.resolve().parent,env=env,stdout=stdout,stderr=stderr,creationflags=0x08000000)
trace_process=None;trace_out=None
if args.trace:
 trace_out=(guard_root/'trace.log').open('w')
 trace_process=subprocess.Popen([str(root/'profilers/dotnet-trace.exe'),'collect','--process-id',str(process.pid),'--profile','gc-verbose','--buffersize','32','--output',str(guard_root/'allocations.nettrace'),'--duration','00:00:02:00'],env=env,stdout=trace_out,stderr=subprocess.STDOUT,creationflags=0x08000000)
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.K32GetProcessMemoryInfo.restype=W.BOOL;k.CloseHandle.argtypes=[W.HANDLE]
handle=k.OpenProcess(0x410,False,process.pid);assert handle
start=time.monotonic();count=0;peak_private=peak_working=0;reason=None
(guard_root/'LAUNCH.json').write_bytes((json.dumps(dict(pid=process.pid,mode=args.mode,task=task['task'],rawDecodeLimit=0,guardBytes=2**31,exeSha256=hashlib.sha256(args.app.read_bytes()).hexdigest(),productAssemblySha256=hashlib.sha256((args.app.parent/'KitaoPhotoSelector.dll').read_bytes()).hexdigest(),startedUtc=time.time()),indent=2)+'\n').encode())
with (guard_root/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8',newline='\n') as journal:
 while process.poll() is None:
  buffer=C.create_string_buffer(80);C.c_uint32.from_buffer(buffer).value=80
  if k.K32GetProcessMemoryInfo(handle,buffer,80):
   import struct
   os_peak,working=struct.unpack_from('<QQ',buffer.raw,8);private=struct.unpack_from('<Q',buffer.raw,72)[0]
   sample=dict(elapsedMs=(time.monotonic()-start)*1000,workingSetBytes=working,osPeakWorkingSetBytes=os_peak,privateBytes=private)
   journal.write(json.dumps(sample)+'\n');journal.flush();count+=1;peak_private=max(private,peak_private);peak_working=max(os_peak,peak_working)
   if os_peak>2**31 or private>2**31:reason='2GiB resource guard'
  if time.monotonic()-start>(360 if 'soak' in args.mode else 180):reason='Replay watchdog'
  if reason:
   (guard_root/'STOP.json').write_bytes((json.dumps(dict(reason=reason,sample=sample),indent=2)+'\n').encode())
   subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);break
  time.sleep(.05)
k.CloseHandle(handle);process.wait();stdout.close();stderr.close()
if trace_process:
 try:trace_process.wait(timeout=20)
 except subprocess.TimeoutExpired:trace_process.terminate();trace_process.wait()
 trace_out.close()
result=dict(pid=process.pid,exitCode=process.returncode,stop=reason,peakPrivateBytes50ms=peak_private,osPeakWorkingSetBytes=peak_working,samples=count,elapsedMs=(time.monotonic()-start)*1000,newRawDecodeLimit=0)
(guard_root/'EXIT.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(json.dumps(result));raise SystemExit(0 if process.returncode==0 and reason is None else 1)
