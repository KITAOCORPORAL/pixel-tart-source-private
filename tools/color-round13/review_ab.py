"""Post-run descriptive review of frozen A/B files only; no rerender/tuning."""
import sys,json,hashlib,csv
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parents[2];sys.path.insert(0,str(repo/'tools/color-round12'));from analyze_existing import read,sha,dump,load_stages
root=Path(sys.argv[1]);out=root/'review-v1'
if out.exists():raise ValueError('New review required')
out.mkdir();local=root.parent;ev=repo/'docs/research/color-engine-20261029/home-round13';natural=read(ev/'candidate-natural.json');synthetic=read(ev/'candidate-synthetic.json');finite=read(repo/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json');rows=[]
def array(folder,name,shape,dtype='<f4'):return np.fromfile(folder/name,dtype=dtype).reshape(shape)
def envelope(v):
 h,w,_=v.shape;p=np.pad(v,((2,2),(2,2),(0,0)),mode='symmetric');lo=v.copy();hi=v.copy()
 for y in range(5):
  for x in range(5):lo=np.minimum(lo,p[y:y+h,x:x+w]);hi=np.maximum(hi,p[y:y+h,x:x+w])
 return lo,hi
lut=np.array([.2126,.7152,.0722]);mapping={'canon600d':['B','A'],'canon6d':['A','B'],'canon5d3':['B','A'],'nikond5100':['A','B'],'sonynex5r':['B','A'],'canonr6':['A','B']};crops={'canon600d':[650,130,300,250],'canon6d':[140,170,300,250],'canon5d3':[20,480,300,250],'nikond5100':[300,350,300,250],'sonynex5r':[270,400,300,250],'canonr6':[0,500,300,250]}
for record in natural['records']:
 name=record['id'];shape=record['shape'];folder=root/'candidate-natural'/(name+'-whole');inp=load_stages(local,finite,name)['print_linear'];a=array(folder,'legacy.f32',shape);b=array(folder,'candidate.f32',shape);lo,hi=envelope(inp);bad=(b<lo)|(b>hi);da=a.astype(float)-inp;db=b.astype(float)-inp;norma=np.linalg.norm(da,axis=2);normb=np.linalg.norm(db,axis=2);meaningful=(norma>1e-5)&(normb>1e-5);cos=np.sum(da*db,2)[meaningful]/(norma[meaningful]*normb[meaningful]);alpha=np.sum(da*db,2)/np.maximum(np.sum(da*da,2),1e-100);residual=db-alpha[:,:,None]*da
 imageA=Image.open(root/'candidate-natural'/(name+'-A.png')).convert('RGB');imageB=Image.open(root/'candidate-natural'/(name+'-B.png')).convert('RGB');ims={'A':imageA,'B':imageB};sequence=mapping[name];tile=Image.new('RGB',(730,280),'#222');draw=ImageDraw.Draw(tile)
 for label,key,x in [('X',sequence[0],0),('Y',sequence[1],370)]:im=ims[key].copy();im.thumbnail((360,245));tile.paste(im,(x,28));draw.text((x+5,6),name+' '+label,fill='white')
 tile.save(out/(name+'-anonymous.png'));x,y,w,h=crops[name];crop=Image.new('RGB',(620,280),'#222');draw=ImageDraw.Draw(crop)
 for label,key,xx in [('X',sequence[0],0),('Y',sequence[1],310)]:crop.paste(ims[key].crop((x,y,x+w,y+h)),(xx,25));draw.text((xx+5,5),name+' '+label+' same-coordinate crop',fill='white')
 crop.save(out/(name+'-crop-anonymous.png'))
 detailGainA=record['response']['gradientMeanLegacy']/record['response']['gradientMeanOriginal'];detailGainB=record['response']['gradientMeanCandidate']/record['response']['gradientMeanOriginal'];row={'id':name,'fullEnvelopeViolationComponents':int(bad.sum()),'gradientGainLegacy':detailGainA,'gradientGainCandidate':detailGainB,'retainedGradientBoostFraction':(detailGainB-1)/(detailGainA-1),'pixelsModifiedFraction':record['response']['modifiedPixels']/(shape[0]*shape[1]),'incrementDirection':{'domain':'linearRGB increment direction only; magnitude>=1e-5 in both to avoid zero/tiny float conditioning; not hue guarantee','validPixels':int(meaningful.sum()),'cosineQuantiles':np.quantile(np.clip(cos,-1,1),[0,.05,.5,1]).tolist() if len(cos) else None,'maxPerComponentResidualFromBestSharedGain':float(np.abs(residual).max())},'fixedCrop':crops[name],'anonymousImageHash':sha(out/(name+'-crop-anonymous.png'))}
 if record.get('fixedRoi'):
  x,y,w,h=record['fixedRoi']['xywh'];row['fixedRoiEnvelopeCorrectGlobalContext']={'xywh':[x,y,w,h],'violationComponents':int(bad[y:y+h,x:x+w].sum()),'note':'Original earlier ROI descriptive table reflects crop edge, not global scene; this uses full-image neighbors and is authoritative'}
 rows.append(row)
 # Save stage readback in fresh NumPy container, independently reload; no GPU and no codec claim.
 np.save(out/(name+'-candidate-reopen.npy'),b);reopen=np.load(out/(name+'-candidate-reopen.npy'));assert np.array_equal(reopen,b)
 rgba=array(folder,'candidate.rgba16',shape[:2]+[4],'<u2');from PIL import TiffImagePlugin
 image=Image.fromarray(np.rint(rgba[:,:,:3]/257).astype(np.uint8));assert np.array_equal(np.asarray(image),np.asarray(imageB));row['savedReopenExact']=True;row['pngDisplayRoundTo8bitExact']=True
# Actual16-bit TIFF via installed frozen tifffile.
import tifffile
for row in rows:
 name=row['id'];record=next(v for v in natural['records'] if v['id']==name);q=array(root/'candidate-natural'/(name+'-whole'),'candidate.rgba16',record['shape'][:2]+[4],'<u2');p=out/(name+'-candidate.tif');tifffile.imwrite(p,q[:,:,:3],photometric='rgb',metadata=None);back=tifffile.imread(p);assert np.array_equal(q[:,:,:3],back);row['tiff16SavedReadbackExact']=True;row['tiffSha256']=sha(p)
# Existing noise fixture is the only known-noise ground truth. No natural noise labeling.
noise=next(r for r in synthetic['records'] if r['id']=='noise')['response'];detail=next(r for r in synthetic['records'] if r['id']=='detail')['response'];low=next(r for r in synthetic['records'] if r['id']=='low_detail')['response']
report={'version':'Round13FrozenABReviewV1','natural':rows,'syntheticTradeoffs':{'noiseStdGainLegacy':noise['linearYStdLegacy']/noise['linearYStdOriginal'],'noiseStdGainCandidate':noise['linearYStdCandidate']/noise['linearYStdOriginal'],'detailStdGainLegacy':detail['linearYStdLegacy']/detail['linearYStdOriginal'],'detailStdGainCandidate':detail['linearYStdCandidate']/detail['linearYStdOriginal'],'lowDetailStdGainLegacy':low['linearYStdLegacy']/low['linearYStdOriginal'],'lowDetailStdGainCandidate':low['linearYStdCandidate']/low['linearYStdOriginal'],'stepAndImpulseCandidateEqualsInput':True,'interpretation':'No-tuning V1 removes extrema overshoot but returns pure step/impulse to original, retaining less sharpening. Noise still amplified; not a denoiser.'},'resourceSummary':{'wholeDevicePoolPeakMax':max(r['runs'][0]['devicePoolPeakBytes'] for r in natural['records']),'streamDevicePoolPeakMax':max(run['devicePoolPeakBytes'] for r in natural['records'] for run in r['runs'][1:]),'osPeakWorkingSetMax':max(run['osPeakWorkingSetBytes'] for r in natural['records'] for run in r['runs']),'scope':'Stage-only includes original and candidate+downstream together; not full pipeline or candidate-only performance'},'preference':'NOT_RUN_AWAITING_HUMAN','independentValidation':'NOT_RUN_DEVELOPMENT_ONLY','productAcceptance':'NOT_ADMITTED','scale':'Only explicit ratio1; product800/1600 NOT_RUN','rawDecode':0,'newGpuCalls':0}
dump(ev/'ab-review.json',report);dump(out/'anonymous-key.json',mapping);print(json.dumps(report,indent=2))
