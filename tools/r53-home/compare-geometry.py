"""Run after GeometryV2ExportsPixelCentersForIndependentReferenceComparison.
Requires existing Pillow, numpy and isolated OpenCV reference runtime. No application dependencies added."""
import argparse, json, pathlib, sys
import numpy as np
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--root',required=True);args=p.parse_args();root=pathlib.Path(args.root);sys.path.insert(0,str(root/'reference-runtime'));import cv2
x=json.loads((root/'scene/GEOMETRY_REFERENCE_INPUT.json').read_text());a=np.array(x['input'],dtype=np.float32).reshape(x['height'],x['width'],3);c=np.array(x['coordinates'],dtype=np.float32).reshape(x['outputHeight'],x['outputWidth'],2);actual=np.array(x['actual']).reshape(x['outputHeight'],x['outputWidth'],3)
cv=cv2.remap(a,c[:,:,0],c[:,:,1],cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(0,0,0));p00=c[0,0].astype(float);dx=c[0,1]-p00;dy=c[1,0]-p00;offset=p00+.5-.5*dx-.5*dy
pi=np.stack([np.asarray(Image.fromarray(a[:,:,k]).transform((x['outputWidth'],x['outputHeight']),Image.Transform.AFFINE,(dx[0],dy[0],offset[0],dx[1],dy[1],offset[1]),Image.Resampling.BILINEAR)) for k in range(3)],axis=-1)
interior=(c[:,:,0]>=0)&(c[:,:,0]<=x['width']-1)&(c[:,:,1]>=0)&(c[:,:,1]<=x['height']-1)
def metric(b,mask):
 e=np.abs(actual[mask]-b[mask])*255;return {'mean8':float(e.mean()),'p95_8':float(np.percentile(e,95)),'max8':float(e.max())}
r={'opencvVersion':cv2.__version__,'pillowVersion':Image.__version__,'opencvCommit':'67f3511f2354ebfb5f98045ee6153345f632aadd','pillowCommit':'bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d','opencv':metric(cv,np.ones(interior.shape,dtype=bool)),'pillowInterior':metric(pi,interior),'pillowAllIncludingDifferentBorderRule':metric(pi,np.ones(interior.shape,dtype=bool)),'notes':['OpenCV INTER_LINEAR 1/32 coefficient table; product double bilinear.','Pillow border behavior differs; interior separately measured.','No color accuracy claim.']}
assert r['opencv']['max8']<4;assert r['pillowInterior']['max8']<.001
(root/'scene/GEOMETRY_REFERENCE_COMPARISON.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2))
