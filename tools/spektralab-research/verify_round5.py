"""Evidence assertions for this bounded round, not a substitute for device execution."""
from pathlib import Path
import argparse,json,hashlib
p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);a=p.parse_args();read=lambda n:json.loads((a.evidence/n).read_text(encoding='utf-8-sig'))
def require(ok,message):
 if not ok:raise ValueError(message)
curves=read('curve-root-cause.json');lineage=read('profile-lineage.json');require(all(x['exactBytesMatch'] for x in lineage['profiles']),'Runtime/upstream resource identity failed')
for c in curves['cases']:
 require(all(n>0 for n in c['rawTable']['negativeAdjacentCountPerChannel']),'Raw profile decline not reproduced')
 require(c['rawTable']['xStrictlyIncreasing'],'Invalid exposure axis')
 require(c['referenceErrors']['observedGpuVsOriginalTraceMaxAbs']==0,'Existing trace mismatch')
 require(c['referenceErrors']['linearInterpolationOutsideBracketCount']==0,'Interpolation bracket violated')
 require(c['referenceErrors']['numpyVsScipyMaxAbs']<1e-12 and c['referenceErrors']['rawDoubleNumpyVsScipyMaxAbs']<1e-12,'Independent linear references disagree')
 require(c['modelDiagnosticOnly']['cdfNegativeStepsPerChannel']==[0,0,0] and not c['modelDiagnosticOnly']['runtimeFilmUsesModel'],'Model/data distinction lost')
 for edge in c['affectedOutputNeighbourhoods']:
  require(all(n['oldTraceExact'] for n in edge['channels']),'Measured channel trace mismatch')
  require(any(n['deltaRawDoubleTable']<0 for n in edge['channels']),'Downward output has no corresponding raw double curve decrease')
require(all(c['installedPythonMatchesPinned'] or c['newlineNormalisedSourceMatchesPinned'] for k,c in read('curve-reference-provenance.json').items() if k in ['numpy','scipy']),'Pinned references changed')
for name in ['bt-complete-path.json','bt-supplement.json']:
 j=read(name);require(len(j['cases'])==32,'Incomplete full-engine run set')
 for c in j['cases']:require(all(c['reopen'][key] for key in ['freshProcess','resolvedExact','inputIdentity','renderExact','savedAndReopenedTiff16Exact']),'Unverified saved render')
 for c in j['comparisons']:
  if (c['left'],c['right']) in [('original','patched'),('patched','patched-os')]:require(c['changedPixels']==0,'Unexpected regular-build difference')
for name in ['bt-actual-decode.json','bt-gradient-decode.json']:
 j=read(name);require(all(c['fullOutputUnaffectedByTap'] for c in j['cases']),'Passive tap changes output')
 for c in j['comparisons']:require(c['decodeChangedInputBits']==['0x3da66554'],'Unexpected decode influence range')
minimal=read('curve-minimal-reproduced.json');require(all(c['numpyDelta']<0 and c['scipyDelta']<0 for c in minimal['cases']),'Two-point minimum failed')
prop=read('bt-spatial-propagation.json')
for c in prop['cases']:
 for s in c['stages']:
  if s['stage']=='print_linear':require(s['changedXColumns']==[9],'Unexpected pre-spatial spread')
  if s['stage']=='print_scan_finish':require(s['changedXColumns']==[7,8,9,10,11],'Spatial spread not reproduced')
summary=read('bt-effect-summary.json');require(all(c['height7Vs1AllRowsExact'] for c in summary['verticalReplication']),'Unexplained vertical size difference')
print('PASS: pinned-resource decline, double-reference cause, old-trace exact, no overshoot, CDF distinction, 64 full-path saved/reopen cases, actual decode and spatial influence, tested height rule')
