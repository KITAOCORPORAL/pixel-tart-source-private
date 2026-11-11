"""Bounded baseline stage comparison. Photos and rendered pixels stay under --local-root."""
import argparse,csv,hashlib,json,math,os,subprocess,time
from pathlib import Path
import numpy as np,psutil
from prepare_input import sha
REPO=Path(__file__).resolve().parents[2]
def decode(v):return np.where(v<=.04045,v/12.92,((np.maximum(v,0)+.055)/1.055)**2.4)
def encode(v):return np.where(v<=.0031308,v*12.92,1.055*np.maximum(v,0)**(1/2.4)-.055)
def luma(v):return v@np.array([.2126,.7152,.0722])
def hue(v):
 # Linear hue angle in an orthonormal opponent plane; achromatic pixels are undefined.
 a=2*v[...,0]-v[...,1]-v[...,2];b=np.sqrt(3)*(v[...,1]-v[...,2]);h=np.degrees(np.arctan2(b,a));c=np.sqrt(a*a+b*b);return h,c

def metrics(v):
 v=np.asarray(v);return dict(min=v.min(axis=0).tolist(),max=v.max(axis=0).tolist(),aboveOne=(v>1).sum(axis=0).tolist(),belowZero=(v<0).sum(axis=0).tolist(),atOne=(v==1).sum(axis=0).tolist(),nonfinite=int((~np.isfinite(v)).sum()),luminanceRange=[float(luma(v).min()),float(luma(v).max())])
def execute(command,root,tag):
 start=time.perf_counter();r=subprocess.Popen([str(x) for x in command],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True);peak=0
 while r.poll() is None:
  try:
   info=psutil.Process(r.pid).memory_info();peak=max(peak,getattr(info,'private',info.rss))
   if peak>512*1048576:r.kill();raise RuntimeError('512 MiB child process bound exceeded')
  except psutil.NoSuchProcess:pass
  time.sleep(.01)
 stdout,stderr=r.communicate();(root/(tag+'-console.txt')).write_text(stdout+stderr)
 if r.returncode:raise RuntimeError(f'{tag} failed {r.returncode}: {stderr}')
 return dict(command=[str(x) for x in command],elapsedMs=(time.perf_counter()-start)*1000,sampledPeakPrivateBytes=peak,exitCode=r.returncode,scope='10ms sampled child private bytes; sampling lower bound')
def main():
 p=argparse.ArgumentParser();p.add_argument('--local-root',type=Path,required=True);a=p.parse_args();root=a.local_root.resolve();out=root/'baseline';out.mkdir(exist_ok=False)
 ev=np.linspace(-6,6,193);shapes=np.array([[1,1,1],[.76,.42,.26],[1,.3,.045]]);base=shapes*(.18/luma(shapes))[:,None];scene=base[:,None,:]*2**ev[None,:,None]
 # Same theoretical linear exposure ladder, encoded input stays in [0,1], then actual product EV+5.
 source=encode(scene/32).astype('<f4');assert source.max()<1
 source.tofile(out/'ramp-encoded.f32');decode(source).astype('<f4').tofile(out/'ramp-linear.f32')
 calls=[];probe=REPO/'tools/PortraitHighlightProbe/bin/Release/net10.0/PortraitHighlightProbe.dll'
 calls.append(execute(['dotnet',probe,out/'ramp-encoded.f32',out/'ramp-product',193,3,5,'encoded-srgb','trace'],out,'ramp-product'))
 calls.append(execute(['dotnet',probe,root/'portrait-encoded.f32',out/'portrait-product',384,384,5,'encoded-srgb','trace'],out,'portrait-product'))
 portrait=np.fromfile(root/'portrait-encoded.f32',dtype='<f4').reshape(-1,3);decode(portrait).astype('<f4').tofile(out/'portrait-linear.f32')
 refmanifest=json.loads((root/'reference-manifest.json').read_text());env=os.environ.copy();env['PATH']=str(Path(refmanifest['build'][0]['command'][0]).parent)+os.pathsep+env['PATH'];os.environ.update(PATH=env['PATH'])
 stats={};raw={}
 for name,n in [('ramp',579),('portrait',384*384)]:
  linear=np.fromfile(out/(name+'-linear.f32'),dtype='<f4').reshape(-1,3)
  for ref,comp in [('dt',None),('rt-exposure',0),('rt-highlight60',60)]:
   dest=out/(name+'-'+ref+'.f32');cmd=[root/'reference-build'/('dt.exe' if ref=='dt' else 'rt.exe'),out/(name+'-linear.f32'),dest,5,n];cmd+=[] if comp is None else [comp];calls.append(execute(cmd,out,name+'-'+ref));v=np.fromfile(dest,dtype='<f4').reshape(-1,3);stats[name+'-'+ref]=metrics(v)
  prod=out/(name+'-product');exposed=np.fromfile(prod/'exposed-linear.f32',dtype='<f4').reshape(-1,3);expected=linear*32;stats[name+'-production-exposure']=metrics(exposed)
  dt=np.fromfile(out/(name+'-dt.f32'),dtype='<f4').reshape(-1,3);rt=np.fromfile(out/(name+'-rt-exposure.f32'),dtype='<f4').reshape(-1,3)
  raw[name]=dict(dtMaxAbsVsProductExposure=float(abs(exposed-dt).max()),rtMaxAbsVsProductExposure=float(abs(exposed-rt).max()))
  for f in prod.glob('*.f32'):stats[name+'-'+f.stem]=metrics(np.fromfile(f,dtype='<f4').reshape(-1,3))
 ramp={f.stem:np.fromfile(f,dtype='<f4').reshape(3,193,3) for f in (out/'ramp-product').glob('*.f32')}
 rows=[];findings={}
 for k,name in enumerate(['neutral','skin-approximation','warm']):
  clip=ramp['output'][k];linear=decode(clip);hin,cin=hue(scene[k]);hout,cout=hue(linear);delta=np.abs((hout-hin+180)%360-180);delta=np.where((cin>1e-6)&(cout>1e-6),delta,np.nan)
  first=np.where((clip>=1).any(axis=1))[0];flat=np.where((np.diff(clip,axis=0)==0).all(axis=1))[0];h1=np.where(delta>1)[0]
  findings[name]=dict(firstClippedEv=float(ev[first[0]]) if len(first) else None,firstEntireRgbFlatEv=float(ev[flat[0]]) if len(flat) else None,firstHueErrorAbove1DegreeEv=float(ev[h1[0]]) if len(h1) else None,maxHueErrorDegrees=float(np.nanmax(delta)) if np.isfinite(delta).any() else None,undefinedHueCount=int((~np.isfinite(delta)).sum()))
  for i,e in enumerate(ev):
   row=dict(color=name,exposureStops=float(e),inputY=float(luma(scene[k,i])),outputY=float(luma(linear[i])),hueErrorDegrees=float(delta[i]) if np.isfinite(delta[i]) else '')
   for stage,v in ramp.items():
    for j,c in enumerate('rgb'):row[stage+'_'+c]=float(v[k,i,j])
   for ref in ['dt','rt-exposure','rt-highlight60']:
    v=np.fromfile(out/('ramp-'+ref+'.f32'),dtype='<f4').reshape(3,193,3)
    for j,c in enumerate('rgb'):row[ref+'_'+c]=float(v[k,i,j])
   rows.append(row)
 with (out/'curves.csv').open('w',newline='') as f:w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
 # Explicit synthetic scene-linear RAW branch. It is not a recovered sensor buffer.
 P=np.array([[.529317,.330092,.140588],[.098368,.873465,.028169],[.016879,.117663,.865457]])
 (scene@P.T).astype('<f4').tofile(out/'ramp-prophoto.f32');calls.append(execute(['dotnet',probe,out/'ramp-prophoto.f32',out/'raw-linear-product',193,3,0,'linear-prophoto-d65','trace'],out,'raw-linear-product'))
 report=dict(version='PortraitHighlightBaselineV1',inputManifestSha256=sha(root/'input-manifest.json'),referenceManifestSha256=sha(root/'reference-manifest.json'),stageMetrics=stats,comparisons=raw,findings=findings,calls=calls,synthetic=dict(exposureMin=-6,exposureMax=6,step=1/16,rows=['neutral','skin-approximation','warm'],baseLinearRgb=base.tolist(),inputEncoding='sRGB OETF of target linear /32, float32; actual rendered EV+5',realSceneHeadroom=False),firstLoss='RenderedSrgbExposure.Apply: encoded-preclip -> Math.Clamp per channel. EOTF and exposure preserve >1; darktable exposure and RT zero-compression float stage preserve >1.',scope='First-round localization on frozen rendered portrait and synthetic linear vectors. No sensor clipping localization, full-app reference rendering or photographic quality approval.')
 (root/'baseline-report.json').write_text(json.dumps(report,indent=2));print(json.dumps(dict(findings=findings,comparisons=raw),indent=2))
if __name__=='__main__':main()
