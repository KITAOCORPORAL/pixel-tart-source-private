"""Additional local-domain causal stats; no candidate tuning or rendering."""
from pathlib import Path
import sys,json,csv
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from analyze_existing import load_stages,read,dump,metrics,sha
from analyze_paper import constants,paper_chain
root,out,summary=map(Path,sys.argv[1:4]);repo=Path(__file__).resolve().parents[2];finite=read(repo/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json');s=load_stages(root,finite,'canonr6');sl=np.s_[500:750,:300];ex=s['film_blurs'][sl].astype(float);log=np.log10(np.maximum(ex,0)+1e-10);d=s['film_log_and_curves'][sl];profile=read(root/'resources-patch/profiles/kodak_portra_400.json')['data'];x=np.array(profile['log_exposure']);v=np.array(profile['density_curves']);v-=v.min(0);slopes=np.diff(v,axis=0)/np.diff(x)[:,None];idx=np.stack([np.clip(np.searchsorted(x,log[:,:,c],side='right')-1,0,len(x)-2) for c in range(3)],2);result=[]
for c in range(3):
 active=(log[:,:,c]>x[0])&(log[:,:,c]<x[-1]);slope=slopes[idx[:,:,c],c];result.append({'channel':c,'logExposureQuantiles':np.quantile(log[:,:,c],[0,.05,.5,.95,1]).tolist(),'curveSlopeUnit':'density coefficient per log10 relative film exposure','inRangeSlopeQuantiles':np.quantile(slope[active],[0,.05,.5,.95,1]).tolist(),'inRangeNegativeSlopeComponents':int((active&(slope<0)).sum()),'atOrBelowFirstKnot':int((log[:,:,c]<=x[0]).sum()),'atOrAboveLastKnot':int((log[:,:,c]>=x[-1]).sum())})
# Reproduce the actual float table shape; no new engine/GPU observations.
x32=x.astype(np.float32);v32=v.astype(np.float32);inv=(1/np.diff(x)).astype(np.float32);l32=(np.log(np.maximum(ex.astype(np.float32),0)+np.float32(1e-10))*np.float32(.4342944819032518)).astype(np.float32);pred=np.empty_like(d)
for c in range(3):
 i=np.clip(np.searchsorted(x32,l32[:,:,c],side='right')-1,0,len(x)-2);frac=(l32[:,:,c]-x32[i])*inv[i];pred[:,:,c]=np.where(l32[:,:,c]<=x32[0],v32[0,c],np.where(l32[:,:,c]>=x32[-1],v32[-1,c],v32[i,c]+frac*(v32[i+1,c]-v32[i,c])))
# Membership change in exact triplet equivalence classes. Quantization is distinct.
previous=s['film_blurs'][sl].reshape(-1,3);sequence=[]
for name in ['film_log_and_curves','film_couplers','film_grain','print_spectral','print_glare','print_linear','print_scan_finish','print_output']:
 current=s[name][sl].reshape(-1,3);before=len(np.unique(previous,axis=0));after=len(np.unique(current,axis=0));sequence.append({'stage':name,'uniqueInputTriples':before,'uniqueOutputTriples':after,'difference':after-before,'limit':'spatial stage can gain distinct values from neighbors; counts are finite-precision repeats, not photographic accuracy'});previous=current
report={'version':'Round12LocalDomainDetailV1','rawDecodeCalls':0,'gpuCalls':0,'fixedRoiXYWH':[0,500,300,250],'filmCurveQueryAndSlope':result,'floatTableAnalyticAgainstObserved':metrics(pred,d),'tripletCounts':sequence,'observedRoiTwoNonzeroNeighborsCollapse':read(repo/'docs/research/color-engine-20261029/home-round12/cause-analysis-v1.json')['B']['collapsedNeighborPairs'],'interpretation':'Lower-bound clamping explains exact zero density; in-range toe and profile nonmonotonicity remain separate. Does not attribute the full 0.132 display response ratio to the floor alone.'}
# Keep all per-pixel lineage inside local-only archive. Git receives counts/hashes only.
zero=np.load(out/'nex5r-selected-zero-pixels.npy');n=load_stages(root,finite,'sonynex5r');qq=np.fromfile(root/'round11/natural-coverage-run1/sonynex5r/whole.rgba16',dtype='<u2').reshape(n['print_output'].shape[:2]+(4,));ys,xs=np.nonzero(zero)
with (out/'nex5r-selected-pixels-all-channels.csv').open('x',newline='') as f:
 w=csv.writer(f);w.writerow(['x','y']+[f'{stage}_{c}' for stage in ['print_linear','print_scan_finish','print_output','clamped_float','rgba16'] for c in ['R','G','B']])
 for y,x in zip(ys,xs):w.writerow([int(x),int(y),*n['print_linear'][y,x].tolist(),*n['print_scan_finish'][y,x].tolist(),*n['print_output'][y,x].tolist(),*np.clip(n['print_output'][y,x],0,1).tolist(),*qq[y,x,:3].tolist()])
report['allSelectedPixelTable']={'rows':len(ys),'sha256':sha(out/'nex5r-selected-pixels-all-channels.csv'),'pathLocalOnly':'nex5r-selected-pixels-all-channels.csv'};dump(summary,report);print(json.dumps(report,indent=2))
