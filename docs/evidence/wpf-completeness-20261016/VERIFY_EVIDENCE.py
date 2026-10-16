import pathlib,json,hashlib,subprocess,zipfile,sys,collections
R=pathlib.Path(r'E:/Kitao-Worktrees/kitao-wpf-result-completeness-20261016');E=R/'docs/evidence/wpf-completeness-20261016';checks=[];errors=[]
def ck(ok,n):
 checks.append({'name':n,'passed':bool(ok)})
 if not ok:errors.append(n)
def read(n):return json.loads((E/n).read_text(encoding='utf-8-sig'))
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append(str(p)+str(e))
ck(read('FINAL_PUBLISHED_CORE_SUMMARY.json')['counts']=={'Passed':1702,'NotExecuted':6},'publishedCore1702/6');ck(read('FINAL_SOURCE_CORE_SUMMARY.json')['counts']=={'Passed':1701,'NotExecuted':7},'sourceCore1701/7');ck(read('FINAL_SPECIAL_SUMMARY.json')['counts']=={'Passed':20},'actualspecial20')
for p in read('RELEASE_FILES.json'):
 for f in p['files']:
  file=pathlib.Path(p['root'])/f['path'];ck(file.is_file() and hashlib.sha256(file.read_bytes()).hexdigest()==f['sha256'],'release'+p['name']+f['path'])
for b in read('PUBLISHED_COMPONENT_BINDINGS.json'):ck(b['matches'],'actualbinding'+b['host']+b['kind']+b['name'])
ck(read('FINAL_SOURCE_IDENTITY.json')['productSrcUnchanged'],'productsrcunchanged');ck(subprocess.check_output(['git','-C',str(R),'diff','3162cb10e0bc4838331a9325a1169baff051f2a9','--','src','tests','tools/planning-r7/verified_runner.py','tools/PrivateWpfHost'])==b'','frozensourceunchanged');ck(all(x['unchanged'] for x in read('FINAL_FROZEN_CONTRACTS.json')),'historicalgoldenP3400unchanged')
p1=read('01_FRESH_P1_METHOD_UNITS.json');ck(p1['returned']==42 and p1['expected42']==42 and p1['unitCount']==10,'P1all42samefreshrun');ck(p1['elapsedTotalSeconds']>600 and p1['maxUnitSeconds']<600,'cumulativeover600boundedunits');ck(all(not u['timeout'] and len(u['results'])==u['expected'] for u in p1['units']),'P1nomissingnofaults')
for n in ['FINAL_COLLISION.jsonl','FINAL_HEADER_HITS.jsonl']:
 rows=[json.loads(l) for l in (E/n).read_text(encoding='utf-8-sig').splitlines() if l.strip()]
 ck(all(x['CollisionCount']==0 for x in rows) if 'COLLISION' in n else all(x['reaches'] for r in rows for x in r),'geometry'+n)
ck(not read('05_NATIVE_PREPARED.json')['started'],'nativeNOTRUN');ck(not json.loads(read('03_CONDITIONAL_BOUND.json')['stdout'])['productionEpsilonAdded'],'noepsilon')
fullpath=E/'FINAL_FULL_WPF_SUMMARY.json'
if fullpath.exists():
 f=read(fullpath.name);ck(f['discovered']==1626 and f['selected']==1626,'full1626');ck(sum(f['counts'].values())==1626,'returnedall1626');ck(all(not r['timeout'] and len(r['results'])==r['expected'] for r in f['runs']),'noMissingResultsNoTimeout');ck(len(read('FINAL_WPF_CLASSIFICATION.json'))==1626,'everytestownership')
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip') as z:ck(z.testzip() is None,'rawarchiveCRC')
m=E/'FILES_AND_TYPES.json'
if m.exists():
 files=read(m.name)['files'];ck(len(files)>50,'nonemptyinventory')
 for f in files:ck(hashlib.sha256((R/f['path']).read_bytes()).hexdigest()==f['sha256'],'evidence'+f['path'])
result={'status':'PASSED' if not errors else 'FAILED','count':len(checks),'errors':errors,'checks':checks,'notProductOrNativeApproval':True};(E/'VALIDATION.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'status':result['status'],'count':len(checks),'errors':errors}));sys.exit(bool(errors))
