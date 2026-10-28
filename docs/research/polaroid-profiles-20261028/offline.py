"""Offline analysis only: existing V3 response reproduction + bounded paired-patch fitter.
Never imported by the product; no third-party LUT weights; no camera colour bias.
"""
from __future__ import annotations
import argparse, hashlib, io, json, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parent
if (ROOT/'deps').exists():sys.path.insert(0,str(ROOT/'deps'))
import numpy as np
from PIL import Image, ImageCms, ImageDraw
from scipy.interpolate import PchipInterpolator, RegularGridInterpolator
from scipy.optimize import least_squares
from skimage.color import rgb2lab, deltaE_ciede2000
DOMAIN={'icc':'sRGB IEC61966-2.1','primaries':'sRGB/Rec709','white_point':'D65','transfer':'sRGB piecewise encoded','range':[0,1],'precision':'float64 offline','alpha':'RGB only; masks separate','orientation':'upright','wb':'already applied; no automatic WB','input_kind':'rendered RGB, not spectral scene radiance'}
PARAMS={'exposure_ev':0.,'toe':.025,'shoulder':.3,'black':.012,'saturation':.9,'apparent_density':1.}
def digest(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,obj):
 p=pathlib.Path(p);p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(obj,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf8')
def decode(x):
 x=np.asarray(x,dtype=float)
 if not np.isfinite(x).all():raise ValueError('non-finite RGB')
 x=np.clip(x,0,1);return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def encode(x):
 x=np.clip(x,0,1);return np.where(x<=.0031308,x*12.92,1.055*x**(1/2.4)-.055)
def validate_rgb(x):
 x=np.asarray(x,dtype=float)
 if x.shape[-1]!=3 or not np.isfinite(x).all() or np.any(x<0) or np.any(x>1):raise ValueError('expected finite RGB [0,1], exactly 3 channels')
 return x

def v3_response(x,bw=False,amount=1):
 """Numerical transcription of pinned CandidateTone.cs; verified against linked C#.
 Spatial, emulsion, defects, paper and random shifts excluded deliberately.
 """
 if not np.isfinite(amount) or not 0<=amount<=1:raise ValueError('amount outside 0..1')
 x=validate_rgb(x)
 if amount==0:return x.copy()
 c=decode(x);l=c@np.array([.2126,.7152,.0722]);toe=l*l/(l+.025+1e-12);u=toe/(1+.3*toe);t=.012+.988*np.clip(u,0,1)
 if bw:mapped=np.repeat(t[...,None],3,axis=-1)
 else:
  sat=.9/(1+.25*np.max(c,axis=-1));scale=np.divide(t,l,out=np.zeros_like(t),where=l>1e-12)
  mapped=t[...,None]+(c-l[...,None])*(scale*sat)[...,None]
  mapped[...,0]+=.006*t*t;mapped[...,2]+=.004*(1-t)**2-.006*t*t
 out=encode(mapped)
 return x+(out-x)*amount

def candidate_response(x,bw=False,amount=1):
 """Independent continuous neutral style V1, NOT a measured Polaroid response.
 Uses existing low-dimensional toe/shoulder defaults; no inherited hue bias.
 Chroma scales with signal above black, so black-floor hue cannot divide by zero.
 """
 x=validate_rgb(x)
 if not np.isfinite(amount) or not 0<=amount<=1:raise ValueError('amount outside 0..1')
 if amount==0:return x.copy()
 c=decode(x);l=c@np.array([.2126,.7152,.0722]);toe=l*l/(l+.025+1e-12);u=toe/(1+.3*toe);signal=.988*np.clip(u,0,1);t=.012+signal
 if bw:mapped=np.repeat(t[...,None],3,axis=-1)
 else:
  sat=.9/(1+.25*np.max(c,axis=-1));scale=np.divide(signal,l,out=np.zeros_like(l),where=l>1e-12)
  mapped=t[...,None]+(c-l[...,None])*(scale*sat)[...,None]
 out=encode(mapped);return x+(out-x)*amount

def cube_grid(n):
 b,g,r=np.meshgrid(np.linspace(0,1,n),np.linspace(0,1,n),np.linspace(0,1,n),indexing='ij');return np.stack([r,g,b],axis=-1)
def cube_write(path,data,title):
 n=data.shape[0];lines=[f'TITLE "{title}"',f'LUT_3D_SIZE {n}','DOMAIN_MIN 0 0 0','DOMAIN_MAX 1 1 1']
 lines += [' '.join(f'{v:.10f}' for v in rgb) for rgb in data.reshape(-1,3)]
 pathlib.Path(path).write_text('\n'.join(lines)+'\n',encoding='ascii')
def cube_read(path):
 n=None;rows=[]
 for line in pathlib.Path(path).read_text().splitlines():
  t=line.split()
  if not t or t[0].startswith('#'):continue
  if t[0]=='LUT_3D_SIZE':n=int(t[1])
  elif t[0] not in ['TITLE','DOMAIN_MIN','DOMAIN_MAX']:rows.append([float(v) for v in t])
 a=np.array(rows)
 if n is None or a.shape!=(n**3,3):raise ValueError('invalid cube dimensions')
 return a.reshape(n,n,n,3)
def cube_sample(data,x):
 x=validate_rgb(x);n=data.shape[0];shape=x.shape
 return RegularGridInterpolator((np.linspace(0,1,n),)*3,data,method='linear',bounds_error=True)(x.reshape(-1,3)[:,::-1]).reshape(shape)
def save_rgb(path,x):
 profile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
 Image.fromarray(np.rint(np.clip(x,0,1)*255).astype('uint8')).save(path,icc_profile=profile)
def load_srgb(path,assume_srgb=False):
 im=Image.open(path)
 if im.mode not in ['RGB','RGBA']:raise ValueError('only 8-bit RGB/RGBA admitted; prepare 16-bit/float externally with audited ICC conversion')
 icc=im.info.get('icc_profile');im=im.convert('RGB')
 if icc:im=ImageCms.profileToProfile(im,ImageCms.ImageCmsProfile(io.BytesIO(icc)),ImageCms.createProfile('sRGB'),outputMode='RGB',renderingIntent=1)
 elif not assume_srgb:raise ValueError('untagged image: explicit --assume-srgb required')
 return np.asarray(im,dtype=float)/255

def metrics(a,b):
 de=deltaE_ciede2000(rgb2lab(validate_rgb(a)),rgb2lab(validate_rgb(b)))
 return {'n':int(de.size),'mean':float(de.mean()),'median':float(np.median(de)),'p90':float(np.percentile(de,90)),'p95':float(np.percentile(de,95)),'p99':float(np.percentile(de,99)),'max':float(de.max()),'rgb_rmse':float(np.sqrt(np.mean((a-b)**2)))}

def profile_generate():
 results=[]
 for kind,bw in [('color',False),('bw',True)]:
  pid=f'polaroid-modern-{kind}-style-smooth-1';p=ROOT/'profiles'/(pid+'.json');cube=p.with_suffix('.cube')
  data=cube_grid(33);cube_write(cube,candidate_response(data,bw),pid)
  obj={'stable_id':pid,'model_version':1,'implementation':'NeutralContinuousStyle1','evidence_grade':'STYLE_APPROXIMATION','ui_label':'风格近似 · 相机修正未标定','chemical_generation':'Modern Polaroid; batch unknown; NOT Impossible or Time-Zero','paper_families':['i-Type','600'],'camera_correction':{'id':'none-unmeasured','matrix':np.eye(3).tolist(),'calibrated':False},'input_domain':DOMAIN,'output_domain':DOMAIN,'parameters':dict(PARAMS,saturation=0 if bw else .9,creative_hue_bias=False,chroma_scale='signal_above_black_over_luminance'),'palette':'NeutralBW' if bw else 'Color','sources':['first-party-independent-neutral-model','existing-CandidateResponse-defaults'], 'change_from_v3':'remove creative hue bias; continuous chroma above black; legacy algorithm untouched','data_manifest':'data_manifest.json','data_manifest_sha256':digest(ROOT/'data_manifest.json'),'model_code_sha256':digest(ROOT/'offline.py'),'calibration_eligible':False,'physical_or_spectral_model':False,'random_effects':{'enabled':False,'frozen_seed':20261017,'prng':'existing SplitMix64SubstreamsV1; unused in colour-only prototype'},'spatial_emulsion_defects_paper':'excluded; reuse existing product components later','strength_scope':'photo colour only; linear blend in encoded sRGB matching existing CandidateSpatial; no frame pixels','license':{'weights':'first-party independent neutral model using documented low-dimensional defaults; existing repository terms unchanged','third_party_lut_or_fit_used':False,'distribution':'internal offline only, product approval not granted'},'cube_sha256':digest(cube),'model_hash_definition':'SHA256 of UTF-8 profile file; see profile-index.json','integration_status':'OFFLINE_CANDIDATE_ONLY'}
  dump(p,obj);results.append({'id':pid,'path':str(p.relative_to(ROOT)),'model_sha256':digest(p),'cube':str(cube.relative_to(ROOT)),'cube_sha256':digest(cube)})
 dump(ROOT/'profiles/profile-index.json',results)
 return results

def comparison():
 source=ROOT/'references/skimage/astronaut.png';x=load_srgb(source,True)
 # Public-domain source is visual example only, never a paired film measurement.
 canvas=Image.new('RGB',(1536,600),(21,25,29));d=ImageDraw.Draw(canvas)
 for i,(name,img) in enumerate([('INPUT / public-domain NASA portrait',x),('COLOR / SMOOTH STYLE V1',candidate_response(x)),('B&W / SMOOTH STYLE V1',candidate_response(x,True))]):
  canvas.paste(Image.fromarray(np.rint(img*255).astype('uint8')),(i*512,52));d.text((i*512+14,18),name,fill='white')
 d.text((14,577),'Colour-only smooth V1 candidate. NOT real Polaroid output. No camera correction, texture, frame or temperature response.',fill=(210,210,210))
 canvas.save(ROOT/'results/comparison-portrait.png',icc_profile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes())
 # Smooth gradients, no downloaded chart data or patches.
 grad=np.tile(np.linspace(0,1,768)[None,:,None],(72,1,3));prim=[]
 for color in [[1,0,0],[0,1,0],[0,0,1],[1,.2,0],[.6,0,1],[0,1,.8]]:prim.append(grad*np.array(color))
 chart=np.concatenate([grad,*prim],axis=0)
 chart=np.concatenate([chart,candidate_response(chart),candidate_response(chart,True)],axis=1);save_rgb(ROOT/'results/comparison-gradients.png',chart)

def interpolation_validation():
 rng=np.random.default_rng(20261028);x=np.concatenate([rng.random((10000,3)),cube_grid(2).reshape(-1,3),np.repeat(np.linspace(0,1,4097)[:,None],3,axis=1)])
 report={'meaning':'LUT approximation of FIRST-PARTY creative transform; not third-party or film-photo fit','thresholds_fixed_before_run':{'max_rgb_abs':.02,'p95_de00':1.,'neutral_monotonic_epsilon':1e-10},'profiles':{}}
 for kind,bw in [('color',False),('bw',True)]:
  p=ROOT/'profiles'/f'polaroid-modern-{kind}-style-smooth-1.cube';data=cube_read(p);actual=cube_sample(data,x);want=candidate_response(x,bw)
  ramp=np.repeat(np.linspace(0,1,4097)[:,None],3,axis=1);out=cube_sample(data,ramp);lum=decode(out)@np.array([.2126,.7152,.0722]);error=np.max(np.abs(actual-want));m=metrics(actual,want)
  r={'distribution':m,'max_rgb_abs':float(error),'gray_min_luminance_step':float(np.diff(lum).min()),'max_gray_second_difference':float(np.abs(np.diff(out,n=2,axis=0)).max()),'strength_zero_exact':bool(np.array_equal(candidate_response(x,bw,0),x)),'strength_one_exact':bool(np.array_equal(candidate_response(x,bw,1),want)),'in_range_and_finite':bool(np.isfinite(actual).all() and np.min(actual)>=0 and np.max(actual)<=1),'status':'PASS' if error<.02 and m['p95']<1 and np.diff(lum).min()>=-1e-10 else 'FAIL'}
  report['profiles'][kind]=r
 dump(ROOT/'results/interpolation-validation.json',report)

def features(x,order):
 dr=x[:,0]-x[:,1];db=x[:,2]-x[:,1];y=x@np.array([.2126,.7152,.0722])
 return np.stack([dr,db] if order==1 else [dr,db,dr*y,db*y,dr*db],axis=1)
def fit_model(x,target,order=1):
 x=validate_rgb(x);target=validate_rgb(target);lin=decode(x);tl=decode(target);lum=lin@np.array([.2126,.7152,.0722]);yl=tl@np.array([.2126,.7152,.0722])
 neutral=np.ptp(x,axis=1)<.04
 if neutral.sum()<8 or np.ptp(lum[neutral])<.45:raise ValueError('need >=8 neutral patches spanning linear luminance >=.45')
 knots=np.linspace(0,1,9)
  # Independent bounded monotonic curve parameterisation: positive increments, bounded endpoints.
 def curve(v):
  inc=np.exp(v[:8]);vals=np.r_[0,np.cumsum(inc)/inc.sum()];black=.08/(1+np.exp(-v[8]));white=.8+.2/(1+np.exp(-v[9]));return black+(white-black)*vals
 def residual(v):
  vals=curve(v);pred=PchipInterpolator(knots,vals)(lum[neutral]);return np.r_[pred-yl[neutral],.15*np.diff(vals,n=2)]
 v=least_squares(residual,np.r_[np.zeros(8),-3.,2.],bounds=(-7,7),max_nfev=1500).x
 tone=curve(v);mapped=PchipInterpolator(knots,tone)(lum);scale=np.divide(mapped,lum,out=np.ones_like(lum),where=lum>1e-10);base=mapped[:,None]+(lin-lum[:,None])*np.divide(mapped-tone[0],lum,out=np.zeros_like(lum),where=lum>1e-10)[:,None]
 coeff=np.zeros((2 if order==1 else 5,3));f=features(lin,order)
 if order:
  def res(c):
   c=c.reshape(-1,3);return np.r_[(base+f@c-tl).ravel(),.25*c.ravel()]
  coeff=least_squares(res,coeff.ravel(),bounds=(-.3,.3),max_nfev=500).x.reshape(-1,3)
 return {'version':1,'kind':'MonotonicTonePlusNeutralPreservingCrossTerms','order':order,'knots':knots.tolist(),'values':tone.tolist(),'coefficients':coeff.tolist(),'input_domain':DOMAIN,'output_domain':DOMAIN,'target_semantics':'film + fixed scan appearance, not film reflectance alone','smooth_residual_lut':'DISABLED; insufficient controlled data','regularization':{'tone_second_diff':.15,'coeff_l2':.25,'coeff_bounds':[-.3,.3]},'production_approved':False}
def predict(x,model):
 x=validate_rgb(x);lin=decode(x);lum=lin@np.array([.2126,.7152,.0722]);tone=PchipInterpolator(model['knots'],model['values'])(lum);base=tone[:,None]+(lin-lum[:,None])*np.divide(tone-model['values'][0],lum,out=np.zeros_like(lum),where=lum>1e-10)[:,None]
 if model['order']:base+=features(lin,model['order'])@np.array(model['coefficients'])
 # Exact black is achromatic and respects fitted black endpoint.
 base[lum<=1e-10]=model['values'][0]
 return encode(base)
REQUIRED=['scene_id','batch_id','parent_capture_id','split','camera','paper','generation','film_date','exposure','illuminant','flash','development_celsius','development_seconds','scanner','scanner_icc_sha256','auto_enhancement','reference_sha256','scan_sha256','registration','rights','normalization']
def validate_manifest(manifest):
 groups={k:{} for k in ['scene_id','batch_id','parent_capture_id','reference_sha256','scan_sha256']};seen=set()
 records=manifest['records']
 real=manifest.get('data_type')=='REAL_PAIRED_FILM_PLUS_FIXED_SCAN'
 if not records:raise ValueError('no real paired data admitted')
 for r in records:
  if real:
   for key in ['reference','scan']:
    path=pathlib.Path(r[key+'_path'])
    if not path.is_file() or digest(path)!=r[key+'_sha256']:raise ValueError('paired file/hash mismatch: '+key)
  for k in REQUIRED:
   if k not in r or r[k] is None or r[k]=='':raise ValueError('missing metadata: '+k)
  if r['split'] not in ['development','validation','heldout']:raise ValueError('invalid split')
  if r['generation'] in ['unknown','mixed']:raise ValueError('chemical generation must be explicit')
  if r['auto_enhancement'] is not False:raise ValueError('scanner enhancement must be disabled')
  if r['normalization'].get('method')!='exposure-wb-only-predeclared':raise ValueError('arbitrary image grading not admitted')
  if r['rights'].get('fit_allowed') is not True:raise ValueError('data fitting rights not cleared')
  for k in groups:
   old=groups[k].setdefault(r[k],r['split'])
   if old!=r['split']:raise ValueError('cross-split leakage: '+k)
  if r['id'] in seen:raise ValueError('duplicate record id')
  seen.add(r['id'])
  for p in r['patches']:
   validate_rgb(p['input']);validate_rgb(p['target'])
   if p.get('region') not in ['gray','skin','skin_highlight','highlight','red_orange','purple','blue_green','shadow','backlight','other','border','reflection','scratch','misregistration']:raise ValueError('missing region label')
   if not isinstance(p.get('valid'),bool):raise ValueError('explicit valid-region flag required')
 if len({(r['paper'],r['generation']) for r in records})!=1:raise ValueError('fit one paper and generation at a time')
 if len({r['scanner_icc_sha256'] for r in records})!=1:raise ValueError('fixed scan profile required')
 if {r['split'] for r in records}!={'development','validation','heldout'}:raise ValueError('all three independent splits required')
 return records

def calibrate(path,out):
 manifest=json.loads(pathlib.Path(path).read_text(encoding='utf-8-sig'));records=validate_manifest(manifest);out=pathlib.Path(out);out.mkdir(parents=True,exist_ok=True)
 regions=['gray','skin','skin_highlight','highlight','red_orange','purple','blue_green','shadow','backlight'];rows={s:[] for s in ['development','validation','heldout']}
 for r in records:
  for p in r['patches']:
   if p['valid'] and p['region'] not in ['border','reflection','scratch','misregistration']:rows[r['split']].append(p)
 if any(not v for v in rows.values()):raise ValueError('empty valid split')
 dev=rows['development'];x=np.array([p['input'] for p in dev]);y=np.array([p['target'] for p in dev]);report={'data_type':manifest.get('data_type'),'input_manifest_sha256':digest(path),'model_selection_rule':'validation only; heldout reported, never used for fitting/selection','thresholds':manifest['thresholds'],'models':{}}
 scores={}
 for label,order in [('tone-only',0),('tone-matrix',1),('tone-cross',2)]:
  model=fit_model(x,y,order);dump(out/(label+'.json'),model);evaluation={}
  for split,rr in rows.items():
   xx=np.array([p['input'] for p in rr]);yy=np.array([p['target'] for p in rr]);pred=predict(xx,model);parts={'all':metrics(pred,yy)}
   for reg in regions:
    mask=np.array([p['region']==reg for p in rr]);parts[reg]=metrics(pred[mask],yy[mask]) if mask.any() else {'status':'NO_DATA'}
   evaluation[split]=parts
  report['models'][label]=evaluation;scores[label]=evaluation['validation']['all']['p95']
 selected=min(scores,key=scores.get);report['selected_by_validation']=selected
 held=report['models'][selected]['heldout'];checks=[]
 for region,threshold in manifest['thresholds'].items():
  m=held.get(region);checks.append(bool(m and 'p95' in m and m['p95']<=threshold))
 report['heldout_gate']='PASS' if checks and all(checks) else 'FAIL_OR_MISSING_REQUIRED_REGION'
 report['physical_accuracy_claim']=False;report['blind_review']='NOT_RUN';dump(out/'fit-report.json',report)
 return report

def main():
 parser=argparse.ArgumentParser();sub=parser.add_subparsers(dest='cmd',required=True)
 sub.add_parser('generate');sub.add_parser('validate')
 p=sub.add_parser('calibrate');p.add_argument('manifest');p.add_argument('--out',required=True)
 p=sub.add_parser('apply');p.add_argument('input');p.add_argument('output');p.add_argument('--bw',action='store_true');p.add_argument('--amount',type=float,default=1);p.add_argument('--assume-srgb',action='store_true')
 args=parser.parse_args()
 if args.cmd=='generate':profile_generate();comparison();interpolation_validation()
 elif args.cmd=='validate':interpolation_validation()
 elif args.cmd=='calibrate':calibrate(args.manifest,args.out)
 elif args.cmd=='apply':save_rgb(args.output,candidate_response(load_srgb(args.input,args.assume_srgb),args.bw,args.amount))
if __name__=='__main__':main()

