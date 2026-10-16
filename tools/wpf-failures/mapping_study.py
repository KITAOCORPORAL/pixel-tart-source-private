"""Explicit research comparison; candidate is NOT approved perceptual mapping or product code."""
import pathlib,sys,json,ctypes as C,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/wpf-failures';sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps');import numpy as np
p=R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc';w=R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc';L=C.CDLL(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll');P=C.c_void_p;U=C.c_uint
for n,rest,args in [('cmsOpenProfileFromFile',P,[C.c_char_p,C.c_char_p]),('cmsCreateTransform',P,[P,U,P,U,U,U]),('cmsDoTransform',None,[P,P,P,U]),('cmsCloseProfile',C.c_int,[P]),('cmsDeleteTransform',None,[P])]:f=getattr(L,n);f.restype=rest;f.argtypes=args
fmt=(1<<22)|(4<<16)|(3<<3)|4
rows=[];scenes={};v=np.linspace(-.25,2,512);scenes['gray']=np.repeat(v[:,None],3,axis=1);scenes['saturated']=np.array([[1,0,0],[0,1,0],[0,0,1],[1.2,.02,.1],[.1,1.3,.3]],float);scenes['skin']=np.array([[.65+d,.35+d/2,.22+d/3] for d in np.linspace(-.15,.15,256)]);h=np.linspace(.8,4,512);scenes['highlight']=np.stack([h,.9*h,.7*h],axis=1)
for name,x in scenes.items():
 x=np.ascontiguousarray(x,dtype=np.float32);sp=L.cmsOpenProfileFromFile(str(w if name=='saturated' else p).encode(),b'r');tp=L.cmsOpenProfileFromFile(str(p).encode(),b'r');values=[]
 for intent in [1,0]:
  t=L.cmsCreateTransform(sp,fmt,tp,fmt,intent,0x144);assert t;y=np.empty_like(x);L.cmsDoTransform(t,x.ctypes.data,y.ctypes.data,len(x));L.cmsDeleteTransform(t);values.append(y)
 L.cmsCloseProfile(sp);L.cmsCloseProfile(tp);linear=values[0].astype(float);clip=np.clip(linear,0,1);luma=linear@np.array([.2126,.7152,.0722]);anchor=np.clip(luma,0,1);chroma=linear-luma[:,None];scale=np.ones(len(linear))
 for c in range(3):
  d=chroma[:,c];upper=np.divide(1-anchor,d,out=np.full_like(d,np.inf),where=d>0);lower=np.divide(-anchor,d,out=np.full_like(d,np.inf),where=d<0);scale=np.minimum(scale,np.where(d>0,upper,np.where(d<0,lower,np.inf)))
 mapped=anchor[:,None]+np.maximum(0,scale[:,None])*chroma
 if not np.isfinite(mapped).all() or mapped.min() < -1e-12 or mapped.max()>1+1e-12:raise ValueError('candidatebounds')
 metrics=[]
 for mode,a in [('preserveExtended',linear),('ExportClip0To1V2',clip),('LumaRayCompressStudy1_NOT_APPROVED',mapped)]:
  lum=a@np.array([.2126,.7152,.0722]);metrics.append(dict(mode=mode,negativeSamples=int((a<0).sum()),aboveOne=int((a>1).sum()),changedPixels=int(np.count_nonzero(np.any(a!=linear,axis=1))),meanLinearRgbAbsChange=float(np.mean(np.abs(a-linear))),meanLumaChange=float(np.mean(np.abs(lum-luma))),unique16bitLumaLevels=int(len(np.unique(np.rint(lum*65535)))),skinMaxDelta=float(np.max(np.abs(a-linear))) if name=='skin' else None))
 rows.append(dict(scene=name,sourceProfile='linearProPhoto' if name=='saturated' else 'linearsRGB',inputSamples=x.tolist() if len(x)<10 else [x[0].tolist(),x[len(x)//2].tolist(),x[-1].tolist()],lcmsPerceptualVsRelativeMaxAbs=float(np.max(np.abs(values[0]-values[1]))),interpretation='matrix/TRC profile intent choice alone provides no gamutmapping',metrics=metrics,candidateApproved=False))
print(json.dumps(dict(algorithm='LumaRayCompressStudy1_NOT_APPROVED',comparison=rows,notes='Candidate clips luminance explicitly and compresses chroma, loses highlightdetail; not a perceptualqualityapproval. Preservedextendednotdirectlyencodableininteger. No defaultmapping/tolerancechange.',CalibrationEligible=False),indent=2))
