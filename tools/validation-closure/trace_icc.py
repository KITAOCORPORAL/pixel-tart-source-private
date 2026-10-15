"""Read-only trace of fixed prior profiles and independently constructed identity-M variant.
No correction is applied to reference output. All differences are preserved.
"""
import pathlib,ctypes as C,json,struct,hashlib,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[2];OLD=pathlib.Path('D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc');OUT=ROOT/'artifacts/validation-closure/icc-stages';OUT.mkdir(parents=True,exist_ok=True)
L=C.CDLL(str(OLD/'lcms/Library/bin/lcms2.dll'));P=C.c_void_p;U=C.c_uint;F=C.c_float;D=C.c_double

def fn(n,r,a):f=getattr(L,n);f.restype=r;f.argtypes=a;return f
op=fn('cmsOpenProfileFromFile',P,[C.c_char_p,C.c_char_p]);close=fn('cmsCloseProfile',C.c_int,[P]);tag=fn('cmsReadTag',P,[P,U]);first=fn('cmsPipelineGetPtrToFirstStage',P,[P]);nxt=fn('cmsStageNext',P,[P]);styp=fn('cmsStageType',U,[P]);dup=fn('cmsStageDup',P,[P]);alloc=fn('cmsPipelineAlloc',P,[P,U,U]);insert=fn('cmsPipelineInsertStage',C.c_int,[P,C.c_int,P]);evaluate=fn('cmsPipelineEvalFloat',None,[C.POINTER(F),C.POINTER(F),P]);free=fn('cmsPipelineFree',None,[P]);xyz=fn('cmsCreateXYZProfile',P,[]);trans=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);execute=fn('cmsDoTransform',None,[P,P,P,U]);dt=fn('cmsDeleteTransform',None,[P]);adapt=fn('cmsSetAdaptationState',D,[D]);raw=fn('cmsReadRawTag',U,[P,U,P,U]);
prior=json.loads((ROOT/'docs/evidence/company-validation-20261015/icc/COMPARISON.json').read_text(encoding='utf-8'));points=prior['points'];RGBFLT=(1<<22)|(4<<16)|(3<<3)|4;XYZDBL=(1<<22)|(9<<16)|(3<<3)
# Build a valid identity M+matrix representation; normalized stage math unchanged.
def add_identity_m(src):
 b=src.read_bytes();count=struct.unpack_from('>I',b,128)[0];rows=[]
 for i in range(count):
  sig,off,length=struct.unpack_from('>4sII',b,132+12*i);data=bytearray(b[off:off+length])
  if data[:4]==b'mAB ' and struct.unpack_from('>I',data,16)[0]==0:
   while len(data)%4:data.append(0)
   mo=len(data)
   for c in range(3):data+=b'curv'+bytes(8)
   matrix=len(data)
   for r in range(3):
    for c in range(3):data+=struct.pack('>i',65536 if r==c else 0)
   data+=bytes(12);struct.pack_into('>I',data,16,matrix);struct.pack_into('>I',data,20,mo)
  rows.append((sig,data))
 result=bytearray(b[:132+count*12]);cursor=len(result)
 for i,(sig,data) in enumerate(rows):
  while cursor%4:result.append(0);cursor+=1
  struct.pack_into('>4sII',result,132+12*i,sig,cursor,len(data));result+=data;cursor+=len(data)
 struct.pack_into('>I',result,0,len(result));p=OUT/'lut-v4-identity-M.icc';p.write_bytes(result);return p
variant=add_identity_m(OLD/'results/lut-v4.3.icc')
paths=[OLD/'results'/n for n in ['lut-v2.1.icc','lut-v4.3.icc','lut-v4.3-explicit-matrix.icc']]+[variant];records=[]
for p in paths:
 profile=op(str(p).encode(),b'r');assert profile
 b=p.read_bytes();rows={};count=struct.unpack_from('>I',b,128)[0]
 for i in range(count):sig,off,size=struct.unpack_from('>4sII',b,132+12*i);rows[sig.decode()]=(off,size,b[off:off+4].decode(errors='replace'))
 for intent in range(4):
  idx=1 if intent==3 else intent;sig='A2B'+str(idx);pipeline=tag(profile,int.from_bytes(sig.encode(),'big'));assert pipeline
  prefix=alloc(None,3,3);s=first(pipeline);stages=[]
  while s:
   assert insert(prefix,1,dup(s));vals=[]
   for point in points:
    x=(F*3)(*point);y=(F*3)();evaluate(x,y,prefix);vals.append(list(y))
   stages.append({'type':styp(s).to_bytes(4,'big').decode(errors='replace'),'output':vals});s=nxt(s)
  free(prefix);dest=xyz();adapt(0. if intent==3 else 1.);t=trans(profile,RGBFLT,dest,XYZDBL,intent,0x0144);assert t
  values=[]
  for point in points:
   x=(F*3)(*point);y=(D*3)();execute(t,x,y,1);values.append(list(y))
  dt(t);close(dest)
  text='\n'.join(' '.join(map(str,p)) for p in points)+'\n';sk=subprocess.run([str(ROOT/'artifacts/validation-closure/skcms_stages.exe'),str(p),str(idx)],input=text,text=True,capture_output=True,timeout=15)
  skrows=[[float(x) for x in line.split()] for line in sk.stdout.splitlines() if len(line.split())==9]
  arg=subprocess.run([str(OLD/'argyll35/Argyll_V3.5.0/bin/xicclu.exe'),'-v0','-ff','-i'+['p','r','s','a'][intent],'-px',str(p)],input=text,text=True,capture_output=True,timeout=15)
  records.append({'profile':p.name,'sha256':hashlib.sha256(b).hexdigest(),'intent':intent,'tagSelected':sig,'tagOffsetLengthType':rows[sig],'lcmsFlags':'NOOPTIMIZE|NOCACHE|NOWHITEONWHITEFIXUP; requested BPC flag OFF; v4 perceptual/saturation internally forces BPC','adaptation':0 if intent==3 else 1,'input':points,'lcmsRawPrefixStagesNormalized':stages,'lcmsFinalPCSXYZ':values,'skcmsStages_A_CLUT_M_MATRIX_B':skrows,'skcmsParsed':sk.stderr,'skcmsExit':sk.returncode,'argyllExit':arg.returncode,'argyllOutput':arg.stdout,'argyllError':arg.stderr,'comparisonClass':'CONFIGURATION_NOT_EQUIVALENT_FORCED_V4_BPC' if p.name.startswith('lut-v4') and intent in [0,2] else 'ABSOLUTE_UNSUPPORTED_SKCMS' if intent==3 else 'COMPARABLE_RELATIVE_STAGES'})
 close(profile)
(OUT/'stage-trace.json').write_text(json.dumps({'pointsUnchanged':True,'priorComparisonSha256':hashlib.sha256((ROOT/'docs/evidence/company-validation-20261015/icc/COMPARISON.json').read_bytes()).hexdigest(),'records':records,'noOutputCorrection':True,'variant':'Identity M curves and matrix added to profile; no change to normalized mathematical map.'},indent=2),encoding='utf-8')
# Minimal white/gray facts are asserted, not used as post-correction.
r={x['profile']:x for x in records if x['intent']==1};a=r['lut-v4.3.icc'];c=r['lut-v4-identity-M.icc'];print('no M white',a['lcmsRawPrefixStagesNormalized'][-1]['output'][4],a['lcmsFinalPCSXYZ'][4],a['skcmsStages_A_CLUT_M_MATRIX_B'][4]);print('identity M white',c['lcmsRawPrefixStagesNormalized'][-1]['output'][4],c['lcmsFinalPCSXYZ'][4],c['skcmsStages_A_CLUT_M_MATRIX_B'][4]);assert max(abs(x-y) for x,y in zip(a['lcmsFinalPCSXYZ'][4],c['lcmsFinalPCSXYZ'][4]))<1e-6
