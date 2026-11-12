from pathlib import Path
import argparse,json,hashlib,numpy as np
from PIL import Image,ImageDraw,ImageFont
from rgb16_io import decode,encode,y,tiff_info,pixels
from probe_runner import probe
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('dotnet');p.add_argument('probe_dll',type=Path);p.add_argument('--output-name',default='roi-comparison02');a=p.parse_args();root=a.root;registry=json.loads((root/'FIXED_ROIS.json').read_text('utf-8'));out=root/a.output_name;out.mkdir(exist_ok=False)
inputs=root/'fixed-inputs';calls=[];results=[];anchor_results={};panels=[]
def candidate_np(v,ev):
 linear=decode(v.astype(np.float64))*2**ev;m=linear.max(axis=-1);d=m-.75;scale=np.ones_like(m);selected=m>.75;mapped=.75+.25*d[selected]/(d[selected]+.25);scale[selected]=mapped/m[selected]
 return encode(linear*scale[...,None])
def old_np(v,ev):return np.clip(encode(decode(v.astype(np.float64))*2**ev),0,1)
def local_rms(values,mask):
 dx=np.diff(values,axis=1);dy=np.diff(values,axis=0);mx=mask[:,1:]&mask[:,:-1];my=mask[1:]&mask[:-1];n=mx.sum()+my.sum()
 return float(np.sqrt((np.square(dx[mx]).sum()+np.square(dy[my]).sum())/n)) if n else None

def metrics(encoded,source,ev):
 linear=decode(encoded.astype(np.float64));original=decode(source.astype(np.float64));exposed=original*2**ev;high=exposed.max(axis=-1)>.75;lum=y(linear);maximum=linear.max(axis=-1);minimum=linear.min(axis=-1)
 encmax=encoded.max(axis=-1);encmin=encoded.min(axis=-1);s=np.divide(maximum-minimum,maximum,out=np.zeros_like(maximum),where=maximum>1e-12);es=np.divide(encmax-encmin,encmax,out=np.zeros_like(encmax),where=encmax>1e-12)
 def hue(v):return np.arctan2(np.sqrt(3)*(v[...,1]-v[...,2]),2*v[...,0]-v[...,1]-v[...,2]),np.ptp(v,axis=-1)
 h0,c0=hue(exposed);h1,c1=hue(linear);valid=(c0>1e-6)&(c1>1e-6);angles=np.abs((h1-h0+np.pi)%(2*np.pi)-np.pi)*180/np.pi
 rms=local_rms(lum,high)
 return dict(meanY=float(lum.mean()),YPercentiles=np.percentile(lum,[1,10,50,90,99]).tolist(),highMaskPixels=int(high.sum()),highMeanY=float(lum[high].mean()) if high.any() else None,highLinearSaturation=float(s[high].mean()) if high.any() else None,highEncodedSaturation=float(es[high].mean()) if high.any() else None,highLocalRms=rms,highRelativeLocalRms=rms/float(lum[high].mean()) if rms is not None and float(lum[high].mean())>0 else None,channelsAtOne=int((encoded>=1).sum()),inputChannelsAtOne=int((source>=1).sum()),hueP99Degrees=float(np.percentile(angles[valid],99)) if valid.any() else None,hueMaxDegrees=float(angles[valid].max()) if valid.any() else None,hueUndefined=int((~valid).sum()))

def fit_anchor(key,ev):
 cachekey=(key,ev)
 if cachekey in anchor_results:return anchor_results[cachekey]
 meta=registry['anchors'][key];image=np.fromfile(inputs/meta['file'],dtype='<f4').reshape(meta['shape']);sample=image[::4,::4];mask=(y(decode(sample.astype(np.float64)))>=.03)&(y(decode(sample.astype(np.float64)))<=.35)
 if mask.sum()<16:anchor_results[cachekey]=dict(status='NO_MIDTONE_ANCHOR',pixels=int(mask.sum()));return anchor_results[cachekey]
 target=float(y(decode(old_np(sample,ev)))[mask].mean())
 def f(e):return float(y(decode(candidate_np(sample,e)))[mask].mean())
 lo,hi=-5.,5.
 if not f(lo)<=target<=f(hi):anchor_results[cachekey]=dict(status='TARGET_OUTSIDE_CANDIDATE_EV_RANGE',target=target);return anchor_results[cachekey]
 for _ in range(45):
  mid=(lo+hi)/2
  if f(mid)<target:lo=mid
  else:hi=mid
 solved=(lo+hi)/2
 if abs(solved-ev)<1e-8:solved=float(ev)
 data,call=probe(a.dotnet,a.probe_dll,inputs/meta['file'],out/(key+'-anchor-E'+str(ev)),meta['shape'][1],meta['shape'][0],solved)
 actual=float(y(decode(data['candidate'][::4,::4].astype(np.float64)))[mask].mean());assert abs(actual-target)<2e-7
 record=dict(status='MATCHED',baselineEv=ev,candidateEv=solved,offsetEv=solved-ev,pixels=int(mask.sum()),targetMeanY=target,actualMeanY=actual,absoluteError=abs(actual-target),sampling='every fourth row and column, fixed input Y mask .03-.35');anchor_results[cachekey]=record;calls.append(call);return record

for roi in registry['inputs']:
 source=np.fromfile(inputs/roi['file'],dtype='<f4').reshape(roi['height'],roi['width'],3);assert hashlib.sha256(source.tobytes()).hexdigest()==roi['encodedF32Sha256']
 for ev in [0,1,2]:
  profile=inputs/(roi['source']+'.icc');profile=profile if profile.exists() else inputs/'company-02.icc'
  stem=roi['id']+'-E'+str(ev);data,call=probe(a.dotnet,a.probe_dll,inputs/roi['file'],out/(stem+'-same'),roi['width'],roi['height'],ev,False,profile);calls.append(call)
  independent=candidate_np(source,ev).astype(np.float32);max_error=float(np.abs(independent-data['candidate']).max());assert max_error<1.2e-7
  anchor=fit_anchor(roi['anchor'],ev);matched=data['candidate'];matched_ev=float(ev)
  if anchor['status']=='MATCHED':
   matched_ev=anchor['candidateEv']
   if matched_ev!=ev:
    matched_data,call=probe(a.dotnet,a.probe_dll,inputs/roi['file'],out/(stem+'-midmatched'),roi['width'],roi['height'],matched_ev,False,profile);matched=matched_data['candidate'];calls.append(call)
  info=tiff_info(out/(stem+'-same')/'candidate-product-rgb16.tif');actual_tiff=pixels(out/(stem+'-same')/'candidate-product-rgb16.tif',info);expected=np.rint(np.clip(data['candidate'],0,1)*np.float32(65535)).astype(np.uint16);assert np.array_equal(actual_tiff,expected);assert info['iccSha256']==hashlib.sha256(profile.read_bytes()).hexdigest();del actual_tiff
  record=dict(id=roi['id'],kind=roi['kind'],roi=roi['roi'],sourceSha256=roi['sourceSha256'],inputF32Sha256=roi['encodedF32Sha256'],baselineEv=ev,candidateSameEv=ev,candidateMidMatchedEv=matched_ev if anchor['status']=='MATCHED' else None,anchor=anchor,numpyVsActualCandidateMaxAbs=max_error,productCandidateTiff16PixelsExact=True,tiffOrientation=info['orientation'],tiffIccSha256=info['iccSha256'],original=metrics(data['output'],source,ev),candidateSame=metrics(data['candidate'],source,ev),candidateMidMatched=metrics(matched,source,ev) if anchor['status']=='MATCHED' else None)
  results.append(record)
  # Main comparisons stay 1:1. All pictures are local artifacts, never Git evidence.
  w=roi['width'];h=roi['height'];canvas=Image.new('RGB',(w*3,h+58),'white');draw=ImageDraw.Draw(canvas);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
  for i,(title,array) in enumerate([('Original E'+str(ev),data['output']),('Candidate same E'+str(ev),data['candidate']),('Candidate mid-match '+format(matched_ev,'.3f'),matched) if anchor['status']=='MATCHED' else ('MID MATCH UNAVAILABLE',np.full_like(matched,.5))]):
   draw.text((i*w+5,3),title,font=font,fill='black');canvas.paste(Image.fromarray(np.rint(np.clip(array,0,1)*255).astype('uint8')),(i*w,25))
  draw.text((5,h+30),roi['id']+' | input '+roi['sourceSha256'][:12]+' | '+roi['kind'],font=font,fill='black');canvas.save(out/(stem+'-comparison.png'),icc_profile=profile.read_bytes())
  if ev in (0,1):panels.append((roi['id'],ev,canvas))
summary=dict(status='PASS_BOUNDED_ROI_COMPARISONS',recipe='Frozen MaxChannelShoulderV1, Knee=.75',exposureCases=[0,1,2],comparisons=results,anchorComparisons=list(anchor_results.values()),calls=calls,newRawDecodes=0,realPhotoRois=4,syntheticSupplements=2,midMatching='Candidate EV solved before the shoulder, baseline EV fixed; fixed anchor and input mask, no output rescale',missing=registry['missing'])
(out/'ROI_RESULTS.json').write_bytes((json.dumps(summary,indent=2)+'\n').encode())
# Six fixed E1 rows for quick observation, same 1:1 ROI dimensions.
selected=[c for _,e,c in panels if e==1];canvas=Image.new('RGB',(selected[0].width,sum(c.height for c in selected)),'white');at=0
for image in selected:canvas.paste(image,(0,at));at+=image.height
canvas.save(out/'E1_FIXED_COMPARISONS.png');print(json.dumps(dict(status=summary['status'],comparisons=len(results),probeCalls=len(calls),newRaw=0)))
