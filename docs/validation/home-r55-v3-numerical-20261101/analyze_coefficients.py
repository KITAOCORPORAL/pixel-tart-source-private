import json,subprocess,math
from pathlib import Path
import numpy as np
from PIL import Image
r=Path(__import__('sys').argv[1]);s=r/'coefficient-stages';rows=[]
for f in json.loads((r/'coefficient-fixtures/FIXTURES.json').read_text()):
 name=f['name'];meta=next(x for x in json.loads((s/'STAGES.json').read_text())if x['name']==name);w,h=meta['width'],meta['height'];out=r/(name+'-ff-low2.yuv');subprocess.run(['ffmpeg','-v','error','-noautorotate','-lowres','2','-i',str(r/'coefficient-fixtures'/(name+'.jpg')),'-frames:v','1','-c:v','rawvideo','-f','rawvideo','-y',str(out)],check=True);a=np.frombuffer(out.read_bytes(),np.uint8);cw,ch=(w//2,h//2)if f['sampling']==2 else(w,h);ff=[a[:w*h].reshape(h,w),a[w*h:w*h+cw*ch].reshape(ch,cw),a[w*h+cw*ch:].reshape(ch,cw)];planes=[]
 for n,d in enumerate(meta['planar'][0]['planes']):
  x=np.frombuffer((s/(name+f'-plane{n}-opt0.raw')).read_bytes(),np.uint8).reshape(d['height'],d['width']);planes.append({'n':n,'wicFirstRow':x[0,:16].tolist(),'ffFirstRow':ff[n][0,:16].tolist(),'wicRange':[int(x.min()),int(x.max())],'ffRange':[int(ff[n].min()),int(ff[n].max())]})
 # Mathematical average-of-four full IDCT sample target for monochromatic 2x2 reduced block.
 def average(start):return sum((f['amplitude']/(4*math.sqrt(2)))*math.cos((2*x+1)*f['u']*math.pi/16) for x in range(start,start+4))/4+128
 row={**f,'output':[w,h],'planes':planes,'areaIDCT2Expected':([round(average(0)),round(average(4))]if f['u'] else[128,128])};rows.append(row)
(r/'COEFFICIENT_RESULT.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))
