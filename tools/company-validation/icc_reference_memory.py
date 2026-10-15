"""Finite isolated reference process memory observations; never product/native attribution."""
import subprocess,pathlib,threading,time,json,hashlib,psutil,os
root=pathlib.Path(__file__).resolve().parents[2]/'artifacts/company-validation/icc';out=root/'results';points=['0 0 0','0.18 0.18 0.18','1 1 1','1 0 0','0 1 0','0 0 1'];data=('\n'.join(points)+'\n')*8000;records=[]
for name,cmd in [('Argyll',[str(root/'argyll35/Argyll_V3.5.0/bin/xicclu.exe'),'-v0','-ff','-ir','-px',str(out/'matrix-v2.1-g1.0.icc')]),('skcms',[str(root/'skcms_probe.exe'),str(out/'matrix-v4.3-g1.0.icc'),'1','f'])]:
 process=subprocess.Popen(cmd,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));samples=[];stop=threading.Event();start=time.perf_counter()
 def sample():
  p=psutil.Process(process.pid)
  while not stop.is_set():
   try:m=p.memory_info();samples.append({'ms':(time.perf_counter()-start)*1000,'workingSet':m.rss,'privateBytes':getattr(m,'private',m.vms)})
   except psutil.NoSuchProcess:break
   time.sleep(.005)
 thread=threading.Thread(target=sample);thread.start();stdout,stderr=process.communicate(data,timeout=60);stop.set();thread.join();records.append({'reference':name,'command':cmd,'points':48000,'exitCode':process.returncode,'elapsedMs':(time.perf_counter()-start)*1000,'outputSha256':hashlib.sha256(stdout.encode()).hexdigest(),'stderr':stderr,'samples':samples,'nativeAllocations':'UNAVAILABLE','comparison':'Includes process launch/parse/text I/O; not LCMS pure transform benchmark'})
(out/'reference-memory.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
