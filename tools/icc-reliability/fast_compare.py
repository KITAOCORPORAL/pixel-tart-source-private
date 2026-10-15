import pathlib,sys,json,hashlib
sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps')
import tifffile,numpy as np
from PIL import Image
A=pathlib.Path(__file__).resolve().parents[2]/'artifacts/icc-reliability';old=A/'photo-old';new=A/'photo-bounded';a=tifffile.imread(old/'output.tif');b=tifffile.imread(new/'output.tif');assert np.array_equal(a,b)
x=b.astype(np.float64)/65535;formula=np.where(x<=.0031308,x*12.92,1.055*x**(1/2.4)-.055);q=np.rint(formula*255).astype(np.uint8);display=np.asarray(Image.open(new/'display.png'));jpg=np.asarray(Image.open(new/'display.jpg'));d=np.abs(display.astype(int)-q.astype(int));loss=np.abs(jpg.astype(float)-q.astype(float))/255;mae=float(loss.mean());rmse=float(np.sqrt(np.mean(loss*loss)));psnr=float(-20*np.log10(rmse));assert d.max()<=1 and mae<.02 and psnr>30
print(json.dumps(dict(tiffExact=True,pngBytesExact=(old/'output.png').read_bytes()==(new/'output.png').read_bytes(),maxDisplayCodeDiff=int(d.max()),jpegMae=mae,jpegPsnr=psnr,jpegMax=float(loss.max()),jpegCriterion='MAE<.02 PSNR>30dB; max recorded not passcriterion',displayPngIccSha256=hashlib.sha256(Image.open(new/'display.png').info['icc_profile']).hexdigest(),displayJpegIccSha256=hashlib.sha256(Image.open(new/'display.jpg').info['icc_profile']).hexdigest()),indent=2))
