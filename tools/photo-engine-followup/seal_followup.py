"""Final static-photo handoff: no photo/private runtime sweep, exact retained runner evidence only."""
import collections,hashlib,importlib.util,json,pathlib,shutil,subprocess,sys,zipfile
ROOT=pathlib.Path(__file__).resolve().parents[2];A=ROOT/'artifacts/followup';E=ROOT/'docs/evidence/photo-engine-followup-20261016';R=E/'06_regression'
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
SUITES=['06-source-core','06-source-wpf','06-published-core','06-published-wpf','06-published-identity']
summary_paths=[];files=set();rows=[]
spec=importlib.util.spec_from_file_location('retained_runner',ROOT/'tools/planning-r7/verified_runner.py');runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
for suite in SUITES:
 paths=list((A/suite).glob('run-*/summary.json'))
 if len(paths)!=1:raise RuntimeError('Exact completed run required: '+suite)
 path=paths[0];s=load(path);discovery=load(path.parent/'discovery.json')
 if s['status']=='RUNNING':raise RuntimeError('Incomplete suite '+suite)
 wanted=collections.Counter(c['fullyQualifiedName'] for u in s['executionPlan'] for c in u['cases']);actual=collections.Counter(x['name'] for r in s['runs'] for x in r['results']);missing=wanted-actual;extra=actual-wanted
 if missing or extra:raise RuntimeError('Discovery mismatch '+suite)
 if len(discovery)!=s['discovered'] or sum(wanted.values())!=s['selected']:raise RuntimeError('Counts mismatch '+suite)
 if sha(pathlib.Path(s['assembly']))!=s['assemblySha256'].upper():raise RuntimeError('Test assembly changed '+suite)
 plan={u['unit']:u['cases'] for u in s['executionPlan']}
 for run in s['runs']:
  directory=pathlib.Path(run['directory']);observed,errors=runner.load_results(directory/'results.trx',plan[run.get('executionUnit',run['class'])])
  if observed!=run['results'] or any(x!='non-passing or not-executed tests' for x in errors):raise RuntimeError('TRX mismatch '+suite)
  for name in ['results.trx','runsettings.xml','stdout.log','stderr.log']:files.add(directory/name)
 for name in ['summary.json','discovery.json','stdout.log','stderr.log']:files.add(path.parent/name)
 shutil.copyfile(path,R/(suite+'-summary.json'));shutil.copyfile(path.parent/'discovery.json',R/(suite+'-discovery.json'));summary_paths.append(path)
 count=collections.Counter(x['outcome'] for run in s['runs'] for x in run['results'])
 rows.append(dict(suite=suite,discovered=s['discovered'],selected=s['selected'],results=sum(count.values()),executed=sum(v for k,v in count.items() if k!='NotExecuted'),counts=dict(count),missing=[],extra=[],timeouts=[r['class'] for r in s['runs'] if r.get('timeout')],rawStatus=s['status'],rawTrxReadback=True))
# Retain first full failure and focused results, not runtime DB/images.
for suite in ['01-baseline-full-core','01-instrumented-full-core','01-index-full-core']:
 path=next((A/suite).glob('run-*/summary.json'));s=load(path)
 for name in ['summary.json','discovery.json','stdout.log','stderr.log']:files.add(path.parent/name)
 for run in s['runs']:
  for name in ['results.trx','runsettings.xml','stdout.log','stderr.log']:files.add(pathlib.Path(run['directory'])/name)
for path in A.glob('*.log'):files.add(path)
for folder in ['01-baseline','01-index-focused','04-contract-tests','05-contract-tests']:
 files.update((A/folder).glob('*.trx'))
manifest=[];archive=R/'RAW_TEST_RESULTS.zip'
if archive.exists():raise RuntimeError('Never overwrite sealed raw evidence')
with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for path in sorted(files):
  relative=path.relative_to(ROOT);resolved=path.resolve(strict=True)
  if not resolved.is_relative_to(ROOT) or path.is_symlink() or any(part in ['runtime','temp','candidate-local'] for part in relative.parts):raise RuntimeError('Unsafe archive path')
  if path.suffix.lower() not in ['.trx','.json','.xml','.log']:raise RuntimeError('Unexpected archive file')
  manifest.append(dict(path=relative.as_posix(),bytes=path.stat().st_size,sha256=sha(path)));z.write(path,relative.as_posix())
with zipfile.ZipFile(archive) as z:
 if z.testzip() is not None:raise RuntimeError('Archive CRC')
 for row in manifest:
  data=z.read(row['path'])
  if hashlib.sha256(data).hexdigest().upper()!=row['sha256']:raise RuntimeError('Archive member hash')
(R/'RAW_TEST_RESULTS_MANIFEST.json').write_text(json.dumps(manifest,indent=2)+'\n')
# Strict test classification: new methods required, inherited conditional names unchanged.
cmd=[sys.executable,str(ROOT/'tools/planning-r7/review_gate.py'),'--summary',*[str(p) for p in summary_paths],'--policy',str(R/'review-classification-followup.json'),'--output',str(R/'STRICT_POSTPUBLISH.json'),'--postpublish']
p=subprocess.run(cmd,capture_output=True,text=True);print(p.stdout,p.stderr)
strict=load(R/'STRICT_POSTPUBLISH.json');fuji=load(E/'02_fuji/02-fuji-default-difference.json');blockers=['Fuji legacy default diverges first after demosaic; no safe source-level fix/version admission','Display preview not connected: independent near-black oracle failed and WIC disagreement; physical compositor/cross-screen NOT_RUN','Candidate source rebuild/license election/provision and clean Windows load NOT_RUN; no ordinary Release inclusion','Foreground desktop/navigation/switch cancellation/physical DPI NOT_RUN','Full ACES/OpenDRT/darktable equivalent comparison and photographer blind review NOT_RUN']
if strict['errors']:blockers.insert(0,{'strictAutomaticRegression':strict['errors']})
release=A/'06-blocked-diagnostic/win-x64';release_files=load(A/'06-release-files.json')
for row in release_files:
 path=release/row['path']
 if sha(path)!=row['sha256'] or path.stat().st_size!=row['bytes']:raise RuntimeError('Release file drift')
if {p.relative_to(release).as_posix() for p in release.rglob('*') if p.is_file()}!={x['path'] for x in release_files}:raise RuntimeError('Release file set drift')
identity=dict(status='BLOCKED_DIAGNOSTIC_BUILD',sourceCommit=(A/'06-release-source.txt').read_text(encoding='utf-8-sig').strip(),baseCommit='fe73d02cf85df518c4eaebf4a09f9a5157451eab',branch='feature/kitao-photo-engine-followup-20261016',directory=str(release),files=len(release_files),exeSha256=sha(release/'KitaoPhotoSelector.exe'),coreSha256=sha(release/'RAWSelectionAssistant.Core.dll'),productExeLaunched=False,candidate0222Bundled=False,displayTransformEnabled=False,blockers=blockers)
(R/'IDENTITY.json').write_text(json.dumps(identity,ensure_ascii=False,indent=2)+'\n');shutil.copyfile(A/'06-release-files.json',R/'RELEASE_FILES.json');shutil.copyfile(A/'06-published-provenance.json',R/'PUBLISHED_PROVENANCE.json');shutil.copyfile(A/'06-published-audit-provenance.json',R/'PUBLISHED_AUDIT_PROVENANCE.json')
sonyp=load(A/'06-sony-published-chain/SONY_DSC08015_CHAIN.json');shutil.copyfile(A/'06-sony-published-chain/SONY_DSC08015_CHAIN.json',E/'03_sony/SONY_PUBLISHED_CHAIN.json')
if sonyp['status']!='PASSED_PRODUCT_BACKEND_CHAIN_NOT_DESKTOP':blockers.append('Actual released Sony backend chain failed')
(E/'RESULTS.json').write_text(json.dumps(dict(status='BLOCKED_DIAGNOSTIC_BUILD',tests=rows,release=identity,rawArchive=dict(path=archive.name,bytes=archive.stat().st_size,sha256=sha(archive),members=len(manifest)),strictTestGate=strict['status'],fujiFirstDifference=fuji['earliestObservedDifferentStage'],sonyReleasedChain=sonyp['status'],noPhotosDatabasesCredentialsBinariesCommitted=True),ensure_ascii=False,indent=2)+'\n')
print(json.dumps(rows,indent=2));print('Release',identity['exeSha256'],'archive',len(manifest),sha(archive))
