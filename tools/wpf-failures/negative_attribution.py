"""Attribute frozen Canon roundtrip negatives using raw ICC fixed matrices and two reference engines. No tolerance modification."""
import pathlib,struct,ctypes as C,hashlib,json,sys,subprocess
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/wpf-failures';sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps');import numpy as np,tifffile
source=pathlib.Path(r'D:/AI AGENT/kitao-controlled-icc-20261015/artifacts/controlled-icc/canon-linear.tif');s=(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc').read_bytes();w=(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc').read_bytes()
def matrix(b):
 d={}
 for i in range(struct.unpack_from('>I',b,128)[0]):
  sig,off,n=struct.unpack_from('>4sII',b,132+i*12);d[sig]=(off,n)
 return np.array([[struct.unpack_from('>i',b,d[name][0]+8+j*4)[0]/65536 for name in [b'rXYZ',b'gXYZ',b'bXYZ']] for j in range(3)],np.float64)
ms,mw=matrix(s),matrix(w);toWork=np.linalg.solve(mw,ms);toOutput=np.linalg.solve(ms,mw);roundtrip=toOutput@toWork
L=C.CDLL(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll');P=C.c_void_p;U=C.c_uint
for n,rest,args in [('cmsOpenProfileFromMem',P,[P,U]),('cmsCreateTransform',P,[P,U,P,U,U,U]),('cmsDoTransform',None,[P,P,P,U]),('cmsDeleteTransform',None,[P]),('cmsCloseProfile',C.c_int,[P])]:f=getattr(L,n);f.restype=rest;f.argtypes=args
fmt=(1<<22)|(4<<16)|(3<<3)|4
handles=[]
def tr(a,b):
 ab=C.create_string_buffer(a);bb=C.create_string_buffer(b);pa=L.cmsOpenProfileFromMem(ab,len(a));pb=L.cmsOpenProfileFromMem(bb,len(b));t=L.cmsCreateTransform(pa,fmt,pb,fmt,1,0x144);assert t;handles.append((t,pa,pb));return t
t1=tr(s,w);t2=tr(w,s);tDirect=tr(s,s);raw=tifffile.imread(source);total=0;negative=0;zeroOrigins=0;positiveOrigins=0;minValue=0.;maxError=0.;maxDirectError=0.;maxDoubleError=0.;maxDoubleNegative=0.;examples=[]
for start in range(0,raw.shape[0],32):
 x=np.ascontiguousarray(raw[start:start+32].astype(np.float32)/65535);work=np.empty_like(x);out=np.empty_like(x);direct=np.empty_like(x);L.cmsDoTransform(t1,x.ctypes.data,work.ctypes.data,x.size//3);edited=work*np.float32(.5);L.cmsDoTransform(t2,edited.ctypes.data,out.ctypes.data,x.size//3);L.cmsDoTransform(tDirect,x.ctypes.data,direct.ctypes.data,x.size//3)
 xd=x.astype(np.float64);wd=xd@toWork.T;od=(wd*.5)@toOutput.T;expected=xd*.5;neg=out<0;negative+=int(neg.sum());zeroOrigins+=int(np.count_nonzero(neg&(x==0)));positiveOrigins+=int(np.count_nonzero(neg&(x>0)));total+=out.size;minValue=min(minValue,float(out.min()));maxError=max(maxError,float(np.abs(out-expected).max()));maxDirectError=max(maxDirectError,float(np.abs(direct-x).max()));maxDoubleError=max(maxDoubleError,float(np.abs(od-expected).max()));maxDoubleNegative=min(maxDoubleNegative,float(od.min()))
 if len(examples)<12:
  for y,xx,c in np.argwhere(neg)[:12-len(examples)]:examples.append(dict(y=int(y+start),x=int(xx),channel=int(c),inputRGB=x[y,xx].tolist(),work=work[y,xx].tolist(),lcmsOutput=out[y,xx].tolist(),doubleMatrixOutput=od[y,xx].tolist()))
for t,pa,pb in handles:L.cmsDeleteTransform(t);L.cmsCloseProfile(pa);L.cmsCloseProfile(pb)
# Meaningful outgamut examples: ProPhoto saturated coordinates mapped into sRGB in double.
real=np.array([[1,0,0],[0,1,0],[0,0,1],[1.5,.2,.1],[.65,.35,.22]],np.float64);realout=real@toOutput.T
report=dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),sourceProfileSha256=hashlib.sha256(s).hexdigest(),workingProfileSha256=hashlib.sha256(w).hexdigest(),intent=1,bpc=False,flags='0x144',samples=total,negativeSamples=negative,negativeFromExactlyZeroInputChannel=zeroOrigins,negativeFromPositiveInputChannel=positiveOrigins,minOutput=minValue,maxRoundtripErrorVsExactHalf=maxError,maxIdentityDirectError=maxDirectError,doubleRoundtripMaxError=maxDoubleError,doubleRoundtripMin=maxDoubleNegative,roundtripMatrix=roundtrip.tolist(),unitRoundoffFloat32=2**-24,one16bitCode=1/65535,examples=examples,meaningfulOutgamut=[dict(input=p.tolist(),output=o.tolist()) for p,o in zip(real,realout)],scope='specificsameprimariesmatrixroundtrip/-1EV; NOT arbitraryprofiles/toleranceapproval',V1Unchanged=True)
points=[e['inputRGB'] for e in examples]+[[1,0,0],[0,1,0],[0,0,1],[1.5,-.1,.2]]
sp=R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc';wp=R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc'
def skrun(points,src,dst):
 p=subprocess.run([str(A/'extended_refs.exe'),str(src),str(dst)],input='\n'.join(' '.join(map(str,x)) for x in points)+'\n',text=True,capture_output=True,timeout=30);assert p.returncode==0;return [[float(v) for v in row.split()[:3]] for row in p.stdout.splitlines()]
skwork=skrun(points,sp,wp);skout=skrun([[v*.5 for v in row] for row in skwork],wp,sp)
report['skcmsCommit']='97adcbb67e98d633a65b29fd405941970f3b476d';report['skcmsRoundtripSamples']=[dict(input=p,working=w,output=o) for p,w,o in zip(points,skwork,skout)];report['referenceExeSha256']=hashlib.sha256((A/'extended_refs.exe').read_bytes()).hexdigest()
(A/'negative-attribution.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
