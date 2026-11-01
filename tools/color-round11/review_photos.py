"""Local-only aligned crops, numeric relative response not color accuracy."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw
r=Path('N:/pixart/ColorResearchHome-20261030/round11');src=r/'natural-coverage-run1';out=r/'local-review-v1'
if out.exists():raise ValueError('Local review exists')
out.mkdir();boxes={'canon600d':{'petal_highlight':[650,130,300,250],'background_dark':[60,40,300,250]},'canon6d':{'snow_white':[200,700,300,250],'saturated_goods':[140,170,300,250]},'canon5d3':{'backlit_petal':[340,20,300,250],'shadow_petal':[20,480,300,250]},'nikond5100':{'arm_skin':[300,350,300,250],'hair_sky_edge':[140,110,300,250]},'sonynex5r':{'flowers_saturated':[270,400,300,250],'sky_building_edge':[100,0,300,250]},'canonr6':{'silhouette_sky':[420,180,300,250],'shadow_vehicle':[0,500,300,250]}}
metrics=[]
def eotf(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
for id,zones in boxes.items():
 before=Image.open(src/id/'before.png');after=Image.open(src/id/'after.png');w,h=before.size;arr_before=np.load(r/'natural-inputs-v1'/(id+'-display-proxy.npy'));arr_after=np.memmap(src/id/'whole.rgba16',dtype='<u2',mode='r',shape=(h,w,4))[:,:,:3]/65535;tile=Image.new('RGB',(640,2*290),'#222');draw=ImageDraw.Draw(tile)
 for i,(label,(x,y,bw,bh)) in enumerate(zones.items()):
  rect=[x,y,min(w,x+bw),min(h,y+bh)];a=arr_before[y:rect[3],x:rect[2]].astype(float);b=arr_after[y:rect[3],x:rect[2]];la=eotf(a)@np.array([.2126,.7152,.0722]);lb=eotf(b)@np.array([.2126,.7152,.0722]);nonzero=la>1e-5;ratio=np.divide(lb,la,out=np.zeros_like(lb),where=nonzero);rec={'id':id,'zone':label,'boxXYXY':rect,'source':'same-coordinate proxy, not sensor ROI','meanLinearYBefore':float(la.mean()),'meanLinearYAfter':float(lb.mean()),'medianLinearYRatio':float(np.median(ratio[nonzero])),'relativeLuminanceStops':float(np.log2(np.median(ratio[nonzero]))) if (ratio[nonzero]>0).any() else None,'newZeroChannels':int(((b==0)&(a>0)).sum()),'outputZeroPixelFraction':float((b==0).any(axis=2).mean()),'meanDisplayRgbBefore':a.mean(axis=(0,1)).tolist(),'meanDisplayRgbAfter':b.mean(axis=(0,1)).tolist(),'relativeResponseNotAccuracy':True};metrics.append(rec)
  ba=before.crop(tuple(rect));aa=after.crop(tuple(rect));tile.paste(ba,(10,i*290+25));tile.paste(aa,(330,i*290+25));draw.text((10,i*290+5),id+' '+label+' BEFORE / AFTER 1:1',fill='white')
 tile.save(out/(id+'-local.png'))
(out/'relative-roi-metrics.json').write_text(json.dumps({'version':'NaturalRelativeRoiReviewV1','metrics':metrics,'skinHueAccuracy':'NOT_RUN_NO_PAIRED_REFERENCE','sampling':'hand-selected after initial image review, descriptive development evidence only'},indent=2));print(json.dumps({'reviewRoot':str(out),'zones':len(metrics)}))
