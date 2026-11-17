from pathlib import Path
import json,sys,subprocess,os,time,ctypes as C,ctypes.wintypes as W,threading,struct,numpy as np,hashlib
root=Path(sys.argv[1]);exe=Path(sys.argv[2]);plan=json.loads((root/'FROZEN_PLAN.json').read_text('utf-8'));old=json.loads((root/'ORIGINAL_ROI_RESULTS.json').read_text());orig=Path('E:/Guangjing-Portrait-Highlight-Review-20261112');samples=root/'samples';out=root/'fair-exact';out.mkdir(exist_ok=False)
k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.CloseHandle.argtypes=[W.HANDLE]
env=os.environ.copy();env['DOTNET_ROOT']=sys.argv[3];env['DOTNET_ROOT_X64']=sys.argv[3]
err=(out/'server.stderr.log').open('w');p=subprocess.Popen([str(exe)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf-8',env=env,creationflags=0x08000000)
handle=k.OpenProcess(0x410,False,p.pid);stop=[];resource=[];start=time.monotonic()
def monitor():
 while p.poll() is None:
  b=C.create_string_buffer(80);C.c_uint32.from_buffer(b).value=80
  if k.K32GetProcessMemoryInfo(handle,b,80):
   ospeak,ws=struct.unpack_from('<QQ',b.raw,8);private=struct.unpack_from('<Q',b.raw,72)[0];resource.append({'elapsedMs':(time.monotonic()-start)*1000,'privateBytes':private,'workingSetBytes':ws,'osPeakWorkingSetBytes':ospeak})
   if private>536870912 or ospeak>536870912 or time.monotonic()-start>600:stop.append(resource[-1]);p.terminate();break
  time.sleep(.05)
t=threading.Thread(target=monitor,daemon=True);t.start();calls=0

def D(a):return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)
def Y(a):return D(a)@np.array([.2126,.7152,.0722])
def load(p):return np.fromfile(p,dtype='<f4').reshape(-1,3)
def ask(method,roi,anchor,ev,strength,refev,output=None,metrics=True):
 global calls
 q={'method':method,'roi':str(roi),'anchor':str(anchor),'ev':float(ev),'strength':float(strength),'referenceEv':refev,'output':None if output is None else str(output),'metrics':metrics};p.stdin.write(json.dumps(q)+chr(10));p.stdin.flush();line=p.stdout.readline()
 if not line:raise RuntimeError('Control server ended')
 r=json.loads(line);calls+=1
 if not r.get('ok'):raise RuntimeError(r.get('error'))
 return r

def midfit(method,roi,anchor,strength,target,ev):
 if target is None:return {'status':'NO_MIDTONE_ANCHOR'}
 grid=np.linspace(-5,5,9);values=[ask(method,roi,anchor,e,strength,ev) for e in grid]
 if any(v['mid'] is None for v in values):return {'status':'NO_MIDTONE_ANCHOR','midPixels':values[0]['midPixels']}
 exact=[i for i,v in enumerate(values) if abs(v['mid']-target)<1e-9]
 if exact:i=exact[0];return {'status':'MATCHED','e':float(grid[i]),'s':strength,'metric':values[i],'midError':abs(values[i]['mid']-target)}
 brackets=[i for i in range(8) if (values[i]['mid']-target)*(values[i+1]['mid']-target)<=0]
 if not brackets:return {'status':'MID_OUTSIDE_BOUNDED_RANGE','coarseMid':[v['mid'] for v in values]}
 i=brackets[0];lo,hi=float(grid[i]),float(grid[i+1]);flo=values[i]['mid']-target
 for _ in range(18):
  e=(lo+hi)/2;r=ask(method,roi,anchor,e,strength,ev);f=r['mid']-target
  if flo*f<=0:hi=e
  else:lo=e;flo=f
 e=(lo+hi)/2;r=ask(method,roi,anchor,e,strength,ev);return {'status':'MATCHED' if abs(r['mid']-target)<=2e-5 else 'MID_ERROR','e':e,'s':strength,'metric':r,'midError':abs(r['mid']-target)}

results=[]
try:
 for item in plan['inputs']:
  roi=samples/item['file'];anchor=samples/plan['anchors'][item['anchor']]['file'];fullroi=orig/'fixed-inputs'/item['file'];fullanchor=orig/'fixed-inputs'/plan['anchors'][item['anchor']]['file']
  for ev in plan['exposureCases']:
   oldr=next(x for x in old['comparisons'] if x['id']==item['id'] and x['baselineEv']==ev);sr=load(roi);ha=np.max(D(sr)*2**ev,axis=1)>.75
   ismatched=oldr['anchor']['status']=='MATCHED';targetMid=oldr['anchor'].get('targetMeanY') if ismatched else None
   cdir=orig/'roi-comparison02'/(item['id']+'-E'+str(ev)+('-midmatched' if ismatched and oldr['candidateMidMatchedEv']!=ev else '-same'));cf=load(cdir/'candidate.f32').reshape(384,384,3);targetHigh=float(Y(cf[::4,::4].reshape(-1,3))[ha].mean()) if ha.sum()>=16 and ismatched else None
   row={'id':item['id'],'kind':item['kind'],'ev':ev,'anchor':item['anchor'],'candidateMidStatus':oldr['anchor']['status'],'targetMid':targetMid,'targetHigh':targetHigh,'controls':[],'candidateSamePath':str(orig/'roi-comparison02'/(item['id']+'-E'+str(ev)+'-same')/'candidate.f32'),'candidateMidPath':str(cdir/'candidate.f32') if ismatched else None}
   for m in plan['methods']:
    method=m['id'];s=m['fixedStrength'];same=ask(method,roi,anchor,ev,s,ev);samefile=out/(item['id']+'-E'+str(ev)+'-'+method+'-same.f32');ask(method,fullroi,fullanchor,ev,s,ev,samefile,False)
    fixed=midfit(method,roi,anchor,s,targetMid,ev)
    if fixed['status']=='MATCHED':
     fixed['file']=str(out/(item['id']+'-E'+str(ev)+'-'+method+'-mid.f32'));ask(method,fullroi,fullanchor,fixed['e'],s,ev,Path(fixed['file']),False)
    joint={'status':'NO_MIDTONE_ANCHOR' if targetMid is None else 'NO_HIGH_ANCHOR'}
    if targetMid is not None and targetHigh is not None:
     scan=[]
     for strength in np.linspace(m['range'][0],m['range'][1],9):
      f=midfit(method,roi,anchor,float(strength),targetMid,ev)
      scan.append(f)
     valid=[(i,f) for i,f in enumerate(scan) if f['status']=='MATCHED' and f['metric']['high'] is not None]
     joint={'status':'NO_JOINT_MATCH_IN_FIXED_RANGE','scan':[{'s':float(np.linspace(m['range'][0],m['range'][1],9)[i]),'status':f['status'],'highResidual':f.get('metric',{}).get('high')-targetHigh if f.get('metric',{}).get('high') is not None else None} for i,f in enumerate(scan)]}
     candidates=[(i,f) for i,f in valid if abs(f['metric']['high']-targetHigh)<=max(1e-8,targetHigh*.005)]
     bracket=[(i,j) for (i,f),(j,g) in zip(valid,valid[1:]) if j==i+1 and (f['metric']['high']-targetHigh)*(g['metric']['high']-targetHigh)<=0]
     chosen=candidates[0][1] if candidates else None
     if chosen is None and bracket:
      i,j=bracket[0];lo=scan[i]['s'];hi=scan[j]['s'];flo=scan[i]['metric']['high']-targetHigh
      for _ in range(14):
       s2=(lo+hi)/2;f=midfit(method,roi,anchor,s2,targetMid,ev)
       if f['status']!='MATCHED':break
       res=f['metric']['high']-targetHigh;chosen=f
       if flo*res<=0:hi=s2
       else:lo=s2;flo=res
     if chosen is not None:
      miderr=chosen['midError'];herr=abs(chosen['metric']['high']-targetHigh);joint.update({'status':'JOINT_MATCHED' if miderr<=2e-5 and herr<=targetHigh*.005 else 'JOINT_ERROR','e':chosen['e'],'s':chosen['s'],'midError':miderr,'highAbsError':herr,'highRelativeError':herr/targetHigh,'metric':chosen['metric']})
      if joint['status']=='JOINT_MATCHED':joint['file']=str(out/(item['id']+'-E'+str(ev)+'-'+method+'-joint.f32'));ask(method,fullroi,fullanchor,joint['e'],joint['s'],ev,Path(joint['file']),False)
    row['controls'].append({'method':method,'fixedStrength':m['fixedStrength'],'same':same,'sameFile':str(samefile),'mid':fixed,'joint':joint})
   results.append(row);(out/'PROGRESS.json').write_text(json.dumps({'cases':len(results),'calls':calls,'last':item['id']+str(ev)},indent=2),encoding='utf-8')
 p.stdin.write('QUIT'+chr(10));p.stdin.flush();code=p.wait(timeout=10);assert code==0,code
 (out/'RESULTS.json').write_text(json.dumps({'status':'EXACT_CURRENT_PRODUCT_CONTROLS_COMPLETE','planSha256':hashlib.sha256((root/'FROZEN_PLAN.json').read_bytes()).hexdigest(),'cases':results,'calls':calls,'newRawDecodes':0,'newCandidateRuns':0,'candidateMathChanged':False,'parentNativeCalls':0,'photosLocalOnly':True},indent=2)+chr(10),encoding='utf-8');print(json.dumps({'cases':len(results),'calls':calls,'exit':code}))
finally:
 if p.poll() is None:p.terminate();p.wait(timeout=5)
 t.join(timeout=2);k.CloseHandle(handle);err.close();(out/'RESOURCE.json').write_text(json.dumps({'pid':p.pid,'exit':p.returncode,'calls':calls,'samples':len(resource),'privatePeak':max([x['privateBytes'] for x in resource] or [0]),'osPeakWorkingSet':max([x['osPeakWorkingSetBytes'] for x in resource] or [0]),'stop':stop,'limitBytes':536870912,'deadlineSeconds':600},indent=2),encoding='utf-8')
