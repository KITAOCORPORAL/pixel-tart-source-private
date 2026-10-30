import argparse,hashlib,json,math,subprocess,time
from pathlib import Path
import numpy as np
import PIL
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
# External reference binaries are test tools only, never product dependencies.
root=a.root/'reference-samples';Image.alpha_composite(Image.open(root/'bottom.png').convert('RGBA'),Image.open(root/'top.png').convert('RGBA')).save(a.output/'pillow.png')
subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-i',str(root/'bottom.png'),'-i',str(root/'top.png'),'-filter_complex','[0:v][1:v]overlay=format=rgb:alpha=straight','-frames:v','1','-update','1','-y',str(a.output/'ffmpeg.png')],check=True)
actual=np.asarray(Image.open(root/'product.png').convert('RGBA')).astype(int);alpha=[]
for name in ['pillow','ffmpeg']:
 other=np.asarray(Image.open(a.output/(name+'.png')).convert('RGBA')).astype(int);visible=(actual[:,:,3]>0)|(other[:,:,3]>0);diff=np.abs(actual-other);alpha.append({'reference':name,'maximumCode':int(diff[visible].max()),'meanCode':float(diff[visible].mean()),'alphaMaximum':int(diff[:,:,3].max()),'visiblePixelCount':int(visible.sum()),'tolerance':1,'pass':bool(diff[visible].max()<=1)})
# Independent scalar-domain/vectorized float64 oracle. WPF layer rasterization is the frozen input,
# not reimplemented typography/ICC. Output pixels map directly to artboard centers (no full image downsample).
root=a.root/'sampling';data=json.loads((root/'ORACLE_INPUT.json').read_text(encoding='utf-8-sig'));sampling=[]
for edge in [1400,140]:
 started=time.perf_counter();scale=min(1,edge/max(data['width'],data['height']));w=round(data['width']*scale);h=round(data['height']*scale);xx,yy=np.meshgrid((np.arange(w)+.5)*data['width']/w,(np.arange(h)+.5)*data['height']/h);result=np.zeros((h,w,4),np.float64)
 for layer in data['layers']:
  item=layer['item'];pixels=np.frombuffer((root/layer['file']).read_bytes(),dtype=np.uint8).reshape(layer['height'],layer['width'],4)/255.;sw=layer['width'];sh=layer['height'];cx=item['X']+item['Width']/2;cy=item['Y']+item['Height']/2;angle=-item['Rotation']*math.pi/180;dx=xx-cx;dy=yy-cy;u=(dx*math.cos(angle)-dy*math.sin(angle)+item['Width']/2)/item['Width'];v=(dx*math.sin(angle)+dy*math.cos(angle)+item['Height']/2)/item['Height'];valid=(u>=0)&(u<1)&(v>=0)&(v<1)
  if item['FlipX']:u=1-u
  if item['FlipY']:v=1-v
  crop=item['CropRect'];sx=(crop['X']+u*crop['Width'])*sw-.5;sy=(crop['Y']+v*crop['Height'])*sh-.5;ix=np.floor(sx).astype(int);iy=np.floor(sy).astype(int);fx=sx-ix;fy=sy-iy;sample=np.zeros_like(result)
  for px in [0,1]:
   for py in [0,1]:
    valid2=valid&(ix+px>=0)&(ix+px<sw)&(iy+py>=0)&(iy+py<sh);q=pixels[np.clip(iy+py,0,sh-1),np.clip(ix+px,0,sw-1)].copy();q[:,:,:3]*=q[:,:,3:4];weight=(fx if px else 1-fx)*(fy if py else 1-fy);sample+=q*(weight*valid2)[:,:,None]
  sample*=item.get('Opacity') if item.get('Opacity') is not None else 1;result=sample+result*(1-sample[:,:,3:4])
 alpha64=result[:,:,3:4];rgb=np.divide(result[:,:,:3],alpha64,out=np.zeros_like(result[:,:,:3]),where=alpha64!=0);expected=np.clip(np.rint(np.concatenate([rgb,alpha64],axis=2)*255),0,255).astype(np.uint8);actual=np.frombuffer((root/f'product-{edge}.bgra').read_bytes(),dtype=np.uint8).reshape(h,w,4);diff=np.abs(expected.astype(int)-actual.astype(int));sampling.append({'edge':edge,'width':w,'height':h,'maxCode':int(diff.max()),'meanCode':float(diff.mean()),'alphaMaxCode':int(diff[:,:,3].max()),'differentComponents':int(np.count_nonzero(diff)),'tolerance':1,'pass':bool(diff.max()<=1),'oracleMs':(time.perf_counter()-started)*1000})
import shutil
ffmpeg=Path(shutil.which('ffmpeg'));import PIL._imaging
out={'pillowVersion':PIL.__version__,'pillowImagingSha256':sha(PIL._imaging.__file__),'ffmpegVersion':subprocess.check_output([str(ffmpeg),'-version'],text=True).splitlines()[0],'ffmpegSha256':sha(ffmpeg),'alpha':alpha,'sampling':sampling,'inputFiles':[{'path':str(f.relative_to(a.root)),'sha256':sha(f)} for directory in ['reference-samples','sampling'] for f in sorted((a.root/directory).iterdir()) if f.is_file()]};(a.output/'NUMERICAL_RESULTS.json').write_text(json.dumps(out,indent=2),encoding='utf-8');print(json.dumps({'alpha':alpha,'sampling':sampling},indent=2));assert all(x['pass'] for x in alpha+sampling)
