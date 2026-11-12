"""Bind frozen R2 checks and normal-App observations to the preserved project."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
from collections import Counter
import argparse,json,hashlib,shutil,sqlite3,urllib.request,xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args()
context=Path(a.context);c=json.loads(context.read_text('utf-8-sig'));r=Path(c['worktree']);b=Path(c['deliveryRoot']);d=r/'docs/validation/product-functional-closure-20261112';rec=d/'receipts'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(p,o):Path(p).write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def copy(f,dest):dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(f,dest)
now=datetime.now(timezone(timedelta(hours=1))).isoformat();runs=[];ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for folder,count in [('r2-booking-tether-auto',61),('r2-navigation-auto',9),('r2-overlay-history-auto',1)]:
 f=b/folder/'results.trx';results=ET.parse(f).findall('.//t:UnitTestResult',ns)
 if len(results)!=count or any(x.get('outcome')!='Passed' for x in results):raise RuntimeError('R2 check count/pass mismatch: '+folder)
 dest=rec/'trx'/folder/'results.trx';copy(f,dest);runs.append({'file':dest.relative_to(r).as_posix(),'passed':count,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
copy(b/'r2-overlay-history-output/LINKED_BOOKING_OVERLAY.json',rec/'R2_COMPLETE_APP_REGRESSION.json')
copy(b/'overlay-fix-regression/results.trx',rec/'attempts/initial-overlay-harness/results.trx')
labels=['booking-saved-planning-linked','old-saved-overlay-manual-close','same-project-history-entry','r2-original-booking-reopen','r2-same-booking-edit','r2-linked-planning-overlay-fixed','r2-project-history-refreshed','r2-reopen-same-project-workflow','r2-add-synthetic-source-dialog','r2-folder-address-focus']
for name in labels:
 f=b/'native-resume'/(name+'.json')
 if f.is_file():
  value=read(f)
  if value.get('authority')!='Normal published EXE via Open Computer Use supported CLI':raise RuntimeError('Unexpected native receipt schema')
  copy(f,rec/'native'/(name+'.json'))
root=Path(c['workflowQaRuntime']);dbpath=root/'Data/pixel-tart.db'
with sqlite3.connect(dbpath.resolve().as_uri()+'?mode=ro',uri=True) as db:
 db.row_factory=sqlite3.Row
 booking=dict(db.execute('select Id,ProjectId,Title,StartAtUtc,EndAtUtc,Revision from ShootBookings where lower(Id)=lower(?)',(c['workflowBookingId'],)).fetchone())
 booking_count=db.execute('select count(*) from ShootBookings').fetchone()[0]
 source_count=db.execute('select count(*) from ProjectSources where lower(ProjectId)=lower(?)',(c['workflowProjectId'],)).fetchone()[0]
planning=read(root/'Data/ProjectPlanning'/(c['workflowProjectId'].replace('-','')+'.planning.json'))
if booking['ProjectId'].lower()!=c['workflowProjectId'].lower() or planning['ProjectId'].lower()!=c['workflowProjectId'].lower() or planning['BookingId'].lower()!=booking['Id'].lower():raise RuntimeError('Project/booking/planning identities disagree')
reopen=read(rec/'native/r2-original-booking-reopen.json')['accessibilityText'];overlay=read(rec/'native/r2-linked-planning-overlay-fixed.json')['accessibilityText'];history=read(rec/'native/r2-project-history-refreshed.json')['accessibilityText'];opened=read(rec/'native/r2-reopen-same-project-workflow.json')['accessibilityText']
checks={'sameBookingReopened':'合成排期 · 主流程核验' in reopen,'samePlanningProject':c['workflowProjectName'] in overlay,'savedOverlayNoLongerPresent':'自定义 拍摄快速编辑器' not in overlay,'historyShowsOriginalProject':c['workflowProjectName'] in history,'originalProjectOpenedIntoWorkflow':'项目已载入，可继续工作。' in opened,'onePersistedBooking':booking_count==1,'planningJsonLinksSameBooking':planning['BookingId'].lower()==booking['Id'].lower()}
if not all(checks.values()):raise RuntimeError('Native R2 evidence does not satisfy bounded claims')
startup=(root/'Logs/app-20261112.log').read_text('utf-8-sig');startup_lines=[line for line in startup.splitlines() if 'STARTUP_OK' in line]
if not startup_lines or ('ProductSourceSha='+c['currentProductSource']) not in startup_lines[-1]:raise RuntimeError('Latest normal App source mismatch')
save(rec/'R2_NATIVE_PROJECT_BOOKING_AND_REOPEN.json',{'status':'PASS_NORMAL_EXE_LINKED_BOOKING_PLANNING_HISTORY_AND_REOPEN','observedAtBerlin':now,'productSource':c['currentProductSource'],'appPid':c['workflowAppPid'],'projectId':c['workflowProjectId'],'booking':booking,'planningProjectId':planning['ProjectId'],'planningBookingId':planning['BookingId'],'checks':checks,'latestStartup':startup_lines[-1],'workflowDatabaseSources':source_count,'nativeImportComplete':False,'fullNewProjectWorkflowComplete':False,'nextNativeStep':'Choose the owned synthetic directory in the open Windows picker; do not substitute another project or inject source rows.','scope':'Normal published App via supported native UIA, independent SQLite/planning JSON and process reopen. No physical mouse, real camera or phone claim.'})
connection=read(Path(c['fullProductLocalTestRoot'])/'PRIVATE_CONNECTION.json')
with urllib.request.urlopen(connection['endpoint']+'health/ready',timeout=4) as response:health=json.load(response)
if health.get('instanceId')!=connection['instanceId'] or health.get('mode')!='LocalDevHttpV1':raise RuntimeError('R2 service root/readiness mismatch')
baseline=rec/'FULL_PACKAGE_BOOTSTRAP_VERIFIED.json'
if not (rec/'baseline-before-r2/FULL_PACKAGE_BOOTSTRAP_VERIFIED.json').exists():copy(baseline,rec/'baseline-before-r2/FULL_PACKAGE_BOOTSTRAP_VERIFIED.json')
save(baseline,{'status':'PASS_R2_COLD_START_AND_TWO_SHELL_REUSE','observedAtBerlin':now,'productSource':c['currentProductSource'],'package':c['package'],'entry':'Start-FullProduct-LocalTest.cmd','servicePid':connection['pid'],'appPid':connection['desktopPid'],'endpoint':connection['endpoint'],'serviceInstanceMatchesOwnedRoot':True,'runtimeRoot':c['fullProductLocalTestRoot'],'selfContainedAppAndBothBackends':True,'phoneRuntimeTouched':False,'credentialsPackaged':False,'scope':'R2 cold bootstrap and two-shell reuse; scratch root is separate from the preserved native project runtime.'})
latest={};history_counts=Counter()
for f in (rec/'trx').rglob('*.trx'):
 for x in ET.parse(f).findall('.//t:UnitTestResult',ns):
  name=x.get('testName');end=datetime.fromisoformat(x.get('endTime')).timestamp();history_counts[x.get('outcome')]+=1
  if name not in latest or end>latest[name][0]:latest[name]=(end,x.get('outcome'))
save(rec/'R2_FROZEN_AUTOMATIC_CHECKS.json',{'status':'PASS_EXACT_FROZEN_SOURCE_R2_RUNS','observedAtBerlin':now,'productSource':c['currentProductSource'],'newFrozenPasses':71,'runs':runs,'latestUniqueOutcomes':dict(Counter(v[1] for v in latest.values())),'retainedHistoricalOutcomeCounts':dict(history_counts),'selectionRule':'Latest endTime per test name; prior failed configuration and opt-in skip retained.','scope':'Domain, UI contracts and full-App automatic host; native EXE interaction is separate.'})
save(rec/'REPAIR_ATTEMPT_HISTORY.json',{'status':'FAILURES_RETAINED_WITH_LATER_VERIFICATION','observedAtBerlin':now,'items':[{'issue':'Initial full-App harness queried a visible control before layout was ready','receipt':'receipts/attempts/initial-overlay-harness/results.trx','resolution':'Final full-App host runs the existing command and checks overlay, identities and history.'},{'issue':'PS7 cold service inherited an empty public-base value','error':'Invalid explicit public test base URL','resolution':'Explicit loopback endpoint; cold service/App and both shell reuse checks passed.'},{'issue':'Earlier darkroom run lacked its isolated root','resolution':'Original failed/NotExecuted TRX retained with later isolated two-process passes.'}]})
c.update(fullPackageBootstrap='PASS_R2_COLD_START_AND_REUSE',fullPackageBootstrapAppPid=connection['desktopPid'],fullPackageBootstrapServicePid=connection['pid'],fullPackageBootstrapEndpoint=connection['endpoint'],workflowStage='NORMAL_R2_BOOKING_PLANNING_HISTORY_REOPEN_PASS_SOURCE_PICKER_PENDING',stage='R2_EVIDENCE_REFRESHED_FINAL_SEAL_PENDING',desktopInteractionBlocked=False)
save(context,c)
print(json.dumps({'status':'R2_EVIDENCE_BOUND','nativeChecks':len(checks),'bookingRevision':booking['Revision'],'nativeSourceRows':source_count,'latestUniqueOutcomes':dict(Counter(v[1] for v in latest.values()))}))
