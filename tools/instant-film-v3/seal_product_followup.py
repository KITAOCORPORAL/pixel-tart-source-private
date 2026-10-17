"""Exact runner files only. No images, raw buffers, downloaded upstream code, DBs or binaries are swept."""
import pathlib,json,hashlib,zipfile,importlib.util,re
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/v3-product';E=R/'docs/research/instant-film-product-v3-20261017';out=E/'07_regression';paths=set();spec=importlib.util.spec_from_file_location('vr',R/'tools/planning-r7/verified_runner.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
for suite in ['07-source-core','07-source-wpf','07-published-core','07-published-wpf','07-candidate-final-verified']:
 p=next((A/suite).glob('run-*/summary.json'));s=json.loads(p.read_text());assert s['status']!='RUNNING';plan={u['unit']:u['cases'] for u in s['executionPlan']}
 assert hashlib.sha256(pathlib.Path(s['assembly']).read_bytes()).hexdigest()==s['assemblySha256']
 for name in ['summary.json','discovery.json','stdout.log','stderr.log']:paths.add(p.parent/name)
 for run in s['runs']:
  d=pathlib.Path(run['directory']);actual,errors=m.load_results(d/'results.trx',plan[run.get('executionUnit',run['class'])]);assert actual==run['results'];assert all(x=='non-passing or not-executed tests' for x in errors)
  for name in ['results.trx','runsettings.xml','stdout.log','stderr.log']:paths.add(d/name)
for folder in ['07-dpi-baseline','07-dpi-current','07-dpi-producer-immediate','03-ownership-baseline','03-ownership-expanded-fixed','04-coordinates','07-candidate-final']:paths.update((A/folder).glob('*.trx'))
# Do not include this seal's own log (created later).
paths.update(p for p in A.glob('*.log') if p.name!='07-seal.log');archive=out/'RAW_TEST_RESULTS.zip';assert not archive.exists();manifest=[]
with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(paths):
  assert p.resolve().is_relative_to(R) and not p.is_symlink();assert p.suffix.lower() in ['.trx','.xml','.json','.log'];relative=p.relative_to(R).as_posix();data=p.read_bytes()
  assert not any(re.search(pattern,data,re.I) for pattern in [rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',rb'gh[pousr]_[A-Za-z0-9_]{30,}',rb'github_pat_[A-Za-z0-9_]{30,}',rb'Authorization\s*:\s*Bearer\s+\S+'])
  manifest.append(dict(path=relative,bytes=len(data),sha256=hashlib.sha256(data).hexdigest().upper()));z.writestr(relative,data)
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 for row in manifest:assert hashlib.sha256(z.read(row['path'])).hexdigest().upper()==row['sha256']
(out/'RAW_TEST_RESULTS_MANIFEST.json').write_text(json.dumps(manifest,indent=2)+'\n');(out/'SEAL.json').write_text(json.dumps(dict(status='EXACT_TRX_AND_ARCHIVE_VERIFIED_NOT_PRODUCT_APPROVAL',members=len(manifest),bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest().upper(),noImagesDatabasesNativeOrCredentials=True),indent=2)+'\n');print('sealed',len(manifest),archive.stat().st_size)
