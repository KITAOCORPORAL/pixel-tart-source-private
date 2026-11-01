import json,subprocess
from pathlib import Path
import numpy as np
r=Path(__import__('sys').argv[1]);out=r/'reconstruction';out.mkdir(exist_ok=True)
def err(a,b):
 d=np.abs(a.astype(int)-b.astype(int));return {'max':int(d.max()),'mean':float(d.mean()),'p95':float(np.quantile(d,.95))}
def convert(y,cb,cr,matrix='601',limited=False):
 y=y.astype(float);cb=cb.astype(float)-128;cr=cr.astype(float)-128
 if limited:y=(y-16)*255/219;cb*=255/224;cr*=255/224
 kr,kb=(.299,.114)if matrix=='601'else(.2126,.0722);kg=1-kr-kb
 rgb=np.stack([y+2*(1-kr)*cr,y-2*kb*(1-kb)/kg*cb-2*kr*(1-kr)/kg*cr,y+2*(1-kb)*cb],2)
 return np.clip(np.floor(rgb+.5),0,255).astype(np.uint8)
def interp(p,w,h,offset):
 x=(np.arange(w)+offset)/2-offset;y=(np.arange(h)+offset)/2-offset
 rows=np.stack([np.interp(x,np.arange(p.shape[1]),row)for row in p]);return np.stack([np.interp(y,np.arange(p.shape[0]),col)for col in rows.T],1)
results=[]
for name,path,low,w,h in [('photo24-420',Path('N:/pixart/Guangjing-R55-CanvasPhoto-20261101/inputs/photo24-420.jpg'),2,1500,1000),('chroma-2',r/'fixtures/chroma-2.jpg',0,512,384),('checker-2',r/'fixtures/checker-2.jpg',0,512,384)]:
 raw=(r/'jpeg-analysis'/(name+f'-ff-low{low}.yuv')).read_bytes();a=np.frombuffer(raw,np.uint8);cw,ch=w//2,h//2;y=a[:w*h].reshape(h,w);cb=a[w*h:w*h+cw*ch].reshape(ch,cw);cr=a[w*h+cw*ch:].reshape(ch,cw)
 variants={}
 for flags in ['default','neighbor','bilinear','bicubic','neighbor+full_chroma_int+accurate_rnd','bilinear+full_chroma_int+accurate_rnd']:
  dest=out/(name+'-'+flags+'.rgb');cmd=['ffmpeg','-v','error','-noautorotate','-lowres',str(low),'-i',str(path)]
  if flags!='default':cmd+=['-vf',f'scale=in_color_matrix=bt601:out_color_matrix=bt601:in_range=full:out_range=full:flags={flags}']
  cmd+=['-pix_fmt','rgb24','-frames:v','1','-f','rawvideo','-y',str(dest)];subprocess.run(cmd,check=True);variants[flags]=np.frombuffer(dest.read_bytes(),np.uint8).reshape(h,w,3)
 row={'name':name,'ffmpegRGB':[],'crossKernel':[]}
 for filter in ['nearest','linear-center','linear-left']:
  u=cb.repeat(2,0).repeat(2,1)if filter=='nearest'else interp(cb,w,h,.5 if filter=='linear-center'else 0)
  v=cr.repeat(2,0).repeat(2,1)if filter=='nearest'else interp(cr,w,h,.5 if filter=='linear-center'else 0)
  for matrix in ['601','709']:
   for limited in [False,True]:
    val=convert(y,u,v,matrix,limited)
    row['ffmpegRGB'].append({'filter':filter,'matrix':matrix,'limited':limited,'vsDefault':err(val,variants['default'])})
 for flag,val in variants.items():row['crossKernel'].append({'flags':flag,'vsDefault':err(val,variants['default'])})
 # Isolate the chroma-grid change while HOLDING WIC Y and BT601 identical.
 if name=='photo24-420':
  stage=json.loads((r/'stages/STAGES.json').read_text())[-1];p=[np.frombuffer((r/'stages'/(name+f'-plane{n}-opt0.raw')).read_bytes(),np.uint8).reshape(h,w)for n in range(3)];wic=convert(*p)
  row['holdWicYChangeOnlyChroma']={}
  for filter in ['nearest','linear-center']:
   u=cb.repeat(2,0).repeat(2,1)if filter=='nearest'else interp(cb,w,h,.5);v=cr.repeat(2,0).repeat(2,1)if filter=='nearest'else interp(cr,w,h,.5)
   row['holdWicYChangeOnlyChroma'][filter]=err(convert(p[0],u,v),wic)
 results.append(row)
(r/'JPEG_RECONSTRUCTION.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
