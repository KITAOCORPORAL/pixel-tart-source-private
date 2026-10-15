"""Independent direct LCMS invocation against product-written decoded input and recipe."""
import sys,pathlib,ctypes as C,json,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/controlled-icc';run=pathlib.Path(sys.argv[1]);sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps');import numpy as np
L=C.CDLL(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll');P=C.c_void_p;U=C.c_uint
def fn(n,r,a):f=getattr(L,n);f.restype=r;f.argtypes=a;return f
op=fn('cmsOpenProfileFromFile',P,[C.c_char_p,C.c_char_p]);cl=fn('cmsCloseProfile',C.c_int,[P]);tr=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);do=fn('cmsDoTransform',None,[P,P,P,U]);dt=fn('cmsDeleteTransform',None,[P]);fmt=(1<<22)|(4<<16)|(3<<3)|4
meta=json.loads((run/'result.json').read_text());profiles=[A/'linear-srgb.icc',pathlib.Path(sys.argv[2]),A/'linear-srgb.icc'];x=np.fromfile(run/'decoded-input-f32.bin',dtype=np.float32);y=np.empty_like(x)
for j in range(2):
 a=op(str(profiles[j]).encode(),b'r');b=op(str(profiles[j+1]).encode(),b'r');t=tr(a,fmt,b,fmt,1,0x144);assert t;do(t,x.ctypes.data,y.ctypes.data,len(x)//3);dt(t);cl(a);cl(b);x=y.copy();y=np.empty_like(x)
 if j==0:x*=np.float32(.5)
product=np.fromfile(run/'preview-output-f32.bin',dtype=np.float32);d=np.abs(x-product)
print(json.dumps(dict(directLcmsVsAdapterMaxAbs=float(d.max()),meanAbs=float(d.mean()),samples=len(x),exact=bool(np.array_equal(x,product)),inputPhoto=meta['sourceHash'],workingProfile=meta['workingProfile'],calibrationEligible=False),indent=2));assert np.array_equal(x,product)
