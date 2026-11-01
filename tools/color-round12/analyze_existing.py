"""Round12 offline causal diagnostics. No engine, RAW decoder or GPU imports.
Independent analytic probes are compared with saved observations, not used to render.
"""
import argparse, csv, hashlib, json, math, struct, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

REPO=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(REPO/'tools/color-round11'))
from verify_saved_outputs import sha, stage_filename
from diagnostic.package_validation import read, need

DOMAINS={
 'prepared':'standard ProPhoto D50 linear RGB (scene proxy after fixed production bridge)',
 'film_scale_and_expose':'relative film sensitivity-weighted linear exposure channels; not display RGB',
 'film_boost':'relative film sensitivity-weighted linear exposure channels',
 'film_overscan':'relative film sensitivity-weighted linear exposure channels',
 'film_blurs':'relative film sensitivity-weighted linear exposure channels',
 'film_log_and_curves':'base-normalized dye density coefficients C/M/Y (log10 optical density basis), not RGB',
 'film_couplers':'base-normalized dye density coefficients C/M/Y',
 'film_grain':'base-normalized dye density coefficients C/M/Y',
 'print_spectral':'paper spectral scan relative CIE XYZ under D50, normalization Y=1 for illuminant',
 'print_glare':'paper spectral scan relative CIE XYZ under D50',
 'print_linear':'linear sRGB D65 after CAT02 XYZ conversion and default CAM16-UCS gamut mapping',
 'print_scan_finish':'linear sRGB D65 after scanner filters; can be negative',
 'print_output':'encoded sRGB D65 after near-identity matrix and OETF; before output clamp/quantization',
 'rgba16':'clamped quantized encoded sRGB D65 uint16; opaque alpha'}


def dump(path,value):
 with Path(path).open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False)


def blob_arrays(file):
 data=Path(file).read_bytes();magic,version,count,reserved=struct.unpack_from('<4sIII',data)
 need(magic==b'SPKR' and version==1,'Blob version')
 result={}
 for i in range(count):
  row=struct.unpack_from('<64sII4IQQ',data,16+104*i);name=row[0].split(b'\0')[0].decode();dtype=['<f8','<f4','<f2'][row[1]];dims=row[3:7][:row[2]];offset,size=row[7:9]
  need(offset+size<=len(data),'Blob bounds');result[name]=np.frombuffer(data,dtype=dtype,count=int(np.prod(dims)),offset=offset).reshape(dims).astype(np.float64)
 return result


def reflect_index(x,length):
 folded=np.where(x<0,-(x+1),x);j=folded%(2*length);return np.where(j>=length,2*length-1-j,j)


def fir(image,sigma=.7,truncate=3.,fused=False):
 radius=int(sigma*truncate+.5);x=np.arange(-radius,radius+1,dtype=np.float64);w=np.exp(-.5*(x/sigma)**2);w=(w/w.sum()).astype(np.float32)
 cur=image.astype(np.float32)
 for axis in (0,1):
  total=np.zeros_like(cur);positions=np.arange(cur.shape[axis])
  for k,weight in zip(range(-radius,radius+1),w):
   sample=np.take(cur,reflect_index(positions+k,cur.shape[axis]),axis=axis)
   total=(total.astype(np.float64)+sample.astype(np.float64)*float(weight)).astype(np.float32) if fused else total+sample*weight
  cur=total
 return cur,w


def metrics(a,b):
 delta=np.abs(a.astype(float)-b.astype(float));return {'changedComponents':int(np.count_nonzero(a!=b)),'maxAbs':float(delta.max(initial=0)),'meanAbs':float(delta.mean()),'signMismatch':int(np.count_nonzero((a<0)!=(b<0))),'zeroMismatch':int(np.count_nonzero((a==0)!=(b==0)))}


def stats(image):
 flat=image.reshape(-1,3);_,counts=np.unique(flat,axis=0,return_counts=True);result={'pixels':len(flat),'uniqueTriples':len(counts),'largestRepeatedTripleCount':int(counts.max()),'channels':[]}
 for c in range(3):
  x=flat[:,c];u,n=np.unique(x,return_counts=True);result['channels'].append({'min':float(x.min()),'max':float(x.max()),'quantiles':np.quantile(x,[0,.05,.5,.95,1]).tolist(),'stdInOwnDomain':float(x.std()),'uniqueValues':len(u),'mostFrequentValue':float(u[np.argmax(n)]),'mostFrequentCount':int(n.max()),'zero':int(np.count_nonzero(x==0)),'negative':int(np.count_nonzero(x<0)),'equalHorizontalEdges':int(np.count_nonzero(image[:,1:,c]==image[:,:-1,c])),'equalVerticalEdges':int(np.count_nonzero(image[1:,:,c]==image[:-1,:,c])),'meanAbsHorizontalDelta':float(np.abs(np.diff(image[:,:,c].astype(float),axis=1)).mean()),'meanAbsVerticalDelta':float(np.abs(np.diff(image[:,:,c].astype(float),axis=0)).mean())})
 return result


def load_stages(root,finite,photo_id):
 case=next(c for c in finite['cases'] if c['id']==photo_id);index=next(i for i,c in enumerate(finite['cases']) if c['id']==photo_id);arrays={}
 for k,rec in enumerate(case['stages']):
  p=root/'round11/natural-stage-finite-v1'/stage_filename(index,k,photo_id,rec);need(sha(p)==rec['sha256'],'Stage hash drift '+str(p));arrays[rec['stage']]=np.fromfile(p,dtype='<f4').reshape(rec['shape'])
 return arrays


def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--local-output',type=Path,required=True);p.add_argument('--summary',type=Path,required=True);a=p.parse_args();root=a.root;out=a.local_output;need(out.is_dir(),'Explicit existing empty local root required');need(not a.summary.exists() and not (out/'nex5r-zero-channels.csv').exists(),'New analysis outputs required')
 evidence=REPO/'docs/research/color-engine-20261029/home-round11';finite=read(evidence/'natural-stage-finite.json');manifest=read(root/'round11/natural-inputs-v1/manifest.json');resources=read(REPO/'tools/cross-device-diagnostic/expected-resources.json');resource_checks=[]
 for name,expected in resources.items():
  actual=sha(root/'resources-patch'/name);need(actual==expected,'Resource drift '+name);resource_checks.append({'path':name,'sha256':actual})
 blob=blob_arrays(root/'resources-patch/spektrafilm_constants.bin');dump(out/'blob-keys.json',list(blob));matrix=(blob['colour/cs/sRGB/matrix_XYZ_to_RGB'].reshape(3,3)@blob['colour/cs/sRGB/matrix_RGB_to_XYZ'].reshape(3,3)).astype(np.float32)
 report={'version':'Round12ExistingDataCauseProbeV1','renderCalls':0,'gpuDispatchCalls':0,'rawDecodeCalls':0,'full20MPCalls':0,'domains':DOMAINS,'resourceChecks':resource_checks,'base':'603e8a49c7e35546053ad51339eaed2ad7989793','outputMatrix':matrix.tolist(),'analysisLimits':'Analytic CPU arithmetic can differ in float rounding from GPU. Not independent accuracy reference or replacement renderer.'}
 # A: preserve the established clean-footprint selection.
 s=load_stages(root,finite,'sonynex5r');h,w,_=s['print_output'].shape;inp=np.load(root/'round11/natural-inputs-v1/sonynex5r-A-proxy.npy');prior_path=root/'round11/natural-coverage-run1/sonynex5r/proxy-prior-endpoint-support.npy';prior=np.load(prior_path);rgba=np.fromfile(root/'round11/natural-coverage-run1/sonynex5r/whole.rgba16',dtype='<u2').reshape(h,w,4);q=rgba[:,:,:3];clean=(inp>0).all(2)&(inp<1).all(2)&~prior;selected=(q==0).any(2)&clean;need(int(selected.sum())==11562,'Original selected pixel count changed')
 linear,finish,encoded=[s[k] for k in ['print_linear','print_scan_finish','print_output']];quant=np.floor(np.clip(encoded,0,1)*np.float32(65535)+np.float32(.5)).astype(np.uint16)
 classification={}
 for scope,mask in [('all',np.ones((h,w),bool)),('cleanSelected11562',selected)]:
  z=(q==0)&mask[:,:,None];classification[scope]={'pixelsWithAnyZero':int(z.any(2).sum()),'zeroComponents':int(z.sum()),'negativeClampedComponents':int((z&(encoded<0)).sum()),'exactFloatZeroComponents':int((z&(encoded==0)).sum()),'positiveRoundedToZeroComponents':int((z&(encoded>0)).sum()),'byChannel':{'negative':(z&(encoded<0)).sum((0,1)).tolist(),'exactZero':(z&(encoded==0)).sum((0,1)).tolist(),'positiveSmall':(z&(encoded>0)).sum((0,1)).tolist()},'allThreeZeroPixels':int(((q==0).all(2)&mask).sum())}
 blurred,weights=fir(linear);pred=(np.float32(1.7)*linear+np.float32(-.7)*blurred);bf,_=fir(linear,fused=True);predf=(np.float32(1.7).astype(float)*linear.astype(float)+np.float32(-.7).astype(float)*bf.astype(float)).astype(np.float32)
 mlinear=(finish@matrix.T).astype(np.float32);epred=np.empty_like(mlinear);low=mlinear<=np.float32(.0031308);epred[low]=np.float32(12.92)*mlinear[low];epred[~low]=np.float32(1.055)*np.power(mlinear[~low],np.float32(1/2.4))-np.float32(.055)
 coords=np.argwhere((q==0)&selected[:,:,None]);headers=['x','y','channel','print_linear','blur_analytic','print_scan_finish','linear_after_output_matrix_analytic','print_output','clamped_float','quantized_rgba16','cause','neighborhood_min','neighborhood_max']
 with (out/'nex5r-zero-channels.csv').open('x',newline='') as f:
  writer=csv.writer(f);writer.writerow(headers)
  for y,x,c in coords:
   ys=reflect_index(np.arange(y-2,y+3),h);xs=reflect_index(np.arange(x-2,x+3),w);patch=linear[np.ix_(ys,xs,[c])];v=encoded[y,x,c];writer.writerow([int(x),int(y),int(c),float(linear[y,x,c]),float(blurred[y,x,c]),float(finish[y,x,c]),float(mlinear[y,x,c]),float(v),float(np.clip(v,0,1)),int(q[y,x,c]),'negative_clamp' if v<0 else 'exact_zero' if v==0 else 'positive_quantization',float(patch.min()),float(patch.max())])
 np.save(out/'nex5r-selected-zero-pixels.npy',selected)
 bbox=[270,400,300,250];x,y,bw,bh=bbox;localmask=selected[y:y+bh,x:x+bw];rgb8=np.asarray(Image.open(root/'round11/natural-coverage-run1/sonynex5r/after.png').convert('RGB'));overlay=rgb8.copy();overlay[selected]=[255,0,255];Image.fromarray(overlay).save(out/'nex5r-zero-map.png');Image.fromarray(rgb8[y:y+bh,x:x+bw]).save(out/'nex5r-fixed-flower.png');Image.fromarray(overlay[y:y+bh,x:x+bw]).save(out/'nex5r-fixed-flower-overlay.png')
 signflip=np.argwhere((finish<0)!=(encoded<0));dump(out/'nex5r-sign-flip-coordinates.json',[{'xyc':[int(x),int(y),int(c)],'finishRGB':finish[y,x].tolist(),'matrixRGBAnalytic':mlinear[y,x].tolist(),'outputRGB':encoded[y,x].tolist()} for y,x,c in signflip])
 report['A']={'selectionCount':int(selected.sum()),'supportMaskSha256':sha(prior_path),'classification':classification,'stageNegativeCounts':{k:int((s[k]<0).sum()) for k in ['print_linear','print_scan_finish','print_output']},'quantizationAgainstSaved':metrics(quant,q),'scannerBlurSigma':0,'unsharp':{'sigma':.7,'amount':.7,'truncate':3,'radius':2,'weights':weights.tolist(),'boundary':'reflect including repeated edge pixel; vertical then horizontal','cpuSeparateAgainstSaved':metrics(pred,finish),'cpuFusedAgainstSaved':metrics(predf,finish),'negativeNeighborhoodInequalityAgrees':int(np.count_nonzero((pred<0)==(finish<0))),'components':int(finish.size),'boundaryNegativeComponents':int(((finish<0)&(((np.arange(h)[:,None]<2)|(np.arange(h)[:,None]>=h-2)|(np.arange(w)[None,:]<2)|(np.arange(w)[None,:]>=w-2))[:,:,None])).sum())},'outputMatrixOetfProbe':metrics(epred,encoded),'signChangedComponents':len(signflip),'fixedFlowerRoi':{'xywh':bbox,'selectedZeroPixels':int(localmask.sum()),'allThreeZeroPixels':int((q[y:y+bh,x:x+bw]==0).all(2).sum()),'fraction':float(localmask.mean())},'localCoordinateTable':{'file':'nex5r-zero-channels.csv','rows':len(coords),'sha256':sha(out/'nex5r-zero-channels.csv')}}
 # B: same prescribed ROI, no cross-domain amplitude ratios.
 s=load_stages(root,finite,'canonr6');inp=np.load(root/'round11/natural-inputs-v1/canonr6-A-proxy.npy');x,y,bw,bh=0,500,300,250;sl=np.s_[y:y+bh,x:x+bw];stages={'prepared':inp,**s};rstats={k:{'domain':DOMAINS[k],**stats(v[sl])} for k,v in stages.items()}
 exposure=s['film_blurs'];density=s['film_log_and_curves'];profile=read(root/'resources-patch/profiles/kodak_portra_400.json')['data'];le=np.array(profile['log_exposure'],float);curves=np.array(profile['density_curves'],float);mins=np.nanmin(curves,axis=0);normalized=curves-mins;log=np.log10(np.maximum(exposure,0).astype(float)+1e-10);predcurve=np.stack([np.interp(log[:,:,c],le,normalized[:,c]) for c in range(3)],2).astype(np.float32)
 z=density==0;floor=log<=le[0];mask=np.zeros(exposure.shape[:2],bool);mask[sl]=True;collapse=[]
 for axis in (0,1):
  er=np.diff(exposure[sl],axis=axis);dr=np.diff(density[sl],axis=axis);collapsed=(er!=0)&(dr==0);collapse.append({'axis':'vertical' if axis==0 else 'horizontal','inputDistinctComponents':int((er!=0).sum()),'collapsedComponents':int(collapsed.sum()),'collapsedToZeroComponents':int((collapsed&(np.take(density[sl],range(density[sl].shape[axis]-1),axis=axis)==0)).sum())})
 selected_density=(density==0).all(2)&mask;plateau=[]
 for name in ['prepared','film_blurs','film_log_and_curves','print_spectral','print_linear','print_scan_finish','print_output']:
  vals=stages[name][selected_density];u,c=np.unique(vals,axis=0,return_counts=True);plateau.append({'stage':name,'domain':DOMAINS[name],'points':len(vals),'uniqueTriples':len(u),'largestGroup':int(c.max()),'min':vals.min(0).tolist(),'max':vals.max(0).tolist(),'mostFrequentTriple':u[np.argmax(c)].tolist()})
 np.save(out/'r6-zero-density-roi-mask.npy',selected_density)
 with (out/'r6-roi-channel-lineage.csv').open('x',newline='') as f:
  writer=csv.writer(f);writer.writerow(['x','y','channel','film_linear_exposure','log10_exposure_analytic','density_observed','curve_prediction_analytic','at_or_below_profile_low','scan_XYZ_corresponding_index','linear_sRGB_corresponding_index','output_sRGB_corresponding_index'])
  for yy in range(y,y+bh):
   for xx in range(x,x+bw):
    for c in range(3):writer.writerow([xx,yy,c,float(exposure[yy,xx,c]),float(log[yy,xx,c]),float(density[yy,xx,c]),float(predcurve[yy,xx,c]),bool(floor[yy,xx,c]),float(s['print_spectral'][yy,xx,c]),float(s['print_linear'][yy,xx,c]),float(s['print_output'][yy,xx,c])])
 report['B']={'roiXYWH':[x,y,bw,bh],'stageDistributions':rstats,'profile':{'logExposureRange':[float(le[0]),float(le[-1])],'normalizedFirst':normalized[0].tolist(),'rawMin':mins.tolist(),'normalizedFirstPositiveIndex':[int(np.flatnonzero(normalized[:,c]>0)[0]) for c in range(3)]},'filmCurveProbe':metrics(predcurve[sl],density[sl]),'densityZeroAttribution':{'roiZeroComponents':int(z[sl].sum()),'roiBelowOrEqualLowerBoundComponents':int(floor[sl].sum()),'roiZeroNotAtLowerBound':int((z&~floor)[sl].sum()),'roiLowerBoundButNonzero':int((~z&floor)[sl].sum()),'roiFilmExposureZero':int((exposure[sl]==0).sum()),'roiFilmExposureNegative':int((exposure[sl]<0).sum()),'roiAllChannelsZeroDensityPixels':int(selected_density.sum()),'byChannelZero':z[sl].sum((0,1)).tolist()},'collapsedNeighborPairs':collapse,'allZeroDensityTrajectory':plateau,'lineageCsvSha256':sha(out/'r6-roi-channel-lineage.csv')}
 dump(a.summary,report);print(json.dumps({'A':report['A'],'B_attribution':report['B']['densityZeroAttribution'],'B_curveProbe':report['B']['filmCurveProbe'],'B_plateau':plateau},indent=2))

if __name__=='__main__':main()
