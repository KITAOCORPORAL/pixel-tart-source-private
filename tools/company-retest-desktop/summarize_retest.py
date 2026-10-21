import pathlib,json,collections,csv,zipfile,hashlib,argparse
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);p.add_argument('--source',required=True);a=p.parse_args();d=pathlib.Path(a.delivery);out=d/'reports';out.mkdir(exist_ok=True)
labels=['FULL_CORE','PUBLISHED_MODULES']+['FULL_WPF_'+str(i) for i in range(4)];runs=[];rows=[];tests=[]
for label in labels:
 r=sorted((d/label).glob('run-*'))[-1];s=json.loads((r/'summary.json').read_text(encoding='utf-8-sig'));v=[v for run in s['runs'] for v in run['results']];c=collections.Counter(v['outcome'] for v in v)
 expected=json.loads((r/'discovery.json').read_text(encoding='utf-8-sig'));expectedCounter=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for x in expected);selectedCounter=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for u in s['executionPlan'] for x in u['cases']);actualCounter=collections.Counter((x['id'],x['name'],x['display']) for x in v)
 rows.append(dict(suite=label,exactMissing=list((selectedCounter-actualCounter).elements()),exactExtra=list((actualCounter-selectedCounter).elements()),planSubsetOfDiscovery=not bool(selectedCounter-expectedCounter),discovered=s['discovered'],selected=s['selected'],obtained=len(v),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],missing=s['selected']-len(v),complete=s['status']!='RUNNING',root=str(r)));runs.append((label,r,s))
 for run in s['runs']:
  for v in run['results']:tests.append(dict(suite=label,name=v['name'],display=v['display'],outcome=v['outcome'],status='PASS' if v['outcome']=='Passed' else 'NOT_RUN' if v['outcome']=='NotExecuted' else 'FAIL',evidence=run['directory'],error=v['error']))
shards=[(r,s) for label,r,s in runs if label.startswith('FULL_WPF_')];discovery=json.loads((shards[0][0]/'discovery.json').read_text(encoding='utf-8-sig'));wanted=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for x in discovery);planned=collections.Counter((v['id'],v['fullyQualifiedName'],v['displayName']) for r,s in shards for u in s['executionPlan'] for v in u['cases']);observed=collections.Counter((v['id'],v['name'],v['display']) for r,s in shards for run in s['runs'] for v in run['results']);counts=collections.Counter(v['outcome'] for r,s in shards for run in s['runs'] for v in run['results']);aggregate=dict(discovered=len(discovery),obtained=sum(observed.values()),passed=counts['Passed'],failed=counts['Failed'],skipped=counts['NotExecuted'],missing=list((wanted-observed).elements()),extra=list((observed-wanted).elements()),plansExactlyMatchDiscovery=wanted==planned,sameAssembly=len(set(s['assemblySha256'] for r,s in shards))==1,complete=all(s['status']!='RUNNING' for r,s in shards))
for name,records in [('ALL_RESULTS.csv',tests),('NONPASS.csv',[x for x in tests if x['status']!='PASS'])]:
 with (out/name).open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=['suite','name','display','outcome','status','evidence','error']);w.writeheader();w.writerows(records)
jsonout=dict(sourceSha=a.source,timezone='Europe/Berlin',rows=rows,wpf=aggregate,priorSupplementIncluded=False);(out/'TEST_SUMMARY.json').write_text(json.dumps(jsonout,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(rows=rows,wpf={k:v for k,v in aggregate.items() if k not in ['missing','extra']},missing=len(aggregate['missing']),extra=len(aggregate['extra']))))
if not all(row['complete'] for row in rows):raise SystemExit(2)
manifest=[]
with zipfile.ZipFile(out/'RAW_RETEST_TRX.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,r,s in runs:
  for f in sorted(r.rglob('*')):
   if not f.is_file() or f.name not in ['summary.json','discovery.json','results.trx','stdout.log','stderr.log','runsettings.xml']:continue
   rel=f.relative_to(r)
   if len(rel.parts)>2:continue
   b=f.read_bytes();path=label+'/'+rel.as_posix();z.writestr(path,b);manifest.append(dict(path=path,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(out/'RAW_RETEST_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
