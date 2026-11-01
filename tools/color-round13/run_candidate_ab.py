"""Frozen A/B stage replay; no RAW decoder or upstream film rerender."""
import os,sys,json,subprocess,time,hashlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parents[2];sys.path.insert(0,str(repo/'tools/color-round12'));from analyze_existing import read,sha,dump,load_stages,metrics
if len(sys.argv)!=5 or sys.argv[3]!='--enable-candidate' or sys.argv[4]!='SharedIncrementEnvelopeUnsharpV1':raise ValueError('Candidate is default-off; explicit --enable-candidate SharedIncrementEnvelopeUnsharpV1 required before any GPU or output')
root=Path(sys.argv[1]);mode=sys.argv[2];local=root.parent;out=root/('candidate-'+mode)
if mode not in ['synthetic','natural'] or out.exists():raise ValueError('New declared batch output required')
out.mkdir();ev=repo/'docs/research/color-engine-20261029/home-round13';build=read(ev/'candidate-build-v3.json');exe=root/'candidate-build-v3/candidate-stage.exe';need=lambda c,m:None if c else (_ for _ in ()).throw(ValueError(m));need(sha(exe)==build['executableSha256'],'Executable changed');resources=root/'candidate-build-v3/resources';need(sha(resources/'vulkan/spk_r13_envelope_v1.spv')==build['files']['candidate-build-v3/resources/vulkan/spk_r13_envelope_v1.spv'],'Shader changed');definition=read(ev/'candidate-definition.json');plan=read(ev/'evaluation-plan.json');need(sha(ev/'candidate-definition.json')==plan['candidateDefinitionSha256'],'Candidate recipe changed')
for f,expected in build['originalResourceHashes'].items():need(sha(resources/f)==expected,'Original resource changed '+f)
env=os.environ.copy();env['PATH']=str(local/'deps/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin')+os.pathsep+env['PATH'];cases=[]
if mode=='synthetic':
 h,w=33,65;y,x=np.mgrid[:h,:w];rng=np.random.default_rng(613)
 def rgb(v):return np.repeat(np.asarray(v,dtype=np.float32)[:,:,None],3,axis=2)
 flat=np.full((h,w),.4,np.float32);step=rgb(np.where(x<w//2,.1,.9));imp=flat.copy();imp[h//2,w//2]=.9;sat=np.where((x<w//2)[:,:,None],np.array([.9,.04,.01],np.float32),np.array([.01,.1,.9],np.float32));edge=flat.copy();edge[:,0]=.9;edge[0,:]=.05
 images={'flat':rgb(flat),'step':step,'impulse':rgb(imp),'saturated_edge':sat,'negative_above_range':rgb(np.where(x<w//2,-.05,1.2)),'boundary':rgb(edge),'low_detail':rgb(.5+.002*np.sin(x*np.pi/4)),'detail':rgb(.5+.05*np.sin(x*np.pi/4)),'noise':rgb(.4+rng.normal(0,.0015,(h,w)))}
 for name,image in images.items():p=out/(name+'-input.f32');image.astype('<f4').tofile(p);cases.append((name,image,p,None))
else:
 need(read(ev/'candidate-synthetic.json')['allTechnicalPassed'],'Synthetic gate required')
 finite=read(repo/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json')
 for name in plan['naturalIds']:
  stage=load_stages(local,finite,name);p=next((local/'round11/natural-stage-finite-v1'/name).glob('*-print_linear-*.f32'));cases.append((name,stage['print_linear'],p,stage))
records=[];calls=[]
def local_extrema(image):
 padded=np.pad(image,((2,2),(2,2),(0,0)),mode='symmetric');lo=image.copy();hi=image.copy();h,w,_=image.shape
 for yy in range(5):
  for xx in range(5):v=padded[yy:yy+h,xx:xx+w];lo=np.minimum(lo,v);hi=np.maximum(hi,v)
 return lo,hi

def response(image,base,cand,qa,qb):
 lo,hi=local_extrema(image);lum=np.array([.2126,.7152,.0722]);a=base@lum;b=cand@lum;original=image@lum
 def grad(v):return float((np.abs(np.diff(v,axis=0)).mean()+np.abs(np.diff(v,axis=1)).mean())/2)
 def chrom(q):
  v=q.astype(float)/65535;v=np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4);xyz=v@np.array([[.4124564,.3575761,.1804375],[.2126729,.7151522,.0721750],[.0193339,.1191920,.9503041]]).T;xyz/=np.array([.95047,1,1.08883]);f=np.where(xyz>(6/29)**3,np.cbrt(xyz),xyz/(3*(6/29)**2)+4/29);lab=np.stack([116*f[:,:,1]-16,500*(f[:,:,0]-f[:,:,1]),200*(f[:,:,1]-f[:,:,2])],2);return lab
 la,lb=chrom(qa[:,:,:3]),chrom(qb[:,:,:3]);ca=np.hypot(la[:,:,1],la[:,:,2]);cb=np.hypot(lb[:,:,1],lb[:,:,2]);valid=(ca>2)&(cb>2);ha=np.arctan2(la[:,:,2],la[:,:,1]);hb=np.arctan2(lb[:,:,2],lb[:,:,1]);hd=np.abs(np.arctan2(np.sin(hb-ha),np.cos(hb-ha)))*180/np.pi
 da=base-image;db=cand-image;dot=np.sum(da.astype(float)*db.astype(float),2);den=np.linalg.norm(da,axis=2)*np.linalg.norm(db,axis=2);validD=den>1e-14;cos=dot[validD]/den[validD]
 return {'originalNegativeComponents':int((image<0).sum()),'legacyNegativeComponents':int((base<0).sum()),'candidateNegativeComponents':int((cand<0).sum()),'legacyOutsideLocalEnvelopeComponents':int(((base<lo)|(base>hi)).sum()),'candidateOutsideLocalEnvelopeComponents':int(((cand<lo)|(cand>hi)).sum()),'legacyRgbZeroPixels':int((qa[:,:,:3]==0).all(2).sum()),'candidateRgbZeroPixels':int((qb[:,:,:3]==0).all(2).sum()),'legacyAnyZeroPixels':int((qa[:,:,:3]==0).any(2).sum()),'candidateAnyZeroPixels':int((qb[:,:,:3]==0).any(2).sum()),'legacyMeanDeltaLinearY':float((a-original).mean()),'candidateMeanDeltaLinearY':float((b-original).mean()),'gradientMeanOriginal':grad(original),'gradientMeanLegacy':grad(a),'gradientMeanCandidate':grad(b),'linearYStdOriginal':float(original.std()),'linearYStdLegacy':float(a.std()),'linearYStdCandidate':float(b.std()),'modifiedPixels':int((cand!=base).any(2).sum()),'candidateEqualsOriginalPixels':int((cand==image).all(2).sum()),'deltaDirectionCosineMin':float(np.clip(cos,-1,1).min()) if len(cos) else None,'encodedLabResponseChangeNotAccuracy':{'hueValidPixels':int(valid.sum()),'hueDeltaDegQuantiles':np.quantile(hd[valid],[.5,.95,1]).tolist() if valid.any() else None,'chromaDeltaQuantiles':np.quantile(cb-ca,[0,.5,.95,1]).tolist(),'LDeltaQuantiles':np.quantile(lb[:,:,0]-la[:,:,0],[0,.5,.95,1]).tolist()}}

for name,image,p,stages in cases:
 h,w,_=image.shape;rows=[0,7] if mode=='synthetic' else [0,17,37];runs=[]
 for rows_ in rows:
  dest=out/(name+('-whole' if rows_==0 else '-r'+str(rows_)));cmd=[str(exe),str(resources),str(p),str(root/'output-matrix.f32'),str(dest),str(w),str(h),str(rows_)];t=time.perf_counter();r=subprocess.run(cmd,env=env,capture_output=True,text=True);call={'case':name,'rows':rows_,'command':cmd,'exitCode':r.returncode,'elapsedProcessMs':(time.perf_counter()-t)*1000,'stdout':r.stdout,'stderr':r.stderr};calls.append(call)
  with (out/'calls.json').open('w') as f:json.dump(calls,f,indent=2)
  need(r.returncode==0,'Stage replay failed '+r.stderr);native=read(dest/'report.json');need(native['deviceLiveAfter']==native['persistentAfter']==0,'Lifetime leak');runs.append((dest,native))
 whole=runs[0][0];base=np.fromfile(whole/'legacy.f32',dtype='<f4').reshape(h,w,3);cand=np.fromfile(whole/'candidate.f32',dtype='<f4').reshape(h,w,3);qa=np.fromfile(whole/'legacy.rgba16',dtype='<u2').reshape(h,w,4);qb=np.fromfile(whole/'candidate.rgba16',dtype='<u2').reshape(h,w,4);need(np.isfinite(base).all() and np.isfinite(cand).all(),'Nonfinite candidate')
 parity=[]
 for dest,native_report in runs[1:]:
  for f in ['legacy.f32','candidate.f32','legacy-encoded.f32','candidate-encoded.f32','legacy.rgba16','candidate.rgba16']:
   need((whole/f).read_bytes()==(dest/f).read_bytes(),'Whole/stream nonexact '+name+'/'+f);parity.append({'rows':native_report['rows'],'file':f,'exact':True})
 frozen=None
 if stages:
  need(np.array_equal(base,stages['print_scan_finish']),'Legacy stage differs from frozen '+name);encoded=np.fromfile(whole/'legacy-encoded.f32',dtype='<f4').reshape(h,w,3);need(np.array_equal(encoded,stages['print_output']),'Legacy encoded differs from frozen '+name);old=local/'round11/natural-coverage-run1'/name/'whole.rgba16';need(old.read_bytes()==(whole/'legacy.rgba16').read_bytes(),'Legacy final differs from frozen '+name);frozen={'stageExact':True,'encodedExact':True,'finalExact':True}
 report=response(image,base,cand,qa,qb);need(report['candidateOutsideLocalEnvelopeComponents']==0,'Envelope contract failed')
 Image.fromarray(np.rint(qa[:,:,:3]/257).astype(np.uint8)).save(out/(name+'-A.png'));Image.fromarray(np.rint(qb[:,:,:3]/257).astype(np.uint8)).save(out/(name+'-B.png'))
 record={'id':name,'shape':[h,w,3],'inputSha256':sha(p),'frozenLegacy':frozen,'parity':parity,'runs':[v for _,v in runs],'response':report,'outputHashes':{f:sha(whole/f) for f in ['legacy.f32','candidate.f32','legacy-encoded.f32','candidate-encoded.f32','legacy.rgba16','candidate.rgba16']}};records.append(record)
 if mode=='natural' and name in ['nikond5100','sonynex5r','canonr6']:
  roi={'nikond5100':[310,390,35,60],'sonynex5r':[270,400,300,250],'canonr6':[0,500,300,250]}[name];x,y,bw,bh=roi;sl=np.s_[y:y+bh,x:x+bw];record['fixedRoi']={'xywh':roi,'response':response(image[sl],base[sl],cand[sl],qa[sl],qb[sl]),'envelopeStatisticsLocalCropWarning':'ROI envelope in descriptive metric reflects crop edge; full-image contract gate above is authoritative'}
report={'version':'Round13FrozenCandidateABV1','mode':mode,'candidate':definition['version'],'definitionSha256':sha(ev/'candidate-definition.json'),'stageReplayCalls':len(calls),'gpuDispatches':sum(run['dispatches'] for row in records for run in row['runs']),'rawDecodeCalls':0,'upstreamFilmRenderCalls':0,'full20MPCalls':0,'allTechnicalPassed':True,'records':records,'scope':'Stage-only GPU replay of synthetic or existing print_linear; exact old downstream matrix/OETF/u16; not full RAW pipeline, product preview or saved-project acceptance. Quality metrics descriptive, not reference color accuracy.'}
with (ev/('candidate-'+mode+'.json')).open('x') as f:json.dump(report,f,indent=2)
print(json.dumps({'mode':mode,'calls':len(calls),'dispatches':report['gpuDispatches'],'allTechnicalPassed':True,'summary':[{'id':r['id'],'response':r['response']} for r in records]},indent=2))
