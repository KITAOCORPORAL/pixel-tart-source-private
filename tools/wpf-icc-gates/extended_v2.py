"""Explicit research V2: preserve extended linear RGB until a named lossy export sink. Never alters V1."""
import argparse,pathlib,ctypes as C,json,hashlib,sys,struct,zlib,math
R=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps')
import numpy as np,tifffile
from PIL import Image
VERSION='ExtendedLinearClipSinkV2';ENGINE_SHA='2f864b9f1bf5839fbc963265a0c9c94a4709124439e5ab5bfcae5f522483a1c4'
def digest(b):return hashlib.sha256(b).hexdigest()
def transform(x,source,target,lib):
 P=C.c_void_p;U=C.c_uint
 def fn(n,r,a):f=getattr(lib,n);f.restype=r;f.argtypes=a;return f
 op=fn('cmsOpenProfileFromMem',P,[P,U]);close=fn('cmsCloseProfile',C.c_int,[P]);create=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);execute=fn('cmsDoTransform',None,[P,P,P,U]);delete=fn('cmsDeleteTransform',None,[P])
 sb=C.create_string_buffer(source);tb=C.create_string_buffer(target);s=op(sb,len(source));t=op(tb,len(target));fmt=(1<<22)|(4<<16)|(3<<3)|4;tr=create(s,fmt,t,fmt,1,0x144)
 if not s or not t or not tr:raise ValueError('Reference transform refused')
 out=np.empty_like(x)
 try:
  flat=x.reshape(-1,3);dest=out.reshape(-1,3)
  for n in range(0,len(flat),16384):execute(tr,flat[n:].ctypes.data,dest[n:].ctypes.data,min(16384,len(flat)-n))
 finally:delete(tr);close(s);close(t)
 if not np.isfinite(out).all():raise ValueError('Nonfiniteoutput')
 return out
def png16(path,a,icc):
 h,w,_=a.shape
 def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data))
 raw=b''.join(b'\0'+a[y].astype('>u2').tobytes() for y in range(h))
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,16,2,0,0,0))+chunk(b'iCCP',b'Linear sRGB\0\0'+zlib.compress(icc))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
def read_png16(p):
 b=p.read_bytes();off=8;data=b'';icc=None
 while off<len(b):
  n=struct.unpack_from('>I',b,off)[0];t=b[off+4:off+8];v=b[off+8:off+8+n];off+=n+12
  if t==b'IHDR':w,h,depth,typ,_,_,interlace=struct.unpack('>IIBBBBB',v);assert depth==16 and typ==2 and interlace==0
  if t==b'IDAT':data+=v
  if t==b'iCCP':i=v.index(0);icc=zlib.decompress(v[i+2:])
 raw=zlib.decompress(data);rows=[];stride=w*6
 for y in range(h):assert raw[y*(stride+1)]==0;rows.append(np.frombuffer(raw[y*(stride+1)+1:(y+1)*(stride+1)],'>u2').reshape(w,3))
 return np.stack(rows).astype(np.uint16),icc
def run(source,out,dll,work,output,display):
 if out.exists():raise ValueError('Newoutputdirectoryrequired')
 engine=pathlib.Path(dll).read_bytes();assert digest(engine)==ENGINE_SHA;lib=C.CDLL(str(dll));working=work.read_bytes();target=output.read_bytes();display_icc=display.read_bytes()
 with tifffile.TiffFile(source) as tf:raw=tf.asarray();embedded=tf.pages[0].tags[34675].value
 x=raw.astype(np.float32) if raw.dtype.kind=='f' else raw.astype(np.float32)/np.float32(65535 if raw.dtype.itemsize==2 else 255)
 if x.ndim!=3 or x.shape[2]!=3 or not np.isfinite(x).all():raise ValueError('finiteRGBonly')
 recipe=dict(algorithm=VERSION,sourceSha256=digest(source.read_bytes()),inputProfile=digest(embedded),workingProfile=digest(working),outputProfile=digest(target),displayProfile=digest(display_icc),engine=ENGINE_SHA,relativeIntent=1,bpc=False,exposureEv=-1,workingDomain='extendedlinearRGB32f',mapping='ExportClip0To1V2 ONLY after outputlinearconversion',quantization='nearest-even16bit')
 def render(recipe):
  assert recipe['algorithm']==VERSION and recipe['mapping']=='ExportClip0To1V2 ONLY after outputlinearconversion'
  working_values=transform(x,embedded,working,lib);edited=working_values*np.float32(2**recipe['exposureEv']);linear=transform(edited,working,target,lib);mapped=np.clip(linear,0,1);codes=np.rint(mapped*65535).astype(np.uint16);return working_values,linear,codes
 out.mkdir(parents=True);(out/'recipe.json').write_text(json.dumps(recipe,indent=2));saved=json.loads((out/'recipe.json').read_text());assert saved['sourceSha256']==digest(source.read_bytes()) and saved['inputProfile']==digest(embedded) and saved['workingProfile']==digest(working) and saved['outputProfile']==digest(target)
 w,l,c=render(recipe);w2,l2,c2=render(saved);assert np.array_equal(l,l2) and np.array_equal(c,c2)
 tifffile.imwrite(out/'before-map-extended-float.tif',l,photometric='rgb',extratags=[(34675,'B',len(target),target,False)]);tifffile.imwrite(out/'output-linear.tif',c,photometric='rgb',extratags=[(34675,'B',len(target),target,False)]);png16(out/'output-linear.png',c,target)
 # Same mapped linear codes define preview and exports; display is explicit nonlinear sRGB, never rawlinearonscreen.
 linear=c.astype(np.float64)/65535;encoded=np.where(linear<=.0031308,12.92*linear,1.055*np.power(linear,1/2.4)-.055);preview=np.rint(encoded*255).astype(np.uint8);Image.fromarray(preview).save(out/'display.png',icc_profile=display_icc);Image.fromarray(preview).save(out/'display.jpg',quality=100,subsampling=0,icc_profile=display_icc)
 png,pi=read_png16(out/'output-linear.png');assert np.array_equal(png,c) and pi==target;assert np.array_equal(tifffile.imread(out/'output-linear.tif'),c);assert np.array_equal(np.asarray(Image.open(out/'display.png')),preview)
 loss=np.abs(np.asarray(Image.open(out/'display.jpg')).astype(float)-preview.astype(float))/255;mae=float(loss.mean());rmse=float(np.sqrt(np.mean(loss*loss)));psnr=float(-20*np.log10(rmse)) if rmse else None
 assert mae<.02 and (psnr is None or psnr>30)
 stats=lambda a:dict(min=float(a.min()),max=float(a.max()),negative=int(np.count_nonzero(a<0)),aboveOne=int(np.count_nonzero(a>1)),pixelsOutside=int(np.count_nonzero(np.any((a<0)|(a>1),axis=-1))))
 report=dict(recipe=recipe,working=stats(w),beforeMapping=stats(l),pixelsClipped=int(np.count_nonzero(np.any((l<0)|(l>1),axis=-1))),floatPreservedSha256=digest(l.tobytes()),reopenExact=True,TIFF16PNG16PreviewCodesExact=True,jpegMae=mae,jpegPsnr=psnr,CalibrationEligible=False,productAdmission=False)
 (out/'recipe.json').write_text(json.dumps(recipe,indent=2));(out/'result.json').write_text(json.dumps(report,indent=2));return report
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--explicit-research-v2',action='store_true',required=True);p.add_argument('--source',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--lcms',type=pathlib.Path,required=True);p.add_argument('--working',type=pathlib.Path,required=True);p.add_argument('--target',type=pathlib.Path,required=True);p.add_argument('--display',type=pathlib.Path,required=True);a=p.parse_args();print(json.dumps(run(a.source,a.output,a.lcms,a.working,a.target,a.display),indent=2))
