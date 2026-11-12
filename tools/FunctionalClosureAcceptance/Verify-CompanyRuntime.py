"""Recheck the two supported launcher shells and the isolated timezone sidecar."""
from pathlib import Path
from datetime import datetime, timezone
import argparse, json, shutil, subprocess, sys, time, urllib.request, urllib.error, uuid
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args()
c=json.loads(Path(a.context).read_text(encoding='utf-8-sig'));r=Path(c['worktree']);b=Path(c['deliveryRoot']);d=r/'docs/validation/product-functional-closure-20261112/receipts'
def save(name,value):(d/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
full_root=Path(c.get('fullProductLocalTestRoot',str(b/'full-product-localtest')))
connection=full_root/'PRIVATE_CONNECTION.json'
def identity():
 x=json.loads(connection.read_text(encoding='utf-8-sig'));return {k:x[k] for k in ['pid','desktopPid','startedUtc','desktopStartedUtc','instanceId']}
before=identity();runs=[]
for shell in ['pwsh.exe','powershell.exe']:
 executable=shutil.which(shell)
 if not executable:raise RuntimeError('Required validation shell unavailable: '+shell)
 result=subprocess.run([executable,'-NoProfile','-ExecutionPolicy','Bypass','-File',str(Path(c['package'])/'Start-OnlineSelection-LocalTest.ps1'),'-DataRoot',str(full_root)],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=15)
 after=identity();ok=result.returncode==0 and before==after and 'no second writer was started' in result.stdout
 runs.append({'shell':shell,'exitCode':result.returncode,'sameServiceAndAppIdentity':before==after,'reusedExistingDesktopMessage':'no second writer was started' in result.stdout})
 if not ok:raise RuntimeError('Launcher reuse failed; existing state retained: '+shell)
save('LAUNCHER_POWERSHELL_REUSE.json',{'status':'PASS_BOTH_SHELLS_SAME_RUNNING_APP_AND_SERVICE','observedAtUtc':datetime.now(timezone.utc).isoformat(),'identity':before,'runs':runs,'fixed':'Compare process start instants rather than serialized date strings; do not overwrite desktop receipt when a new process exits before readiness.','recoveredOriginalOwnedAppIdentity':True,'runtimeDataPreserved':True,'scope':'Launcher/service readiness only; no business-flow UI completion claimed.'})

root=b/('observer-selftest-'+uuid.uuid4().hex);root.mkdir();nonce=uuid.uuid4().hex;round_id='synthetic-'+uuid.uuid4().hex;public='http://127.0.0.1:17668'
config={'nonce':nonce,'roundId':round_id,'deviceId':'synthetic-device-not-iphone','publicBaseUrl':public,'outputFile':str(root/'OBSERVATION.jsonl')}
cp=root/'config-private.json';cp.write_text(json.dumps(config),encoding='utf-8');obs=r/'tools/MobileScheduleAcceptance/BrowserTimeZoneObserver.py'
log=(root/'server.log').open('ab');server=subprocess.Popen([sys.executable,str(obs),'--config',str(cp),'--port','17668'],stdout=log,stderr=log,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));log.close()
def req(path,body=None,origin=None):
 headers={'User-Agent':'Guangjing-Diagnostic-Selftest/1.0'}
 if origin:headers['Origin']=origin
 if body is not None:headers['Content-Type']='application/json'
 request=urllib.request.Request(public+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
 try:
  with urllib.request.urlopen(request,timeout=2) as response:return response.status,response.read(),dict(response.headers)
 except urllib.error.HTTPError as e:return e.code,e.read(),dict(e.headers)
try:
 for _ in range(30):
  try:code,payload,_=req('/qa/health/ready');break
  except urllib.error.URLError:time.sleep(.1)
 health=json.loads(payload);checks={'healthBindsSameRoundAndOrigin':code==200 and health['roundId']==round_id and health['publicBaseUrl']==public}
 code,html,headers=req('/qa/start/'+nonce);checks['startPageAndCsp']=code==200 and 'Content-Security-Policy' in headers and b'/calendar#token=' in html
 body={'roundId':round_id,'deviceId':config['deviceId'],'browserTimeZone':'Asia/Shanghai','browserUtcOffsetMinutes':480,'browserNowUtc':datetime.now(timezone.utc).isoformat(),'language':'en','userAgent':'Guangjing-Diagnostic-Selftest/1.0'}
 code,_,_=req('/qa/observe/'+nonce,body,public);checks['syntheticObservationAccepted']=code==200
 code,_,_=req('/qa/observe/'+nonce,body,'https://unrelated.invalid');checks['wrongOriginRejected']=code==403
 code,_,_=req('/qa/observe/'+nonce,{},public);checks['invalidObservationRejected']=code==400
 code,_,_=req('/qa/start/wrong');checks['wrongNonceRejected']=code==404
 record=json.loads(Path(config['outputFile']).read_text('utf-8').splitlines()[-1]);checks['notRecordedAsIphoneSafari']=record['iPhoneSafariUserAgentObserved'] is False
 save('OBSERVER_BOUNDARY_SELFTEST.json',{'status':'PASS_SYNTHETIC_OBSERVER_SELFTEST_NOT_IPHONE' if all(checks.values()) else 'FAIL','observedAtUtc':datetime.now(timezone.utc).isoformat(),'checks':checks,'phoneRealDeviceEvidence':False,'bookingMutations':False,'scope':'Real local HTTP sidecar with synthetic user agent. Browser metadata alone never grants real-device acceptance.'})
 if not all(checks.values()):raise RuntimeError('Observer selftest failed')
finally:server.terminate();server.wait(timeout=5)
print(json.dumps({'status':'PASS','launcherShells':2,'observerChecks':len(checks),'phoneDeviceEvidence':False}))