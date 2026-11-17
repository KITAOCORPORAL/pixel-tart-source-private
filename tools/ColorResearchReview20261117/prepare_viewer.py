"""Copy only pinned existing PNGs to a fresh local-only viewer; no pixel/decode/algorithm work."""
from pathlib import Path
import argparse,json,hashlib,shutil
p=argparse.ArgumentParser();p.add_argument('--evidence',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
evidence=a.evidence.resolve();target=a.output.resolve()
if target.exists():raise RuntimeError('Choose a fresh viewer root; existing evidence is never overwritten')
manifest=json.loads((evidence/'LOCAL_COMPARISON_INDEX.json').read_text('utf-8'));xt=json.loads((evidence/'LOCAL_XTRANS_VISUAL_INDEX.json').read_text('utf-8'));data=json.loads((evidence/'VIEWER_DATA.json').read_text('utf-8'))
rows=[]
for r in manifest['materials']:
 if r['role']=='existing-frozen-highlight-panel':rows.append((Path(r['originalFile']),r['localViewerFile'],r['sha256']))
 elif r['role']=='existing-numeric-figure-no-photographs':rows.append((Path(r['originalFile']),'media/ev0-curves.png',r['sha256']))
for panel in xt['panels']:
 for r in panel['individuals']:rows.append((Path(r['file']),r['localViewerFile'],r['sha256']))
assert len(rows)==34,'18 highlight+1 curve+15 XTrans components required'
verified=[]
for source,relative,expected in rows:
 dest=(target/relative).resolve()
 if target not in dest.parents:raise RuntimeError('Viewer output escapes selected root')
 if source.suffix.lower()!='.png' or not source.is_file():raise RuntimeError('Missing pinned PNG: '+str(source))
 digest=hashlib.sha256(source.read_bytes()).hexdigest()
 if digest.lower()!=expected.lower():raise RuntimeError('Frozen source changed: '+str(source))
 verified.append((source,dest,digest))
target.mkdir(parents=True,exist_ok=False)
for source,dest,digest in verified:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,dest)
shutil.copyfile(Path(__file__).with_name('viewer.html'),target/'index.html')
(target/'data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+chr(10),encoding='utf-8')
(target/'LOCAL_VIEWER_RECEIPT.json').write_text(json.dumps({'photographsLocalOnly':True,'filesVerifiedAndCopied':len(verified),'newCandidateRenders':0,'newRawDecodes':0,'target':str(target)},indent=2)+chr(10),encoding='utf-8')
print(json.dumps({'prepared':str(target),'copied':len(verified),'newRawDecodes':0}))
