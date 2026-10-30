"""Numerical/lifetime assertions on observed stream evidence; no full rerender."""
from pathlib import Path
import argparse,json
p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);a=p.parse_args();read=lambda n:json.loads((a.evidence/n).read_text(encoding='utf-8-sig'))
def need(ok,msg):
 if not ok:raise ValueError(msg)
small=read('small-exact-results.json')['cases'];need(len(small)==10,'Missing exact oracle cases')
for c in small:need(c['success'] and c['oracle']['wholeVsStreamExact'] and c['oracle']['changedChannels']==0,'Small whole/stream mismatch');need(c['native']['halo']==2 and c['native']['deviceLiveAfter']==c['native']['stagingLiveAfter']==0,'Halo/lifetime changed');need(c['resolved']['film_stock']=='kodak_portra_400' and c['resolved']['input_cctf_decoding']==False,'Contract broadened')
controls=read('control-results.json')['cases'];failed=[c for c in controls if not c['success']];synthetic=[c for c in controls if c['directory'].startswith('synthetic-r')];repeat=[c for c in controls if c['directory'].startswith('repeat-process')];need(len(failed)==10 and len(synthetic)==4 and len(repeat)==2,'Native controls incomplete')
for c in controls:need(c['joinedBeforeRelease'] and c['native']['deviceLiveAfter']==c['native']['stagingLiveAfter']==0 and c['native']['negativeStoreAbsentAfter'],'Failure cleanup/lifetime failed')
for c in synthetic+repeat:need(c['success'] and c['oracle']['wholeVsStreamExact'],'Synthetic/repeat output mismatch')
need(read('rows-budget-control.json')['success'] and read('rows-budget-control.json')['native']['rows']<4096,'Rows not constrained by budget')
height=read('height-scaling-proof.json');need(height['largerHeightWholeVsStreamExact']['wholeVsStreamExact'] and all(x[0]==x[1] for x in height['peakComparisons'].values()),'Hidden height-scaled buffer')
concurrent=read('concurrency-cancel.json');need(concurrent['secondCallStatus']==-30 and concurrent['firstCallCancelStatus']==-7 and concurrent['firstJoined'],'Thread/cancel contract failed')
full=read('full-stream-report.json');need(full['success'] and full['native']['success'] and [full['width'],full['height']]==[5496,3670] and full['tiff16Exact'],'Full pixel output incomplete');need(full['native']['devicePeakBytes']<=full['budgets']['deviceBytes'] and full['native']['stagingPeakBytes']<=full['budgets']['stagingBytes'],'Native full budget failed');need(full['native']['deviceLiveAfter']==full['native']['stagingLiveAfter']==0,'Native full leaked')
reopen=read('full-independent-reopen.json');io=read('full-corrected-row-io.json');need(reopen['renderCalls']==io['renderCalls']==0 and reopen['allExact'] and io['allExact'] and reopen['tiffPixelHash']==io['tiffPixelHash']==full['outputPixelSha256'],'Saved full output integrity failed');need(reopen['withinCpuCap'] and io['sampledPeakRssBytes']<=io['hostCapBytes'],'Corrected bounded I/O host gate failed')
caveat=read('full-budget-caveat.json');need(not caveat['initialEntireProcessWithinHostCap'] and caveat['fullNativeRenderCalls']==1 and not caveat['fullRerenderAfterCorrection'],'Host budget failure hidden/extra full render')
need(read('source-reproduction.json')['allMatch'],'Source generator differs from compiled source')
print('PASS:10natural small exact;4synthetic exact;10native failures;2same-process repeats;concurrency/cancel;rows and height bounds;one20MP pixel output+native budgets;full reopened I/O exact;initial combined host-cap failure explicitly retained.')
