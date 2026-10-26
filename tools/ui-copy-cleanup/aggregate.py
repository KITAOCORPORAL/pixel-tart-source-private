import pathlib,json,collections,csv,zipfile,hashlib
r=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026");d=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");out=d/'reports';out.mkdir(exist_ok=True);rows=[];summaries=[];runs=[]
for label in ['CANDIDATE_SPECIAL','CANDIDATE2_SPECIAL','CANDIDATE3_SPECIAL','FINAL_CORE','FINAL_WPF','SEALED_CORE','SEALED_WPF']:
 folders=sorted((d/label).glob('run-*'))
 if not folders:continue
 p=folders[-1];s=json.loads((p/'summary.json').read_text(encoding='utf-8-sig'));assert s['status']!='RUNNING',label
 wanted=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for u in s['executionPlan'] for x in u['cases']);actual=collections.Counter((x['id'],x['name'],x['display']) for u in s['runs'] for x in u['results']);assert wanted==actual,label;c=collections.Counter(x['outcome'] for u in s['runs'] for x in u['results']);summaries.append(dict(label=label,discovered=s['discovered'],selected=s['selected'],obtained=sum(actual.values()),passed=c['Passed'],failed=c['Failed'],notRun=c['NotExecuted'],missing=0,extra=0,timeouts=sum(u['timeout'] for u in s['runs']),assemblySha256=s['assemblySha256'],authority='final-app-bound' if label.startswith('SEALED_') else 'retained-earlier-run',root=str(p)));runs.append((label,p))
 for u in s['runs']:
  for x in u['results']:rows.append(dict(suite=label,name=x['name'],outcome=x['outcome'],status='PASS' if x['outcome']=='Passed' else 'NOT_RUN' if x['outcome']=='NotExecuted' else 'FAIL',error=x['error'],evidence=u['directory']))
(out/'TEST_SUMMARY.json').write_text(json.dumps(dict(productSha='b9cb1e25e811903b117aef3afeb203aadf7d0790',testRevisionSha='0505216f8aacbcc67ea10f7584a8469767afe923',scope='Affected UI/planning/canvas/navigation/layout only, not full software release acceptance',suites=summaries),ensure_ascii=False,indent=2),encoding='utf-8')
for name,values in [('NONPASS.csv',[x for x in rows if x['status']!='PASS'])]:
 with (out/name).open('w',encoding='utf-8-sig',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(values)
manifest=[]
with zipfile.ZipFile(out/'RAW_TESTS.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,p in runs:
  for f in sorted(p.rglob('*')):
   if not f.is_file() or len(f.relative_to(p).parts)>2 or f.name not in ['summary.json','discovery.json','runsettings.xml','stdout.log','stderr.log','results.trx']:continue
   b=f.read_bytes();arc=label+'/'+f.relative_to(p).as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 for group in ['canvas-before','canvas-before2','canvas-before3','canvas-after1','canvas-after2']:
  for f in sorted((d/group).glob('*')):
   if not f.is_file() or f.suffix not in ['.json','.trx','.log']:continue
   b=f.read_bytes();arc=group+'/'+f.name;z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 for f in sorted(d.glob('*.log')):
  b=f.read_bytes();arc='commands/'+f.name;z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(out/'RAW_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');print(json.dumps(summaries[-2:]))
