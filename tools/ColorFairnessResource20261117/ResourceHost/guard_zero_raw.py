from pathlib import Path
import json,os,sys,time,subprocess,ctypes as C,ctypes.wintypes as W,struct,hashlib
sdk=Path(sys.argv[1]);dll=Path(sys.argv[2]);out=Path(sys.argv[3]);payload=Path(sys.argv[4]);out.mkdir(parents=True,exist_ok=False)
assert dll.name=='ColorZeroRawResourceHost20261117.dll'
assert not (dll.parent/'raw_r.dll').exists()
env=dict(os.environ)
for k in list(env):
 if k.startswith('PIXEL_TART_') or k.startswith('KITAO_'):env.pop(k,None)
env.update(PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(out/'runtime'))
o=(out/'stdout.log').open('w');e=(out/'stderr.log').open('w');p=subprocess.Popen([str(sdk),str(dll),str(out),str(payload),*sys.argv[5:]],cwd=dll.parent,env=env,stdout=o,stderr=e,creationflags=0x08000000)
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.CloseHandle.argtypes=[W.HANDLE];h=k.OpenProcess(0x410,False,p.pid);assert h
start=time.monotonic();samples=[];stop=None
with (out/'RESOURCE_SAMPLES.jsonl').open('w') as f:
 while p.poll() is None:
  b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
  if k.K32GetProcessMemoryInfo(h,b,80):
   ospeak,ws=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0]
   try:phase=(out/'phase.txt').read_text('utf-8').strip()
   except OSError:phase='startup'
   row={'elapsedMs':(time.monotonic()-start)*1000,'privateBytes':private,'workingSetBytes':ws,'osPeakWorkingSetBytes':ospeak,'phase':phase};samples.append(row);f.write(json.dumps(row)+chr(10));f.flush()
   if private>2**31 or ospeak>2**31 or time.monotonic()-start>600:stop=row;p.terminate();break
  time.sleep(.05)
p.wait(timeout=10);k.CloseHandle(h);o.close();e.close();r={'pid':p.pid,'exitCode':p.returncode,'stop':stop,'peakPrivateBytes50ms':max([x['privateBytes'] for x in samples] or [0]),'osPeakWorkingSetBytes':max([x['osPeakWorkingSetBytes'] for x in samples] or [0]),'samples':len(samples),'guardBytes':2**31,'deadlineSeconds':600,'newRawDecodes':0,'noHeavyLedger':True,'sdk':str(sdk),'hostSha256':hashlib.sha256(dll.read_bytes()).hexdigest(),'forcedGc':False}
(out/'EXIT.json').write_text(json.dumps(r,indent=2)+chr(10));print(json.dumps(r));sys.exit(0 if p.returncode==0 and stop is None else 1)
