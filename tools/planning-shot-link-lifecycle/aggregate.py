import pathlib,json,collections,hashlib,zipfile,csv,subprocess,shutil
r=pathlib.Path("E:/Kitao-Worktrees/planning-shot-link-lifecycle-company-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-shot-link-lifecycle-company-20261023");sha="3cd7543050722b2449a75ec7115e70aa29dcff6b";reports=d/'reports';reports.mkdir(exist_ok=True);summaries=[];rows=[];runs=[]
for label in ['NEW_CORE','NEW_WPF','FIXED_CORE','FIXED_WPF','FINAL_CORE','FINAL_WPF','REGRESSION_CORE','REGRESSION2_CORE','REGRESSION2_WPF','SEALED_CORE','SEALED_WPF','VERIFIED_CORE','VERIFIED_WPF']:
 folders=sorted((d/label).glob('run-*'))
 if not folders:continue
 folder=folders[-1];s=json.loads((folder/'summary.json').read_text(encoding='utf-8-sig'))
 if s['status']=='RUNNING':raise RuntimeError('Unfinished '+label)
 wanted=collections.Counter((c['id'],c['fullyQualifiedName'],c['displayName']) for u in s['executionPlan'] for c in u['cases']);actual=collections.Counter((v['id'],v['name'],v['display']) for u in s['runs'] for v in u['results']);c=collections.Counter(v['outcome'] for u in s['runs'] for v in u['results'])
 assert wanted==actual,label
 summaries.append(dict(label=label,discovered=s['discovered'],selected=s['selected'],obtained=sum(actual.values()),passed=c['Passed'],failed=c['Failed'],notRun=c['NotExecuted'],missing=0,extra=0,timeouts=sum(u['timeout'] for u in s['runs']),assemblySha256=s['assemblySha256'],authority='final-published-components' if label.startswith('VERIFIED_') else 'retained-development-run',evidence=str(folder)))
 runs.append((label,folder,s))
 for u in s['runs']:
  for v in u['results']:rows.append(dict(suite=label,name=v['name'],display=v['display'],outcome=v['outcome'],status='PASS' if v['outcome']=='Passed' else 'NOT_RUN' if v['outcome']=='NotExecuted' else 'FAIL',error=v['error'],evidence=u['directory']))
(reports/'TEST_SUMMARY.json').write_text(json.dumps(dict(sourceSha=sha,scope='Affected planning/storage/DOCX only, not full Core/WPF or application release gate',suites=summaries),ensure_ascii=False,indent=2),encoding='utf-8')
for name,values in [('ALL_RESULTS.csv',rows),('NONPASS.csv',[x for x in rows if x['status']!='PASS'])]:
 with (reports/name).open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(values)
manifest=[]
with zipfile.ZipFile(reports/'RAW_TESTS.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,folder,s in runs:
  for f in sorted(folder.rglob('*')):
   if not f.is_file() or len(f.relative_to(folder).parts)>2 or f.name not in ['summary.json','discovery.json','runsettings.xml','stdout.log','stderr.log','results.trx']:continue
   b=f.read_bytes();arc=label+'/'+f.relative_to(folder).as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 for f in sorted(d.glob('*.log')):
  b=f.read_bytes();arc='commands/'+f.name;z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(reports/'RAW_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(summaries[-2:]))
