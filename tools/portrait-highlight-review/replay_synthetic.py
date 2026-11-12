from pathlib import Path
import argparse,csv,json,hashlib,numpy as np
from rgb16_io import decode,encode,y
from probe_runner import probe,execute
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('dotnet');p.add_argument('probe_dll',type=Path);p.add_argument('frozen_curves',type=Path);a=p.parse_args();out=a.root/'synthetic-review';out.mkdir(exist_ok=False)
shapes=np.array([[1,1,1],[.76,.42,.26],[1,.3,.045]]);base=shapes*(.18/y(shapes))[:,None];ev=np.linspace(-6,6,193);scene=base[:,None,:]*2**ev[None,:,None]
source=encode(scene/32).astype('<f4');source.tofile(out/'historical-579.f32');actual,call=probe(a.dotnet,a.probe_dll,out/'historical-579.f32',out/'historical-traced',193,3,5,True)
quiet,quietcall=probe(a.dotnet,a.probe_dll,out/'historical-579.f32',out/'historical-quiet',193,3,5,False)
assert all(np.array_equal(actual[k],quiet[k]) for k in actual),'Observer changed actual materialized output'
with a.frozen_curves.open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
assert len(rows)==579
historic={}
for name,key in [('output','output'),('candidate','candidate'),('basic-tone-output','basic-tone-output')]:
 expected=np.array([[float(r[key+'_'+c]) for c in 'RGB'] for r in rows],dtype=np.float32).reshape(3,193,3);value=actual[name]
 historic[name]=dict(bitExact=bool(np.array_equal(expected,value)),maxAbs=float(np.abs(expected-value).max()))
 assert np.array_equal(expected,value),name
linear=np.fromfile(out/'historical-traced/decoded-display-linear.f32',dtype='<f4');linear.tofile(out/'historical-observed-linear.f32')
refs={}
for name,args in [('dt',[]),('rt',[0]),('rt-highlight60',[60])]:
 target=out/(name+'.f32');exe=a.root/'reference-build'/('dt.exe' if name=='dt' else 'rt.exe');log=out/(name+'-console.txt');record=execute([exe,out/'historical-observed-linear.f32',target,5,579,*args],log)
 values=np.fromfile(target,dtype='<f4').reshape(3,193,3);exposed=np.fromfile(out/'historical-traced/exposed-linear.f32',dtype='<f4').reshape(3,193,3)
 refs[name]=dict(maxAbsVsProductExposure=float(abs(values-exposed).max()),aboveOneChannels=int((values>1).sum()),call=record)
 if name in ('dt','rt'):assert np.array_equal(values,exposed)
 if name=='rt-highlight60':
  frozen=np.array([[float(r['rt-highlight60_'+c]) for c in 'RGB'] for r in rows],dtype=np.float32).reshape(3,193,3);refs[name]['maxAbsVsFrozenCompiler']=float(abs(values-frozen).max());refs[name]['sameFrozenCompilerPixels']=bool(np.array_equal(values,frozen))
# A dense EV0 ladder includes samples at and above the fixed linear max-channel knee.
levels=np.linspace(0,1,1025);source0=encode(shapes[:,None,:]/np.max(shapes,axis=1)[:,None,None]*levels[None,:,None]).astype('<f4')
# CLI hard bound is 512x512, so use a 205x15 raster to preserve all 3075 samples.
source0.reshape(15,205,3).tofile(out/'ev0-dense.f32');ev0,ev0call=probe(a.dotnet,a.probe_dll,out/'ev0-dense.f32',out/'ev0-dense',205,15,0,True)
records=[]
for row,name in enumerate(['neutral','skin-shape','warm-shape']):
 original=decode(source0[row].astype(np.float64));old=decode(ev0['output'].reshape(3,1025,3)[row].astype(np.float64));candidate=decode(ev0['candidate'].reshape(3,1025,3)[row].astype(np.float64))
 old_y=y(old);candidate_y=y(candidate);max0=original.max(axis=1)
 for i in range(1025):records.append(dict(color=name,maxInputLinear=float(max0[i]),inputY=float(y(original[i])),originalY=float(old_y[i]),candidateY=float(candidate_y[i]),oldLinearSaturation=float(np.ptp(old[i])/max(old[i].max(),1e-12)),candidateLinearSaturation=float(np.ptp(candidate[i])/max(candidate[i].max(),1e-12)),originalLocalSlope=float(np.gradient(old_y,max0)[i]),candidateLocalSlope=float(np.gradient(candidate_y,max0)[i]),candidateR=float(candidate[i,0]),candidateG=float(candidate[i,1]),candidateB=float(candidate[i,2])))
with (out/'EV0_CURVES.csv').open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,records[0]);w.writeheader();w.writerows(records)
summary=dict(status='PASS_FROZEN_HISTORICAL_AND_EV0_REPLAY',historicalRows=579,observerMaterializedOutputExact=True,historicalMaterialized=historic,references=refs,referenceCompiler='MSVC alternative; frozen source/function hashes identical',calls=[call,quietcall,ev0call],ev0=dict(kneeLinearMax=.75,kneeEncoded=float(encode(np.array(.75))),whiteInputY=1,whiteOutputY=float(records[1024]['candidateY']),whiteLuminanceRatio=float(records[1024]['candidateY']),whiteDeltaStops=float(np.log2(records[1024]['candidateY'])),whiteEncodedOutput=float(ev0['candidate'].reshape(3,1025,3)[0,-1,0]),whiteLocalSlope=float(records[1024]['candidateLocalSlope']),contract='EV0 is not identity above knee; mid-grey remains unchanged'),newRawDecodes=0)
(out/'SYNTHETIC_RESULTS.json').write_bytes((json.dumps(summary,indent=2)+'\n').encode());print(json.dumps(summary,indent=2))
