"""Run each synthetic WPF probe in its own process under the unchanged 2GiB/50ms guard."""
from pathlib import Path
import argparse,ctypes as C,ctypes.wintypes as W,json,os,struct,subprocess,time,hashlib
p=argparse.ArgumentParser();p.add_argument('--exe',type=Path,required=True);p.add_argument('--root',type=Path,required=True);p.add_argument('--round',default='v1');p.add_argument('--scenarios',default='close-saved,close-nested-frame,close-nested-frame-repeat,close-cancel-other-handler,close-repeated,close-save-pending,close-hide-pending,close-workspace-first,save-cancel,save-failure-retry,save-undo-reopen-export,preview-after-unload,preview-editor-change-after-unload,preview-workspace-dispose,preview-loaded-unload-retains,save-during-edit');a=p.parse_args()
root=a.root.resolve()/a.round;root.mkdir(parents=True,exist_ok=False);exe=a.exe.resolve();assert exe.name=='Lifecycle.exe'
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.K32GetProcessMemoryInfo.restype=W.BOOL;k.CloseHandle.argtypes=[W.HANDLE]
rows=[]
for label in a.scenarios.split(','):
 case=root/label;case.mkdir();scenario='close-nested-frame' if label=='close-nested-frame-repeat' else label
 env=os.environ.copy();env.update(PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(case/'runtime'))
 for name in list(env):
  if name.startswith('PIXEL_TART_ACCEPTANCE_') or name.startswith('PIXEL_TART_XTRANS_'):env.pop(name,None)
 with (case/'stdout.log').open('w') as stdout,(case/'stderr.log').open('w') as stderr:
  proc=subprocess.Popen([str(exe),scenario,str(case)],cwd=exe.parent,env=env,stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW)
  (case/'LAUNCH.json').write_text(json.dumps(dict(pid=proc.pid,exe=str(exe),scenario=scenario,source='B1_WORKTREE',exeSha256=hashlib.sha256(exe.read_bytes()).hexdigest(),guardBytes=2**31,intervalMs=50,newRawDecodes=0,forcedGc=False),indent=2)+'\n',encoding='utf-8')
  handle=k.OpenProcess(0x410,False,proc.pid);assert handle
  start=time.monotonic();peak_private=peak_working=count=0;stop=None
  with (case/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8') as samples:
   while proc.poll() is None:
    b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
    if k.K32GetProcessMemoryInfo(handle,b,80):
     ospeak,working=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0];count+=1;peak_private=max(peak_private,private);peak_working=max(peak_working,ospeak);row=dict(atUtc=time.time(),elapsedMs=(time.monotonic()-start)*1000,privateBytes=private,workingSetBytes=working,osPeakWorkingSetBytes=ospeak);samples.write(json.dumps(row)+'\n')
     if max(private,ospeak)>2**31:stop='2GiB resource guard'
    if time.monotonic()-start>45:stop='45s diagnostic timeout'
    if stop:
     (case/'STOP.json').write_text(json.dumps(dict(reason=stop,pid=proc.pid),indent=2)+'\n',encoding='utf-8');proc.kill();break
    time.sleep(.05)
  k.CloseHandle(handle);proc.wait()
 row=dict(case=label,pid=proc.pid,exitCode=proc.returncode,stop=stop,samples=count,peakPrivateBytes50ms=peak_private,osPeakWorkingSetBytes=peak_working,lifetimeMs=(time.monotonic()-start)*1000,timingScope='Entire synthetic WPF process including startup and deliberate waiting; not product operation latency',newRawDecodes=0,forcedGc=False)
 (case/'EXIT.json').write_text(json.dumps(row,indent=2)+'\n',encoding='utf-8');rows.append(row)
 print(json.dumps(dict(case=label,exitCode=proc.returncode,stop=stop,peakMiB=round(peak_private/2**20,2))),flush=True)
(root/'RUN_SUMMARY.json').write_text(json.dumps(dict(source='B1_WORKTREE',cases=rows),indent=2)+'\n',encoding='utf-8')

import sys
sys.exit(1 if any(r['exitCode']!=0 or r['stop'] for r in rows) else 0)
