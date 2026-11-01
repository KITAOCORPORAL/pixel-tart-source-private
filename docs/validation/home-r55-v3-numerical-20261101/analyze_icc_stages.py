import ctypes as c,json,hashlib,io
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms
r=Path(__import__('sys').argv[1]);s=r/(__import__('sys').argv[2] if len(__import__('sys').argv)>2 else 'stages');out=r/(s.name+'-icc-analysis');out.mkdir(exist_ok=True)
def err(a,b):
 d=np.abs(a.astype(np.int32)-b.astype(np.int32));return {'max':int(d.max()),'mean':float(d.mean()),'p95':float(np.quantile(d,.95)),'exactFraction':float(np.mean(d==0))}
# System native API independent of WPF wrapper; same exact ICC files.
m=c.WinDLL('mscms.dll',use_last_error=True)
class PROFILE(c.Structure):_fields_=[('kind',c.c_uint32),('data',c.c_void_p),('size',c.c_uint32)]
m.OpenColorProfileW.argtypes=[c.POINTER(PROFILE),c.c_uint32,c.c_uint32,c.c_uint32];m.OpenColorProfileW.restype=c.c_void_p
m.CreateMultiProfileTransform.argtypes=[c.POINTER(c.c_void_p),c.c_uint32,c.POINTER(c.c_uint32),c.c_uint32,c.c_uint32,c.c_uint32];m.CreateMultiProfileTransform.restype=c.c_void_p
m.TranslateBitmapBits.argtypes=[c.c_void_p,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32,c.c_uint32,c.c_void_p,c.c_uint32,c.c_uint32,c.c_void_p,c.c_void_p];m.TranslateBitmapBits.restype=c.c_bool
m.DeleteColorTransform.argtypes=[c.c_void_p];m.CloseColorProfile.argtypes=[c.c_void_p]
def profile(path):
 data=c.create_unicode_buffer(str(path));p=PROFILE(1,c.cast(data,c.c_void_p),c.sizeof(data));h=m.OpenColorProfileW(c.byref(p),1,1,3);assert h,c.get_last_error();return h
rows=[]
for stage in json.loads((s/'STAGES.json').read_text()):
 name=stage['name'];src=s/(name+'-source.icc')
 if not src.exists():continue
 w,h=stage['width'],stage['height'];a=np.frombuffer((s/(name+'-native.bgra')).read_bytes(),np.uint8).reshape(h,w,4);actual=np.frombuffer((s/(name+'-icc.bgra')).read_bytes(),np.uint8).reshape(h,w,4);rgb=np.ascontiguousarray(a[:,:,:3][:,:,::-1]);wic=actual[:,:,:3][:,:,::-1]
 row={'name':name,'sourceICC':hashlib.sha256(src.read_bytes()).hexdigest(),'targetICC':hashlib.sha256((s/'wic-target.icc').read_bytes()).hexdigest(),'lcms':[],'mscms':[]}
 for intent in range(4):
  for flags in [0,8192,256,16384,8192|256,8192|16384,1024,2048,8192|1024,8192|2048]:
   ref=np.asarray(ImageCms.profileToProfile(Image.fromarray(rgb),str(src),str(s/'wic-target.icc'),renderingIntent=intent,outputMode='RGB',flags=flags));row['lcms'].append({'intent':intent,'flags':flags,**err(ref,wic)})
 sh,dh=profile(src),profile(s/'wic-target.icc');handles=(c.c_void_p*2)(sh,dh)
 try:
  for intent in range(4):
   intents=(c.c_uint32*2)(intent,intent)
   for flags in [1,2,3,3|0x20000,3|0x80000000]:
    transform=m.CreateMultiProfileTransform(handles,2,intents,2,flags,0)
    if not transform:row['mscms'].append({'intent':intent,'flags':flags,'error':c.get_last_error()});continue
    try:
     source=np.ascontiguousarray(a);dest=np.empty_like(source)
     ok=m.TranslateBitmapBits(transform,source.ctypes.data,0x8,w,h,w*4,dest.ctypes.data,0x8,w*4,None,None)
     row['mscms'].append({'intent':intent,'flags':flags,'ok':bool(ok),**err(dest[:,:,:3],actual[:,:,:3])} if ok else {'intent':intent,'flags':flags,'error':c.get_last_error()})
    finally:m.DeleteColorTransform(transform)
 finally:m.CloseColorProfile(sh);m.CloseColorProfile(dh)
 # Exact geometric stage, with no ICC or resampling hidden in it.
 oriented=np.frombuffer((s/(name+'-oriented.bgra')).read_bytes(),np.uint8).reshape(stage['orientedHeight'],stage['orientedWidth'],4)
 expected=np.rot90(actual,-1) if stage['orientation']==6 else actual
 row['orientationOnly']=err(expected,oriented);rows.append(row)
(out/'ICC_STAGES.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))
