"""Serial zero-RAW replay under the unchanged 2GiB/50ms observer guard."""
from pathlib import Path
import argparse,json,ctypes as C,ctypes.wintypes as W,struct,subprocess,time,os,hashlib
p=argparse.ArgumentParser();p.add_argument('stage',type=Path);p.add_argument('task',type=Path);p.add_argument('out',type=Path);p.add_argument('mode');p.add_argument('scenario');a=p.parse_args();stage=a.stage.resolve();out=a.out.resolve();task=a.task.resolve();assert not out.exists();assert not (stage/'raw_r.dll').exists();out.mkdir(parents=True)
env=dict(os.environ)
for k in list(env):
 if k.startswith('PIXEL_TART_') or k.startswith('KITAO_'):env.pop(k,None)
stdout=(out/'stdout.log').open('w');stderr=(out/'stderr.log').open('w');host=stage/'LegacyExportMemoryHost.dll';proc=subprocess.Popen(['dotnet',str(host),str(task),str(out),a.mode,a.scenario],cwd=stage,env=env,stdout=stdout,stderr=stderr,creationflags=0x08000000)
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.CloseHandle.argtypes=[W.HANDLE];handle=k.OpenProcess(0x410,False,proc.pid);assert handle
launch={'pid':proc.pid,'startedUtc':time.time(),'mode':a.mode,'scenario':a.scenario,'task':json.loads(task.read_text('utf-8-sig')),'stage':str(stage),'hostSha256':hashlib.sha256(host.read_bytes()).hexdigest(),'coreSha256':hashlib.sha256((stage/'RAWSelectionAssistant.Core.dll').read_bytes()).hexdigest(),'appSha256':hashlib.sha256((stage/'KitaoPhotoSelector.dll').read_bytes()).hexdigest(),'guardBytes':2**31,'newRawDecodes':0};(out/'LAUNCH.json').write_text(json.dumps(launch,indent=2)+'\n')
start=time.monotonic();count=peak=peakws=0;stop=None
with (out/'RESOURCE_SAMPLES.jsonl').open('w') as journal:
 while proc.poll() is None:
  b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
  if k.K32GetProcessMemoryInfo(handle,b,80):
   ospeak,ws=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0]
   try:phase=(out/'phase.txt').read_text().strip()
   except OSError:phase='startup'
   row={'elapsedMs':(time.monotonic()-start)*1000,'privateBytes':private,'workingSetBytes':ws,'osPeakWorkingSetBytes':ospeak,'phase':phase};journal.write(json.dumps(row)+'\n');journal.flush();count+=1;peak=max(peak,private);peakws=max(peakws,ospeak)
   if private>2**31 or ospeak>2**31 or time.monotonic()-start>600:stop=row;proc.terminate();break
  time.sleep(.05)
proc.wait(timeout=10);k.CloseHandle(handle);stdout.close();stderr.close();result={'pid':proc.pid,'exitCode':proc.returncode,'stop':stop,'peakPrivateBytes50ms':peak,'osPeakWorkingSetBytes':peakws,'samples':count,'guardBytes':2**31,'samplingMs':50,'newRawDecodes':0,'forcedGc':False};(out/'EXIT.json').write_text(json.dumps(result,indent=2)+'\n');
if stop:(out/'STOP.json').write_text(json.dumps({'reason':'2GiB or 600s guard','sample':stop},indent=2)+'\n')
print(json.dumps(result));raise SystemExit(0 if proc.returncode==0 and stop is None else 1)
