import pathlib,json,collections,zipfile,hashlib,argparse,csv
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);p.add_argument('--source',required=True);a=p.parse_args();d=pathlib.Path(a.delivery);out=d/'final-report';out.mkdir(exist_ok=True)
labels=['FIXED_FULL_CORE','FIXED_MODULES','FIX_CORRECTNESS_PRE','FIX_SORT_FILTER_CORRECTNESS','FIXED_AUTHORIZED_SUPPLEMENT']+['FIXED_WPF_'+str(i) for i in range(4)]
runs=[];rows=[]
for label in labels:
 r=sorted((d/label).glob('run-*'))[-1];s=json.loads((r/'summary.json').read_text(encoding='utf-8-sig'));v=[v for x in s['runs'] for v in x['results']];c=collections.Counter(v['outcome'] for v in v);row=dict(label=label,discovered=s['discovered'],selected=s['selected'],obtained=len(v),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],missing=s['selected']-len(v),complete=s['status']!='RUNNING',root=str(r));rows.append(row);runs.append((label,r,s))
shards=[(r,s) for l,r,s in runs if l.startswith('FIXED_WPF_')];discovery=json.loads((shards[0][0]/'discovery.json').read_text(encoding='utf-8-sig'));expected=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for x in discovery);planned=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for r,s in shards for u in s['executionPlan'] for x in u['cases']);observed=collections.Counter((v['id'],v['name'],v['display']) for r,s in shards for x in s['runs'] for v in x['results']);counts=collections.Counter(v['outcome'] for r,s in shards for x in s['runs'] for v in x['results']);agg=dict(discovered=len(discovery),obtained=sum(observed.values()),passed=counts['Passed'],failed=counts['Failed'],skipped=counts['NotExecuted'],missing=list((expected-observed).elements()),extra=list((observed-expected).elements()),plansExactlyMatchDiscovery=planned==expected,complete=all(s['status']!='RUNNING' for r,s in shards));result=dict(sourceSha=a.source,timezone='Europe/Berlin',rows=rows,wpf=agg)
(out/'TEST_SUMMARY.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');tests=[]
for label,r,s in runs:
 for unit in s['runs']:
  for t in unit['results']:tests.append(dict(suite=label,name=t['name'],display=t['display'],outcome=t['outcome'],evidence=unit['directory'],error=t['error']))
for name,values in [('ALL_RESULTS.csv',tests),('NONPASS.csv',[t for t in tests if t['outcome']!='Passed'])]:
 with (out/name).open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=['suite','name','display','outcome','evidence','error']);w.writeheader();w.writerows(values)
if not all(x['complete'] for x in rows):print(json.dumps(agg));raise SystemExit(2)
manifest=[]
with zipfile.ZipFile(out/'RAW_FINAL_TESTS.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,r,s in runs:
  for f in r.rglob('*'):
   if not f.is_file() or f.name not in ['summary.json','discovery.json','results.trx','stdout.log','stderr.log','runsettings.xml']:continue
   rel=f.relative_to(r)
   if len(rel.parts)>2:continue
   b=f.read_bytes();arc=label+'/'+rel.as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(out/'RAW_FINAL_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');print(json.dumps(agg))
