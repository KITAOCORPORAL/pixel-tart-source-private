"""Aggregate current adapter runs, preserving first failures and exact identities.
Not a GUI test; module regex mappings are related fixture coverage only.
"""
import argparse,pathlib,json,collections,csv,re,hashlib,zipfile,shutil
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--delivery',required=True);p.add_argument('--source',required=True);a=p.parse_args();root=pathlib.Path(a.root);d=pathlib.Path(a.delivery);sha=a.source;out=d/'reports';out.mkdir(exist_ok=True)
def dump(name,v): (out/name).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def csvwrite(name,rows):
 if not rows:return
 with (out/name).open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
def run(label):
 dirs=sorted((d/label).glob('run-*'))
 if not dirs:raise RuntimeError('No current run: '+label)
 r=dirs[-1];s=json.loads((r/'summary.json').read_text(encoding='utf-8-sig'))
 if s['status']=='RUNNING':raise RuntimeError('Unfinished run: '+label)
 return label,r,s
labels=['FULL_CORE','FULL_CORE_CORRECTED','PUBLISHED_MODULES','FULL_DOCUMENTS','FULL_RESEARCH','FULL_DPI','PLANNING_SAMPLE_RECHECK','PLANNING_SAMPLE_NATIVE_RECHECK']+['FULL_WPF_'+str(i) for i in range(4)]
runs=[run(x) for x in labels];summary=[];allrows=[]
for label,r,s in runs:
 vals=[v for u in s['runs'] for v in u['results']];wanted=collections.Counter((v['id'],v['fullyQualifiedName'],v['displayName']) for u in s['executionPlan'] for v in u['cases']);observed=collections.Counter((v['id'],v['name'],v['display']) for v in vals);counts=collections.Counter(v['outcome'] for v in vals)
 summary.append(dict(suite=label,discovered=s['discovered'],selected=s['selected'],obtained=len(vals),passed=counts['Passed'],failed=counts['Failed'],skipped=counts['NotExecuted'],missing=list((wanted-observed).elements()),extra=list((observed-wanted).elements()),timeout=sum(u.get('timeout',False) for u in s['runs']),status='FAIL' if counts['Failed'] or wanted!=observed else 'PARTIAL' if counts['NotExecuted'] else 'PASS',assemblySha256=s['assemblySha256'],evidence=str(r),authority='retained-first-run' if label=='FULL_CORE' else 'research-only' if label=='FULL_RESEARCH' else 'independent-prototype' if label=='FULL_DOCUMENTS' else 'logical-DPI-contract' if label=='FULL_DPI' else 'current-published-component-bound'))
 for u in s['runs']:
  for v in u['results']:allrows.append(dict(Suite=label,Class=u['class'],Name=v['name'],Display=v['display'],Outcome=v['outcome'],Status='PASS' if v['outcome']=='Passed' else 'NOT_RUN' if v['outcome']=='NotExecuted' else 'FAIL',Evidence=u['directory'],Error=v['error']))
shards=[(r,s) for label,r,s in runs if label.startswith('FULL_WPF_')];discovered=json.loads((shards[0][0]/'discovery.json').read_text(encoding='utf-8-sig'));wanted=collections.Counter((v['id'],v['fullyQualifiedName'],v['displayName']) for v in discovered);planned=collections.Counter((v['id'],v['fullyQualifiedName'],v['displayName']) for r,s in shards for u in s['executionPlan'] for v in u['cases']);observed=collections.Counter((v['id'],v['name'],v['display']) for r,s in shards for u in s['runs'] for v in u['results']);counts=collections.Counter(v['outcome'] for r,s in shards for u in s['runs'] for v in u['results']);wpf=dict(suite='FULL_WPF',discovered=len(discovered),selected=sum(s['selected'] for r,s in shards),obtained=sum(observed.values()),passed=counts['Passed'],failed=counts['Failed'],skipped=counts['NotExecuted'],missing=list((wanted-observed).elements()),extra=list((observed-wanted).elements()),plansMatchDiscovery=wanted==planned,sameAssembly=len({s['assemblySha256'] for r,s in shards})==1,sameDiscovery=all(collections.Counter((v['id'],v['fullyQualifiedName'],v['displayName']) for v in json.loads((r/'discovery.json').read_text(encoding='utf-8-sig')))==wanted for r,s in shards),status='FAIL' if counts['Failed'] or wanted!=observed else 'PARTIAL' if counts['NotExecuted'] else 'PASS');summary.append(wpf)
if wanted!=planned or wanted!=observed or not wpf['sameAssembly'] or not wpf['sameDiscovery'] or any(x['missing'] or x['extra'] or x.get('timeout',0) for x in summary):raise RuntimeError('Coverage/identity/timeout gate failed; preserve raw results and investigate')
dump('TEST_SUMMARY.json',dict(sourceSha=sha,timezone='Europe/Berlin',suites=summary,wpfExactCoverage=wpf,scope='This execution only. Skips remain not run. No physical business operation accepted.'))
csvwrite('ALL_TEST_RESULTS.csv',allrows);csvwrite('NONPASS.csv',[x for x in allrows if x['Status']!='PASS'])
final=[x for x in allrows if x['Suite']=='FULL_CORE_CORRECTED' or x['Suite'].startswith('FULL_WPF_') or x['Suite']=='PUBLISHED_MODULES']
modules=json.loads((d/'inventory/MODULE_SPECS.json').read_text(encoding='utf-8'));ops=json.loads((d/'inventory/OPERATIONS.json').read_text(encoding='utf-8'));planning=json.loads((d/'inventory/PLANNING_15.json').read_text(encoding='utf-8'));actions=json.loads((d/'inventory/ACTIONS.json').read_text(encoding='utf-8'));moduleRows=[]
for key,name,source,pattern in modules:
 related=[x for x in final if re.search(pattern,x['Class'])];c=collections.Counter(x['Status'] for x in related)
 moduleRows.append(dict(Module=name,Route=key,BuildIncluded='NOT_RUN' if key=='Installer' else 'PARTIAL' if key=='Watermark' else 'PASS',AutoRelated=len(related),Passed=c['PASS'],Failed=c['FAIL'],NotRun=c['NOT_RUN'],ActualOperations='NOT_RUN',ManualDesktop='NOT_RUN' if key=='Installer' else 'BLOCKED',Status='NOT_RUN' if key=='Installer' else 'FAIL' if c['FAIL'] else 'PARTIAL',Source=source,Evidence='TEST_SUMMARY.json;DESKTOP_FINAL_OPERATIONS.json',Issue='VAL-INSTALL-001' if key=='Installer' else 'VAL-DESKTOP-001',Blocker='No Setup; ZIP is diagnostic only' if key=='Installer' else 'No target input geometry or dedicated capture; fixture success is not desktop acceptance'))
 for op in ops:
  if op['ModuleKey']!=key:continue
  relevant=[x for x in related if re.search(op['TestPattern'],x['Class'])] if op.get('TestPattern') else related;rc=collections.Counter(x['Status'] for x in relevant)
  op.update(Automated='PARTIAL' if relevant else 'NOT_RUN',RelatedTests=len(relevant),RelatedPassed=rc['PASS'],RelatedFailed=rc['FAIL'],RelatedNotRun=rc['NOT_RUN'],TestMapping='Related fixture coverage only; no individual operation inferred from regex',TestEvidence=';'.join(sorted({x['Evidence'] for x in relevant})),HistoricalDelivery='Prior reports for comparison only; not current score',SourceSHA=sha)
for section in planning:
 related=[x for x in final if re.search(section['TestPattern']+'|PlanningBatch3BusinessTests|PlanningSummaryExportTests|PlanningSummaryImageExportTests|PlanningDocxKeepNextTests',x['Class'])];c=collections.Counter(x['Status'] for x in related);section.update(Automated='PARTIAL' if related else 'NOT_RUN',RelatedTests=len(related),RelatedPassed=c['PASS'],RelatedFailed=c['FAIL'],RelatedNotRun=c['NOT_RUN'],Evidence=';'.join(sorted({x['Evidence'] for x in related})),TestNames=';'.join(sorted({x['Name'] for x in related})),SourceSHA=sha,MappingBoundary='Current related fixtures, not unique per-section desktop acceptance; original multi-section service persistence and summary export run in current suite',ManualDesktop='BLOCKED',Operation='NOT_RUN',Status='FAIL' if c['FAIL'] else 'PARTIAL')
lookup={x['Module']:x for x in moduleRows}
for x in actions:
 m=lookup.get(x['Module']);x.update(SourceSHA=sha,AutoTest='PARTIAL' if m and m['AutoRelated'] else 'NOT_RUN',RelatedModuleTestCount=m['AutoRelated'] if m else 0,TestMapping='Related module candidates only; hidden/dynamic/shared actions not promoted to buttons',ManualDesktop='BLOCKED',Evidence='ALL_TEST_RESULTS.csv;DESKTOP_FINAL_OPERATIONS.json',Status='PARTIAL',BuildIncluded='PARTIAL',Issue='VAL-DESKTOP-001',Blocker='Candidate route/visibility/action not exercised in current desktop')
for name,rows in [('OPERATIONS',ops),('PLANNING_15',planning),('ACTIONS',actions),('MODULES',moduleRows)]:dump(name+'.json',rows);csvwrite(name+'.csv',rows)
nav=json.loads((root/'docs/validation/whole-app-20261020/NAVIGATION.json').read_text(encoding='utf-8'));nav['sourceSha']=sha
for x in nav['entries']:x.update(RuntimeObserved='PARTIAL',ActualOperation='NOT_RUN',ManualDesktop='BLOCKED',Reason='Current full EXE navigation tree observed; input geometry blocks actual action')
dump('NAVIGATION.json',nav)
def table(rows,fields):return '| '+' | '.join(fields)+' |\n|'+ '|'.join(['---']*len(fields))+'|\n'+'\n'.join('| '+' | '.join(str(r.get(f,'')).replace('|','/').replace('\n',' ') for f in fields)+' |' for r in rows)+'\n'
text='# 全模块本轮续接总表\n\n2026-10-22 Europe/Berlin；完整范围诊断包，不能发行。相关测试数量在模块间有重叠，不可求和；页面、源码及fixture通过均不代表人工业务闭环。\n\n'+table(moduleRows,['Module','BuildIncluded','AutoRelated','Passed','Failed','NotRun','ActualOperations','ManualDesktop','Status'])+'\n## 当前测试\n'+table([x for x in summary if not x['suite'].startswith('FULL_WPF_')],['suite','discovered','selected','obtained','passed','failed','skipped','status'])+'\n## 策划15区\n'+table(planning,['Section','BuildIncluded','RelatedTests','RelatedPassed','RelatedFailed','ManualDesktop','Status','Blocker'])
(out/'MODULE_TOTAL.md').write_text(text,encoding='utf-8')
manifest=[]
with zipfile.ZipFile(out/'RAW_TEST_EVIDENCE.zip','w',zipfile.ZIP_DEFLATED) as z:
 for label,r,s in runs:
  for f in sorted(r.rglob('*')):
   if not f.is_file() or f.is_symlink() or f.name not in ['summary.json','discovery.json','runsettings.xml','stdout.log','stderr.log','results.trx']:continue
   if len(f.relative_to(r).parts)>2:continue
   b=f.read_bytes();arc=label+'/'+f.relative_to(r).as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 for f in sorted(d.glob('*.log')):
  b=f.read_bytes();arc='commands/'+f.name;z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
dump('RAW_TEST_EVIDENCE_MANIFEST.json',dict(files=manifest,excludes='No runtime/database/private photos/generated image outputs/EXE/DLL or credentials',sha256=hashlib.sha256((out/'RAW_TEST_EVIDENCE.zip').read_bytes()).hexdigest()))
print(json.dumps(dict(suites=summary,modules=len(modules),operations=len(ops),planning=len(planning),sourceActions=len(actions)),ensure_ascii=False))
