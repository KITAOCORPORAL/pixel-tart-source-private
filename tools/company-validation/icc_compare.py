import ctypes as C,pathlib,json,hashlib,subprocess,struct,time,itertools,math,os
R=pathlib.Path(__file__).resolve().parents[2];root=R/'artifacts/company-validation/icc';out=root/'results';out.mkdir(exist_ok=True)
L=C.CDLL(str(root/'lcms/Library/bin/lcms2.dll'))
def fn(n,r,args):f=getattr(L,n);f.restype=r;f.argtypes=args;return f
P=C.c_void_p;U=C.c_uint;D=C.c_double
class xyY(C.Structure):_fields_=[('x',D),('y',D),('Y',D)]
class Triple(C.Structure):_fields_=[('Red',xyY),('Green',xyY),('Blue',xyY)]
gamma=fn('cmsBuildGamma',P,[P,D]);rgb=fn('cmsCreateRGBProfile',P,[C.POINTER(xyY),C.POINTER(Triple),C.POINTER(P)]);save=fn('cmsSaveProfileToFile',C.c_int,[P,C.c_char_p]);close=fn('cmsCloseProfile',C.c_int,[P]);version=fn('cmsSetProfileVersion',None,[P,D]);openp=fn('cmsOpenProfileFromFile',P,[C.c_char_p,C.c_char_p]);xyz=fn('cmsCreateXYZProfile',P,[]);transform=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);do=fn('cmsDoTransform',None,[P,P,P,U]);delete=fn('cmsDeleteTransform',None,[P]);adapt=fn('cmsSetAdaptationState',D,[D]);code=fn('cmsGetEncodedCMMversion',U,[])()
RGB16=(4<<16)|(3<<3)|2;RGBFLT=(1<<22)|(4<<16)|(3<<3)|4;XYZDBL=(1<<22)|(9<<16)|(3<<3)
def signature(s):return int.from_bytes(s.encode(),'big')
placeholder=fn('cmsCreateProfilePlaceholder',P,[P]);setspace=fn('cmsSetColorSpace',None,[P,U]);setpcs=fn('cmsSetPCS',None,[P,U]);setclass=fn('cmsSetDeviceClass',None,[P,U]);write=fn('cmsWriteTag',C.c_int,[P,U,P]);pipeline=fn('cmsPipelineAlloc',P,[P,U,U]);clut=fn('cmsStageAllocCLut16bit',P,[P,U,U,U,C.POINTER(C.c_ushort)]);tone=fn('cmsStageAllocToneCurves',P,[P,U,C.POINTER(P)]);insert=fn('cmsPipelineInsertStage',C.c_int,[P,C.c_int,P]);freepipe=fn('cmsPipelineFree',None,[P]);mlu=fn('cmsMLUalloc',P,[P,U]);mluset=fn('cmsMLUsetASCII',C.c_int,[P,C.c_char_p,C.c_char_p,C.c_char_p]);mlufree=fn('cmsMLUfree',None,[P])
class XYZ(C.Structure):_fields_=[('X',D),('Y',D),('Z',D)]
white=xyY(.3127,.329,1);prim=Triple(xyY(.64,.33,1),xyY(.30,.60,1),xyY(.15,.06,1));profiles=[]
for v in [2.1,4.3]:
 for g in [1.,2.2]:
  curves=(P*3)(*[gamma(None,g) for _ in range(3)]);p=rgb(C.byref(white),C.byref(prim),curves);version(p,v);path=out/f'matrix-v{v}-g{g}.icc';assert save(p,str(path).encode());close(p);profiles.append((path,'matrix',v,g))
 # Synthetic LUT maps device RGB to bounded PCS XYZ; not a camera profile.
 p=placeholder(None);version(p,v);setspace(p,signature('RGB '));setpcs(p,signature('XYZ '));setclass(p,signature('scnr'));w=XYZ(.9642,1,.8249);assert write(p,signature('wtpt'),C.byref(w))
 for tag,text in [('desc',b'Kitao synthetic identity-like XYZ LUT'),('cprt',b'Original numerical test fixture; no device accuracy claim')]:
  m=mlu(None,1);mluset(m,b'en',b'US',text);write(p,signature(tag),m);mlufree(m)
 vals=[]
 for r,g,b in itertools.product(range(3),repeat=3):
  for x in [.436*r/2+.385*g/2+.143*b/2,.222*r/2+.717*g/2+.061*b/2,.014*r/2+.097*g/2+.714*b/2]:vals.append(round(x/1.999969482421875*65535))
 arr=(C.c_ushort*len(vals))(*vals)
 for tag in ['A2B0','A2B1','A2B2']:
  pipe=pipeline(None,3,3);curves=(P*3)(*[gamma(None,1.) for _ in range(3)]);assert insert(pipe,1,tone(None,3,curves));assert insert(pipe,1,clut(None,3,3,3,arr));curves=(P*3)(*[gamma(None,1.) for _ in range(3)]);assert insert(pipe,1,tone(None,3,curves));assert write(p,signature(tag),pipe);freepipe(pipe)
 path=out/f'lut-v{v}.icc';assert save(p,str(path).encode());close(p);profiles.append((path,'LUT',v,1.))
 if v>4:
  # A second legitimate v4 LUT explicitly contains M curves + a non-identity
  # matrix. Keep the matrix-absent sample as an independently failing boundary.
  matrixStage=fn('cmsStageAllocMatrix',P,[P,U,U,C.POINTER(D),C.POINTER(D)])
  p=placeholder(None);version(p,v);setspace(p,signature('RGB '));setpcs(p,signature('XYZ '));setclass(p,signature('scnr'));write(p,signature('wtpt'),C.byref(w))
  for tag,text in [('desc',b'Kitao v4 LUT with explicit M matrix'),('cprt',b'Original synthetic numerical fixture')]:
   m=mlu(None,1);mluset(m,b'en',b'US',text);write(p,signature(tag),m);mlufree(m)
  for tag in ['A2B0','A2B1','A2B2']:
   pipe=pipeline(None,3,3);curves=(P*3)(*[gamma(None,1.) for _ in range(3)]);insert(pipe,1,tone(None,3,curves));insert(pipe,1,clut(None,3,3,3,arr));curves=(P*3)(*[gamma(None,1.) for _ in range(3)]);insert(pipe,1,tone(None,3,curves));matrix=(D*9)(.9,0,0,0,.9,0,0,0,.9);insert(pipe,1,matrixStage(None,3,3,matrix,None));curves=(P*3)(*[gamma(None,1.) for _ in range(3)]);insert(pipe,1,tone(None,3,curves));assert write(p,signature(tag),pipe);freepipe(pipe)
  path=out/'lut-v4.3-explicit-matrix.icc';assert save(p,str(path).encode());close(p);profiles.append((path,'LUT-explicit-matrix',v,1.))
# Fix profile creation date: input/profile hashes must be reproducible across runs.
for path,_,_,_ in profiles:
 data=bytearray(path.read_bytes());struct.pack_into('>6H',data,24,2026,10,15,0,0,0);data[84:100]=bytes(16);path.write_bytes(data)
points=[[0,0,0],[.001,.001,.001],[.18,.18,.18],[.5,.5,.5],[1,1,1],[1,0,0],[0,1,0],[0,0,1],[.9,.2,.1],[.1,.9,.3],[-.1,.2,1.2],[2,2,2]]
exe=root/'argyll35/Argyll_V3.5.0/bin/xicclu.exe';records=[]
for path,kind,v,g in profiles:
 for intent,letter in [(0,'p'),(1,'r'),(2,'s'),(3,'a')]:
  for typ in ['float','uint16']:
   used=points if typ=='float' else [[round(min(1,max(0,c))*65535)/65535 for c in p] for p in points]
   p=openp(str(path).encode(),b'r');dest=xyz();adapt(0.0 if intent==3 else 1.0);t=transform(p,RGBFLT if typ=='float' else RGB16,dest,XYZDBL,intent,0x0100|0x0040|0x0004)
   if not t:records.append({'profile':path.name,'intent':intent,'type':typ,'status':'LCMS_REFUSED'});close(p);close(dest);continue
   inp=(C.c_float*(len(used)*3))(*[x for row in used for x in row]) if typ=='float' else (C.c_ushort*(len(used)*3))(*[round(x*65535) for row in used for x in row]);result=(D*(len(used)*3))();start=time.perf_counter();do(t,inp,result,len(used));elapsed=(time.perf_counter()-start)*1000;delete(t);close(p);close(dest)
   cmd=[str(exe),'-v0','-ff','-i'+letter,'-px',str(path)];text='\n'.join(' '.join(map(str,row)) for row in used)+'\n';start=time.perf_counter();proc=subprocess.run(cmd,input=text,text=True,capture_output=True,timeout=30,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));argtime=(time.perf_counter()-start)*1000
   (out/(path.stem+f'-{letter}-{typ}.stdout')).write_text(proc.stdout,encoding='utf-8');(out/(path.stem+f'-{letter}-{typ}.stderr')).write_text(proc.stderr,encoding='utf-8')
   arg=[]
   for line in proc.stdout.splitlines():
    try:row=[float(x) for x in line.split()];
    except ValueError:continue
    if len(row)==3:arg.append(row)
   values=[[result[i*3+c] for c in range(3)] for i in range(len(used))];diff=[[values[i][c]-arg[i][c] for c in range(3)] for i in range(len(arg))] if len(arg)==len(values) else []
   records.append({'profile':path.name,'kind':kind,'version':v,'gamma':g,'profileSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'intent':intent,'flags':'NOOPTIMIZE|NOCACHE|NOWHITEONWHITEFIXUP; BPC off','adaptationState':0 if intent==3 else 1,'inputType':typ,'domain':'device RGB 0..1; extended cases explicitly separate; output PCS XYZ D50 doubles','input':used,'lcms':values,'argyll':arg,'differences':diff,'maxAbsInRange':max([abs(d) for row,pnt in zip(diff,used) if all(0<=x<=1 for x in pnt) for d in row],default=None),'maxAbsAll':max([abs(d) for row in diff for d in row],default=None),'lcmsMs':elapsed,'argyllProcessMs':argtime,'argyllExit':proc.returncode,'command':cmd,'status':'COMPARED' if diff else 'REFERENCE_REFUSED_OR_OUTPUT_PARSE_FAILED'})
# skcms is an independent v4-capable reference. It exposes relative PCS transforms,
# not an absolute-intent adaptation control; those cells remain unsupported, not invented.
skexe=root/'skcms_probe.exe'
for rec in records:
 if 'lcms' not in rec:continue
 if rec['intent']==3:rec['skcmsStatus']='UNAVAILABLE_ABSOLUTE_ADAPTATION_CONTROL';continue
 cmd=[str(skexe),str(out/rec['profile']),str(rec['intent']),'u' if rec['inputType']=='uint16' else 'f'];text='\n'.join(' '.join(map(str,row)) for row in rec['input'])+'\n';skstart=time.perf_counter();proc=subprocess.run(cmd,input=text,text=True,capture_output=True,timeout=30,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));skms=(time.perf_counter()-skstart)*1000;values=[]
 for line in proc.stdout.splitlines():
  try:row=[float(x) for x in line.split()]
  except ValueError:continue
  if len(row)==3:values.append(row)
 rec['skcmsProcessMs']=skms;rec['skcmsCommand']=cmd;rec['skcmsExit']=proc.returncode;rec['skcmsError']=proc.stderr;rec['skcms']=values;rec['skcmsStatus']='COMPARED' if len(values)==len(rec['lcms']) else 'REFUSED'
 if rec['skcmsStatus']=='COMPARED':
  rec['skcmsDifferences']=[[a-b for a,b in zip(l,sk)] for l,sk in zip(rec['lcms'],values)];rec['skcmsMaxAbsInRange']=max(abs(d) for row,point in zip(rec['skcmsDifferences'],rec['input']) if all(0<=x<=1 for x in point) for d in row)
# Explicit malformed inputs. No reference may be reported as accepting corrupt bytes.
malformed=[]
for name,data in [('truncated',b'bad ICC'),('bad-size',b'\xff\xff\xff\xff'+(out/'matrix-v4.3-g1.0.icc').read_bytes()[4:]),('tag-out-of-range',None)]:
 if data is None:
  b=bytearray((out/'matrix-v4.3-g1.0.icc').read_bytes());struct.pack_into('>I',b,136,0xfffffffc);data=bytes(b)
 path=out/(name+'.icc');path.write_bytes(data);profile=openp(str(path).encode(),b'r');lcmsAccepted=bool(profile)
 lcmsTransformAccepted=False
 if profile:
  destination=xyz();attempt=transform(profile,RGBFLT,destination,XYZDBL,1,0x0100|0x0040|0x0004);lcmsTransformAccepted=bool(attempt)
  if attempt:delete(attempt)
  close(destination);close(profile)
 proc=subprocess.run([str(skexe),str(path),'1','f'],input='0.18 0.18 0.18\n',text=True,capture_output=True,timeout=15);malformed.append({'name':name,'sha256':hashlib.sha256(data).hexdigest(),'lcmsOpenAccepted':lcmsAccepted,'lcmsTransformAccepted':lcmsTransformAccepted,'skcmsExit':proc.returncode,'skcmsError':proc.stderr})
report={'malformed':malformed,'skcmsCommit' :'97adcbb67e98d633a65b29fd405941970f3b476d','skcmsExeSha256':hashlib.sha256(skexe.read_bytes()).hexdigest(),'researchOnly' :True,'LCMSVersion':code,'lcmsDllSha256':hashlib.sha256((root/'lcms/Library/bin/lcms2.dll').read_bytes()).hexdigest(),'argyllSha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'points':points,'records':records,'license':'LCMS MIT; Argyll executable AGPL3 separate local process, not shipped. Source icclib MIT separate license; no product linkage.','calibrationEligible':False}
(out/'comparison.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print('lcms',code,'records',len(records),'compared',sum(r['status']=='COMPARED' for r in records));print([(r['profile'],r['intent'],r['inputType'],r.get('maxAbsInRange'),r['status']) for r in records[:10]])
