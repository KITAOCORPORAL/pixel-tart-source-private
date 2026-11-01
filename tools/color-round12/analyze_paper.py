"""Offline fixed-ROI density -> paper -> scan attribution. No GPU/rendering."""
import sys,json,csv
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from analyze_existing import read,need,sha,dump,load_stages,metrics,stats,DOMAINS,fir

def constants(root):
 raw=np.fromfile(root/'cpu-constants.dat',dtype='<f8');out={}
 for line in (root/'cpu-constants.idx').read_text().splitlines():
  name,offset,count=line.split();out[name]=raw[int(offset):int(offset)+int(count)]
 return out

def spectral(cmy,chd,base,ixs,gain,offset,log_output):
 chd=chd.reshape(81,3).astype(np.float32);base=base.astype(np.float32);ixs=ixs.reshape(81,3).astype(np.float32);v=cmy.reshape(-1,3).astype(np.float32);acc=np.zeros_like(v)
 for wavelength in range(81):
  density=((v[:,0]*chd[wavelength,0]+v[:,1]*chd[wavelength,1])+v[:,2]*chd[wavelength,2])+base[wavelength]
  transmission=np.exp2(-density*np.float32(3.321928094887362));acc+=transmission[:,None]*ixs[wavelength]
 value=np.maximum(acc*np.asarray(gain,np.float32)+np.asarray(offset,np.float32),0)+np.float32(1e-10)
 return (np.log(value)*np.float32(.4342944819032518) if log_output else value).reshape(cmy.shape)

def curve(v,k):
 x=k['print/curve_x'].reshape(-1,3).astype(np.float32);y=k['print/curve_y'].reshape(-1,3).astype(np.float32);inv=k['print/curve_inv'].reshape(-1,3).astype(np.float32);out=np.empty_like(v)
 for c in range(3):
  idx=np.clip(np.searchsorted(x[:,c],v[...,c],side='right')-1,0,len(x)-2);frac=(v[...,c]-x[idx,c])*inv[idx,c];out[...,c]=np.where(v[...,c]<=x[0,c],y[0,c],np.where(v[...,c]>=x[-1,c],y[-1,c],y[idx,c]+frac*(y[idx+1,c]-y[idx,c])))
 return out

def paper_chain(d,k):
 log=spectral(d,k['print/chd'],k['print/base'],k['print/ixs'],k['print/gain'],k['print/offset'],True)
 log2=np.log(np.maximum(np.exp2(log*np.float32(3.321928094887362)),0)+np.float32(1e-10))*np.float32(.4342944819032518) # print exposure=1, black/white correction disabled
 paper=curve(log2,k);xyz=spectral(paper,k['scan/chd'],k['scan/base'],k['scan/ixs'],[1,1,1],[0,0,0],False)
 return log2,paper,xyz

def eotf(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)

def main():
 root,out,summary=map(Path,sys.argv[1:4]);repo=Path(__file__).resolve().parents[2];k=constants(out);identity=read(repo/'docs/research/color-engine-20261029/home-round12/cpu-setup-identity.json');need(sha(out/'cpu-constants.dat')==identity['dataSha256'] and sha(out/'cpu-constants.idx')==identity['indexSha256'],'CPU setup identity');need(identity['resolvedMatchesFrozen'],'Configuration mismatch');hidden_names=['scannerBlur','unsharpSigma','unsharpAmount','blackCorrection','whiteCorrection','previewMode','deactivateSpatial','lutMode','paperMorphActive','normalizePrintExposure','printExposureCompensation'];hidden=dict(zip(hidden_names,k['hidden'].tolist()));need(hidden['scannerBlur']==0 and hidden['unsharpSigma']==hidden['unsharpAmount']==.7 and hidden['blackCorrection']==hidden['whiteCorrection']==0,'Actual hidden configuration changed')
 finite=read(repo/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json');s=load_stages(root,finite,'canonr6');box=[0,500,300,250];sl=np.s_[500:750,0:300];d=s['film_log_and_curves'][sl];log,paper,xyz=paper_chain(d,k);np.savez(out/'r6-paper-chain-analytic.npz',logExposure=log,paperDensity=paper,xyz=xyz)
 axis=k['print/curve_x'].reshape(-1,3);py=k['print/curve_y'].reshape(-1,3)
 zero_log,zero_paper,zero_xyz=paper_chain(np.zeros((1,1,3),np.float32),k);xyz_saved=s['print_spectral'][sl];zero_selected=(d==0).all(2);before=np.load(root/'round11/natural-inputs-v1/canonr6-display-proxy.npy')[sl].astype(float);rgba=np.fromfile(root/'round11/natural-coverage-run1/canonr6/whole.rgba16',dtype='<u2').reshape(s['print_output'].shape[:2]+(4,));after=rgba[sl][:,:,:3]/65535;yb=eotf(before)@np.array([.2126,.7152,.0722]);ya=eotf(after)@np.array([.2126,.7152,.0722]);pred_rgb=s['print_spectral'][sl]@k['scan/xyz_to_rgb'].reshape(3,3).T;post=s['print_linear'][sl];spatial,_=fir(s['print_linear']);spatial=np.float32(1.7)*s['print_linear']-np.float32(.7)*spatial
 quant=rgba[sl][:,:,:3];quant_stats=stats(quant)
 report={'version':'Round12PaperCauseProbeV1','gpuCalls':0,'rawDecodeCalls':0,'roiXYWH':box,'hiddenParamsActualCpuSetup':hidden,'fixedPublicResolvedCount':len(read(out/'cpu-resolved.json')),'printGain':k['print/gain'].tolist(),'printOffset':k['print/offset'].tolist(),'paperSetupMatchesFrozenConfig':True,'paperChainCpuAgainstSavedXYZ':metrics(xyz,xyz_saved),'paperLogExposureRange':{'min':log.min((0,1)).tolist(),'max':log.max((0,1)).tolist()},'paperProfileDomain':{'low':axis[0].tolist(),'high':axis[-1].tolist(),'outOfLowComponents':int((log<axis[0]).sum()),'outOfHighComponents':int((log>axis[-1]).sum()),'densityMax':py.max(0).tolist()},'paperDensityStats':{'domain':'paper dye density coefficients (not RGB)',**stats(paper)},'zeroFilmDensityChain':{'filmDensity':[0,0,0],'paperLogExposureAnalytic':zero_log.reshape(3).tolist(),'paperDensityAnalytic':zero_paper.reshape(3).tolist(),'paperAsymptoticDensityMax':py.max(0).tolist(),'scanXYZAnalytic':zero_xyz.reshape(3).tolist(),'scanXYZObserved':xyz_saved[zero_selected][0].tolist(),'relativeY':float(zero_xyz[0,0,1]),'blackWhiteCorrectionActive':False,'important':'zero film density retains film base_density and paper dye/base spectral response; not optical zero or display RGB black'},'xyzToLinearRgbBeforeGamutAnalytic':{'domain':'CAT02-adapted linear sRGB before CAM16 mapping (CPU analytic)','zeroDensityRGB':pred_rgb[zero_selected][0].tolist(),'zeroDensityAfterGamutObserved':post[zero_selected][0].tolist(),'allRoiDifferenceAgainstAfterGamut':metrics(pred_rgb.astype(np.float32),post),'negativeInputComponents':int((pred_rgb<0).sum())},'scannerUnsharpAgainstSavedRoi':metrics(spatial[sl],s['print_scan_finish'][sl]),'displayYComparison':{'domain':'sRGB EOTF Y in both before/after; different display rendering, not physical accuracy','beforeStd':float(yb.std()),'afterStd':float(ya.std()),'ratio':float(ya.std()/yb.std()),'beforeMedian':float(np.median(yb)),'afterMedian':float(np.median(ya)),'notOptimizationTarget':True},'finalQuantizedRoiStats':quant_stats,'sameDensityPixelsPostSharpen':'different neighbor context can reintroduce differences; not recovered original lost signal'}
 dump(summary,report);print(json.dumps(report,indent=2))
if __name__=='__main__':main()
