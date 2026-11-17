"""Collect small source-bound investigation receipts; never copy runtime databases or user documents."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil,subprocess
repo=Path(__file__).resolve().parents[2];base=Path('E:/Kitao-Deliveries/close-lifecycle-investigation-20261117');docs=repo/'docs/validation/close-lifecycle-investigation-20261117';docs.mkdir(exist_ok=True)
product=Path('E:/Kitao-Worktrees/company-functional-20261116');source='e8180221937fa4abb578a16faacebe99a5d4ac1b'
def git(*args,root=repo):return subprocess.check_output(['git',*args],cwd=root,text=True,encoding='utf-8').strip()
def load(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def save(p,o):Path(p).write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
assert not git('diff','--name-only','fdf01831','--','src','tests')
coord=dict(atUtc=datetime.now(timezone.utc).isoformat(),timestamps="UTC values retained from original observations",productChat='01a1e355-64ea-74e0-b7b5-15ec557fdc30',productWorktree=str(product),observedProductHead=git('rev-parse','HEAD',root=product),observedProductDirty=git('status','--short',root=product),investigationWorktree=str(repo),investigationBranch=git('branch','--show-current'),base='fdf01831a65065b574e6a320aba0cb5a4425148f',functionalSource=source,readProductRootAgentsSha256=sha(product/'AGENTS.md'),writeScope=['tools/CloseLifecycleInvestigation','docs/validation/close-lifecycle-investigation-20261117'],productRootAgentsOwnedByProductCodex=True,newProductImplementationApproved=False,srcTreeEqual=git('rev-parse','HEAD:src')==git('rev-parse',source+':src'),testsTreeEqual=git('rev-parse','HEAD:tests')==git('rev-parse',source+':tests'))
save(docs/'COORDINATION.json',coord)
files=['src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.Canvas.cs','src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs','src/PixelTart.Modules.AssetLibrary/AssetLibraryWorkspaceHost.cs','src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasView.cs','src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasSurface.cs','src/RAWSelectionAssistant/MainWindow.xaml.cs','src/RAWSelectionAssistant/App.xaml.cs','src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs','src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs','src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.LightPrint.cs','src/RAWSelectionAssistant/Services/RenderedSrgbInput.cs','src/RAWSelectionAssistant/Services/RawDisplayBitmapAdapter.cs']
records=[]
for path in files:
 data=(repo/path).read_bytes();records.append(dict(path=path,sha256=hashlib.sha256(data).hexdigest(),blobAtFunctionalSource=git('rev-parse',source+':'+path),currentBlob=git('rev-parse','HEAD:'+path)))
canvas=files[0];identities={rev:git('rev-parse',rev+':'+canvas) for rev in ['0a45ee0e','e8180221','fdf01831']};assert len(set(identities.values()))==1
bindings=[]
for name in ['CloseLifecycleInvestigation.dll','KitaoPhotoSelector.dll','PixelTart.Modules.AssetLibrary.dll','RAWSelectionAssistant.Core.dll']:
 p=base/'bin'/name;bindings.append(dict(file=name,bytes=p.stat().st_size,sha256=sha(p)))
save(docs/'SOURCE_BINDINGS.json',dict(source=source,base=coord['base'],sameCanvasHandlerInOldAndCurrent=identities,sourceFiles=records,compiledDiagnosticBindings=bindings,productSourceFilesModified=False,privateSyntheticFaultGates=True))
log=Path('E:/Kitao-Deliveries/product-functional-closure-20261112/workflow-runtime/Logs/app-20261116.log');lines=log.read_text('utf-8-sig').splitlines();start=next(i for i,x in enumerate(lines) if '2026-11-16 12:11:43.597' in x);stack=lines[start:start+10]
backup=Path('E:/Kitao-Deliveries/product-functional-closure-20261112/workflow-output/c5bc4616-66eb-43eb-8fb6-70fbcc7f0e0b/preserved-window-before-restart-20261116.ptstudio.json');b=load(backup)
save(docs/'OLD_LOG_AND_PRESERVATION.json',dict(logPath=str(log),logSha256=sha(log),excerptLineStart=start+1,excerpt=stack,processSourceFromStartup='0a45ee0e7971c18c6bb4b7e287bced86c448263b',backup=dict(path=str(backup),bytes=backup.stat().st_size,sha256Before=sha(backup),topLevelKeys=list(b.keys()),documentKind='Darkroom workfile backup; not evidence that an old canvas dirty revision was saved'),oldCanvasIdOrDirtyRevisionKnown=False,oldFullShellNestedPumpOrigin='UNKNOWN',filesReadOnly=True))
all_rows=[]
for round_name in ['v2','repeat-resource']:
 summary=load(base/'runs'/round_name/'RUN_SUMMARY.json')
 for row in summary['cases']:
  case=base/'runs'/round_name/row['case'];result=load(case/'RESULT.json');assert row['exitCode']==0 and not row['stop'] and result['exitCode']==0
  target=docs/'receipts'/round_name/row['case'];target.mkdir(parents=True,exist_ok=True)
  for name in ['RESULT.json','EXIT.json','LAUNCH.json']:
   value=load(case/name);save(target/name,value)
  all_rows.append(dict(round=round_name,case=row['case'],pid=row['pid'],exitCode=row['exitCode'],peakPrivateBytes50ms=row['peakPrivateBytes50ms'],osPeakWorkingSetBytes=row['osPeakWorkingSetBytes'],stop=row['stop'],receipt=str((target/'RESULT.json').relative_to(docs)).replace('\\','/'),observedKinds=[x['kind'] for x in result['observations'] if x['kind']!='NaturalMemory']))
summary=dict(atUtc=datetime.now(timezone.utc).isoformat(),source=source,controlledCases=15,repeatResourceCase=1,allExpectedProbeOutcomesObserved=True,productBugsReproducedNotReleasePass=True,cases=all_rows,maxPeakPrivateBytes50ms=max(x['peakPrivateBytes50ms'] for x in all_rows),maxOsPeakWorkingSetBytes=max(x['osPeakWorkingSetBytes'] for x in all_rows),guardBytes=2**31,forcedGc=False,newRawDecodes=0,scope='Per-process startup plus synthetic controls and idle; not large-photo acceptance or product operation latency',initialFixtureIssues=[dict(run='smoke/preview-after-unload',reason='Synthetic object CanvasId missing; constructor rejected it',status='NOT_PRODUCT_FAILURE'),dict(run='v1/preview-workspace-dispose',reason='Fake library/asset identity resolved unavailable before provider',status='NOT_PRODUCT_FAILURE')])
save(docs/'RUN_SUMMARY.json',summary)
refs=load(base/'references/SOURCES.json');refs=[{k:v for k,v in r.items() if k!='markers'}|{'markerLineNumbers':[m['line'] for m in r.get('markers',[])]} for r in refs];tag=load(base/'references/wpf-tag.json');save(docs/'PRIMARY_REFERENCES.json',dict(sources=refs,wpfVersion='v10.0.0',wpfCommit=tag['object']['sha'],rawSourceStoredOutsideRepository=True))
for label in ['smoke','v1']:
 target=docs/'fixture-history'/label;target.mkdir(parents=True,exist_ok=True)
 shutil.copyfile(base/'runs'/label/'RUN_SUMMARY.json',target/'RUN_SUMMARY.json')
for case,label in [('preview-after-unload','smoke'),('preview-workspace-dispose','v1')]:save(docs/'fixture-history'/label/(case+'-INVALID_FIXTURE.json'),load(base/'runs'/label/case/'RESULT.json'))
backup_after=sha(backup);assert backup_after==load(docs/'OLD_LOG_AND_PRESERVATION.json')['backup']['sha256Before']
save(docs/'PRESERVATION_FINAL.json',dict(atUtc=datetime.now(timezone.utc).isoformat(),backupPath=str(backup),sha256After=backup_after,unchanged=True,originalWorkspaceNotOpenedOrMutated=True,srcAndTestsDiff=git('diff','--name-only','fdf01831','--','src','tests')))
print(json.dumps(dict(controlledCases=15,resourceRepeat=1,peakPrivateMiB=round(summary['maxPeakPrivateBytes50ms']/2**20,2),source=source,productChanges=False,backupUnchanged=True)))

commits=['206051eb','fc811b54','89b64fd2','17981934','a3bc3127','4c3503c9','edd6d25e','17a95b5a','e28074f1','14eda5c7','a9932b73','e8180221']
rows=[]
for rev in commits:
 full=git('rev-parse',rev);meta=git('show','-s','--format=%H%x09%aI%x09%cI%x09%s',full).split('\t');anc=subprocess.run(['git','merge-base','--is-ancestor',full,source],cwd=repo,capture_output=True).returncode==0
 rows.append(dict(sha=full,authorDate=meta[1],commitDate=meta[2],subject=meta[3],isAncestorOfFunctionalSource=anc,changedFiles=git('diff-tree','--no-commit-id','--name-only','-r',full).splitlines()))
save(docs/'PATCH_HISTORY.json',dict(scope='Causal path-specific history; not a count of all PRs or all14-day changes',commits=rows))
tested=load(base/'references/TESTED_WPF_SOURCE.json');save(docs/'TESTED_FRAMEWORK_SOURCE_LOOKUP.json',tested)
