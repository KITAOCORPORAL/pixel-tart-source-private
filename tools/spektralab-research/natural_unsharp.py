"""Read existing natural-window trace only. No source/resource/sharpening changes."""
from pathlib import Path
import argparse,json
import numpy as np
from PIL import Image,ImageDraw,ImageCms
from run_matrix import sha,require
p=argparse.ArgumentParser();p.add_argument('local',type=Path);a=p.parse_args();root=a.local;report={'version':'NaturalScannerUnsharpImpactV1','internalDefault':{'sigmaFullTierPixels':.7,'amount':.7,'scannerLensBlur':0},'pixels':{'nativeWindow':[640,640],'fullFrameAccepted':False,'interiorExcludesPixels':16},'cases':[]};icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();sheet=Image.new('RGB',(1280,730),'#17191c');draw=ImageDraw.Draw(sheet);draw.text((8,3),'DEFAULT UNSHARP DIAGNOSTIC: native-window linear sRGB stages, only sRGB transfer for observation. NOT a candidate/no-sharpen render.',fill='#ffcc88')
def transfer(x):
 x=np.clip(x,0,1);return np.where(x<=.0031308,12.92*x,1.055*np.power(x,1/2.4)-.055)
for row,route in enumerate(['A','B']):
 d=root/('unsharp-trace-'+route)
 def load(name):
  files=list(d.glob('*-'+name+'-640-640-3.f32'));require(len(files)==1,'Missing unique stage');return np.fromfile(files[0],dtype='<f4').reshape(640,640,3),files[0]
 before,bp=load('print_linear');after,ap=load('print_scan_finish');delta=after-before;inner=delta[16:-16,16:-16];report['cases'].append({'route':route,'beforeHash':sha(bp),'afterHash':sha(ap),'maxAbsLinearRgb':float(np.abs(delta).max()),'meanAbsLinearRgb':float(np.abs(delta).mean()),'interiorMaxAbs':float(np.abs(inner).max()),'interiorMeanAbs':float(np.abs(inner).mean()),'beforeMinMax':[float(before.min()),float(before.max())],'afterMinMax':[float(after.min()),float(after.max())],'newNegativeSamples':int(((before>=0)&(after<0)).sum()),'newAboveOneSamples':int(((before<=1)&(after>1)).sum()),'changedFraction':float(np.any(delta!=0,axis=2).mean()),'limits':'float linear-sRGB scanner-stage comparison; does not establish full-frame spatial-context equality, perceptual significance, or no-unsharp final output'})
 for col,(data,label) in enumerate([(before,'before scanner finish'),(after,'after default unsharp')]):
  # 1:1 central 320x320 coordinate crop; choose intersection of head edge and sky.
  window=data[80:400,180:500];im=Image.fromarray(np.rint(transfer(window)*255).astype(np.uint8));x=col*640; y=30+row*345;sheet.paste(im,(x,y+20));draw.text((x+5,y),f'{route}: {label}; native ROI x180..499,y80..399',fill='white')
  amplified=Image.fromarray(np.rint(np.clip(.5+(after-before)[80:400,180:500]*50,0,1)*255).astype(np.uint8));sheet.paste(amplified,(x+320,y+20));draw.text((x+326,y),'delta x50 about gray (not photograph)',fill='#ffcc88')
sheet.save(root/'observations/unsharp-impact.png',icc_profile=icc);report['observationImage']={'path':str(root/'observations/unsharp-impact.png'),'sha256':sha(root/'observations/unsharp-impact.png')};(root/'unsharp-impact.json').write_text(json.dumps(report,indent=2));print(json.dumps(report['cases']))
