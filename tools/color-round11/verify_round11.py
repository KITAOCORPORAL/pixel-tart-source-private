"""Read-only report and artifact checks. No RAW decode or GPU render."""
from pathlib import Path
import argparse,json,hashlib,sys
p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);a=p.parse_args();o=a.evidence;repo=Path(__file__).resolve().parents[2]
def read(n):return json.loads((o/n).read_text(encoding='utf-8-sig'))
def need(c,m):
 if not c:raise ValueError(m)
preserve=read('final-round10-preservation.json');need(preserve['fullRenderCallsThisBatch']==0 and preserve['allPassed'],'Round10 preservation/full-call scope');need(read('company-status.json')['status']=='NOT_RUN','Company status misrepresented');coverage=read('coverage-ledger.json');natural=read('natural-coverage-metrics.json');need(coverage['actualPhotos']==len(coverage['photos'])==len(natural['photos'])==6,'Six-photo bound');need({x['id'] for x in coverage['photos']}=={x['id'] for x in natural['photos']},'Photo identity alignment')
for photo in natural['photos']:
 need(max(photo['shape'][:2])<=1200 and photo['numeric']['finitePreparedInput'],'Photo size/finite');need(len(photo['streams'])==2 and {s['rows'] for s in photo['streams']}=={17,37},'Photo rows')
 for s in photo['streams']:
  need(s['comparison']['changedChannels']==0 and s['native']['deviceLiveAfter']==s['native']['stagingLiveAfter']==0 and s['native']['negativeStoreAbsentAfter'],'Photo exact/lifetime')
 need(not any(photo['numeric']['outputMaxFraction']),'Unexpected recorded output high endpoint')
need(read('natural-original-entry-preservation.json')['allPassed'] and read('natural-source-reproduction.json')['allExact'],'Independent guard/byte reproduction');guards=read('natural-guard-controls.json');need(len(guards['cases'])==4 and all(x['status']!=0 and x['readCallbacks']==0 and x['deviceLive']==x['stagingLive']==0 for x in guards['cases']),'Natural guard controls')
sys.path.insert(0,str(repo/'tools/color-round11/diagnostic'));from package_validation import validate_package
_,validation=validate_package(o/'diagnostic-home-v2','home');need(validation['dataChecksRecomputed'],'Shared package actual-data validation')
findings=read('concrete-findings.json');shadow=next(x for x in findings['relativeRoi'] if x['id']=='canonr6');need(shadow['stdRatio']<.14 and shadow['medianYRatio']>8,'Shadow recorded finding');flower=next(x for x in findings['endpointRecords'] if x['id']=='sonynex5r');need(flower['outputZeroWithAllProxyChannelsStrictInteriorAndNoPriorEndpointFootprint']==11562,'Flower recorded finding')
from verify_saved_outputs import validate_stage_manifest
validate_stage_manifest(read('natural-stage-finite.json'), natural['photos'])
print('HOME_ROUND11_SEALED_CHECK_PASS: recorded round10 freeze, company NOT_RUN, 6 developmental photo reports and 72 stage records; diagnostic pixels recomputed, natural saved-data audit separate; no GPU rerun')
