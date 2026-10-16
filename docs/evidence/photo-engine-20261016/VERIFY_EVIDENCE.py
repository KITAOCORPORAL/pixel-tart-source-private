import pathlib,json,hashlib,zipfile,subprocess,sys
R=pathlib.Path(r'E:/Kitao-Worktrees/kitao-photo-engine-candidate-20261016');E=R/'docs/evidence/photo-engine-20261016';checks=[];errors=[]
def rd(n):return json.loads((E/n).read_text(encoding='utf-8-sig'))
def ck(ok,n):
 checks.append({'name':n,'passed':bool(ok)})
 if not ok:errors.append(n)
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append(str(p)+str(e))
ck(len(rd('01_UNEXECUTED_GATES_FINAL.json')['items'])==29,'29originalgatesclassified');ck(all(g['originalOutcome']=='NotExecuted' and g['statusUnchanged'] for g in rd('01_UNEXECUTED_GATES_FINAL.json')['items']),'notpromoted')
f=rd('FINAL_FULL_WPF_SUMMARY.json');ck(f['discovered']==1626 and f['selected']==1626 and sum(f['counts'].values())==1626,'fullWpfcoverage');ck(f['counts']=={'Passed':1603,'NotExecuted':23},'fullWpf1603/23');ck(all(not r['timeout'] and len(r['results'])==r['expected'] for r in f['runs']),'noTimeoutNoMissing');ck(len(rd('FINAL_WPF_MISSING_RESULTS.json'))==0,'missing0')
ck(rd('FINAL_PUBLISHED_CORE_SUMMARY.json')['counts']=={'Passed':1702,'NotExecuted':6},'publishedCore1702/6');ck(rd('FINAL_SOURCE_CORE_SUMMARY.json')['counts']=={'Passed':1701,'NotExecuted':7},'sourceCore1701/7');ck(rd('FINAL_SPECIAL_SUMMARY.json')['counts']=={'Passed':17},'special17')
for p in rd('RELEASE_FILES.json'):
 ck(p['count']==len(p['files']),'releasecount'+p['name'])
 for row in p['files']:
  file=pathlib.Path(p['root'])/row['path'];ck(file.is_file() and hashlib.sha256(file.read_bytes()).hexdigest()==row['sha256'],'release'+p['name']+'/'+row['path'])
for b in rd('PUBLISHED_COMPONENT_BINDINGS.json'):ck(b['matches'],'bound'+b['host']+b['kind']+b['name'])
ck(subprocess.check_output(['git','-C',str(R),'diff','3aa65a13dba96747e52be73960c34ab89226e08b','--','src','tests','tools/planning-r7/verified_runner.py'])==b'','product/test/runnerunchanged');ck(all(x['gitContentUnchanged'] for x in rd('FINAL_FROZEN_CONTRACTS.json')),'goldenP3unchanged');ck(all(x['matches'] for x in rd('FINAL_AUTHORIZED_INPUT_HASHES.json')),'originalhashes')
raw=rd('02_RAW_RESULTS.json');ck(sum(x['code']==0 and 'operation' not in x for x in raw)==3,'3fullRAW');ck(next(x for x in raw if x.get('operation')=='callbackCancel100ms')['code']==3,'callbackcancel');ck(all(not x['outputExists'] for x in raw if 'operation' in x),'boundarynofakeoutput');ck(rd('02_RAW_NATIVE_IDENTITIES.json')['dllMatchesInherited'],'exactcandidateDLL')
photo=rd('03_FINAL_PHOTO_SUITE_RESULTS.json');ck(len(photo['cases'])==3 and all(c['previewReopenCodesExact'] and c['TIFF16PNG16CodesExact'] for c in photo['cases']),'3photoworkflowsreopen/readback');ck(photo['independentEotfMaxAbs']<.0001,'independentEOTF');ck(len(photo['rejections'])==2,'missingunsupportedreject');ck(json.loads(rd('03_OCIO_COLOUR_DERIVED_MATRIX.json')['stdout'])['maxAbs']<1e-6,'OCIOColourderived')
ck(rd('04_OUTPUT_SUMMARY.json')['executedComparisons']==18,'18referencecomparisons');ck(rd('04_OUTPUT_SUMMARY.json')['fullDarktable'].startswith('NOT_RUN'),'DTpartialnotfull');ck(not rd('05_NATIVE_PREPARED.json')['started'],'nativeNOTRUN');ck(rd('FINAL_SOURCE_IDENTITY.json')['srcDiffFromBase']['stdout']=='','noSRCchange')
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip') as z:ck(z.testzip() is None,'rawarchiveCRC')
m=E/'FILES_AND_TYPES.json'
if m.exists():
 rows=rd(m.name)['files'];ck(len(rows)>90,'inventorynonempty')
 for row in rows:ck(hashlib.sha256((R/row['path']).read_bytes()).hexdigest()==row['sha256'],'evidence'+row['path'])
result={'status':'PASSED' if not errors else 'FAILED','count':len(checks),'errors':errors,'checks':checks,'distributionApproval':False,'nativeApproval':False};(E/'VALIDATION.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'status':result['status'],'count':len(checks),'errors':errors}));sys.exit(bool(errors))
