import pathlib,json,zipfile,hashlib,collections,argparse,csv,shutil
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);p.add_argument('--source',required=True);p.add_argument('--package',required=True);p.add_argument('--output',required=True);a=p.parse_args();d=pathlib.Path(a.delivery);pkg=pathlib.Path(a.package);reports=d/'reports';reports.mkdir(exist_ok=True);allruns=[];rows=[];tests=[]
for label in ['NEW_IMAGE_CORE','NEW_IMAGE_WPF','REVIEW_IMAGE_CORE','REVIEW_IMAGE_WPF','CANDIDATE_IMAGE_WPF','FINAL_CORE','FINAL_WPF','METADATA_EDGE_WPF','METADATA_EDGE_FRESH_WPF','SEALED_CORE','SEALED_WPF','KEEP_NEXT_REPRO','KEEP_NEXT_FIXED_PRE','KEEP_NEXT_FINAL_CORE','KEEP_NEXT_FINAL_WPF']:
 r=sorted((d/label).glob('run-*'))[-1];s=json.loads((r/'summary.json').read_text(encoding='utf-8-sig'));wanted=collections.Counter((v['id'],v['fullyQualifiedName'],v['displayName']) for u in s['executionPlan'] for v in u['cases']);observed=collections.Counter((v['id'],v['name'],v['display']) for u in s['runs'] for v in u['results']);c=collections.Counter(v['outcome'] for u in s['runs'] for v in u['results'])
 rows.append(dict(label=label,authority='final-release-bound' if label in ['KEEP_NEXT_FINAL_CORE','KEEP_NEXT_FINAL_WPF'] else 'retained-earlier-run-not-final-score',discovered=s['discovered'],selected=s['selected'],obtained=sum(observed.values()),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],missing=list((wanted-observed).elements()),extra=list((observed-wanted).elements()),assemblySha256=s['assemblySha256'],complete=s['status']!='RUNNING',root=str(r)));allruns.append((label,r,s))
 for u in s['runs']:
  for v in u['results']:tests.append(dict(suite=label,name=v['name'],display=v['display'],outcome=v['outcome'],status='PASS' if v['outcome']=='Passed' else 'NOT_RUN' if v['outcome']=='NotExecuted' else 'FAIL',evidence=u['directory'],error=v['error']))
if any(not x['complete'] or x['missing'] or x['extra'] for x in rows):raise RuntimeError('Unfinished or missing test results')
(reports/'TEST_SUMMARY.json').write_text(json.dumps(dict(sourceSha=a.source,scope='Affected planning/image-summary fixtures only; not full Core/WPF gates or physical acceptance',timezone='Europe/Berlin',rows=rows),ensure_ascii=False,indent=2),encoding='utf-8')
for name,values in [('ALL_RESULTS.csv',tests),('NONPASS.csv',[x for x in tests if x['status']!='PASS'])]:
 with (reports/name).open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=['suite','name','display','outcome','status','evidence','error']);w.writeheader();w.writerows(values)
manifest=[]
with zipfile.ZipFile(reports/'RAW_TESTS.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,r,s in allruns:
  for f in sorted(r.rglob('*')):
   if not f.is_file() or f.name not in ['summary.json','discovery.json','results.trx','stdout.log','stderr.log','runsettings.xml']:continue
   rel=f.relative_to(r)
   if len(rel.parts)>2:continue
   b=f.read_bytes();arc=label+'/'+rel.as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 for f in sorted(d.glob('*.log')):
  b=f.read_bytes();arc='commands/'+f.name;z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(reports/'RAW_TEST_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
for f in reports.iterdir():
 if f.is_file():(pkg/'reports').mkdir(exist_ok=True);shutil.copy2(f,pkg/'reports'/f.name)
archive=pathlib.Path(a.output).resolve();files=[]
if archive.exists():raise FileExistsError('Use a new package path, preserve old packages')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts:continue
  b=f.read_bytes();arc=pkg.name+'/'+rel.as_posix();z.writestr(arc,b);files.append(dict(path=arc,bytes=len(b),type=f.suffix,sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 for f in files:
  if hashlib.sha256(z.read(f['path'])).hexdigest()!=f['sha256']:raise RuntimeError('Zip readback mismatch')
result=dict(sourceSha=a.source,path=str(archive),bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),files=len(files),readbackVerified=True,UserDataIncluded=False,isSetup=False,diagnosticOnly=True)
(d/(archive.stem+'-identity.json')).write_text(json.dumps(result,indent=2),encoding='utf-8');(d/(archive.stem+'-files.json')).write_text(json.dumps(files,indent=2),encoding='utf-8');print(json.dumps(result))
