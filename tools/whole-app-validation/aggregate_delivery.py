import pathlib,json,collections,csv,argparse,re,hashlib
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--delivery',required=True);p.add_argument('--source',required=True);a=p.parse_args();root=pathlib.Path(a.root);d=pathlib.Path(a.delivery);source=a.source;out=d/'reports';out.mkdir(exist_ok=True)
def read(label):
 dirs=sorted((d/label).glob('run-*'));return json.loads((dirs[-1]/'summary.json').read_text(encoding='utf-8-sig')),dirs[-1]
def count(s):
 vals=[v for r in s['runs'] for v in r['results']];c=collections.Counter(v['outcome'] for v in vals)
 return dict(discovered=s['discovered'],selected=s['selected'],obtained=len(vals),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],notRun=max(0,s['selected']-len(vals)),status='FAIL' if c['Failed'] else 'PARTIAL' if c['NotExecuted'] or len(vals)!=s['selected'] else 'PASS',complete=s['status']!='RUNNING')
sets={};summaryRows=[]
for label in ['FULL_CORE','FULL_WPF','FINAL_CORE','FINAL_PUBLISHED_MODULES','FULL_DOCUMENTS','RESEARCH_CANDIDATE_TESTS','FULL_DPI_CONTRACTS']:
 s,path=read(label);sets[label]=(s,path);summaryRows.append(dict(Suite=label,**count(s),evidence=str(path)))
shards=[read('FINAL_WPF_SHARD_'+str(i)) for i in range(4)];discovery=json.loads((shards[0][1]/'discovery.json').read_text(encoding='utf-8-sig'));expected=collections.Counter((x['id'],x['fullyQualifiedName'],x['displayName']) for x in discovery)
planned=collections.Counter((c['id'],c['fullyQualifiedName'],c['displayName']) for s,_ in shards for unit in s['executionPlan'] for c in unit['cases'])
if planned!=expected:raise RuntimeError('Shards do not exactly cover discovered cases')
if len(set(s['assemblySha256'] for s,_ in shards))!=1:raise RuntimeError('Shard assembly identity differs')
values=[v for s,_ in shards for r in s['runs'] for v in r['results']];obs=collections.Counter((v['id'],v['name'],v['display']) for v in values);missing=expected-obs;extra=obs-expected
c=collections.Counter(v['outcome'] for v in values);final=dict(Suite='FINAL_WPF',discovered=len(discovery),selected=sum(s['selected'] for s,_ in shards),obtained=len(values),passed=c['Passed'],failed=c['Failed'],skipped=c['NotExecuted'],notRun=sum(missing.values()),extra=sum(extra.values()),status='FAIL' if c['Failed'] or extra else 'PARTIAL' if c['NotExecuted'] or missing else 'PASS',complete=all(s['status']!='RUNNING' for s,_ in shards),evidence=[str(path) for _,path in shards]);summaryRows.append(final)
(out/'TEST_SUMMARY.json').write_text(json.dumps({'sourceSha':source,'timezone':'America/Chicago','suites':summaryRows,'wpfExactCoverage':{'missing':list(missing.elements()),'extra':list(extra.elements()),'plannedEqualsDiscovered':planned==expected}},ensure_ascii=False,indent=2),encoding='utf-8')
allrows=[]
for suite,(s,path) in sets.items():
 for run in s['runs']:
  for v in run['results']:allrows.append(dict(Suite=suite,Class=run['class'],Name=v['name'],Display=v['display'],Outcome=v['outcome'],Status='PASS' if v['outcome']=='Passed' else 'NOT_RUN' if v['outcome']=='NotExecuted' else 'FAIL',Evidence=run['directory'],Error=v['error']))
for s,path in shards:
 for run in s['runs']:
  for v in run['results']:allrows.append(dict(Suite='FINAL_WPF',Class=run['class'],Name=v['name'],Display=v['display'],Outcome=v['outcome'],Status='PASS' if v['outcome']=='Passed' else 'NOT_RUN' if v['outcome']=='NotExecuted' else 'FAIL',Evidence=run['directory'],Error=v['error']))
def csvwrite(path,rows):
 if not rows:return
 with path.open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
csvwrite(out/'ALL_TEST_RESULTS.csv',allrows);nonpass=[r for r in allrows if r['Status']!='PASS'];csvwrite(out/'RAW_FAILURES_AND_OPEN_TESTS.csv',nonpass)
finaltests=[x for x in allrows if x['Suite'] in ['FINAL_CORE','FINAL_WPF','FINAL_PUBLISHED_MODULES','FULL_DOCUMENTS']]
modules=json.loads((d/'inventory/MODULE_SPECS.json').read_text(encoding='utf-8'));operations=json.loads((d/'inventory/OPERATIONS.json').read_text(encoding='utf-8'));moduleRows=[]
for key,name,path,pattern in modules:
 related=[x for x in finaltests if re.search(pattern,x['Class'])];counter=collections.Counter(x['Status'] for x in related)
 for op in operations:
  if op['ModuleKey']!=key:continue
  op['Automated']='PARTIAL' if related else 'NOT_RUN';op['RelatedTests']=len(related);op['RelatedPassed']=counter['PASS'];op['RelatedFailed']=counter['FAIL'];op['RelatedNotRun']=counter['NOT_RUN'];op['TestMapping']='Module-level relevant fixtures only; not a claim this business action was exercised';op['TestEvidence']=';'.join(sorted(set(x['Evidence'] for x in related)));op['HistoricalDelivery']='Historical docs/evidence and docs/research remain references, never current-run PASS';op['SourceSHA']=source
  if key=='Installer':op['ManualDesktop']='NOT_RUN';op['Status']='NOT_RUN';op['Issue']='VAL-INSTALL-001';op['Blocker']='No Setup produced; portable ZIP is not an installed product'
 moduleRows.append(dict(Module=name,Route=key,BuildIncluded='NOT_RUN' if key=='Installer' else 'PARTIAL' if key=='Watermark' else 'PASS',AutoRelated=len(related),Passed=counter['PASS'],Failed=counter['FAIL'],NotRun=counter['NOT_RUN'],ActualOperations='NOT_RUN',ManualDesktop='NOT_RUN' if key=='Installer' else 'BLOCKED',Status='NOT_RUN' if key=='Installer' else 'FAIL' if counter['FAIL'] else 'PARTIAL',Source=path,Evidence='TEST_SUMMARY.json;DESKTOP_OPERATIONS.json',Issue='VAL-INSTALL-001' if key=='Installer' else 'VAL-DESKTOP-001',Blocker='No installer' if key=='Installer' else 'Desktop input denied; module-level tests do not substitute business operation acceptance'))
csvwrite(out/'OPERATIONS.csv',operations);csvwrite(out/'MODULES.csv',moduleRows);(out/'OPERATIONS.json').write_text(json.dumps(operations,ensure_ascii=False,indent=2),encoding='utf-8')
planning=json.loads((d/'inventory/PLANNING_15.json').read_text(encoding='utf-8'))
for section in planning:
 related=[x for x in finaltests if re.search(section['TestPattern'],x['Class'])];counter=collections.Counter(x['Status'] for x in related);section.update(Automated='PARTIAL' if related else 'NOT_RUN',RelatedTests=len(related),RelatedPassed=counter['PASS'],RelatedFailed=counter['FAIL'],RelatedNotRun=counter['NOT_RUN'],Evidence=';'.join(sorted(set(x['Evidence'] for x in related))),TestNames=';'.join(sorted(set(x['Name'] for x in related))),SourceSHA=source)
csvwrite(out/'PLANNING_15.csv',planning);(out/'PLANNING_15.json').write_text(json.dumps(planning,ensure_ascii=False,indent=2),encoding='utf-8')
actions=json.loads((d/'inventory/ACTIONS.json').read_text(encoding='utf-8'));module_lookup={m['Module']:m for m in moduleRows}
for row in actions:
 row['SourceSHA']=source;row['HistoricalDelivery']='Historical evidence only';match=module_lookup.get(row['Module']);row['RelatedModuleTestCount']=match['AutoRelated'] if match else 0;row['TestMapping']='PARTIAL: module-level candidates, not per-action execution' if match else 'NOT_RUN: dynamic/shared entry unresolved';row['AutoTest']='PARTIAL' if match and match['AutoRelated'] else 'NOT_RUN';row['Evidence']='ALL_TEST_RESULTS.csv;'+row['Evidence'];row['BuildIncluded']='PARTIAL';row['InclusionReason']='Source included in full build; runtime route/visibility/gates and generated variants remain individually unverified'
csvwrite(out/'ACTIONS.csv',actions);(out/'ACTIONS.json').write_text(json.dumps(actions,ensure_ascii=False,indent=2),encoding='utf-8')
def table(rows,fields):return '| '+' | '.join(fields)+' |\n|'+ '|'.join(['---']*len(fields))+'|\n'+'\n'.join('| '+' | '.join(str(r.get(f,'' )).replace('|','/').replace('\n',' ') for f in fields)+' |' for r in rows)+'\n'
text='# 全模块诊断总表\n\nPARTIAL：完整范围诊断包，不是已验证发行版。页面/源码/相关测试不等于业务动作或人工实机通过。\n\n'+table(moduleRows,['Module','BuildIncluded','AutoRelated','Passed','Failed','NotRun','ActualOperations','ManualDesktop','Status'])+'\n## 本轮测试原始统计\n'+table(summaryRows,['Suite','discovered','selected','obtained','passed','failed','skipped','notRun','status','complete'])+'\n## 策划十五分区\n'+table(planning,['Section','BuildIncluded','Automated','RelatedTests','RelatedPassed','RelatedFailed','ManualDesktop','Status','Blocker'])
(out/'MODULE_TOTAL.md').write_text(text,encoding='utf-8');print(json.dumps({'finalWpf':final,'actions':len(actions),'operations':len(operations),'planning':len(planning)},ensure_ascii=False))
