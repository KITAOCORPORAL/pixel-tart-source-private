from pathlib import Path
import json,subprocess,time,psutil,numpy as np

def execute(command,log,limit=512*1048576):
 start=time.perf_counter();process=subprocess.Popen([str(c) for c in command],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',errors='replace');peak=0
 while process.poll() is None:
  try:
   info=psutil.Process(process.pid).memory_info();peak=max(peak,getattr(info,'private',info.rss))
   if peak>limit or time.perf_counter()-start>90:process.kill();raise RuntimeError('Research child resource/watchdog guard')
  except psutil.NoSuchProcess:pass
  time.sleep(.01)
 stdout,stderr=process.communicate();Path(log).write_bytes((stdout+stderr).encode())
 if process.returncode:raise RuntimeError('Research process failed: '+stderr[-2000:])
 return dict(exitCode=process.returncode,elapsedMs=(time.perf_counter()-start)*1000,sampledPeakPrivateBytes=peak,guardBytes=limit)

def probe(dotnet,dll,source,out,w,h,ev,trace=False,icc=None):
 out=Path(out);assert not out.exists(),'Preserve old probe output'
 command=[dotnet,dll,source,out,w,h,format(ev,'.17g'),'encoded-srgb']
 if trace:command.append('trace')
 if icc is not None:command.append('icc='+str(icc))
 call=execute(command,out.with_name(out.name+'-console.txt'))
 return {name:np.fromfile(out/(name+'.f32'),dtype='<f4').reshape(h,w,3) for name in ['output','candidate','basic-tone-output']},call
