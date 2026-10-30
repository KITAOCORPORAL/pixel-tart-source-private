"""Assert observed round7 evidence, not another full GPU submission."""
from pathlib import Path
import argparse,json,hashlib
p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);a=p.parse_args();read=lambda n:json.loads((a.evidence/n).read_text(encoding='utf-8-sig'))
def need(ok,text):
 if not ok:raise ValueError(text)
text=(a.evidence/'admission-tests.txt').read_text();need('Ran 24 tests' in text and 'OK' in text and '\nFAILED (' not in text,'Failure controls incomplete')
need([r['accepted'] for r in read('output-root-tests.json')]==[False,False,False,True],'Output isolation failed')
prep=read('bounded-preparation-check.json');need(prep['sameMathSameFullAndProxyBytes'] and len(prep['checks'])==10,'Input math/bytes migrated')
for left,right in [('admitted-case.json','admitted-reopen.json'),('new-preparation-admitted-case.json','new-preparation-admitted-reopen.json')]:
 x,y=read(left),read(right);need(x['success'] and y['success'] and x['tiff16Exact'] and y['tiff16Exact'],'Actual bounded output/reopen failed')
 for k in ['manifestSha256','sourceSha256','admittedEntry','inputPixelSha256','librarySha256','resourceIdentity','resolved','outputPixelSha256']:need(x[k]==y[k],'First-vs-reopen identity differs '+k)
 need(x['boundedValidation']['maximumFiniteBooleanBytes']<=32*x['inputShape'][1]*3,'Not bounded finite buffer')
need(read('row-stream-repeat.json')['repeatExact'],'Bounded repeated output failed')
b=read('complete-bridge.json');need(b['colourFullMaxAbs']<1e-12 and b['ocioFullMaxAbs']<1e-5,'Complete independent bridge comparison failed');need(b['negativeAudit']['f32Counts']==[0,942981,590359] and b['negativeAudit']['createdOnlyByFinalF32Cast']==[0,0,0],'Negative audit mismatch')
m=read('allocation-measurements.json');need(len(m['cases'])==8 and len(m['comparisons'])==5,'Measurements missing');need(all(c['wholeVsStripeExact'] for c in m['comparisons']),'Stripe output differs');need(all(not c['liveAfterDestroy'] for c in m['cases']),'Tracked allocations leaked');x=next(c for c in m['cases'] if c['size']==[641,429] and c['label']=='stripe37');y=next(c for c in m['cases'] if c['label']=='stripe37budget1');need(x['allocationPeaks']==y['allocationPeaks'],'Budget-field observation changed')
f=read('full-path-feasibility.json');need(f['fullEngineCreateCalls']==f['fullOpenUploadCalls']==f['fullRenderCalls']==0,'Unexpected full submission');need(f['sourceBasedHoldings']['requiredDeviceLocalLowerBound56n']>f['currentAdmissionBytes'],'Recorded blocking inequality not true')
print('PASS:24targeted controls;output roots;first-manifest+reopen identity;bounded preparation/readback;complete bridge;8allocator runs/5exact stripe comparisons;single full blocker without upload.')
