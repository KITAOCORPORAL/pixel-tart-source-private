"""Reproduce recorded single full blocker; does not create engine or resubmit full render."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('snapshot',type=Path);a=p.parse_args();j=json.loads(a.snapshot.read_text());n=j['input'][0]*j['input'][1]
checks={'pixels':n==j['pixels'],'source12n':12*n==j['sourceBasedHoldings']['fullSourceDeviceLocal'],'negative12n':12*n==j['sourceBasedHoldings']['cachedNegativeDeviceLocal'],'twoRgb24n':24*n==j['sourceBasedHoldings']['atLeastTwoRetainedFullRgbPlanes'],'rgba8n':8*n==j['sourceBasedHoldings']['gpuRgba16'],'floor56n':56*n==j['sourceBasedHoldings']['requiredDeviceLocalLowerBound56n'],'budgetIndependentCap':j['currentAdmissionBytes']<=j['researchCapBytes'],'blocking':56*n>j['currentAdmissionBytes'],'noFullRequest':j['fullEngineCreateCalls']==j['fullOpenUploadCalls']==j['fullRenderCalls']==0}
if not all(checks.values()):raise ValueError(checks)
print(json.dumps({'version':'RecordedFullBlockerReproductionV1','checks':checks,'necessaryLowerBound':56*n,'recordedBudget':j['currentAdmissionBytes'],'status':'BLOCKED_AT_RECORDED_LOAD_NOT_ETERNAL_DEVICE_CLAIM','engineCreated':False}))
