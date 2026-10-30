"""Recheck sealed evidence assertions; does not execute GPU or reread private pixels."""
from pathlib import Path
import json,sys
r=Path(sys.argv[1])
def read(n):return json.loads((r/n).read_text(encoding='utf-8-sig'))
def need(c,m):
 if not c:raise ValueError(m)
s=read('small-exact-results.json');need(len(s['cases'])==10,'Natural cases');need(all(x['success'] and x['oracle']['changedChannels']==0 and x['wholeCallerWithinCap'] for x in s['cases']),'Natural exact/host');need(s['syntheticPassed'] and s['consumerPassed'] and len(s['syntheticCases'])==4,'Synthetic/consumer')
c=read('control-results.json');need(len(c['cases'])==10 and all(not x['success'] and x['native']['deviceLiveAfter']==x['native']['stagingLiveAfter']==0 for x in c['cases']),'Fault controls');need(c['concurrencyPassed'] and c['concurrency']['firstCallCancelStatus']==-7 and c['concurrency']['secondCallStatus']==-30,'Concurrency')
f=read('full-stream-original-report.json');need(f['width']==5496 and f['height']==3670 and f['native']['devicePeakBytes']<=256*1048576 and f['native']['stagingPeakBytes']<=64*1048576,'Full sizes/device/staging');need(f['sampledProcessPeakRssBytes']<=512*1048576 and f['osProcessPeakWorkingSetBytes']<=512*1048576,'Observed full prepublication peaks');need(f['native']['deviceLiveAfter']==f['native']['stagingLiveAfter']==0 and f['native']['negativeStoreAbsentAfter'],'Full cleanup');need(read('full-budget-scope-audit.json')['fullTotalHostGateIncludingFinalReportPublication']=='PARTIAL_NOT_UPGRADED','Do not upgrade full final scope')
a=read('full-saved-audit-final.json');need(a['renderCalls']==0 and a['allExact'] and a['rawPixelHash']==f['outputPixelSha256'] and a['monitorJoined'],'Saved audit not render');d=read('cross-device-comparison.json');need(d['sameSourceAndFullPreparedInput'] and not d['sameFullHash'] and d['firstDifference'] is None,'Cross-device boundary');need(read('whole20MP-preflight.json')['wholeCalls']==0,'No forced20MPwhole')
b=read('bt-minimal-summary.json');need(b['cases'][0]['maxAbsCpuFloatCompatibilityError']>1e-5 and b['cases'][1]['maxAbsCpuFloatCompatibilityError']<1e-7,'BT original/patch')
m=read('d80014-success-report.json');v=read('d80014-reopen.json');need(m['recipe']['version']=='MatrixDecodeResearchV2' and v['previewExact'] and v['exportExact'] and v['pixelHash']==m['recipe']['pixelHash'],'D800V2');need(read('d80014-cancel.json')['success'],'D800 cancel')
print('HOME_ROUND9_SEALED_EVIDENCE_CHECK_PASS (not a device rerun; full final publication host scope remains PARTIAL)')
