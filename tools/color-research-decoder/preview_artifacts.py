import sys,json,numpy as np
from pathlib import Path
from PIL import Image,ImageCms
r=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(exist_ok=False);profile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();report={}
for name in ['baseline2','matrix3','headroom3']:
 data=np.fromfile(r/name/'preview-display.f32',dtype='<f4').reshape(534,800,3);Image.fromarray(np.rint(np.clip(data,0,1)*255).astype(np.uint8)).save(out/f'{name}.png',icc_profile=profile)
 if name!='baseline2':
  other=np.fromfile(r/'baseline2/preview-display.f32',dtype='<f4').reshape(534,800,3);d=np.abs(data-other);report[name]={'meanAbsolutePreviewDelta':float(d.mean()),'maxAbsolutePreviewDelta':float(d.max()),'different8bitPixels':int(np.any(np.rint(data*255)!=np.rint(other*255),axis=2).sum())}
print(json.dumps(report,indent=2))
