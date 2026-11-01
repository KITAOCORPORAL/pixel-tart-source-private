"""Metadata verification only; no old20MP run and no newRAW decode."""
from pathlib import Path
import argparse,json,hashlib,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args();repo=Path(__file__).resolve().parents[2];root=Path('N:/pixart/ColorResearchHome-20261030');old=repo/'docs/research/color-engine-20261029/home-round10';f=json.loads((old/'execution-freeze.json').read_text());records=[]
def sha(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as handle:
  for block in iter(lambda:handle.read(4194304),b''):h.update(block)
 return h.hexdigest()
for item in f['localSources']:records.append({'scope':'frozenCallerSource','path':item['path'],'expected':item['sha256'],'sha256':sha(item['path']),'match':sha(item['path'])==item['sha256']})
receipt=json.loads((root/'round10-full-final-caller-once/acceptance.json').read_text());report=json.loads((root/'round10-full-final-caller-once/report.json').read_text());matches={'reportShaMatchesReceipt':sha(root/'round10-full-final-caller-once/report.json')==receipt['reportSha256'],'receiptMatchesGitEvidence':sha(root/'round10-full-final-caller-once/acceptance.json')==sha(old/'full-acceptance.json'),'rawPixelHashMatchesReceipt':sha(root/'round10-full-final-caller-once/rgba16.bin')==receipt['outputPixelSha256'],'rawSizeCorrect':(root/'round10-full-final-caller-once/rgba16.bin').stat().st_size==5496*3670*8,'oldRound9AuditStillExists':(root/'full5496x3670-home-once/AUDITED_STATUS.json').is_file()}
changed=subprocess.check_output(['git','-C',str(repo),'diff','--name-only','07df3c56c551c38893abc1e4b7eaefb8e4a83be4','--','src','tools/spektralab-research','tools/cross-device-diagnostic','docs/research/color-engine-20261029/home-round10'],text=True).splitlines()
result={'version':'Round11FrozenStagePreservationV2','fullRenderCallsThisBatch':0,'oldFrozenPathsChangedInGit':changed,'records':records,'outputChecks':matches,'allPassed':not changed and all(x['match'] for x in records) and all(matches.values()),'scope':'read-only file hashes and Git diff; not old20MP rerender or new performance claim'}
if a.output.exists():raise ValueError('New preservation report required')
a.output.write_text(json.dumps(result,indent=2));print('FROZEN_STAGE_PRESERVED',result['allPassed']);sys.exit(0 if result['allPassed'] else 1)
