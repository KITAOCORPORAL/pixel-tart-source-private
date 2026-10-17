"""Seal only exact retained test outputs. Never include photos, raw buffers, databases, binaries or private directory listings."""
import pathlib,json,hashlib,zipfile,importlib.util,collections,shutil,re,xml.etree.ElementTree as ET,subprocess
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/v3-live';E=R/'docs/research/instant-v3-live-20261017';O=E/'06_regression';paths=set();rows=[]
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
spec=importlib.util.spec_from_file_location('retained_runner',R/'tools/planning-r7/verified_runner.py');vr=importlib.util.module_from_spec(spec);spec.loader.exec_module(vr)
suites=['06-source-core','06-source-frozen-wpf','06-published-core','06-published-wpf','06-delivery-full-wpf','06-delivery-core','06-delivery-candidate','06-candidate-final','06-delivery-specialized']
for suite in suites:
 p=next((A/suite).glob('run-*/summary.json'));s=load(p)
 if s['status']=='RUNNING':raise RuntimeError('Unfinished suite '+suite)
 expected=collections.Counter(x['fullyQualifiedName'] for u in s['executionPlan'] for x in u['cases']);actual=collections.Counter(x['name'] for run in s['runs'] for x in run['results']);assert expected==actual,suite
 assert sha(pathlib.Path(s['assembly']))==s['assemblySha256'].upper(),suite
 plan={u['unit']:u['cases'] for u in s['executionPlan']}
 for run in s['runs']:
  d=pathlib.Path(run['directory']);result,errors=vr.load_results(d/'results.trx',plan[run.get('executionUnit',run['class'])]);assert result==run['results'];assert all(x=='non-passing or not-executed tests' for x in errors)
  for name in ['results.trx','runsettings.xml','stdout.log','stderr.log']:paths.add(d/name)
 for name in ['summary.json','discovery.json','stdout.log','stderr.log']:paths.add(p.parent/name)
 shutil.copyfile(p,O/(suite+'-summary.json'));shutil.copyfile(p.parent/'discovery.json',O/(suite+'-discovery.json'))
 counts=collections.Counter(x['outcome'] for run in s['runs'] for x in run['results']);rows.append(dict(suite=suite,discovered=s['discovered'],selected=s['selected'],executed=sum(v for k,v in counts.items() if k!='NotExecuted'),counts=dict(counts),missing=[],timeouts=[r['class'] for r in s['runs'] if r.get('timeout')],rawStatus=s['status']))
# All first failed attempts retained as exact runner-owned raw records, never runtime trees.
for suite in ['06-source-wpf','06-final-source-wpf']:
 p=next((A/suite).glob('run-*/summary.json'));s=load(p);assert s['status']!='RUNNING'
 for name in ['summary.json','discovery.json','stdout.log','stderr.log']:paths.add(p.parent/name)
 for run in s['runs']:
  for name in ['results.trx','runsettings.xml','stdout.log','stderr.log']:paths.add(pathlib.Path(run['directory'])/name)
paths.update(p for p in A.glob('*.log') if p.name!='06-seal-live.log')
for folder in ['02-product-tests','03-product-controls','03-legacy-tests','04-component-versions','04-lifecycle-stress','04-locale-product','04-pressure','04-comparison-geometry','06-last-patch','06-dpi']:
 paths.update((A/folder).glob('*.trx'))
# Logical producer text metadata only. No synthetic images required in Git either.
manifest=load(R/'artifacts/current-run-visual/current-run-visual-evidence.json');logical=[]
for entry in manifest['captures']:
 image=pathlib.Path(entry['path']);metadata=pathlib.Path(entry['metadata_path']);assert sha(image)==entry['sha256'];d=load(metadata);assert d['sourceCommit']==manifest['source_commit'];logical.append(dict(group=entry['group'],state=entry['state'],imageSha256=sha(image),metadataSha256=sha(metadata),source=d['sourceCommit'],physical=d['physicalDpiManuallyTested']))
(O/'LOGICAL_IDENTITIES.json').write_text(json.dumps(dict(source=manifest['source_commit'],exe=manifest['executable_sha256'],captures=logical,physicalDpi='NOT_RUN'),indent=2)+'\n')
# Final actual delivery identities, distinct from previous full tested payloads.
release=A/'06-delivery/win-x64';files=load(O/'DELIVERY_FILES.json')
for row in files:assert sha(release/row['path'])==row['sha256']
assert len(files)==len([p for p in release.rglob('*') if p.is_file()])==290
identity=dict(status='BLOCKED_DIAGNOSTIC_BUILD_WITH_PRODUCT_V3',source=(A/'06-delivery-source.txt').read_text(encoding='utf-8-sig').strip(),base='a51bcf96efc0e3d03438138dfa525ab61c7449b7',directory=str(release),exeSha256=sha(release/'KitaoPhotoSelector.exe'),coreSha256=sha(release/'RAWSelectionAssistant.Core.dll'),toolCandidateDllBundled=False,implementation='Formal Core InstantV3',actualDeliveryFullSuites=['06-delivery-full-wpf','06-delivery-core'],physicalDesktop='NOT_RUN_ACTIVE_INPUT',releaseBlockers=['Inherited Fuji native nondeterminism','P3 historical long-tail failures retained; one green run not root-cause closure','No physical DPI/native foreground acceptance','Extension visual/aliasing extreme matrix incomplete; multiple-exposure second-layer UI not implemented'])
(O/'IDENTITY.json').write_text(json.dumps(identity,indent=2)+'\n')
archive=O/'RAW_TEST_RESULTS.zip';assert not archive.exists();members=[]
with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(paths):
  relative=p.relative_to(R).as_posix();assert p.resolve().is_relative_to(R) and not p.is_symlink();assert p.suffix.lower() in ['.trx','.json','.xml','.log'];data=p.read_bytes()
  if any(re.search(x,data,re.I) for x in [rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',rb'gh[pousr]_[A-Za-z0-9_]{30,}',rb'github_pat_[A-Za-z0-9_]{30,}',rb'Authorization\s*:\s*Bearer\s+\S+']):raise RuntimeError('Sensitive pattern in '+relative)
  members.append(dict(path=relative,bytes=len(data),sha256=hashlib.sha256(data).hexdigest().upper()));z.writestr(relative,data)
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 for row in members:assert hashlib.sha256(z.read(row['path'])).hexdigest().upper()==row['sha256']
(O/'RAW_TEST_RESULTS_MANIFEST.json').write_text(json.dumps(members,indent=2)+'\n')
(E/'TEST_COUNTS.json').write_text(json.dumps(rows,indent=2)+'\n');(O/'SEAL.json').write_text(json.dumps(dict(status='EXACT_TRX_RELEASE_AND_ARCHIVE_READBACK_VERIFIED',archiveSha256=sha(archive),bytes=archive.stat().st_size,members=len(members),scopedPrivacyPatterns='no findings, not a global guarantee',noPhotosDatabasesCredentialsBinaries=True),indent=2)+'\n');print(json.dumps(rows,indent=2));print('archive',len(members),sha(archive))
