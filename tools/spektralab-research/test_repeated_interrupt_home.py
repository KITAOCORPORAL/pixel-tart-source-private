"""Three actual console interrupts during a held native read; no20MP."""
from pathlib import Path
import json,subprocess,signal,time,sys,os
from natural_admission import load_json,sha,need
from host_environment import environment
host=environment();root=Path(host['researchRoot']);small=Path(host['smallRoot']);manifest=small/'admission-manifest-v2.json';dest=root/'round10-repeat-interrupt';log=root/'evidence/round10-repeat-interrupt.txt';need(not dest.exists() and not log.exists(),'Interrupt control exists');cmd=[sys.executable,str(Path(__file__).with_name('stream_case.py')),str(small/'whole-scene-321x215.npy'),str(dest),'--manifest',str(manifest),'--manifest-sha',sha(manifest),'--library',host['streamLibrary'],'--resources',host['resources'],'--rows','17','--fault','hold-read']
with log.open('x') as f:
 p=subprocess.Popen(cmd,stdout=f,stderr=f,creationflags=subprocess.CREATE_NEW_PROCESS_GROUP);start=time.monotonic();signals=[]
 while not (dest/'test-native-read-entered').exists() and p.poll() is None and time.monotonic()-start<15:time.sleep(.01)
 need((dest/'test-native-read-entered').exists(),'Native hold control not reached')
 for signum in [signal.CTRL_BREAK_EVENT,signal.CTRL_BREAK_EVENT,signal.CTRL_C_EVENT]:
  # CTRL_C cannot reliably target one group on all hosts; use SIGBREAK for repeated native test.
  sent=signal.CTRL_BREAK_EVENT;p.send_signal(sent);signals.append(int(sent));time.sleep(.12)
 rc=p.wait(20)
r=load_json(dest/'report.json');need(not r['success'] and r['returncode']==-7 and r['joinedBeforeRelease'] and len(r['deferredInterruptSignals'])>=2,'Repeated interrupts did not cancel/join safely');need(r['native']['deviceLiveAfter']==r['native']['stagingLiveAfter']==0 and r['native']['negativeStoreAbsentAfter'],'Native not cleaned');need(not (dest/'rgba16.bin').exists() and not (dest/'display-rgba16.tif').exists(),'Cancelled call committed output');j={'version':'RepeatedNativeInterruptControlV1','success':True,'signalsSent':signals,'signalsObserved':r['deferredInterruptSignals'],'exitCode':rc,'nativeCancelStatus':r['returncode'],'workerJoined':r['joinedBeforeRelease'],'nativeLive0':True,'noFinalGroup':True,'readHoldSeconds':1.5,'testedNativeCase':'small321x215 read callback hold; not every possible shader checkpoint'};(root/'evidence/round10-repeat-interrupt.json').write_text(json.dumps(j,indent=2));print(json.dumps(j))
