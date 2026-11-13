"""Verify the single-source diagnostic without overriding failed or pending acceptance."""
from pathlib import Path
from collections import Counter
from datetime import datetime,timezone
import argparse,hashlib,json,re,subprocess,zipfile,xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args();c=json.loads(Path(a.context).read_text('utf-8-sig'));r=Path(c['worktree']);d=r/c['evidenceDirectory'];package=Path(c['package']);checks={}
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def check(name,ok):
 checks[name]=bool(ok)
 if not ok:raise RuntimeError(name)
def git(*args):return subprocess.check_output(['git',*args],cwd=r,text=True,encoding='utf-8').strip()
check('frozenProductInputs',not git('diff','--name-only',c['currentProductSource'],'--','src','extensions'))
check('functionalFixesPreserved',not git('diff','--name-only','ea5b421b',c['currentProductSource'],'--','src/RAWSelectionAssistant/MainWindow.xaml.cs','src/RAWSelectionAssistant/ViewModels/MainViewModel.cs'))
check('noCoreDecoderOrExtensionChange',not git('diff','--name-only','ea5b421b',c['currentProductSource'],'--','src/RAWSelectionAssistant.Core','extensions'))
status=read(d/'FUNCTION_STATUS.json');rows=status['rows'];receipt=read(d/'CURRENT_RECEIPT.json')
check('42UniqueRows',len(rows)==42 and len({x['id'] for x in rows})==42)
check('classificationsMatchReceipt',dict(Counter(x['status'] for x in rows))==receipt['classificationCounts'])
check('sourcePathsResolve',all((r/x['sourcePath']).is_file() for x in rows))
check('newAndInheritedEvidenceResolve',all((r/f).is_file() for x in rows for f in x.get('evidenceFiles',[])+x.get('inheritedEvidenceFiles',[])))
entry=read(d/'ENTRY_INVENTORY.json');check('tenPrimaryFifteenPlanningEntries',len(entry['primaryNavigation'])==10 and len(entry['planningSections'])==15)
check('uiDraftsRemainUnapproved',receipt['approvedGeneratedDrafts']==0 and receipt['uiRedesignWithdrawn'])
auto=read(d/'receipts/FINAL_AUTOMATIC_CHECKS.json');check('86ExactFrozenChecks',auto['frozenCurrentPasses']==86 and sum(x['passed'] for x in auto['runs'])==86)
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};latest={}
for f in (d/'receipts').rglob('*.trx'):
 if 'attempts' in f.parts:continue
 for x in ET.parse(f).findall('.//t:UnitTestResult',ns):
  name=x.get('testName');t=datetime.fromisoformat(x.get('endTime')).timestamp()
  if name not in latest or t>latest[name][0]:latest[name]=(t,x.get('outcome'))
check('latestOutcomesMatchReceipt',dict(Counter(v[1] for v in latest.values()))==auto['latestUniqueOutcomes'])
tiff=read(d/'receipts/TIFF_PIPELINE_COMPARISON.json');check('realTiffFilesAndReopenConsistent',all(tiff['checks'].values()) and tiff['inputToOutputRgb48PixelMatch'] and tiff['sourceFileUnchanged'])
check('tiffChannelPrecisionRetained',tiff['inputChannelBitDepth']==tiff['outputChannelBitDepth']==16)
visual=read(d/'receipts/VISUAL_TIFF_GUARD_FAILURES.json');check('failedVisualGuardRetained',any(x['stop']=='2GiB resource guard' for x in visual['runs']) and receipt['realTiffVisual2GiBFlow']=='BLOCKED')
check('phoneAndMainFlowNotFalselyCompleted',receipt['phoneTrueDeviceRoundtrip']=='NOT_RUN' and not receipt['fullTaskCompleted'])
phone=read(d/'receipts/PHONE_CURRENT.json');check('existingPhoneSessionReadOnly',phone['existingCredentialAccepted'] and phone['anonymousRejected'] and phone['requestsWereGetOnly'])
index=read(d/'EVIDENCE_INDEX.json');check('evidenceDigests',all((d/x['file']).stat().st_size==x['bytes'] and sha(d/x['file'])==x['sha256'] for x in index['artifacts']))
tools=read(d/'DIAGNOSTIC_TOOL_IDENTITY.json');check('toolDigests',all(sha(r/x['file'])==x['sha256'] for x in tools['tools']))
manifest=read(package/'PACKAGE_FILES.json');listed={x['path']:x for x in manifest['files']};actual={f.relative_to(package).as_posix() for f in package.rglob('*') if f.is_file() and f.name!='PACKAGE_FILES.json'}
check('packageCoverage',actual==set(listed));check('packageFileDigests',all((package/name).stat().st_size==x['bytes'] and sha(package/name)==x['sha256'] for name,x in listed.items()))
check('allModuleExecutables',all((package/x).is_file() for x in ['App/KitaoPhotoSelector.exe','ScheduleService/Guangjing.Schedule.Server.exe','SelectionService/PixelTart.SelectionApi.Server.exe','Start-FullProduct-LocalTest.cmd']))
delivery=read(d/'PACKAGE_DELIVERY.json');archive=Path(delivery['archive']);check('archiveHash',sha(archive)==delivery['archiveSha256'])
with zipfile.ZipFile(archive) as z:
 check('archiveCrcAndCount',z.testzip() is None and len(z.infolist())==delivery['archiveFiles']);prefix=package.name+'/'
 check('archiveManifest',json.loads(z.read(prefix+'PACKAGE_FILES.json'))==manifest)
 check('archiveEvidenceIndex',json.loads(z.read(prefix+'diagnostics-reports/'+d.name+'/EVIDENCE_INDEX.json'))==index)
mobile=read(c['mobileContext']);secret=[read(Path(mobile['runtimeRoot'])/'PHONE_DEVICE_PRIVATE.json')['token'].encode(),(Path(mobile['runtimeRoot'])/'schedule-owner.key').read_bytes().strip()]
for f in [Path(c['fullProductLocalTestRoot'])/'PRIVATE_CONNECTION.json',Path('E:/Kitao-Deliveries/product-functional-closure-20261112/SELECTION_CONNECTION_PRIVATE.json')]:
 o=read(f)
 for key,value in o.items():
  if key.lower()=='ownerkey' and value:secret.append(value.encode())
texttypes={'.json','.jsonl','.trx','.csv','.md','.txt','.py','.ps1','.cmd','.js','.html','.xml','.cs'}
files=[f for f in d.rglob('*') if f.is_file()]+[r/x['file'] for x in tools['tools']]+[f for f in package.rglob('*') if f.is_file() and f.suffix.lower() in texttypes]
for f in files:
 data=f.read_bytes()
 if re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}',data) or any(value and value in data for value in secret):raise RuntimeError('Credential or login link in deliverable: '+str(f))
check('credentialsAndLoginLinksExcluded',True)
for folder in ['tools/FunctionalClosureAcceptance','tools/ProductMemoryIntegrationReplay']:
 for f in (r/folder).glob('*.py'):compile(f.read_text('utf-8'),str(f),'exec')
check('pythonSyntax',True)
result={'status':'PASS_UNIFIED_BUNDLE_WITH_EXPLICIT_FUNCTIONAL_BLOCKERS','recordedAtUtc':datetime.now(timezone.utc).isoformat(),'productSource':c['currentProductSource'],'checks':checks,'evidenceFiles':len(index['artifacts']),'packageFiles':len(actual)+1,'archiveSha256':delivery['archiveSha256'],'releaseReady':False,'fullTaskCompleted':False}
(d/'BUNDLE_VALIDATION.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({'status':result['status'],'checks':len(checks),'evidenceFiles':len(index['artifacts']),'packageFiles':len(actual)+1,'archiveSha256':delivery['archiveSha256']}))
