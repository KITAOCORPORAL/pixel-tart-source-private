"""Build local-only contact sheets and same-coordinate window observations."""
from pathlib import Path
import argparse,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageCms
from run_matrix import sha,require
p=argparse.ArgumentParser();p.add_argument('local',type=Path);a=p.parse_args();root=a.local;dest=root/'observations';dest.mkdir(exist_ok=True);icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();films=['portra','ektar','trix'];production=json.loads((root/'canon-production-v2/report.json').read_text());w,h=production['width'],production['height'];src=np.memmap(root/'canon-production-v2/production-display-srgb.f32',dtype='<f4',mode='r',shape=(h,w,3));manifest=json.loads((root/'canon-inputs/manifest.json').read_text());statistics=[]
def image(array):return Image.fromarray(np.rint(np.clip(array,0,1)*255).astype(np.uint8))
def panel(canvas,x,y,img,label,width=400,height=267):
 d=ImageDraw.Draw(canvas);d.text((x+6,y+4),label,fill='white');img=img.copy();img.thumbnail((width,height));canvas.paste(img,(x+(width-img.width)//2,y+25+(height-img.height)//2))
contact=Image.new('RGB',(1600,650),'#17191c');draw=ImageDraw.Draw(contact);draw.text((8,6),'AUTHORIZED DEVELOPMENT SAMPLE: sunset silhouettes; NO usable skin/highlight validation. PROXY 1200x801.',fill='white');draw.text((8,24),'Full 5496x3670 engine output REFUSED by GPU budget. Do not treat proxies or native windows as full-frame acceptance.',fill='#ffcc88')
base=Image.open(root/'canon-inputs/production-display-proxy.png')
for row,route in enumerate(['A','B']):
 y=50+row*295;panel(contact,0,y,base,'Production display EV0 (same baseline)')
 for c,film in enumerate(films):panel(contact,(c+1)*400,y,Image.open(root/f'{film}-{route}-proxy/observation.png'),f'{route} {film} / '+('linear D50 bridge' if route=='A' else 'creative display sRGB'))
contact.save(dest/'contact-proxy.png',icc_profile=icc)
# Native coordinate windows, no resampling of engine inputs. Contact layout may reduce only for display.
regions={**manifest['regions'],**(json.loads((root/'additional-windows.json').read_text())['regions'] if (root/'additional-windows.json').exists() else {})}
for name,box in regions.items():
 sheet=Image.new('RGB',(2240,390),'#17191c');d=ImageDraw.Draw(sheet);d.text((8,4),f'NATIVE WINDOW ONLY {"sky_gradient (old id sky_wire)" if name=="sky_wire" else name}: x={box["left"]}, y={box["top"]}, 640x640 INPUT; sheet displays 320x320. No full-frame context.',fill='#ffcc88');x,y=box['left'],box['top'];ref=src[y:y+640,x:x+640];baseline=image(ref);panel(sheet,0,28,baseline,'Production baseline',320,320);entries=[]
 for k,(route,film) in enumerate([(route,film) for route in ['A','B'] for film in films],start=1):
  directory=root/f'{film}-{route}-crop-{name}';r=json.loads((directory/'report.json').read_text());output=np.fromfile(directory/'rgba16.bin',dtype='<u2').reshape(640,640,4);panel(sheet,k*320,28,Image.open(directory/'observation.png'),f'{route} {film} native window',320,320)
  luma=(output[:,:,:3].astype(np.float64)/65535)@np.array([.2126,.7152,.0722]);entries.append({'route':route,'film':film,'outputHash':r['outputPixelSha256'],'zeroChannels':r['statistics']['zeroFractionRGB'],'maximumChannels':r['statistics']['maximumFractionRGB'],'meanEncodedRGB':(output[:,:,:3].astype(np.float64)/65535).mean(axis=(0,1)).tolist(),'lumaPercentiles':np.percentile(luma,[1,50,99]).tolist()})
 sheet.save(dest/f'window-{name}.png',icc_profile=icc);statistics.append({'window':name,'sourceBox':box,'cases':entries})
# A vs B numeric difference is a route difference, not an accuracy ranking.
comparisons=[]
for film in films:
 a=np.fromfile(root/f'{film}-A-proxy/rgba16.bin',dtype='<u2').reshape(801,1200,4);b=np.fromfile(root/f'{film}-B-proxy/rgba16.bin',dtype='<u2').reshape(801,1200,4);delta=b.astype(np.int32)-a.astype(np.int32);comparisons.append({'film':film,'A_vs_B_changedPixels':int(np.any(delta[:,:,:3]!=0,axis=2).sum()),'maxAbsU16':int(np.abs(delta).max()),'meanAbsRgbU16':float(np.abs(delta[:,:,:3]).mean()),'interpretation':'different intentional input routes; NOT independent accuracy error'})
report={'version':'NaturalLocalObservationArtifactsV1','privateImagesRemainLocal':True,'contactSheet':str(dest/'contact-proxy.png'),'regions':statistics,'routeComparisons':comparisons,'images':[{'path':str(p),'sha256':sha(p),'bytes':p.stat().st_size} for p in dest.glob('*.png')],'limits':'No skin/color accuracy/highlight stress validation. Native-window input excludes outside pixels; not full-frame exact crop.'};(dest/'report.json').write_text(json.dumps(report,indent=2));print('Local contact +'+str(len(regions))+' native window sheets complete')
