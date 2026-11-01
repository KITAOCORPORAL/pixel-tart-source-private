"""Same-fixture native-L comparison; not a colour-pipeline equivalence claim."""
from pathlib import Path
import sys,json
import numpy as np
repo=Path(__file__).resolve().parents[2];root=Path(sys.argv[1]);ev=repo/'docs/research/color-engine-20261029/home-round13';refs=json.loads((ev/'reference-numerical.json').read_text());syn=json.loads((ev/'candidate-synthetic.json').read_text());h,w=refs['height'],refs['width'];inner=np.s_[2:-2,2:-2];rows=[]
def diff(a,b):
 d=np.abs(a.astype(float)-b.astype(float));return {'maxAbsLStarUnit':float(d.max()),'meanAbsLStarUnit':float(d.mean()),'changedValues':int(np.count_nonzero(a!=b))}
for name in ['flat','step','impulse','saturated_edge','negative_above_range','boundary','low_detail','detail','noise']:
 outputs={}
 for mode in ['dt-zero','dt-threshold','rt-open','rt-threshold','rt-halo']:
  record=next(r for r in refs['records'] if r['case']==name and r['mode']==mode);outputs[mode]=np.fromfile(record['command'][3],dtype='<f4').reshape(h,w,3)[:,:,0]
 source=np.fromfile(root/'reference-fixtures'/(name+'.f32'),dtype='<f4').reshape(h,w,3)[:,:,0]
 row={'case':name,'dtThresholdVsNoThreshold':diff(outputs['dt-threshold'],outputs['dt-zero']),'rtHaloVsSameThresholdUncontrolled':diff(outputs['rt-halo'],outputs['rt-threshold']),'dtVsRtOpen':diff(outputs['dt-zero'],outputs['rt-open']),'referenceInputLMinMax':[float(source.min()),float(source.max())]}
 # Illustrative native scalar scaling only; does not assert L*=100*linearRGB physically.
 linear=np.fromfile(root/'candidate-synthetic'/(name+'-input.f32'),dtype='<f4').reshape(h,w,3);legacy=np.fromfile(root/'candidate-synthetic'/(name+'-whole/legacy.f32'),dtype='<f4').reshape(h,w,3)
 if name in ['flat','step','negative_above_range','low_detail','detail','noise']:
  row['scaledScalarShapeProbe']={'note':'For diagnostics only: L*=100*x is a numeric fixture scaling, NOT a valid linearRGB-to-Lab colour conversion. Saturated/other nonidentical fixtures excluded.','inputMaxDelta':float(np.abs(source-linear[:,:,0]*100).max()),'legacyVsDtNoThresholdInterior':diff(legacy[:,:,0][inner]*100,outputs['dt-zero'][inner])}
 rows.append(row)
with (ev/'reference-cross-comparison.json').open('x') as f:json.dump({'version':'Round13ReferenceCrossComparisonV1','domain':'native Lab L* numeric fixtures, no colour accuracy claim','records':rows,'gpuCallsAdded':0},f,indent=2)
print(json.dumps(rows,indent=2))
