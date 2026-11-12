"""Validate status/entry claims, evidence hashes, packaged bytes and secret exclusion."""
from pathlib import Path
from collections import Counter
from datetime import datetime,timezone
import argparse,hashlib,json,re,subprocess,xml.etree.ElementTree as ET,zipfile
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args()
c=json.loads(Path(a.context).read_text(encoding='utf-8-sig'));r=Path(c['worktree']);d=r/'docs/validation/product-functional-closure-20261112';package=Path(c['package']);checks={}
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for chunk in iter(lambda:f.read(1048576),b''):h.update(chunk)
 return h.hexdigest()
def check(name,valid):
 checks[name]=bool(valid)
 if not valid:raise RuntimeError(name)
status=read(d/'FUNCTION_STATUS.json');rows=status['rows'];current=read(d/'CURRENT_RECEIPT.json')
check('exactly42UniqueSubflowRows',len(rows)==42 and len({x['id'] for x in rows})==42)
check('fourClassificationsMatchReceipt',dict(Counter(x['status'] for x in rows))==current['classificationCounts'] and len(status['categories'])==4)
check('allRowSourcePathsExist',all((r/x['sourcePath']).is_file() for x in rows))
check('allRowEvidenceFilesExist',all((r/f).is_file() for x in rows for f in x['evidenceFiles']))
entry=read(d/'ENTRY_INVENTORY.json')
check('tenPrimaryEntriesObservedInNormalApp',len(entry['primaryNavigation'])==10 and all(x['observedInCurrentNormalApp'] for x in entry['primaryNavigation']))
check('fifteenPlanningSectionsSeparatelyListed',len(entry['planningSections'])==15 and len([x for x in rows if x['id'].startswith('PLAN-') and x['id'][5:].isdigit() and x['id']!='PLAN-00'])==15)
check('uiWithdrawalAndNoApprovedDrafts',current['uiRedesignWithdrawn'] and current['approvedGeneratedDrafts']==0)
delta=subprocess.check_output(['git','-C',str(r),'diff','--name-only',c['currentProductSource'],'--','src','extensions'],text=True)
check('frozenProductInputsMatchSource',not delta.strip())
index=read(d/'EVIDENCE_INDEX.json')
check('allIndexedEvidenceHashesAndSizesMatch',all((d/x['file']).is_file() and (d/x['file']).stat().st_size==x['bytes'] and sha(d/x['file'])==x['sha256'] for x in index['artifacts']))
identity=read(d/'DIAGNOSTIC_TOOL_IDENTITY.json')
check('allDiagnosticToolHashesMatch',all(sha(r/x['file'])==x['sha256'] for x in identity['tools']))
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};outcomes={};latest_times={};history=Counter()
for f in (d/'receipts/trx').rglob('*.trx'):
 for result in ET.parse(f).findall('.//t:UnitTestResult',ns):
  name=result.get('testName');ended=datetime.fromisoformat(result.get('endTime')).timestamp();history[result.get('outcome')]+=1
  if name not in latest_times or ended>latest_times[name]:latest_times[name]=ended;outcomes[name]=result.get('outcome')
check('215LatestUniquePassesOneExplicitSkip',dict(Counter(outcomes.values()))=={'Passed':215,'NotExecuted':1})
check('originalConfigurationFailureRetained',history['Failed']==1 and history['NotExecuted']==2)
check('new61ChecksPassed',read(d/'receipts/RESUME_AUTOMATIC_CHECKS.json')['freshRun']['counts']=={'Passed':61})
check('observerSevenSyntheticChecksOnly',all(read(d/'receipts/OBSERVER_BOUNDARY_SELFTEST.json')['checks'].values()) and not read(d/'receipts/OBSERVER_BOUNDARY_SELFTEST.json')['phoneRealDeviceEvidence'])
check('twoShellsReusedOriginalProcesses',all(x['sameServiceAndAppIdentity'] for x in read(d/'receipts/LAUNCHER_POWERSHELL_REUSE.json')['runs']))
check('localMobileCurrentChecksAllPass',all(read(d/'receipts/MOBILE_READONLY_CURRENT.json')['checks'].values()))
check('nativeWorkflowStillNotFalselyClaimed',not current['nativeWorkflowCompleted'] and read(d/'receipts/WORKFLOW_CURRENT_READONLY.json')['nativeBookingSaveCompleted'] and all(read(d/'receipts/R2_NATIVE_PROJECT_BOOKING_AND_REOPEN.json')['checks'].values()))
check('phoneRoundtripStillNotFalselyClaimed',current['phoneRealDevice'].startswith('NOT_RUN'))
manifest=read(package/'PACKAGE_FILES.json');listed={x['path']:x for x in manifest['files']}
check('allPackageFilesHaveExpectedHashes',all((package/name).is_file() and (package/name).stat().st_size==x['bytes'] and sha(package/name)==x['sha256'] for name,x in listed.items()))
actual={f.relative_to(package).as_posix() for f in package.rglob('*') if f.is_file() and f.name!='PACKAGE_FILES.json'}
check('noUnlistedPackageFiles',actual==set(listed))
check('bothBackendsAndFullProductEntryPresent',all((package/x).is_file() for x in ['SelectionService/PixelTart.SelectionApi.Server.exe','ScheduleService/Guangjing.Schedule.Server.exe','Start-FullProduct-LocalTest.cmd']))
delivery=read(d/'PACKAGE_DELIVERY.json');archive=Path(delivery['archive'])
check('archiveHashMatchesExternalReceipt',sha(archive)==delivery['archiveSha256'] and archive.stat().st_size==delivery['archiveBytes'])
with zipfile.ZipFile(archive) as z:
 check('archiveAllCrcChecksPass',z.testzip() is None)
 check('archiveFileCountMatches',len(z.infolist())==delivery['archiveFiles'])
 prefix=package.name+'/'
 check('archiveManifestEqualsDeliveredManifest',json.loads(z.read(prefix+'PACKAGE_FILES.json'))==manifest)
 check('archiveEvidenceIndexEqualsCommittedIndex',json.loads(z.read(prefix+'diagnostics-reports/product-functional-closure-20261112/EVIDENCE_INDEX.json'))==index)
mobile=read(c['mobileContext']);private=read(Path(mobile['runtimeRoot'])/'PHONE_DEVICE_PRIVATE.json')
secrets=[private['token'].encode(),(Path(mobile['runtimeRoot'])/'schedule-owner.key').read_bytes().strip()]
for private_path in [Path(c['fullProductLocalTestRoot'])/'PRIVATE_CONNECTION.json',Path(c['deliveryRoot'])/'SELECTION_CONNECTION_PRIVATE.json']:
 value=read(private_path).get('ownerKey')
 if value:secrets.append(value.encode())
text_suffixes={'.json','.jsonl','.trx','.csv','.md','.py','.ps1','.cmd','.txt','.html','.js','.xml'}
files=[f for f in d.rglob('*') if f.is_file()]+[r/x['file'] for x in identity['tools']]
files += [f for f in package.rglob('*') if f.is_file() and f.suffix.lower() in text_suffixes]
for f in files:
 data=f.read_bytes()
 if re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}', data) or any(value and value in data for value in secrets):raise RuntimeError('Private credential/link found in deliverable: '+str(f))
check('liveOwnerKeyDeviceTokenAndLoginLinkExcluded',True)
for f in (r/'tools/FunctionalClosureAcceptance').glob('*.py'):compile(f.read_text('utf-8'),str(f),'exec')
check('pythonToolsSyntaxValid',True)
result={'status':'PASS_BUNDLE_INTEGRITY_WITH_EXPLICIT_FUNCTIONAL_BLOCKERS','observedAtUtc':datetime.now(timezone.utc).isoformat(),'checks':checks,
 'indexedEvidenceFiles':len(index['artifacts']),'packageFiles':len(actual)+1,'archiveSha256':delivery['archiveSha256'],
 'phoneRealDeviceRoundtrip':'NOT_RUN','nativeNewProjectWorkflow':'INCOMPLETE_BOOKING_DRAFT_ONLY','releaseReady':False,
 'scope':'Delivery integrity, exact evidence source/entry bindings, and bounded runtime checks; does not override remaining external/device/product blockers.'}
(d/'BUNDLE_VALIDATION.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'status':result['status'],'checksPassed':len(checks),'evidenceFiles':len(index['artifacts']),'packageFiles':len(actual)+1,'archiveSha256':delivery['archiveSha256']}))