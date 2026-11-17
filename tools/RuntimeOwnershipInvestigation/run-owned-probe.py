from pathlib import Path
import subprocess,sys,uuid,time,json,shutil,ctypes as C,ctypes.wintypes as W,struct,hashlib,os
built=Path(sys.argv[1]);base=Path(sys.argv[2]);nonce=uuid.uuid4().hex;root=base/('ownership-'+nonce[:10]);root.mkdir(parents=True,exist_ok=False);binroot=root/'bin';binroot.mkdir()
for p in built.iterdir():
 if p.is_file():shutil.copy2(p,binroot/p.name)
exe=binroot/('KitaoOwnershipProbe_'+nonce[:10]+'.exe');shutil.copy2(binroot/'KitaoRuntimeOwnershipProbe_20261117.exe',exe)
procs=[];peaks={};samples=[];stop=None
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.CloseHandle.argtypes=[W.HANDLE]
def save(n,j):(root/n).write_text(json.dumps(j,ensure_ascii=False,indent=2)+chr(10),encoding='utf-8')
def launch(role):
 p=subprocess.Popen([str(exe),role,str(root),nonce],stdout=(root/(role+'.stdout')).open('w'),stderr=(root/(role+'.stderr')).open('w'),creationflags=0x08000000);procs.append((role,p));return p
def tick():
 global stop
 for role,p in procs:
  if p.poll() is not None:continue
  h=k.OpenProcess(0x410,False,p.pid)
  if not h:continue
  try:
   b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
   if k.K32GetProcessMemoryInfo(h,b,80):
    ospeak,working=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0];v=peaks.setdefault(role,{'privateBytes':0,'workingSetBytes':0});v['privateBytes']=max(v['privateBytes'],private);v['workingSetBytes']=max(v['workingSetBytes'],working)
    samples.append({'role':role,'pid':p.pid,'atMonotonic':time.monotonic(),'privateBytes':private,'workingSetBytes':working})
    if private>2**31 or ospeak>2**31:stop='2GiB fixture process guard'
  finally:k.CloseHandle(h)
 if stop:
  save('STOP.json',{'reason':stop})
  for _,p in procs:
   if p.poll() is None:p.terminate()
  raise RuntimeError(stop)
 time.sleep(.05)
try:
 start=time.monotonic();a=launch('ownerA');b=launch('ownerB');save('OWNED_PIDS.json',[a.pid,b.pid]);deadline=time.monotonic()+10
 while not all((root/(r+'-acquire.json')).exists() for r in ['ownerA','ownerB']):
  tick()
  if time.monotonic()>deadline:raise RuntimeError('Synthetic owner failed to start')
 before=[json.loads((root/(r+'-acquire.json')).read_text()) for r in ['ownerA','ownerB']]
 assert all(x['acquired'] and x['mainWindow']==0 and x['productName']==x['expectedProduct'] for x in before)
 while time.monotonic()-start<16.5:tick()
 recovery_start=time.monotonic();c=launch('recoverA')
 while c.poll() is None:
  tick()
  if time.monotonic()-recovery_start>12:raise RuntimeError('Recovery did not return')
 for _ in range(5):tick()
 acquired=json.loads((root/'recoverA-acquire.json').read_text())
 result={'status':'CONFIRMED_CROSS_ROOT_STALE_RECOVERY_TERMINATION' if a.poll() is not None and b.poll() is not None and acquired['acquired'] else 'NOT_REPRODUCED','scope':'Exact linked production SingleInstanceManager/Branding, owned console fixtures only; not ordinary desktop acceptance','nonce':nonce,'run':str(root),'owners':before,'recover':acquired,'exitCodes':{r:p.poll() for r,p in procs},'otherRootWasNotRequested':True,'otherRootTerminated':b.poll() is not None,'elapsedMs':(time.monotonic()-start)*1000,'recoveryMs':(time.monotonic()-recovery_start)*1000,'resourcePeaks':peaks,'guardBytes':2**31,'forcedGc':False,'newRawDecodes':0,'noProductProcessOrUserRootUsed':True,'exeSha256':hashlib.sha256(exe.read_bytes()).hexdigest()};save('RESULT.json',result);(base/'OWNERSHIP_LATEST.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+chr(10),encoding='utf-8');print(json.dumps({'status':result['status'],'run':str(root),'otherRootTerminated':result['otherRootTerminated'],'recoveryMs':result['recoveryMs']}))
finally:
 (root/'FINISH').write_text('finish',encoding='utf-8')
 for _,p in procs:
  try:p.wait(timeout=8)
  except subprocess.TimeoutExpired:p.terminate();p.wait(timeout=5)
 with (root/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8') as f:
  for x in samples:f.write(json.dumps(x)+chr(10))
