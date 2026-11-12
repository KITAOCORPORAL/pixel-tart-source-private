"""Launch the integrated diagnostic app in a fresh runtime under the 2GiB guard.
Only already-rendered company TIFF input is used in the UI checks; never RAW.
"""
from pathlib import Path
import argparse,ctypes as C,ctypes.wintypes as W,hashlib,json,os,subprocess,time
p=argparse.ArgumentParser();p.add_argument('app',type=Path);p.add_argument('output',type=Path);p.add_argument('source_sha');a=p.parse_args();a.output.mkdir(exist_ok=False)
env=os.environ.copy();env['PIXEL_TART_ISOLATED_RUNTIME']='1';env['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=str((a.output/'runtime').resolve())
for name in ['PIXEL_TART_XTRANS_STABLE_V1','PIXEL_TART_XTRANS_PLAN','PIXEL_TART_XTRANS_STEP','PIXEL_TART_HUMAN_ACCEPTANCE','PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY','PIXEL_TART_ACCEPTANCE_ROOT']:env.pop(name,None)
out=(a.output/'stdout.log').open('w');err=(a.output/'stderr.log').open('w');process=subprocess.Popen([str(a.app.resolve())],cwd=a.app.resolve().parent,env=env,stdout=out,stderr=err,creationflags=0x08000000)
(a.output/'LAUNCH.json').write_bytes((json.dumps(dict(pid=process.pid,sourceSha=a.source_sha,app=str(a.app.resolve()),runtime=str((a.output/'runtime').resolve()),mainDllSha256=hashlib.sha256((a.app.parent/'KitaoPhotoSelector.dll').read_bytes()).hexdigest(),nativeRawInputsAuthorized=0,startedUtc=time.time()),indent=2)+'\n').encode())
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.K32GetProcessMemoryInfo.restype=W.BOOL;k.CloseHandle.argtypes=[W.HANDLE];handle=k.OpenProcess(0x410,False,process.pid);assert handle
start=time.monotonic();peak_private=peak_working=0;count=0;reason=None
with (a.output/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8',newline='\n') as f:
 while process.poll() is None:
  b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
  if k.K32GetProcessMemoryInfo(handle,b,80):
   import struct
   ospeak,working=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0];sample=dict(atUtc=time.time(),elapsedMs=(time.monotonic()-start)*1000,workingSetBytes=working,osPeakWorkingSetBytes=ospeak,privateBytes=private);f.write(json.dumps(sample)+'\n');f.flush();count+=1;peak_private=max(peak_private,private);peak_working=max(peak_working,ospeak)
   if ospeak>2**31 or private>2**31:
    reason='2GiB resource guard';(a.output/'STOP.json').write_bytes((json.dumps(dict(reason=reason,sample=sample),indent=2)+'\n').encode());subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);break
  time.sleep(.05)
k.CloseHandle(handle);process.wait();out.close();err.close();result=dict(pid=process.pid,exitCode=process.returncode,stop=reason,samples=count,peakPrivateBytes50ms=peak_private,osPeakWorkingSetBytes=peak_working,lifetimeMs=(time.monotonic()-start)*1000,timingScope='Whole process including UI automation, dialogs and idle; not pure operation latency',newRawDecodesAuthorized=0);(a.output/'EXIT.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(json.dumps(result))
