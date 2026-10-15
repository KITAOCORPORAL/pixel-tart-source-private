"""Read-only authorized photographic domain audit. No output correction or source rewrite."""
import sys,pathlib,ctypes as C,json,hashlib,struct
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/wpf-icc-gates';sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps');import numpy as np,tifffile
old=pathlib.Path(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc');L=C.CDLL(str(old/'lcms/Library/bin/lcms2.dll'));P=C.c_void_p;U=C.c_uint
def fn(n,r,a):f=getattr(L,n);f.restype=r;f.argtypes=a;return f
op=fn('cmsOpenProfileFromMem',P,[P,U]);cl=fn('cmsCloseProfile',C.c_int,[P]);tr=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);do=fn('cmsDoTransform',None,[P,P,P,U]);dt=fn('cmsDeleteTransform',None,[P]);fmt=(1<<22)|(4<<16)|(3<<3)|4
profiles=[(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc').read_bytes(),(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc').read_bytes()];work=profiles[1];allowed=set(hashlib.sha256(p).hexdigest() for p in profiles)
def convert(x,s,t):
 sb=C.create_string_buffer(s);tb=C.create_string_buffer(t);sp=op(sb,len(s));tp=op(tb,len(t));tt=tr(sp,fmt,tp,fmt,1,0x144);assert tt
 out=np.empty_like(x);do(tt,x.ctypes.data,out.ctypes.data,x.size//3);dt(tt);cl(tp);cl(sp);return out
paths=[pathlib.Path(r'D:/AI AGENT/kitao-controlled-icc-20261015/artifacts/controlled-icc/raw')/(n+'.rt-true.tif') for n in ['TNAN8886.CR3','DSCF0347.RAF','DSC09916.ARW']]+[pathlib.Path(r'D:/AI AGENT/kitao-controlled-icc-20261015/artifacts/controlled-icc')/n for n in ['canon-linear.tif','canon-linear-headroom.tif']]
rows=[]
for p in paths:
 with tifffile.TiffFile(p) as tf:a=tf.asarray();src=tf.pages[0].tags[34675].value
 x=a.astype(np.float32)/np.float32(65535);w=convert(x,src,work);o=convert(w*np.float32(.5),work,profiles[0]);pixel=lambda z:int(np.count_nonzero(np.any((z<0)|(z>1)|~np.isfinite(z),axis=-1)))
 ranges=lambda z:dict(min=float(z.min()),max=float(z.max()),negativeSamples=int(np.count_nonzero(z<0)),aboveOneSamples=int(np.count_nonzero(z>1)),pixelsOutside=pixel(z))
 sourceHash=hashlib.sha256(src).hexdigest();approved=sourceHash in allowed
 rows.append(dict(file=p.name,fileSha256=hashlib.sha256(p.read_bytes()).hexdigest(),inputProfileSha256=sourceHash,shape=list(a.shape),inputApprovedSubset=approved,actualV1RejectionStage='PROFILE_ADMISSION' if not approved else 'WORKING_DOMAIN' if pixel(w) else 'OUTPUT_DOMAIN' if pixel(o) else 'PASS',working=ranges(w),outputBeforeAnyMapping=ranges(o),extendedProbeAuthority='directLCMSdiagnosticonly; notV1admission; original16bitcodefileunaltered',editEv=-1))
print(json.dumps(dict(photos=rows,approvedProfileInputs=sum(r['inputApprovedSubset'] for r in rows),passedV1=sum(r['actualV1RejectionStage']=='PASS' for r in rows),total=len(rows),calibrationEligible=False),indent=2))
