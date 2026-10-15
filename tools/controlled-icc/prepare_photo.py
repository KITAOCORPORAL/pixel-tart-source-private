"""Authorized photographic derivative. Never assign a linear profile to encoded pixels."""
import sys,pathlib,json,ctypes as C,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/controlled-icc';A.mkdir(exist_ok=True)
sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps')
import tifffile,numpy as np
old=pathlib.Path(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc');L=C.CDLL(str(old/'lcms/Library/bin/lcms2.dll'));P=C.c_void_p;U=C.c_uint;D=C.c_double

def fn(n,r,a):f=getattr(L,n);f.restype=r;f.argtypes=a;return f
op=fn('cmsOpenProfileFromMem',P,[P,U]);cls=fn('cmsCloseProfile',C.c_int,[P]);tr=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);do=fn('cmsDoTransform',None,[P,P,P,U]);delete=fn('cmsDeleteTransform',None,[P]);gamma=fn('cmsBuildGamma',P,[P,D]);rgb=fn('cmsCreateRGBProfile',P,[P,P,P]);save=fn('cmsSaveProfileToFile',C.c_int,[P,C.c_char_p]);ver=fn('cmsSetProfileVersion',None,[P,D])
class xy(C.Structure):_fields_=[('x',D),('y',D),('Y',D)]
class prim(C.Structure):_fields_=[('r',xy),('g',xy),('b',xy)]
w=xy(.3127,.329,1);p=prim(xy(.64,.33,1),xy(.30,.60,1),xy(.15,.06,1));curves=(P*3)(*[gamma(None,1) for _ in range(3)]);p1=rgb(C.byref(w),C.byref(p),curves);ver(p1,2.1);target=A/'linear-srgb.icc';assert save(p1,str(target).encode());cls(p1)
b=bytearray(target.read_bytes());import struct;struct.pack_into('>6H',b,24,2026,10,15,0,0,0);b[84:100]=bytes(16);target.write_bytes(b)
# Broad linear ProPhoto working profile for non-identity transform evidence.
w2=xy(.3457,.3585,1);p2=prim(xy(.7347,.2653,1),xy(.1596,.8404,1),xy(.0366,.0001,1));c2=(P*3)(*[gamma(None,1) for _ in range(3)]);pp=rgb(C.byref(w2),C.byref(p2),c2);ver(pp,2.1);wp=A/'linear-prophoto.icc';assert save(pp,str(wp).encode());cls(pp);wb=bytearray(wp.read_bytes());struct.pack_into('>6H',wb,24,2026,10,15,0,0,0);wb[84:100]=bytes(16);wp.write_bytes(wb)
source=pathlib.Path(r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/raw/TNAN8886.CR3.configured.rt.tif')
with tifffile.TiffFile(source) as t: image=t.asarray();input_icc=t.pages[0].tags[34675].value
src=C.create_string_buffer(input_icc);dst=C.create_string_buffer(bytes(b));ps=op(src,len(input_icc));pd=op(dst,len(b));assert ps and pd
fmt=(1<<22)|(4<<16)|(3<<3)|4;tt=tr(ps,fmt,pd,fmt,1,0x144);assert tt
x=(image.astype(np.float32)/65535).copy();y=np.empty_like(x);do(tt,x.ctypes.data,y.ctypes.data,image.shape[0]*image.shape[1]);delete(tt);cls(ps);cls(pd)
assert np.isfinite(y).all();clipped=np.count_nonzero((y<0)|(y>1));q=np.rint(np.clip(y,0,1)*65535).astype(np.uint16)
photo=A/'canon-linear.tif';tifffile.imwrite(photo,q,photometric='rgb',compression=None,extratags=[(34675,'B',len(b),bytes(b),False)])
report=dict(derivativeOf='authorized Canon TNAN8886.CR3',originalSha256='b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401',renderedSource=str(source),renderedSourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),sourceEmbeddedIccSha256=hashlib.sha256(input_icc).hexdigest(),targetProfileSha256=hashlib.sha256(b).hexdigest(),photoSha256=hashlib.sha256(photo.read_bytes()).hexdigest(),preparation='direct LCMS relative intent1 flags0x144; source RTv4_sRGB actual embedded to linear-sRGB; full resolution; explicit uint16 nearest-even quantization',clippedSamples=int(clipped),size=list(q.shape),noSourceModified=True)
(A/'photo-preparation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))

# Separate explicit photographic stress fixture with headroom avoids claiming tiny out-of-gamut roundtrip is in scope.
headroom=np.rint((q.astype(np.float64)/65535*.8+.05)*65535).astype(np.uint16);tifffile.imwrite(A/'canon-linear-headroom.tif',headroom,photometric='rgb',compression=None,extratags=[(34675,'B',len(b),bytes(b),False)])
report['headroomFixture']={'file':'canon-linear-headroom.tif','operation':'linear .8*x+.05 before fixture input; not an output correction or decoder accuracy test','sha256':hashlib.sha256((A/'canon-linear-headroom.tif').read_bytes()).hexdigest()};(A/'photo-preparation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
