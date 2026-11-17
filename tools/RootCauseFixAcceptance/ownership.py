from pathlib import Path
import argparse,json,subprocess,time,uuid,os,hashlib
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);p.add_argument('--dll',required=True);p.add_argument('--root',required=True);a=p.parse_args();root=Path(a.root).resolve();root.mkdir(exist_ok=False);name='RootCause-'+uuid.uuid4().hex
owners=[];rows=[]
def owner(role,instance):
 proc=subprocess.Popen([a.dotnet,a.dll,role,instance,str(root)],creationflags=subprocess.CREATE_NO_WINDOW);owners.append(proc)
 until=time.monotonic()+10
 while not (root/(role+'-'+str(proc.pid)+'.json')).exists():
  assert proc.poll() is None and time.monotonic()<until;time.sleep(.03)
 return proc
def run(role,instance):
 start=time.monotonic();subprocess.run([a.dotnet,a.dll,role,instance,str(root)],check=True,timeout=15,creationflags=subprocess.CREATE_NO_WINDOW);rows.append(dict(case=role,instance=instance,durationMs=(time.monotonic()-start)*1000))
try:
 A=owner('owner-unresponsive',name+'-A');B=owner('owner-slow',name+'-B');time.sleep(16.6)
 run('recover',name+'-A');assert A.poll() is None and B.poll() is None
 recover=next(json.loads(p.read_text()) for p in root.glob('recover-*.json'));assert not recover['acquired'] and recover['outcome']=='ExistingInstanceUnresponsive'
 for proc in owners:assert (root/('unsaved-'+str(proc.pid)+'.txt')).read_text()=='in-memory-work-not-committed'
 C=owner('owner-ack',name+'-C');run('activate',name+'-C');activate=next(json.loads(p.read_text()) for p in root.glob('activate-*.json'));assert activate['outcome']=='ActivatedExisting' and not activate['acquired']
 run('unowned',name+'-unowned');run('abandoned',name+'-abandoned')
 # The production manager no longer queries or acts on any PID. A reused PID cannot
 # be selected by this recovery path, including fabricated stale metadata.
 (root/'stale-pid-metadata.json').write_text(json.dumps(dict(pid=B.pid,root='A',creationTime=0)))
 run('recover-again',name+'-A');assert A.poll() is None and B.poll() is None
 source=Path(__file__).resolve().parents[2]/'src/RAWSelectionAssistant/Utilities/SingleInstanceManager.cs';text=source.read_text('utf-8')
 assert all(s not in text for s in ['Process.GetProcesses','Process.GetProcessById','.Kill(','out var createdNew'])
 result=dict(status='PASS',cases=rows,twoRootsCoexist=True,unresponsiveAndSlowNoWindowPreserved=True,unsavedContentPreserved=True,pidReuseCannotAuthorizeTermination=True,pidReuseScope='No PID lookup or termination in recovery; stale metadata has no authority',actualMutexAndAbandonedAcquired=True,sameRootActivation=True,sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest())
 (root/'RESULT.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(result))
finally:
 for proc in owners:(root/('finish-'+str(proc.pid))).touch()
 for proc in owners:proc.wait(timeout=10)
