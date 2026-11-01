import argparse,hashlib,json,math,subprocess,time
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms
import PIL,PIL._imaging
p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--inputs',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--chroma420',type=Path);p.add_argument('--chroma-native',type=Path);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
def error(x,y):
 d=np.abs(x.astype(int)-y.astype(int));return {'maxCode':int(d.max()),'meanCode':float(d.mean()),'p95Code':float(np.quantile(d,.95))}
rows=[]
for row in json.loads(a.inputs.read_text(encoding='utf-8-sig'))[:2]:
 src=Path(row['path']);name=src.stem;actual=np.asarray(Image.open(a.run/(name+'-native-local-only.png')).convert('RGB'));h,w=actual.shape[:2];image=Image.open(src);image.draft('RGB',(w,h));assert image.size==(w,h);ref=np.asarray(image.convert('RGB'));d=error(actual,ref);d.update(reference='Pillow draft native factor',sourceSha256=sha(src),width=w,height=h);rows.append(d)
 ff=a.output/(name+'-ffmpeg-LOCAL-ONLY.png');subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-noautorotate','-lowres','2','-i',str(src),'-pix_fmt','rgb24','-frames:v','1','-y',str(ff)],check=True);d=error(actual,np.asarray(Image.open(ff).convert('RGB')));d.update(reference='FFmpeg native lowres2 IDCT',sourceSha256=sha(src),width=w,height=h);rows.append(d)
 # ICC/EXIF stage independent Pillow LCMS, not just matching encoded byte hashes.
 original=Image.open(src);profile=ImageCms.ImageCmsProfile(__import__('io').BytesIO(original.info['icc_profile']));srgb=ImageCms.ImageCmsProfile(str(a.run/'wic-target-profile.icc'));converted=ImageCms.profileToProfile(Image.fromarray(actual),profile,srgb,outputMode='RGB');orientation=original.getexif().get(274,1)
 if orientation==6:converted=converted.transpose(Image.Transpose.ROTATE_270)
 b=np.frombuffer((a.run/(name+'-oriented-local-only.bgra')).read_bytes(),dtype=np.uint8).reshape(converted.height,converted.width,4);d=error(b[:,:,:3][:,:,::-1],np.asarray(converted));d.update(reference='Pillow LCMS ICC then EXIF',sourceSha256=sha(src),orientation=orientation,width=converted.width,height=converted.height);rows.append(d)
if a.chroma420:
 assert a.chroma_native is not None,'--chroma-native required with --chroma420';src=a.chroma420;actual=np.asarray(Image.open(a.chroma_native).convert('RGB'));h,w=actual.shape[:2];image=Image.open(src);image.draft('RGB',(w,h));assert image.size==(w,h)
 d=error(actual,np.asarray(image.convert('RGB')));d.update(reference='Pillow draft native factor 4:2:0',sourceSha256=sha(src),width=w,height=h);rows.append(d)
 ff=a.output/'photo24-420-ffmpeg-LOCAL-ONLY.png';subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-noautorotate','-lowres','2','-i',str(src),'-pix_fmt','rgb24','-frames:v','1','-y',str(ff)],check=True);d=error(actual,np.asarray(Image.open(ff).convert('RGB')));d.update(reference='FFmpeg native lowres2 IDCT 4:2:0',sourceSha256=sha(src),width=w,height=h);rows.append(d)
# Independent float64 compositor fed actual frozen proxy layers. No private pixels are written to metrics.
root=a.run/'proxy-oracle-local-only';data=json.loads((root/'INPUT.json').read_text(encoding='utf-8-sig'));w,h=data['width'],data['height'];xx,yy=np.meshgrid((np.arange(w)+.5)*data['artboardWidth']/w,(np.arange(h)+.5)*data['artboardHeight']/h);result=np.zeros((h,w,4))
for layer in data['layers']:
 item=layer['item'];pixels=np.frombuffer((root/layer['file']).read_bytes(),dtype=np.uint8).reshape(layer['height'],layer['width'],4)/255.;sw,sh=layer['width'],layer['height'];cx=item['X']+item['Width']/2;cy=item['Y']+item['Height']/2;t=-item['Rotation']*math.pi/180;dx=xx-cx;dy=yy-cy;u=(dx*math.cos(t)-dy*math.sin(t)+item['Width']/2)/item['Width'];v=(dx*math.sin(t)+dy*math.cos(t)+item['Height']/2)/item['Height'];valid=(u>=0)&(u<1)&(v>=0)&(v<1)
 if item['FlipX']:u=1-u
 if item['FlipY']:v=1-v
 crop=item['CropRect'];sx=(crop['X']+u*crop['Width'])*sw-.5;sy=(crop['Y']+v*crop['Height'])*sh-.5;ix=np.floor(sx).astype(int);iy=np.floor(sy).astype(int);fx=sx-ix;fy=sy-iy;sample=np.zeros_like(result)
 for px in [0,1]:
  for py in [0,1]:
   valid2=valid&(ix+px>=0)&(ix+px<sw)&(iy+py>=0)&(iy+py<sh);q=pixels[np.clip(iy+py,0,sh-1),np.clip(ix+px,0,sw-1)].copy();q[:,:,:3]*=q[:,:,3:4];weight=(fx if px else 1-fx)*(fy if py else 1-fy);sample+=q*(weight*valid2)[:,:,None]
 sample*=item.get('Opacity') if item.get('Opacity') is not None else 1;result=sample+result*(1-sample[:,:,3:4])
alpha=result[:,:,3:4];rgb=np.divide(result[:,:,:3],alpha,out=np.zeros_like(result[:,:,:3]),where=alpha!=0);expected=np.clip(np.rint(np.concatenate([rgb,alpha],axis=2)*255),0,255).astype(np.uint8);actual=np.frombuffer((root/'product.bgra').read_bytes(),dtype=np.uint8).reshape(h,w,4);d=error(expected,actual);seams=np.unique(np.concatenate([np.arange(63,h,64),np.arange(64,h,64)]));d.update(reference='independent float64 proxy compositor',alphaMax=int(np.abs(expected[:,:,3].astype(int)-actual[:,:,3].astype(int)).max()),rowBoundaryMax=int(np.abs(expected[seams].astype(int)-actual[seams].astype(int)).max()),width=w,height=h);rows.append(d)
for x in rows:
 if x['reference'].startswith('FFmpeg'):x['pass']=x['maxCode']<=24 and x['meanCode']<=1.5 and x['p95Code']<=3
 elif x['reference'].startswith('independent'):x['pass']=x['maxCode']<=1 and x['rowBoundaryMax']<=1
 elif x['reference'].startswith('Pillow LCMS'):x['pass']=x['maxCode']<=1;x['disposition']='OBSERVED_NOT_EQUIVALENT_EXISTING_WIC_ICC' if not x['pass'] else 'PASS'
 else:x['pass']=x['maxCode']<=1
out={'rows':rows,'targetProfileSha256':sha(a.run/'wic-target-profile.icc'),'pillow':PIL.__version__,'pillowImagingSha256':sha(PIL._imaging.__file__),'ffmpegVersion':subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],'privacy':'private source/decoded pixels are LOCAL ONLY; report contains digests/parameters/errors, not image data','overallPass':all(x['pass'] for x in rows),'scope':'native JPEG sampling differs from v2 full-resolution sampling; same factor independent IDCT comparisons, no camera calibration'};(a.output/'NUMERICAL_RESULTS.json').write_text(json.dumps(out,indent=2),encoding='utf-8');print(json.dumps(rows,indent=2));raise SystemExit(0 if out['overallPass'] else 1) # Every preset gate counts; known ICC/chroma failures stay nonzero.
