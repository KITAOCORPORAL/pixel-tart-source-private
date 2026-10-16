import pathlib,json,hashlib,subprocess,zipfile,sys
R=pathlib.Path(r'E:/Kitao-Worktrees/kitao-wpf-failures-20261016');E=R/'docs/evidence/wpf-failures-20261016';checks=[];errors=[]
def read(n):return json.loads((E/n).read_text(encoding='utf-8-sig'))
def ck(ok,label):
 checks.append({'label':label,'passed':bool(ok)})
 if not ok:errors.append(label)
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append(str(p)+str(e))
ck(read('FINAL_RELEASE_CORE_SUMMARY.json')['counts']=={'Passed':1702,'NotExecuted':6},'actualCore1702/6');ck(read('FINAL_RELEASE_SOURCE_CORE_SUMMARY.json')['counts']=={'Passed':1701,'NotExecuted':7},'sourceCore1701/7');ck(read('FINAL_RELEASE_SPECIAL_SUMMARY.json')['counts']=={'Passed':18},'affected18')
ck(read('01_TIMEOUT_HOST.json')['timedOut'] and read('01_TIMEOUT_HOST.json')['exitCode']==124 and read('01_TIMEOUT_HOST.json')['jobAssigned'],'hostboundedtimeout124');ck(not read('01_TIMEOUT_CLEANUP.json')['remaining'],'ownedparentchildgone')
for p in ['02_BOUNDED_HEADER_COLLISION.jsonl','FINAL_RELEASE_COLLISION.jsonl']:
 rows=[json.loads(l) for l in (E/p).read_text(encoding='utf-8-sig').splitlines() if l.strip()];ck(len(rows)>=7 and all(r['CollisionCount']==0 for r in rows),'nocollision'+p)
for p in ['02_BOUNDED_HEADER_HITS.jsonl','FINAL_RELEASE_HITS.jsonl']:
 rows=[json.loads(l) for l in (E/p).read_text(encoding='utf-8-sig').splitlines() if l.strip()];ck(rows and all(x['reaches'] for r in rows for x in r),'hittest'+p)
for pack in read('RELEASE_FILES_FINAL.json'):
 for f in pack['files']:
  p=pathlib.Path(pack['root'])/f['path'];ck(p.exists() and p.stat().st_size==f['bytes'] and hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'],'release'+pack['name']+'/'+f['path'])
for b in read('PUBLISHED_COMPONENT_BINDINGS_FINAL.json'):ck(b['matches'],'binding'+b['host']+b['kind']+b['name'])
ck(read('FINAL_SOURCE_FREEZE.json')['postFreezeSourceDiff']['stdout']=='','frozennoSourceDiff');ck(read('FINAL_SOURCE_FREEZE.json')['pixelAlgorithmsUnchangedFromBase']['stdout']=='','nopixelalgdiff');ck(all(f['gitContentUnchanged'] for f in read('FINAL_SOURCE_FREEZE.json')['oldContracts']),'PV456P3400unchanged')
ck(subprocess.check_output(['git','-C',str(R),'diff','c6142c14c7fb4d1c9af500b42673ddb7a4a910ae','--','src','tests','tools/PrivateWpfHost','tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedRunSet.ps1'])==b'','currentfrozencontent')
n=read('03_NEGATIVE_ATTRIBUTION_FINAL.json');ck(n['negativeFromPositiveInputChannel']==0 and n['negativeFromExactlyZeroInputChannel']==n['negativeSamples'],'Canonnegspecificzeroorigin');ck(n['maxIdentityDirectError']==0,'directidentity');ck(not read('03_TOLERANCE_DECISION.json')['productionEpsilonAdded'],'noGlobalEpsilon')
ck(not read('05_ACTUAL_NATIVE_STATUS.json')['startedThisBatch'] and not read('05_FINAL_NATIVE_PREPARED.json')['started'],'nativeNOTRUN');ck(read('04_EXACT_NATIVE_RECHECK.json')['cleanWindows']=='NOT_RUN_NO_ISOLATED_VM','cleanVMnotclaimed')
fullpath=E/'FINAL_RELEASE_FULL_WPF_SUMMARY.json'
if fullpath.exists():
 f=read(fullpath.name);ck(f['selected']==1626 and f['discovered']==1626 and len(f['runs'])==212,'fullWPFselectedall');ck(f['status']=='FAILED','strictpreservedifgaps');missing=read('FINAL_WPF_MISSING_RESULTS.json');ck(sum(r['expected']-r['actual'] for r in missing)==1626-sum(f['counts'].values()),'missingcoverageaccounted')
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip') as z:ck(z.testzip() is None,'rawarchiveCRC')
m=E/'FILES_AND_TYPES.json'
if m.exists():
 rows=read(m.name)['files'];ck(len(rows)>70,'nonemptyinventory')
 for row in rows:ck(hashlib.sha256((R/row['path']).read_bytes()).hexdigest()==row['sha256'],'evidence'+row['path'])
result={'status':'PASSED' if not errors else 'FAILED','count':len(checks),'errors':errors,'checks':checks,'integrityOnlyNotProductAcceptance':True};(E/'VALIDATION.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'status':result['status'],'count':len(checks),'errors':errors}));sys.exit(bool(errors))
