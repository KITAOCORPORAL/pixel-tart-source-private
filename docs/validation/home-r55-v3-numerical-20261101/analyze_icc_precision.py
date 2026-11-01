import ctypes as c,json,hashlib
from pathlib import Path
import numpy as np
r=Path(__import__('sys').argv[1]);s=r/'icc-stages';a=np.frombuffer((s/'icc-ramps-native.bgra').read_bytes(),np.uint8).reshape(8,256,4);wic=np.frombuffer((s/'icc-ramps-icc.bgra').read_bytes(),np.uint8).reshape(8,256,4)[:,:,:3][:,:,::-1];rgb=np.ascontiguousarray(a[:,:,:3][:,:,::-1]);rows=[]
def err(x,y):
 d=np.abs(x.astype(float)-y.astype(float));return {'max':float(d.max()),'mean':float(d.mean()),'p95':float(np.quantile(d,.95))}
lib=Path('N:/pixart/Guangjing-R55-CanvasPhoto-20261101/Guangjing-R55-CanvasPhoto-Diagnostic-win-x64/lcms2.dll');m=c.CDLL(str(lib));m.cmsOpenProfileFromMem.argtypes=[c.c_void_p,c.c_uint32];m.cmsOpenProfileFromMem.restype=c.c_void_p;m.cmsCreateTransform.argtypes=[c.c_void_p,c.c_uint32,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32];m.cmsCreateTransform.restype=c.c_void_p;m.cmsDoTransform.argtypes=[c.c_void_p,c.c_void_p,c.c_void_p,c.c_uint32];m.cmsDeleteTransform.argtypes=[c.c_void_p];m.cmsCloseProfile.argtypes=[c.c_void_p];m.cmsSetAdaptationState.argtypes=[c.c_double];m.cmsSetAdaptationState.restype=c.c_double
b1=(s/'icc-ramps-source.icc').read_bytes();b2=(s/'wic-target.icc').read_bytes();p=m.cmsOpenProfileFromMem(b1,len(b1));q=m.cmsOpenProfileFromMem(b2,len(b2));assert p and q
saved={}
for intent in [0,1,3]:
 for adaptation in [0.,1.]:
  m.cmsSetAdaptationState(adaptation)
  for flags in [0x140,0x140|0x2000]:
   for typ,label,dtype,scale in [((4<<16)|(3<<3)|1,'u8',np.uint8,255),((4<<16)|(3<<3)|2,'u16',np.uint16,65535),((1<<22)|(4<<16)|(3<<3)|4,'float',np.float32,1)]:
    x=np.ascontiguousarray(rgb.astype(dtype)if label=='u8' else(rgb.astype(np.float64)/255*scale).astype(dtype));y=np.zeros_like(x);t=m.cmsCreateTransform(p,typ,q,typ,intent,flags);assert t;m.cmsDoTransform(t,x.ctypes.data,y.ctypes.data,2048);m.cmsDeleteTransform(t);codes=y.astype(float)/scale*255
    if intent==1 and adaptation==1 and flags==0x140:saved[label]=codes.tolist()
    rows.append({'intent':intent,'adaptation':adaptation,'flags':flags,'precision':label,'vsWic':err(np.rint(codes),wic),'gray16':codes[0,:24,0].tolist()});
m.cmsCloseProfile(p);m.cmsCloseProfile(q)
(r/'ICC_PRECISION.json').write_text(json.dumps({'librarySha256':hashlib.sha256(lib.read_bytes()).hexdigest(),'version':m.cmsGetEncodedCMMversion(),'rows':rows,'outputsForQuantizationAudit':saved},indent=2));print(json.dumps(rows,indent=2))
