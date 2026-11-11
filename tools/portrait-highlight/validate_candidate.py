"""Validate the isolated candidate, real staged functions, reference scope, and export/reopen parity."""
import argparse,csv,json,math,os,subprocess,time
from pathlib import Path
import numpy as np,tifffile,psutil
from PIL import Image,ImageDraw
from run_baseline import decode,encode,luma,hue,execute,REPO
from prepare_input import sha

def linear_metrics(values):
 a=np.asarray(values);hh,cc=hue(a);valid=cc>1e-6;angles=np.radians(hh[valid]);mean=float(np.degrees(np.arctan2(np.sin(angles).mean(),np.cos(angles).mean()))) if valid.any() else None
 return dict(hueCircularMeanDegrees=mean,hueUndefinedCount=int((~valid).sum()),opponentChromaRange=[float(cc.min()),float(cc.max())],min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist(),aboveOne=(a>1).sum(axis=0).tolist(),belowZero=(a<0).sum(axis=0).tolist(),atOne=(a==1).sum(axis=0).tolist(),nonfinite=int((~np.isfinite(a)).sum()),linearYRange=[float(luma(a).min()),float(luma(a).max())])
def angle_error(before,after):
 h0,c0=hue(before);h1,c1=hue(after);valid=(c0>1e-6)&(c1>1e-6);d=np.abs((h1-h0+180)%360-180)
 return dict(maxDegrees=float(d[valid].max()) if valid.any() else None,p99Degrees=float(np.percentile(d[valid],99)) if valid.any() else None,definedCount=int(valid.sum()),undefinedCount=int((~valid).sum()))
def curve_diagnostics(encoded):
 a=decode(encoded);h,c=hue(a)
 return dict(rgbFlatSteps=int(np.all(np.diff(encoded,axis=0)==0,axis=1).sum()),channelFlatSteps=(np.diff(encoded,axis=0)==0).sum(axis=0).tolist(),maxChannelAtOne=int(np.sum(np.max(encoded,axis=1)>=1)),undefinedHueCount=int((c<=1e-6).sum()))
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--local-root',type=Path,required=True);a=ap.parse_args();root=a.local_root.resolve();out=root/'candidate-validation';out.mkdir(exist_ok=False)
 recipe=dict(Version=1,ExposureEv=5,Knee=.75,Input='RenderedSrgbD65',Output='SrgbD65RationalMaxShoulderV1',ResearchOnly=True)
 (out/'recipe.json').write_text(json.dumps(recipe,indent=2))
 refs=json.loads((root/'reference-manifest.json').read_text());os.environ['PATH']=str(Path(refs['build'][0]['command'][0]).parent)+os.pathsep+os.environ['PATH'];probe=REPO/'tools/PortraitHighlightProbe/bin/Release/net10.0/PortraitHighlightProbe.dll'
 calls=[];cases={};metrics={};deltas={};baseline=root/'baseline'
 for name,w,h,inputpath in [('ramp',193,3,baseline/'ramp-encoded.f32'),('portrait',384,384,root/'portrait-encoded.f32')]:
  target=out/(name+'-product');calls.append(execute(['dotnet',probe,inputpath,target,w,h,5,'encoded-srgb','trace',out/'recipe.json','icc='+str(root/'source-srgb.icc')],out,name+'-product'))
  cases[name]=dict(target=target,w=w,h=h,input=inputpath)
  for file in target.glob('*.f32'):
   v=np.fromfile(file,dtype='<f4').reshape(-1,3);linear=decode(v) if file.stem in ['output','candidate','output-clipped','encoded-preclip','basic-gamut-oetf','basic-tone-output'] else v
   metrics[name+'-'+file.stem]=dict(storedDomain='sRGB encoded' if linear is not v else 'linear sRGB D65',storedChannelRange=[v.min(axis=0).tolist(),v.max(axis=0).tolist()],linear=linear_metrics(linear))
   old=baseline/(name+'-product')/file.name
   if old.exists():deltas[name+'-'+file.stem]=dict(exact=old.read_bytes()==file.read_bytes(),maxAbs=float(abs(np.fromfile(old,dtype='<f4')-v.ravel()).max()))
  candidate=np.fromfile(target/'candidate.f32',dtype='<f4').reshape(-1,3);original=np.fromfile(inputpath,dtype='<f4').reshape(-1,3);production=np.fromfile(target/'output.f32',dtype='<f4').reshape(-1,3)
  exposed=decode(original)*32
  metrics[name+'-candidate-hue']=angle_error(exposed,decode(candidate));metrics[name+'-production-hue']=angle_error(exposed,decode(production))
  # Re-run two references with the exact product-observed EOTF buffer, isolating gain from bridge precision.
  observed=target/'decoded-display-linear.f32'
  for ref in ['dt','rt']:
   dest=out/(name+'-'+ref+'-same-observed-decode.f32');cmd=[root/'reference-build'/(ref+'.exe'),observed,dest,5,w*h]+([0] if ref=='rt' else []);calls.append(execute(cmd,out,name+'-'+ref+'-same-decode'));same=np.fromfile(dest,dtype='<f4').reshape(-1,3);productLinear=np.fromfile(target/'exposed-linear.f32',dtype='<f4').reshape(-1,3)
   metrics[name+'-'+ref+'-same-decode']=dict(exact=bool(np.array_equal(same,productLinear)),maxAbs=float(abs(same-productLinear).max()));assert np.array_equal(same,productLinear)
 # Reopen in a genuinely new process from saved recipe + original frozen input.
 recipe=dict(Version=1,ExposureEv=5,Knee=.75,Input='RenderedSrgbD65',Output='SrgbD65RationalMaxShoulderV1',ResearchOnly=True)
 (out/'recipe.json').write_text(json.dumps(recipe,indent=2));restored=json.loads((out/'recipe.json').read_text())
 calls.append(execute(['dotnet',probe,root/'portrait-encoded.f32',out/'portrait-reopened',384,384,restored['ExposureEv'],'encoded-srgb',out/'recipe.json','icc='+str(root/'source-srgb.icc')],out,'reopened'))
 reopenExact=(out/'portrait-reopened/candidate.f32').read_bytes()==(out/'portrait-product/candidate.f32').read_bytes();assert reopenExact
 candidate=np.fromfile(out/'portrait-product/candidate.f32',dtype='<f4').reshape(384,384,3)
 # Export row quantization matches production float multiplication/nearest-even boundary.
 q=np.rint(candidate*np.float32(65535)).clip(0,65535).astype('<u2');profile=tifffile.TiffFile(root/'portrait-crop.rgb16.tif').pages[0].tags[34675].value
 tifffile.imwrite(out/'candidate.rgb16.tif',q,photometric='rgb',iccprofile=profile,metadata=None);back=tifffile.imread(out/'candidate.rgb16.tif');assert np.array_equal(q,back)
 productBack=tifffile.imread(out/'portrait-product/candidate-product-rgb16.tif');assert np.array_equal(productBack,q)
 with tifffile.TiffFile(out/'portrait-product/candidate-product-rgb16.tif') as t:assert t.pages[0].tags[34675].value==profile
 exportError=float(abs(back.astype(np.float64)/65535-candidate).max());assert exportError<=1/65535
 # Show real ROI at native sampling; output panel labels indicate the +5EV stress.
 panels=[('frozen input',np.fromfile(root/'portrait-encoded.f32',dtype='<f4').reshape(384,384,3)),('production +5 EV',np.fromfile(out/'portrait-product/output.f32',dtype='<f4').reshape(384,384,3)),('candidate V1 +5 EV',candidate),('RT high-comp60 +5 EV',encode(np.fromfile(baseline/'portrait-rt-highlight60.f32',dtype='<f4').reshape(384,384,3)))]
 sheet=Image.new('RGB',(384*4,414),'#181818');draw=ImageDraw.Draw(sheet)
 for i,(label,v) in enumerate(panels):sheet.paste(Image.fromarray(np.rint(np.clip(v,0,1)*255).astype('uint8')),(i*384,30));draw.text((i*384+8,8),label,fill='white')
 sheet.save(out/'portrait-comparison.png')
 rows=[];curveSummary={};ev=np.linspace(-6,6,193)
 stageNames=['decoded-display-linear','exposed-linear','encoded-preclip','output','candidate','basic-decoded-linear','basic-tone-linear','basic-gamut-oetf','basic-tone-output']
 ramps={s:np.fromfile(out/'ramp-product'/(s+'.f32'),dtype='<f4').reshape(3,193,3) for s in stageNames}
 for k,name in enumerate(['neutral','skin-approximation','warm']):
  before=ramps['exposed-linear'][k];curveSummary[name]={s:curve_diagnostics(ramps[s][k]) for s in ['output','candidate','basic-tone-output']};curveSummary[name]['candidateHue']=angle_error(before,decode(ramps['candidate'][k]))
  for j,e in enumerate(ev):
   row=dict(color=name,ev=float(e),inputY=float(luma(before[j])))
   for s,v in ramps.items():
    lin=decode(v[k]) if s in ['encoded-preclip','output','candidate','basic-gamut-oetf','basic-tone-output'] else v[k]
    hh,cc=hue(lin[j]);row[s+'_Y']=float(luma(lin[j]));row[s+'_hue']=float(hh) if cc>1e-6 else ''
    for c,tag in enumerate('RGB'):row[s+'_'+tag]=float(v[k,j,c])
   for ref in ['dt','rt-exposure','rt-highlight60']:
    v=np.fromfile(baseline/('ramp-'+ref+'.f32'),dtype='<f4').reshape(3,193,3);row[ref+'_Y']=float(luma(v[k,j]))
    for c,tag in enumerate('RGB'):row[ref+'_'+tag]=float(v[k,j,c])
   rows.append(row)
 with (out/'curves.csv').open('w',newline='') as f:writer=csv.DictWriter(f,fieldnames=rows[0]);writer.writeheader();writer.writerows(rows)
 # Quantization is an explicit remaining loss, even when float curve stays strictly increasing.
 quantized=np.rint(ramps['candidate']*np.float32(65535)).clip(0,65535).astype(np.uint16)
 quantizationFlats={name:int(np.all(np.diff(quantized[k].astype(np.int32),axis=0)==0,axis=1).sum()) for k,name in enumerate(['neutral','skin-approximation','warm'])}
 # Negative and invalid domain fixtures, and real function memory admission refusals.
 refusals=[]
 for name,values,w,h,domain in [('bad-shape',[0,0,0],513,513,'encoded-srgb'),('bad-domain',[0,0,0],1,1,'sensor-native'),('nan',[np.nan,0,0],1,1,'encoded-srgb')]:
  path=out/(name+'.f32');np.array(values,dtype='<f4').tofile(path);r=subprocess.run(['dotnet',str(probe),str(path),str(out/name),str(w),str(h),'0',domain],capture_output=True,text=True);refusals.append(dict(case=name,exitCode=r.returncode,refused=r.returncode!=0,error=(r.stderr or r.stdout).splitlines()[:2]));assert r.returncode
 # Pin a negative/>1 linear reference stress; no RGB16/source recovery claim.
 stress=np.array([[-.1,.18,1.4],[0,0,0],[1,1,1],[4,.7,.1],[1,0,0],[0,1,0],[0,0,1]],dtype='<f4');stress.tofile(out/'negative-headroom.f32')
 for ref in ['dt','rt']:
  dest=out/(ref+'-negative-headroom.f32');cmd=[root/'reference-build'/(ref+'.exe'),out/'negative-headroom.f32',dest,0,len(stress)]+([0] if ref=='rt' else []);calls.append(execute(cmd,out,ref+'-stress'));v=np.fromfile(dest,dtype='<f4').reshape(len(stress),3);metrics[ref+'-stress']=linear_metrics(v);assert np.allclose(v,stress,rtol=2e-7,atol=1e-7)
 calls.append(execute(['dotnet',probe,baseline/'ramp-prophoto.f32',out/'raw-double-product',193,3,0,'linear-prophoto-d65','trace'],out,'raw-double-product'))
 # Read synthetic scene RAW output captured through exact production function.
 rawStages={p.stem:np.fromfile(p,dtype='<f4').reshape(3,193,3) for p in (out/'raw-double-product').glob('*.f32')}
 raw={}
 for k,name in enumerate(['neutral','skin-approximation','warm']):
  raw[name]={}
  for stage,v in rawStages.items():
   flat=np.where(np.all(np.diff(v[k],axis=0)==0,axis=1))[0];raw[name][stage]=dict(firstFloat32EntireRgbFlatEv=float(ev[flat[0]]) if len(flat) else None,atOne=(v[k]==1).sum(axis=0).tolist())
 precision={}
 for branch in ['ramp-product','raw-double-product']:
  precision[branch]={}
  for file in (out/branch).glob('*.f64'):
   data=np.fromfile(file,dtype='<f8').reshape(3,193,3);precision[branch][file.stem]={}
   for k,name in enumerate(['neutral','skin-approximation','warm']):
    idx=np.where(np.all(np.diff(data[k],axis=0)==0,axis=1))[0];precision[branch][file.stem][name]=dict(firstFloat64EntireRgbFlatEv=float(ev[idx[0]]) if len(idx) else None)
 rawDouble={}
 for stage in ['matrix-linear-srgb','luminance-shoulder','neutral-axis-gamut']:
  data=np.fromfile(out/'raw-double-product'/(stage+'.f64'),dtype='<f8').reshape(3,193,3)
  rawDouble[stage]={name:angle_error(np.fromfile(baseline/'ramp-prophoto.f32',dtype='<f4').reshape(3,193,3)[k]@np.linalg.inv(np.array([[.529317,.330092,.140588],[.098368,.873465,.028169],[.016879,.117663,.865457]])).T,data[k]) for k,name in enumerate(['neutral','skin-approximation','warm'])}
 report=dict(rawDoubleLinearHue=rawDouble,precision=precision,version='PortraitHighlightCandidateValidationV1',candidateRecipe=recipe,metrics=metrics,stageTraceParityWithBaseline=deltas,curves=curveSummary,quantizedRgb16FlatSteps=quantizationFlats,rawSynthetic=raw,newProcessReopenExact=reopenExact,productTiff16MatchesIndependentQuantizer=True,embeddedProfileExact=True,tiff16RoundtripExact=True,tiff16MaxEncodedError=exportError,negativeRefusals=refusals,calls=calls,admission='RESEARCH_ONLY_NOT_PRODUCT_APPROVED',limits=['Frozen dark portrait with explicit +5 EV stress, not a calibrated bright RAW capture','No sensor/headroom reconstruction; no early RAW stages observed','Constant linear RGB ratios preserve opponent hue, not guaranteed perceptual hue in all viewing conditions','Candidate changes the shoulder even at EV0 above knee; explicit version and separate recipe required','Two original CPU reference stages executed, not full application outputs','No camera/color-chart accuracy, general ICC, human aesthetic or company RAW batch approval'])
 (root/'candidate-report.json').write_text(json.dumps(report,indent=2));assert all(d['exact'] for d in deltas.values());print(json.dumps(dict(curves=curveSummary,portraitHue={k:v for k,v in metrics.items() if k.startswith('portrait-') and k.endswith('-hue')},reopenExact=reopenExact,quantizedFlats=quantizationFlats,rawSynthetic=raw),indent=2))
if __name__=='__main__':main()
