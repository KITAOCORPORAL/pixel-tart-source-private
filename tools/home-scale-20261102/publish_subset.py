"""Publish exactly the same permitted small gray subset as company. CPU only."""
from pathlib import Path
import sys,json,hashlib,shutil
repo,home=map(Path,sys.argv[1:3]);base=repo/'docs/research/color-engine-20261029';e=base/'home-scale-localization-20261102';out=e/'public-gray-taps'
if out.exists():raise ValueError('New public subset required')
idx=json.loads((base/'company-scale-localization-20261102/public-subset-index.json').read_text());run=json.loads((home/'run.json').read_text());allowed=[x['path'].removeprefix('public-gray-taps/') for x in idx['publicFiles']];forbidden=idx['notPublishedLocalSetupArrays'];out.mkdir();records=[]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
for name in allowed:
 if name in forbidden:raise ValueError('Restricted setup excluded')
 p=out/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(home/name,p);records.append({'path':'public-gray-taps/'+name,'bytes':p.stat().st_size,'sha256':sha(p)})
localonly=[]
for name in forbidden:
 p=home/name;meta=next(x for x in run['observations'] if x['file']==name);localonly.append({'file':name,'bytes':p.stat().st_size,'sha256':sha(p),'dtype':meta['dtype'],'values':meta['values'],'min':meta['min'],'max':meta['max']})
manifest={'version':'PublicGraySubstageSubsetV1','localFullCapture':str(home),'subsetPath':out.relative_to(repo).as_posix(),'isCompleteFullCapture':False,'publicFiles':records,'notPublishedLocalSetupArrays':forbidden,'pairedComparatorRequiresBothCompleteLocalCaptures':True,'scope':'Same company-approved public gray subset; complete local setup/runtimearrays not redistributed'}
with (e/'public-subset-index.json').open('x',encoding='utf-8') as f:json.dump(manifest,f,indent=2)
with (e/'local-only-setup-observations.json').open('x',encoding='utf-8') as f:json.dump({'version':'LocalSetupConstantObservationIndexV1','fullLutAndSensitivityPublished':False,'localRun':str(home),'notIncludedInGit':localonly,'reason':'Restricted engine/profile derived complete arrays only in authorized external home capture; Git records hashes/counts only'},f,indent=2)
print(json.dumps({'publicFiles':len(records),'publicBytes':sum(x['bytes'] for x in records),'restrictedFiles':len(localonly)}))
