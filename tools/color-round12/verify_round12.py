"""Read-only sealed round12 report cross-checks; never rerenders."""
import ast,json,sys
from pathlib import Path
repo=Path(__file__).resolve().parents[2];e=repo/'docs/research/color-engine-20261029/home-round12'
def r(n):return json.loads((e/n).read_text(encoding='utf-8-sig'))
def need(v,m):
 if not v:raise ValueError(m)
a=r('cause-analysis-v1.json');b=r('paper-cause-v1.json');v=r('visual-correspondence.json');scope=r('execution-scope.json');src=r('source-identity.json');prior=r('prior-intermediate-audit.json')
need(a['A']['selectionCount']==11562,'A selection');c=a['A']['classification']['cleanSelected11562'];need(c['zeroComponents']==22326 and [c['negativeClampedComponents'],c['exactFloatZeroComponents'],c['positiveRoundedToZeroComponents']]==[22322,0,4],'A partition');need(a['A']['quantizationAgainstSaved']['changedComponents']==0,'A quantization');need(a['A']['unsharp']['cpuSeparateAgainstSaved']['signMismatch']==0,'A spatial signs');need(a['A']['outputMatrixOetfProbe']['signMismatch']==0,'Output signs');need(b['roiXYWH']==[0,500,300,250],'Fixed B ROI');need(a['B']['densityZeroAttribution']['roiZeroComponents']==19567 and a['B']['densityZeroAttribution']['roiZeroNotAtLowerBound']==0,'B floor');need(b['paperProfileDomain']['outOfLowComponents']==b['paperProfileDomain']['outOfHighComponents']==0,'Paper table domain');need(b['paperSetupMatchesFrozenConfig'],'Paper resolved');need(v['r6PriorLossOverlap']['zeroDensityComponentsWithoutPriorFootprint']==13,'Prior loss boundary');need(src['allHistoricalBindingsMatch'] and r('round10-preservation.json')['allPassed'],'Frozen evidence');need(all(x==0 for k,x in scope['counts'].items() if k!='cpuSetupOnlyHelperRuns'),'No GPU/RAW');need(scope['counts']['cpuSetupOnlyHelperRuns']==1 and scope['companyDiagnostic']=='NOT_RUN','Scope');need(all(x['supportRecomputedExact'] for x in prior['records']),'Support identity');need(r('failures-and-candidate-gate.json')['pixelCandidate']=='NOT_CREATED_NOT_ADMITTED','No pixel candidate');need(scope['timezone']=='Europe/Helsinki','Client timezone')
files=list((repo/'tools/color-round12').glob('*.py'))
for p in files:ast.parse(p.read_text(encoding='utf-8-sig'),filename=str(p))
print(json.dumps({'version':'Round12SealedCheckV1','passed':True,'syntaxFiles':len(files),'gpuCalls':0,'rawDecodeCalls':0,'candidate':'NOT_CREATED','company':'NOT_RUN'}))
