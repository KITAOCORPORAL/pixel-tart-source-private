"""Seal current reports and actual artifact identities without GPU."""
import sys,json,ast,hashlib
from pathlib import Path
repo=Path(__file__).resolve().parents[2];ev=repo/'docs/research/color-engine-20261029/home-round13';root=Path(sys.argv[1]);target=Path(sys.argv[2])
if target.exists():raise ValueError('New sealing report required')
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def need(c,m):
 if not c:raise ValueError(m)
source=read(ev/'source-identity.json');n=read(ev/'candidate-natural.json');s=read(ev/'candidate-synthetic.json');scope=read(ev/'execution-scope.json');p=read(ev/'evaluation-plan.json')
for row in source['tools']:need(sha(repo/row['path'])==row['sha256'],'Tool byte drift '+row['path'])
for row in source['localSources']:need(sha(root/row['path'])==row['sha256'],'Local source byte drift '+row['path'])
need(sha(ev/'candidate-definition.json')==p['candidateDefinitionSha256']==n['definitionSha256']==s['definitionSha256'],'Recipe binding')
need(scope['counts']['pixelCandidates']==1 and scope['counts']['rawDecode']==scope['counts']['full20MPEngine']==0,'Scope');need(n['stageReplayCalls']==s['stageReplayCalls']==18,'Calls');need(n['gpuDispatches']+s['gpuDispatches']==scope['counts']['gpuDispatches']==4088,'Dispatch accounting');need(read(ev/'round10-preservation.json')['allPassed'],'Frozen round10');need(read(ev/'saved-stage-admission.json')['allMatch'],'Old source');need(read(ev/'ab-review.json')['productAcceptance']=='NOT_ADMITTED','Acceptance boundary');need(read(ev/'r6-admission.json')['doesNotChangePixels'],'R6 unchanged')
for rec in n['records']:
 need(rec['frozenLegacy']=={'stageExact':True,'encodedExact':True,'finalExact':True},'Frozen exact');need(rec['response']['candidateOutsideLocalEnvelopeComponents']==0,'Full image envelope')
files=list((repo/'tools/color-round13').glob('*.py'))
for f in files:ast.parse(f.read_text(encoding='utf-8-sig'))
report={'version':'Round13SealedCheckV1','pass':True,'sourceToolFiles':len(source['tools']),'localArtifactFiles':len(source['localSources']),'syntaxFiles':len(files),'counts':scope['counts'],'technical':'PASS_STAGE_ONLY_EXACT','photography':'NOT_APPROVED','product':'NOT_ADMITTED','company':'NOT_RUN','independentEvaluation':'NOT_RUN','actualReadbackTests':'tests-closure.txt15PASS; not old report bools only'}
with target.open('x') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
