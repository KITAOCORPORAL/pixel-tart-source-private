"""Photo-only exact-profile workflow candidate; no native library distribution or normal UI enable."""
import pathlib,sys,ctypes as C,json,hashlib,struct,zlib,argparse
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/photo-engine';sys.path.insert(0,str(A/'python-deps'));import numpy as np,tifffile
from PIL import Image
ALGORITHM='PhotoSrgbLinear1';INPUTS={'43a86e39836b4f56029aae55090f2b73c1577d74d3394fc4f395d3e35f70b653','17aebbdf8a88c39b07eb881dcd824eb1cf9828914d5e74a7324d7a31045e8871'};ENGINE='2f864b9f1bf5839fbc963265a0c9c94a4709124439e5ab5bfcae5f522483a1c4'
def sha(b):return hashlib.sha256(b).hexdigest()
def profile(b,allowed):
 if not b:raise ValueError('ICC_MISSING')
 if len(b)<132 or len(b)>4*1024*1024 or struct.unpack_from('>I',b)[0]!=len(b) or b[36:40]!=b'acsp':raise ValueError('ICC_LENGTH_OR_SIGNATURE')
 if b[16:20]!=b'RGB ' or b[20:24]!=b'XYZ ' or struct.unpack_from('>I',b,64)[0]>3:raise ValueError('ICC_SPACE_OR_INTENT')
 n=struct.unpack_from('>I',b,128)[0]
 if n>128 or 132+n*12>len(b):raise ValueError('ICC_TAG_TABLE')
 names=set()
 for i in range(n):
  sig,o,c=struct.unpack_from('>4sII',b,132+i*12)
  if sig in names or o%4 or o<132+n*12 or c<8 or o+c>len(b):raise ValueError('ICC_TAG_BOUNDS_OR_DUPLICATE')
  names.add(sig)
 if sha(b) not in allowed:raise ValueError('ICC_NOT_EVALUATED')
def convert(x,source,target,dll,integer=False):
 P=C.c_void_p;U=C.c_uint
 for n,r,a in [('cmsOpenProfileFromMem',P,[P,U]),('cmsCloseProfile',C.c_int,[P]),('cmsCreateTransform',P,[P,U,P,U,U,U]),('cmsDoTransform',None,[P,P,P,U]),('cmsDeleteTransform',None,[P])]:f=getattr(dll,n);f.restype=r;f.argtypes=a
 sb=C.create_string_buffer(source);tb=C.create_string_buffer(target);s=dll.cmsOpenProfileFromMem(sb,len(source));t=dll.cmsOpenProfileFromMem(tb,len(target));f=(1<<22)|(4<<16)|(3<<3)|4;outformat=(4<<16)|(3<<3)|2 if integer else f;tr=dll.cmsCreateTransform(s,f,t,outformat,1,0x144)
 if not s or not t or not tr:raise ValueError('LCMS_REFUSED')
 out=np.empty(x.shape,np.uint16 if integer else np.float32);xf=x.reshape(-1,3);of=out.reshape(-1,3)
 try:
  for n in range(0,len(xf),32768):dll.cmsDoTransform(tr,xf[n:].ctypes.data,of[n:].ctypes.data,min(32768,len(xf)-n))
 finally:dll.cmsDeleteTransform(tr);dll.cmsCloseProfile(t);dll.cmsCloseProfile(s)
 if not np.isfinite(out).all():raise ValueError('NONFINITE')
 return out
def png16(p,a,icc):
 def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b))
 h,w,_=a.shape;raw=b''.join(b'\0'+row.astype('>u2').tobytes() for row in a);p.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,16,2,0,0,0))+chunk(b'iCCP',b'sRGB\0\0'+zlib.compress(icc))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
def readpng16(p):
 b=p.read_bytes();o=8;data=b'';icc=None
 while o<len(b):
  n=struct.unpack_from('>I',b,o)[0];t=b[o+4:o+8];v=b[o+8:o+8+n];o+=12+n
  if t==b'IHDR':w,h,depth,color,_,_,interlace=struct.unpack('>IIBBBBB',v);assert depth==16 and color==2 and interlace==0
  if t==b'iCCP':i=v.index(0);icc=zlib.decompress(v[i+2:])
  if t==b'IDAT':data+=v
 raw=zlib.decompress(data);stride=w*6;rows=[]
 for y in range(h):
  assert raw[y*(stride+1)]==0;rows.append(np.frombuffer(raw[y*(stride+1)+1:(y+1)*(stride+1)],'>u2').reshape(w,3))
 return np.stack(rows).astype(np.uint16),icc
def run(source,out,dllpath,outputprofile,ev=-.5):
 if out.exists():raise ValueError('OUTPUT_EXISTS')
 if not -3<=ev<=0:raise ValueError('EV_RANGE')
 if sha(dllpath.read_bytes())!=ENGINE:raise ValueError('ENGINE_HASH')
 dll=C.CDLL(str(dllpath));target=outputprofile.read_bytes();profile(target,{'43a86e39836b4f56029aae55090f2b73c1577d74d3394fc4f395d3e35f70b653'});linear=(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc').read_bytes();profile(linear,{'dafbff7207d22b526baa8f61a280e197b639af68cbe2e48529455157612b7407'})
 if source.suffix.lower() in ['.tif','.tiff']:
  with tifffile.TiffFile(source) as tf:
   if len(tf.pages)!=1:raise ValueError('MULTIPAGE_REFUSED')
   page=tf.pages[0];orientation=page.tags.get(274);orientation=orientation.value if orientation else 1
   if orientation!=1:raise ValueError('ORIENTATION_UNSUPPORTED')
   raw=page.asarray();embedded=page.tags[34675].value if 34675 in page.tags else None
  if raw.dtype!=np.uint16 or raw.ndim!=3 or raw.shape[-1]!=3:raise ValueError('RGB16_TIFF_ONLY')
  x=np.ascontiguousarray(raw.astype(np.float32)/65535)
 elif source.suffix.lower() in ['.jpg','.jpeg']:
  im=Image.open(source)
  if im.mode!='RGB' or im.getexif().get(274,1)!=1:raise ValueError('RGB_JPEG_OR_ORIENTATION')
  embedded=im.info.get('icc_profile');raw=np.asarray(im);x=np.ascontiguousarray(raw.astype(np.float32)/255)
 else:raise ValueError('PHOTO_CONTAINER_UNSUPPORTED')
 profile(embedded,INPUTS);sourcehash=sha(source.read_bytes());recipe=dict(algorithm=ALGORITHM,sourceSha256=sourcehash,inputProfile=sha(embedded),workingProfile=sha(linear),outputProfile=sha(target),engine=ENGINE,intent=1,BPC=False,adaptation=1,EV=ev,workingDomain='extendedlinear-sRGBD65/PCSXYZD50',outputSink='PhotoSrgbRelativeIntegerSink1 explicitLCMSuint16saturation',display='encodedsRGBfromsameoutputcodes')
 def render(r):
  if r!=recipe:raise ValueError('RECIPE_IDENTITY')
  work=convert(x,embedded,linear,dll);edit=work*np.float32(2**r['EV']);encoded=convert(edit,linear,target,dll);codes=convert(edit,linear,target,dll,True);return work,edit,encoded,codes
 work,edit,encoded,codes=render(recipe);out.mkdir(parents=True);(out/'recipe.json').write_text(json.dumps(recipe,indent=2));reopened=json.loads((out/'recipe.json').read_text());w2,e2,o2,c2=render(reopened);assert sha(source.read_bytes())==sourcehash and np.array_equal(codes,c2) and np.array_equal(encoded,o2)
 tifffile.imwrite(out/'working-extended.tif',edit,photometric='rgb',extratags=[(34675,'B',len(linear),linear,False)]);tifffile.imwrite(out/'output.tif',codes,photometric='rgb',extratags=[(34675,'B',len(target),target,False)]);png16(out/'output.png',codes,target);preview=np.rint(codes.astype(np.float64)/257).astype(np.uint8);Image.fromarray(preview).save(out/'preview.png',icc_profile=target);Image.fromarray(preview).save(out/'output.jpg',quality=100,subsampling=0,icc_profile=target)
 pr,pi=readpng16(out/'output.png');assert np.array_equal(pr,codes) and pi==target
 with tifffile.TiffFile(out/'output.tif') as tf:assert np.array_equal(tf.asarray(),codes) and tf.pages[0].tags[34675].value==target
 for p in ['preview.png','output.jpg']:assert Image.open(out/p).info['icc_profile']==target
 assert np.array_equal(np.asarray(Image.open(out/'preview.png')),preview);loss=np.abs(np.asarray(Image.open(out/'output.jpg')).astype(float)-preview.astype(float))/255;rmse=float(np.sqrt(np.mean(loss*loss)));mae=float(loss.mean());psnr=-20*np.log10(rmse) if rmse else None
 if mae>=.02 or (psnr is not None and psnr<=30):raise ValueError('JPEG_METRICS')
 stats=lambda a:dict(min=float(a.min()),max=float(a.max()),negativeSamples=int((a<0).sum()),aboveOneSamples=int((a>1).sum()))
 report=dict(recipe=recipe,width=int(codes.shape[1]),height=int(codes.shape[0]),inputPrecision='16bit' if raw.dtype==np.uint16 else 'JPEG8',working=stats(work),edited=stats(edit),beforeIntegerSink=stats(encoded),sinkPixelsOutside=int(np.count_nonzero(np.any((encoded<0)|(encoded>1),axis=-1))),codesSha256=sha(codes.tobytes()),previewReopenCodesExact=True,TIFF16PNG16CodesExact=True,JpegMae=mae,JpegPsnr=psnr,CalibrationEligible=False,distributionApproval=False)
 (out/'result.json').write_text(json.dumps(report,indent=2));return report
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--explicit-photo-candidate',action='store_true',required=True);p.add_argument('--source',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--lcms',type=pathlib.Path,required=True);p.add_argument('--output-profile',type=pathlib.Path,required=True);p.add_argument('--ev',type=float,default=-.5);a=p.parse_args()
 try:print(json.dumps(run(a.source,a.output,a.lcms,a.output_profile,a.ev),indent=2))
 except Exception as e:print(json.dumps(dict(status='REFUSED',reason=str(e))),file=sys.stderr);raise SystemExit(2)
