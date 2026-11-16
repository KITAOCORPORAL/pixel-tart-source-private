"""Normal company App lifetime recorder. Credentials stay in external private files."""
from pathlib import Path
import argparse,ctypes as C,ctypes.wintypes as W,hashlib,json,os,subprocess,time,struct,sys
p=argparse.ArgumentParser();p.add_argument('--context',type=Path,required=True);p.add_argument('--run',type=Path,required=True);p.add_argument('--source',required=True);a=p.parse_args();c=json.loads(a.context.read_text('utf-8-sig'));assert c['workflowAppProductSource']==a.source
# Refuse an ambiguous live owner; reuse only the exact recorded creation identity.
kcheck=C.WinDLL('kernel32',use_last_error=True);kcheck.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];kcheck.OpenProcess.restype=W.HANDLE;kcheck.CloseHandle.argtypes=[W.HANDLE];kcheck.GetProcessTimes.argtypes=[W.HANDLE,C.POINTER(W.FILETIME),C.POINTER(W.FILETIME),C.POINTER(W.FILETIME),C.POINTER(W.FILETIME)];kcheck.QueryFullProcessImageNameW.argtypes=[W.HANDLE,W.DWORD,W.LPWSTR,C.POINTER(W.DWORD)]
old=kcheck.OpenProcess(0x1000,False,int(c.get('workflowAppPid') or 0))
if old:
 code=W.DWORD();kcheck.GetExitCodeProcess.argtypes=[W.HANDLE,C.POINTER(W.DWORD)];assert kcheck.GetExitCodeProcess(old,C.byref(code))
 if code.value!=259:kcheck.CloseHandle(old);old=None
if old:
 try:
  creation,ex,kt,ut=W.FILETIME(),W.FILETIME(),W.FILETIME(),W.FILETIME();name=C.create_unicode_buffer(32768);length=W.DWORD(32768);assert kcheck.GetProcessTimes(old,C.byref(creation),C.byref(ex),C.byref(kt),C.byref(ut));assert kcheck.QueryFullProcessImageNameW(old,0,name,C.byref(length));stamp=str((creation.dwHighDateTime<<32)|creation.dwLowDateTime)
  if stamp==c.get('workflowAppCreatedFileTime') and os.path.normcase(os.path.abspath(name.value))==os.path.normcase(os.path.abspath(c['workflowAppExecutable'])):print(json.dumps({'reuseExistingApp':True,'pid':c['workflowAppPid'],'noSecondWriter':True}));sys.exit(0)
  raise RuntimeError('Live recorded PID has a different or unverified owner; preserve it')
 finally:kcheck.CloseHandle(old)
a.run.mkdir(parents=True,exist_ok=False);app=Path(c['workflowAppExecutable']).resolve();root=Path(c['workflowQaRuntime']).resolve();env=os.environ.copy();env['PIXEL_TART_ISOLATED_RUNTIME']='1';env['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=str(root)
for name in ['PIXEL_TART_ACCEPTANCE_ROOT','PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY','PIXEL_TART_HUMAN_ACCEPTANCE','PIXEL_TART_XTRANS_STABLE_V1','PIXEL_TART_XTRANS_PLAN','PIXEL_TART_XTRANS_STEP','PIXELTART_SELECTION_ENDPOINT','PIXELTART_SELECTION_OWNER_KEY']:env.pop(name,None)
if c.get('selectionPrivatePath'):
 private=json.loads(Path(c['selectionPrivatePath']).read_text('utf-8-sig'));assert private['endpoint'].rstrip('/')==c['workflowSelectionEndpoint'].rstrip('/');env['PIXELTART_SELECTION_ENDPOINT']=private['endpoint'];env['PIXELTART_SELECTION_OWNER_KEY']=private['ownerKey']
args=[str(app)]
if c.get('launchPhone'):args+=['--mobile-schedule']
elif c.get('workflowProjectId'):
 args+=['--workflow-project='+c['workflowProjectId']]
 if c.get('sessionFile'):args+=['--studio-resume='+c['sessionFile']]
stdout=(a.run/'stdout.log').open('w');stderr=(a.run/'stderr.log').open('w');process=subprocess.Popen(args,cwd=app.parent,env=env,stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW)
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.K32GetProcessMemoryInfo.restype=W.BOOL;k.GetProcessTimes.argtypes=[W.HANDLE,C.POINTER(W.FILETIME),C.POINTER(W.FILETIME),C.POINTER(W.FILETIME),C.POINTER(W.FILETIME)];h=k.OpenProcess(0x410,False,process.pid);assert h
creation,exit_time,kernel,user=W.FILETIME(),W.FILETIME(),W.FILETIME(),W.FILETIME();assert k.GetProcessTimes(h,C.byref(creation),C.byref(exit_time),C.byref(kernel),C.byref(user));stamp=str((creation.dwHighDateTime<<32)|creation.dwLowDateTime)
c.update(workflowAppPid=process.pid,workflowAppCreatedFileTime=stamp,activeGuardRun=str(a.run));temp=a.context.with_name(a.context.name+'.'+str(process.pid)+'.tmp');temp.write_text(json.dumps(c,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');os.replace(temp,a.context)
if c.get('selectionPrivatePath'):
 private.update(desktopPid=process.pid,desktopStartedUtc='filetime:'+stamp,desktopRoot=str(root));private_path=Path(c['selectionPrivatePath']);private_tmp=private_path.with_name(private_path.name+'.'+str(process.pid)+'.tmp');private_tmp.write_text(json.dumps(private,indent=2)+'\n',encoding='utf-8');os.replace(private_tmp,private_path)
(a.run/'LAUNCH.json').write_text(json.dumps({'pid':process.pid,'createdFileTime':stamp,'app':str(app),'runtime':str(root),'source':a.source,'mainDllSha256':hashlib.sha256((app.parent/'KitaoPhotoSelector.dll').read_bytes()).hexdigest(),'startedUtc':time.time(),'newRawDecodesAuthorized':0,'guardBytes':2147483648},indent=2),encoding='utf-8')
start=time.monotonic();peak_private=peak_working=count=0;reason=None
with (a.run/'RESOURCE_SAMPLES.jsonl').open('w',encoding='utf-8') as journal:
 while process.poll() is None:
  b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
  if k.K32GetProcessMemoryInfo(h,b,80):
   ospeak,working=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0];row={'atUtc':time.time(),'elapsedMs':(time.monotonic()-start)*1000,'privateBytes':private,'workingSetBytes':working,'osPeakWorkingSetBytes':ospeak};journal.write(json.dumps(row)+'\n');journal.flush();count+=1;peak_private=max(peak_private,private);peak_working=max(peak_working,ospeak)
   if max(private,ospeak)>2147483648:
    reason='2GiB resource guard';(a.run/'STOP.json').write_text(json.dumps({'reason':reason,'sample':row}),encoding='utf-8');subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);break
  time.sleep(.05)
k.CloseHandle.argtypes=[W.HANDLE];k.CloseHandle(h);process.wait();stdout.close();stderr.close();(a.run/'EXIT.json').write_text(json.dumps({'pid':process.pid,'exitCode':process.returncode,'stop':reason,'samples':count,'peakPrivateBytes50ms':peak_private,'osPeakWorkingSetBytes':peak_working,'lifetimeMs':(time.monotonic()-start)*1000,'scope':'Whole App lifetime including idle, dialogs and automation; excludes services/browser. Not operation latency.','newRawDecodesAuthorized':0},indent=2),encoding='utf-8')
