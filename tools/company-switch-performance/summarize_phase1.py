import pathlib,json,collections,zipfile,hashlib,argparse
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);a=p.parse_args();d=pathlib.Path(a.delivery);out=d/'phase1-report';out.mkdir(exist_ok=True)
labels=['PHASE1_PLANNING_CORE','PHASE1_PLANNING_WPF','PHASE1_FULL_CORE','PHASE1_MODULES']+['PHASE1_WPF_'+str(i) for i in range(4)];rows=[];allruns=[];shards=[]
for label in labels:
 r=sorted((d/label).glob('run-*'))[-1];s=json.loads((r/'summary.json').read_text(encoding='utf-8-sig'));v=[x for run in s['runs'] for x in run['results']];c=collections.Counter(x['outcome'] for x in v)
 rows.append(dict(label=label,discovered=s['discovered'],selected=s['selected'],obtained=len(v),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],missing=s['selected']-len(v),complete=s['status']!='RUNNING',root=str(r)))
 allruns.append((label,r,s));
 if label.startswith('PHASE1_WPF_'):shards.append((r,s))
expected=json.loads((shards[0][0]/'discovery.json').read_text(encoding='utf-8-sig'));key=lambda x:(x['id'],x['fullyQualifiedName'],x['displayName']);wanted=collections.Counter(key(x) for x in expected);planned=collections.Counter(key(x) for r,s in shards for u in s['executionPlan'] for x in u['cases']);observed=collections.Counter((v['id'],v['name'],v['display']) for r,s in shards for run in s['runs'] for v in run['results']);c=collections.Counter(v['outcome'] for r,s in shards for run in s['runs'] for v in run['results']);aggregate=dict(discovered=len(expected),obtained=sum(observed.values()),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],missing=list((wanted-observed).elements()),extra=list((observed-wanted).elements()),plannedExactlyMatches=planned==wanted,complete=all(s['status']!='RUNNING' for r,s in shards))
(out/'TEST_SUMMARY.json').write_text(json.dumps(dict(sourceSha='99d7818e035310c9273565e73752ee0359ebb8f1',timezone='Europe/Berlin',suites=rows,wpf=aggregate),ensure_ascii=False,indent=2),encoding='utf-8')
if not all(r['complete'] for r in rows):print(json.dumps(aggregate));raise SystemExit(2)
manifest=[]
with zipfile.ZipFile(out/'RAW_COMPANY_TESTS.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,r,s in allruns:
  for f in r.rglob('*'):
   if not f.is_file() or f.name not in ['summary.json','discovery.json','results.trx','stdout.log','stderr.log','runsettings.xml']:continue
   rel=f.relative_to(r)
   if len(rel.parts)>2:continue
   b=f.read_bytes();arc=label+'/'+rel.as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(out/'RAW_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');print(json.dumps(aggregate))
