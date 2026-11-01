import ctypes as c,json,struct,math,hashlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms
r=Path(__import__('sys').argv[1]);stageDir=__import__('sys').argv[2] if len(__import__('sys').argv)>2 else 'icc-stages';name=__import__('sys').argv[3] if len(__import__('sys').argv)>3 else 'icc-ramps';s=r/stageDir;meta=next(x for x in json.loads((s/'STAGES.json').read_text())if x['name']==name);w,h=meta['width'],meta['height'];out=r/'icc-mechanism';out.mkdir(exist_ok=True);data=np.frombuffer((s/(name+'-native.bgra')).read_bytes(),np.uint8).reshape(h,w,4);rgb=np.ascontiguousarray(data[:,:,:3][:,:,::-1]);wic=np.frombuffer((s/(name+'-icc.bgra')).read_bytes(),np.uint8).reshape(h,w,4)[:,:,:3][:,:,::-1]
m=c.WinDLL('mscms.dll',use_last_error=True)
class PROFILE(c.Structure):_fields_=[('kind',c.c_uint32),('data',c.c_void_p),('size',c.c_uint32)]
m.OpenColorProfileW.argtypes=[c.POINTER(PROFILE),c.c_uint32,c.c_uint32,c.c_uint32];m.OpenColorProfileW.restype=c.c_void_p;m.CreateMultiProfileTransform.argtypes=[c.POINTER(c.c_void_p),c.c_uint32,c.POINTER(c.c_uint32),c.c_uint32,c.c_uint32,c.c_uint32];m.CreateMultiProfileTransform.restype=c.c_void_p;m.TranslateBitmapBits.argtypes=[c.c_void_p,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32,c.c_uint32,c.c_void_p,c.c_uint32,c.c_uint32,c.c_void_p,c.c_void_p];m.TranslateBitmapBits.restype=c.c_bool;m.DeleteColorTransform.argtypes=[c.c_void_p];m.CloseColorProfile.argtypes=[c.c_void_p]
def openp(p):
 buf=c.create_unicode_buffer(str(p));a=PROFILE(1,c.cast(buf,c.c_void_p),c.sizeof(buf));h=m.OpenColorProfileW(c.byref(a),1,1,3);assert h,c.get_last_error();return h
rows=[]
for inp,outp,label in [(name+'-source.icc','wic-target.icc','original'),(name+'-source.icc',name+'-source.icc','source-identity'),('wic-target.icc','wic-target.icc','target-identity')]:
 p,q=openp(s/inp),openp(s/outp);t=m.CreateMultiProfileTransform((c.c_void_p*2)(p,q),2,(c.c_uint32*2)(1,1),2,3,0);assert t
 for bits,fmt in [(8,2),(8,8),(16,10)]:
  a=(data.copy() if fmt==8 else rgb.copy())if bits==8 else(rgb.astype(np.uint16)*257);b=np.zeros_like(a);channels=4 if fmt==8 else 3;assert m.TranslateBitmapBits(t,a.ctypes.data,fmt,w,h,w*channels*(bits//8),b.ctypes.data,fmt,w*channels*(bits//8),None,None)
  if fmt==8:b=b[:,:,:3][:,:,::-1]
  codes=b.astype(float)/(257 if bits==16 else 1);rows.append({'label':label,'precision':bits,'bitmapFormat':fmt,'gray':codes[0,:,0].tolist(),'maxVsInput':float(np.abs(codes-rgb).max()),'maxVsWic':float(np.abs(np.rint(codes)-wic).max())})
 m.DeleteColorTransform(t);m.CloseColorProfile(p);m.CloseColorProfile(q)
# Independent matrix/TRC floating oracle for these two matrix shaper profiles only.
def tags(path):
 b=path.read_bytes();return {b[132+i*12:136+i*12].decode():b[int.from_bytes(b[136+i*12:140+i*12],'big'):int.from_bytes(b[136+i*12:140+i*12],'big')+int.from_bytes(b[140+i*12:144+i*12],'big')]for i in range(int.from_bytes(b[128:132],'big'))}
assert hashlib.sha256((s/(name+'-source.icc')).read_bytes()).hexdigest()=='aca1f6f3a5856485b39f917fdb151cb93da3c117e45f497af426f0ce46eb9596'
assert hashlib.sha256((s/'wic-target.icc').read_bytes()).hexdigest()=='2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e'
x=tags(s/(name+'-source.icc'));y=tags(s/'wic-target.icc');assert x['rTRC'][:4]==b'para' and y['rTRC'][:4]==b'curv'
fixed=lambda b:np.array(struct.unpack('>'+str(len(b)//4)+'i',b))/65536
matrix=lambda t:np.stack([fixed(t[ch+'XYZ'][8:20])for ch in 'rgb'],1)
a,b,c0,d,e=fixed(x['rTRC'][12:]);v=rgb.astype(float)/255;linear=np.where(v>=e,(b*v+c0)**a,d*v);M=np.linalg.inv(matrix(y))@matrix(x);lin=np.einsum('ij,...j->...i',M,linear);table=np.frombuffer(y['rTRC'][12:],'>u2').astype(float)/65535;grid=np.linspace(0,1,len(table));expected=np.stack([np.interp(np.clip(lin[:,:,i],0,1),table,grid)for i in range(3)],2);lcms=np.array(ImageCms.profileToProfile(Image.fromarray(rgb),str(s/(name+'-source.icc')),str(s/'wic-target.icc'),outputMode='RGB'));oracle={'maxVsLcms':float(np.abs(np.rint(expected*255)-lcms).max()),'maxVsWic':float(np.abs(np.rint(expected*255)-wic).max()),'grayExpected':(expected[0,:,0]*255).tolist()};(r/('ICC_MECHANISM.json' if name=='icc-ramps' else 'ICC_MECHANISM_'+name+'.json')).write_text(json.dumps({'windows':rows,'oracle':oracle},indent=2));print([(x['label'],x['precision'],x['maxVsInput'],x['maxVsWic'],x['gray'][:20])for x in rows]);print({k:v for k,v in oracle.items()if k!='grayExpected'})
