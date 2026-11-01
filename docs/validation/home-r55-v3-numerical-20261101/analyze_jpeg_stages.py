import json,subprocess,hashlib,io
from pathlib import Path
import numpy as np
from PIL import Image,ImageCms
root=Path(__file__).resolve().parent
p=__import__('argparse').ArgumentParser();p.add_argument('root',type=Path);a=p.parse_args();r=a.root;s=r/'stages';out=r/'jpeg-analysis';out.mkdir(exist_ok=True)
def err(a,b):
 d=np.abs(a.astype(np.int32)-b.astype(np.int32));return {'max':int(d.max()),'mean':float(d.mean()),'p95':float(np.quantile(d,.95))}
def ffplanes(path,lowres,w,h,name):
 output=out/(name+f'-ff-low{lowres}.yuv');cmd=['ffmpeg','-hide_banner','-loglevel','error','-noautorotate','-lowres',str(lowres),'-i',str(path),'-frames:v','1','-c:v','rawvideo','-f','rawvideo','-y',str(output)];subprocess.run(cmd,check=True);buf=np.frombuffer(output.read_bytes(),np.uint8);im=Image.open(path);is420=im.layer[0][1:3]==(2,2);cw=(w+1)//2 if is420 else w;ch=(h+1)//2 if is420 else h;assert buf.size==w*h+2*cw*ch,(name,lowres,buf.size,w,h,cw,ch);return [buf[:w*h].reshape(h,w),buf[w*h:w*h+cw*ch].reshape(ch,cw),buf[w*h+cw*ch:].reshape(ch,cw)]
results=[]
paths=json.loads((r/'STAGE_INPUTS_LOCAL.json').read_text())
for path,stage in zip(paths,json.loads((s/'STAGES.json').read_text())):
 im=Image.open(path);w,h=stage['width'],stage['height'];factor=round(im.width/w);lowres={1:0,2:1,4:2,8:3}[factor];name=stage['name'];ff=ffplanes(path,lowres,w,h,name)
 row={'name':name,'factor':factor,'sampling':im.layer,'width':w,'height':h,'planes':[]}
 for opt in stage['planar']:
  if not opt.get('supported'):continue
  planes=[np.frombuffer((s/(name+f'-plane{n}-opt{opt["options"]}.raw')).read_bytes(),np.uint8).reshape(desc['height'],desc['width']) for n,desc in enumerate(opt['planes'])]
  for n,x in enumerate(planes):
   if x.shape==ff[n].shape:row['planes'].append({'options':opt['options'],'channel':n,'against':'FF same reduced IDCT','error':err(x,ff[n])})
   elif n>0 and lowres>0:
    high=ffplanes(path,lowres-1,w*2,h*2,name+'-chroma')
    row['planes'].append({'options':opt['options'],'channel':n,'against':'FF chroma one larger IDCT block','error':err(x,high[n])})
  if all(x.shape==(h,w) for x in planes):
   y,cb,cr=[x.astype(float) for x in planes];cb-=128;cr-=128
   rgb=np.stack([y+1.402*cr,y-.344136*cb-.714136*cr,y+1.772*cb],2);rgb=np.clip(np.floor(rgb+.5),0,255).astype(np.uint8)
   wic=np.frombuffer((s/(name+'-native.bgra')).read_bytes(),np.uint8).reshape(h,w,4)[:,:,:3][:,:,::-1];row['planarToWicRgb601']=err(rgb,wic)
 draft=Image.open(path);draft.draft('RGB',(w,h));row['pillowNative']=err(np.asarray(draft.convert('RGB')),np.frombuffer((s/(name+'-native.bgra')).read_bytes(),np.uint8).reshape(h,w,4)[:,:,:3][:,:,::-1]);results.append(row)
(out/'JPEG_STAGES.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
