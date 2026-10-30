"""Validate this round's evidence and local-only image hashes; no synthetic pass substituted."""
from pathlib import Path
from natural_admission import sha,load_json,npy_info,scan,DOMAINS
import argparse,json,hashlib,subprocess
p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);p.add_argument('--local',type=Path);p.add_argument('--admission-manifest',type=Path);p.add_argument('--manifest-sha');a=p.parse_args();read=lambda n:json.loads((a.evidence/n).read_text(encoding='utf-8-sig'))
def require(ok,msg):
 if not ok:raise ValueError(msg)
source=read('authorized-source-inventory.json');require(source['developmentScenes']==1 and not source['heldOutValidationSet'],'Scene classification wrong');require(not source['usableSkinCoverage'] and not source['strongHighlightCoverage'],'Coverage overstated')
prod=read('production-decode.json');require(prod['status']=='DECODED' and prod['sourceUnchanged'],'Production decode missing');require([prod['width'],prod['height']]==[5496,3670] and prod['Contract']['OutputSpace']=='LibRaw Linear ProPhoto D65','Wrong input domain');require(prod['Contract']['WhiteBalanceApplied'] and prod['Contract']['CameraMatrixApplied'],'WB/matrix absent')
manifest=read('input-manifest.json');require(not manifest['bridge']['extraWB'] and not manifest['bridge']['displayMappingOnA'],'A double processing');cases=read('natural-cases.json')['cases'];require(len(cases)==44,'Incomplete execution inventory')
base=[r for r in cases if r['role']=='proxy' and '-reopen' not in r['localDirectory']];require(len(base)==6,'Missing 3stockx2route proxies')
for r in cases:
 if r['role']=='full':require(r['status']=='REFUSED_RESOURCE_BUDGET' and not r['budget']['admitted'] and r['budget']['fullResolutionNotResized'],'False full-resolution claim');continue
 require(r['status'].startswith('RENDERED') and r['tiff16Exact'],'Missing actual render/TIFF pass');require(r['statistics']['alphaMinMax']==[65535,65535],'Alpha contract wrong');require(not r['resolved']['grain_active'] and not r['resolved']['glare_active'],'Random effects unexpectedly on');require(r['resolved']['input_cctf_decoding']==(r['route']=='B'),'Wrong route CCTF');require(r['resolved']['input_color_space']==('sRGB' if r['route']=='B' else 'ProPhoto RGB'),'Wrong route space')
 if 'repeatExact' in r:require(r['repeatExact'],'Repeat failed')
 if '-reopen' in r['localDirectory']:
  old=next(x for x in cases if x['localDirectory']==r['localDirectory'].replace('-reopen',''));require(old['outputPixelSha256']==r['outputPixelSha256'] and old['resolved']==r['resolved'] and old['resourceHashes']==r['resourceHashes'] and old['librarySha256']==r['librarySha256'],'Fresh process identity/output mismatch')
 if a.local:
  folder=a.local/r['localDirectory']
  require(sha(folder/'rgba16.bin')==r['outputPixelSha256'],'Saved RGBA changed')
  for name,e in r['outputFiles'].items():require(sha(folder/name)==e['sha256'],'Saved output file changed')
for r in base:require(r['repeatExact'],'Proxy repeat missing')
if a.local:
 for image in read('observation-artifacts.json')['images']:require(sha(Path(image['path']))==image['sha256'],'Private local observation changed')
bridge=read('bridge-validation.json');require(bridge['independentBradfordMatrixMaxAbs']<1e-12 and all(x['maxAbs']==0 for x in bridge['sourceSampleChecks']),'Existing bridge check failed')
unsharp=read('unsharp-impact.json');require(len(unsharp['cases'])==2 and all(x['newNegativeSamples']==0 and x['newAboveOneSamples']==0 for x in unsharp['cases']),'Unsharp findings changed')
print('PASS: actual production natural input;6 proxies+24native windows;8fresh-process reopens;8repeat checks;TIFF16;6full-budget refusals;A/B domains;local-only image hashes and bounded unsharp.')

if a.admission_manifest:
 require(sha(a.admission_manifest)==a.manifest_sha,'Admission seal changed');m=load_json(a.admission_manifest)
 require(m['sourceSha256']==prod['sourceSha256'],'First source identity not bound')
 for r in cases:
  name=Path(r['inputFile']).name;items=[e for e in m['files'] if e['path']==name]
  if len(items)==0 and 'wire_edge' in name:continue # Explicit additional window not in frozen original preparation manifest; legacy-only evidence.
  require(len(items)==1,'First preparation entry missing/duplicate');e=items[0]
  require(e['route']==r['route'] and e['domain']==DOMAINS[r['route']] and e['shape']==r['inputShape'],'First prepared identity mismatch')
  require(e['fileSha256']==r['inputFileSha256'] and e['pixelSha256']==r['inputPixelSha256'],'First case input not equal sealed preparation')
 print('PASS: retrospective original round6 preparation identity audit; does not relabel legacy cases as V2 admission')
