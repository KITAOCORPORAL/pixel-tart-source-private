from pathlib import Path
import json,sys,numpy as np
from PIL import Image
root=Path(sys.argv[1]);data=json.loads((root/'fair-exact/FULL_ROI_METRICS.json').read_text());old=Path('E:/Guangjing-Portrait-Highlight-Review-20261112');checks=[]
for item in [('skin-shoulder',0,'post-curve','mid+high'),('warm-lit-texture',1,'post-curve','mid+high'),('warm-lit-texture',1,'basic-highlights','mid+high'),('sunset-gradient',1,'basic-highlights','mid+high')]:
 r=next(x for x in data['rows'] if (x['id'],x['ev'],x['method'],x['condition'])==item);p=root/'fair-exact'/r['panel'];im=np.asarray(Image.open(p).convert('RGB'));assert im.shape[:2]==(450,1152)
 src=np.fromfile(old/'roi-comparison02'/(r['id']+'-E'+str(r['ev'])+'-same')/'output.f32',dtype='<f4').reshape(384,384,3);assert np.array_equal(im[28:412,0:384],np.rint(np.clip(src,0,1)*255).astype('uint8'))
 if r['array']:
  c=np.fromfile(r['array'],dtype='<f4').reshape(384,384,3);assert np.array_equal(im[28:412,768:1152],np.rint(np.clip(c,0,1)*255).astype('uint8'))
 else:assert np.all(im[28:412,768:1152]==119)
 checks.append({'id':r['id'],'ev':r['ev'],'method':r['method'],'condition':r['condition'],'status':r['status'],'nativeCell384Exact':True})
Path(sys.argv[2]).write_text(json.dumps({'panelsChecked':len(checks),'status':'PASS_DISPLAY_CELLS_NO_RESAMPLING','checks':checks,'noScientificPreferenceClaim':True},indent=2)+chr(10));print(json.dumps({'panelsChecked':len(checks),'displayCellsExact':True}))
