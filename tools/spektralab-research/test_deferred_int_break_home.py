"""Controlled SIGINT+SIGBREAK dispatch while real small native reader is held.
No broadcast CTRL_C event is sent to other applications. Frozen caller stays untouched.
"""
import json,time,signal,sys
from pathlib import Path
from types import SimpleNamespace
import stream_case
from host_environment import environment
from natural_admission import sha,load_json,need,atomic_json
h=environment();r=Path(h['researchRoot']);small=Path(h['smallRoot']);manifest=small/'admission-manifest-v2.json';out=r/'round10-controlled-int-and-break';original=stream_case.join_native;injected=[False]
def controlled_join(worker,cancel):
 if worker is not None and worker.name!='host-budget-monitor' and not injected[0]:
  deadline=time.monotonic()+10
  while not (out/'test-native-read-entered').exists() and worker.is_alive() and time.monotonic()<deadline:time.sleep(.01)
  need((out/'test-native-read-entered').exists(),'Native hold not reached');injected[0]=True
  for s in [signal.SIGINT,signal.SIGINT,signal.SIGBREAK]:signal.raise_signal(s);time.sleep(.05)
 return original(worker,cancel)
stream_case.join_native=controlled_join
try:
 a=SimpleNamespace(input=small/'whole-scene-321x215.npy',output=out,manifest=manifest,manifest_sha=sha(manifest),library=Path(h['streamLibrary']),resources=Path(h['resources']),rows=17,consumer_rows=7,device_mib=256,staging_mib=64,host_mib=512,oracle=None,fault='hold-read',freeze=None);report=stream_case.run(a)
finally:stream_case.join_native=original
need(injected[0] and report['deferredInterruptSignals']==[2,2,21] and report['returncode']==-7 and report['joinedBeforeRelease'] and not report['success'],'Deferred INT/BREAK failed');need(report['native']['deviceLiveAfter']==report['native']['stagingLiveAfter']==0,'Native owners not released');atomic_json(r/'evidence/round10-controlled-int-and-break.json',{'version':'ControlledInterruptDispatchV1','success':True,'signalsObserved':report['deferredInterruptSignals'],'nativeStatus':report['returncode'],'joinedBeforeRelease':report['joinedBeforeRelease'],'noFinalGroup':not(out/'rgba16.bin').exists(),'mechanism':'signal.raise_signal under real held native call; targeted Ctrl-Break separately tested in another process','doesNotBroadcastToOtherApps':True});print('INT_INT_BREAK_NATIVE_JOIN_PASS')
