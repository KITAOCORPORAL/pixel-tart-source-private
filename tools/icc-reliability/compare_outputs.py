import pathlib,sys,json,hashlib
sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps')
import tifffile,numpy as np
from PIL import Image,ImageCms
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/icc-reliability';old=A/'photo-old';new=A/'photo-bounded';rows=[]
for ext in ['tif','png']:
 def read(p):
  if ext=='tif':return tifffile.imread(p)
  # WIC-produced PNG16 via ImageMagick unavailable; parse PNG zlib filters separately.
  import struct,zlib
  b=p.read_bytes();off=8;data=b''
  while off<len(b):
   n=struct.unpack_from('>I',b,off)[0];tag=b[off+4:off+8];v=b[off+8:off+8+n];off+=12+n
   if tag==b'IHDR':w,h,depth,typ,_,_,interlace=struct.unpack('>IIBBBBB',v);assert depth==16 and typ==2 and interlace==0
   if tag==b'IDAT':data+=v
  raw=zlib.decompress(data);stride=w*6;out=np.empty((h,stride),np.uint8);prev=np.zeros(stride,np.uint8);cursor=0
  for y in range(h):
   f=raw[cursor];cur=np.frombuffer(raw[cursor+1:cursor+1+stride],np.uint8).copy();cursor+=1+stride
   for x in range(stride):
    a=int(cur[x-6]) if x>=6 else 0;b=int(prev[x]);c=int(prev[x-6]) if x>=6 else 0
    if f==1:v=a
    elif f==2:v=b
    elif f==3:v=(a+b)//2
    elif f==4:
     p=a+b-c;pa=abs(p-a);pb=abs(p-b);pc=abs(p-c);v=a if pa<=pb and pa<=pc else b if pb<=pc else c
    else:v=0
    cur[x]=(int(cur[x])+v)%256
   out[y]=cur;prev=cur
  return out.view('>u2').reshape(h,w,3).astype(np.uint16)
 a=read(old/('output.'+ext));b=read(new/('output.'+ext));d=np.abs(a.astype(np.int32)-b.astype(np.int32));rows.append(dict(format=ext,shape=list(a.shape),exact=bool(np.array_equal(a,b)),maxCodeDifference=int(d.max())));assert np.array_equal(a,b)
linear=tifffile.imread(new/'output.tif').astype(np.float64)/65535
expected=np.where(linear<=.0031308,12.92*linear,1.055*np.power(linear,1/2.4)-.055);q=np.rint(expected*255).astype(np.uint8)
screen=np.asarray(Image.open(new/'display.png'));jpeg=np.asarray(Image.open(new/'display.jpg'));delta=np.abs(screen.astype(int)-q.astype(int));loss=np.abs(jpeg.astype(float)-q.astype(float))/255
# floatOETF vsdoublequantization may differ a code only at rounding ties.
assert delta.max()<=1
mae=float(loss.mean());rmse=float(np.sqrt(np.mean(loss*loss)));psnr=float(-20*np.log10(rmse));assert mae<.02 and psnr>30
rows.append(dict(format='displayPNG8',transfer='IECsRGBOETF',maxCodeDifferenceFromDouble=int(delta.max()),profileSha256=hashlib.sha256(Image.open(new/'display.png').info['icc_profile']).hexdigest()))
rows.append(dict(format='displayJPEG8',transfer='IECsRGBOETF',lossy=True,maxAbs=float(loss.max()),meanAbs=mae,psnr=psnr,criterion='MAE<.02 PSNR>30dB; notbyteexact',profileSha256=hashlib.sha256(Image.open(new/'display.jpg').info['icc_profile']).hexdigest()))
print(json.dumps(rows,indent=2))
