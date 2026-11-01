"""Regenerate only original owned fixtures, never manufacture photographic provenance."""
from pathlib import Path
import argparse,hashlib,json,runpy,sys
import numpy as np
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('new_root',type=Path);p.add_argument('--phase',choices=['explore','holdout'],required=True);a=p.parse_args();r=a.new_root;r.mkdir(parents=True,exist_ok=True);fixtures=r/('fixtures'if a.phase=='explore'else'holdout-fixtures');fixtures.mkdir(exist_ok=False)
if a.phase=='explore':
 x,y=np.meshgrid(np.arange(512),np.arange(384));cases={'neutral':np.stack([x*255//511]*3,2),'chroma':np.stack([np.full_like(x,128),np.where((x//19)%2,190,66),np.where((y//23)%2,180,76)],2),'checker':np.stack([np.where((x//13+y//17)%2,220,30),np.where((x//11)%2,180,60),np.where((y//7)%2,200,40)],2)}
 for name,pixels in cases.items():
  for ss in [0,2]:Image.fromarray(pixels.astype('uint8'),'YCbCr').save(fixtures/(name+'-'+str(ss)+'.jpg'),quality=94,subsampling=ss)
 x=np.arange(256,dtype=np.uint8);pixels=np.zeros((8,256,3),np.uint8);pixels[0]=x[:,None];pixels[1,:,0]=x;pixels[2,:,1]=x;pixels[3,:,2]=x;pixels[4,:,0]=x;pixels[4,:,1]=64;pixels[4,:,2]=192;pixels[5,:,0]=192;pixels[5,:,1]=x;pixels[5,:,2]=64;pixels[6,:,0]=64;pixels[6,:,1]=192;pixels[6,:,2]=x;pixels[7]=255-x[:,None];Image.fromarray(pixels).save(fixtures/'icc-ramps.png',icc_profile=(Path(__file__).parent/'fixtures/source-generated-srgb.icc').read_bytes())
else:
 for w,h,name,ori in [(17,23,'hold-icc-dark-grid',1),(23,17,'hold-icc-rotated',6)]:
  x,y=np.meshgrid(np.arange(w),np.arange(h));pixels=np.stack([(x*13+y*7)%256,(x*5+y*11)%256,(x*3+y*17)%256],2).astype('uint8');
  if name=='hold-icc-dark-grid':pixels[0]=np.arange(w,dtype=np.uint8)[:,None]
  exif=Image.Exif();exif[274]=ori;Image.fromarray(pixels).save(fixtures/(name+'.tif'),icc_profile=(Path(__file__).parent/'fixtures/source-generated-srgb.icc').read_bytes(),exif=exif)
rows=[{'file':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}for p in fixtures.iterdir()];(fixtures/'GENERATED_IDENTITIES.json').write_text(json.dumps(rows,indent=2))
