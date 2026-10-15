import pathlib,json,hashlib,zipfile,subprocess,sys
R=pathlib.Path(r'E:/Kitao-Worktrees/kitao-wpf-icc-gates-20261015');E=R/'docs/evidence/wpf-icc-gates-20261015';checks=[];errors=[]
def read(n):return json.loads((E/n).read_text(encoding='utf-8-sig'))
def ck(ok,label):
 checks.append({'label':label,'passed':bool(ok)})
 if not ok:errors.append(label)
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append(str(p)+str(e))
full=read('FINAL_PUBLISHED_FULL_WPF_SUMMARY.json');ck(full['counts']=={'Passed':1600,'NotExecuted':23,'Failed':3},'fullWPF1600/3/23');ck(full['selected']==1626 and full['discovered']==1626 and len(full['runs'])==212,'full1626adaptercoverage');ck(full['status']=='FAILED','fullWPFnotgreen')
ck(read('FINAL_PUBLISHED_CORE_SUMMARY.json')['counts']=={'Passed':1702,'NotExecuted':6},'publishedCore1702/6');ck(read('FINAL_SOURCE_CORE_SUMMARY.json')['counts']=={'Passed':1701,'NotExecuted':7},'sourceCore1701/7');ck(read('FINAL_B_WPF.json')['counts']=={'Passed':73},'secondroot73');ck(read('01_OPTIN_SUMMARY.json')['counts']=={'Passed':5,'Failed':1},'extraoptin5/1retained')
ck(len(read('FINAL_WPF_CLASSIFICATION.json'))==1626,'all1626classified');ck(len(read('FINAL_WPF_NONPASSING.json'))==26,'26nonpassingnotomitted');ck(not read('FINAL_PRIVATE_A_HOST.json')['switchedInputDesktop'],'nophysicaldesktop');ck(read('FINAL_PRIVATE_A_STATION.json')['station'].startswith('KitaoTests_'),'privatestationobserved')
for b in read('PUBLISHED_COMPONENT_BINDINGS.json'):ck(b['matches'],'binding'+b['host']+b['kind']+b['name'])
for p in read('RELEASE_FILES.json'):
 for f in p['files']:
  file=pathlib.Path(p['root'])/f['path'];ck(file.is_file() and file.stat().st_size==f['bytes'] and hashlib.sha256(file.read_bytes()).hexdigest()==f['sha256'],'release'+p['name']+'/'+f['path'])
for row in read('FINAL_SOURCE_IDENTITY.json')['oldContracts']:ck(row['unchanged'],'oldcontract'+row['path'])
ck(subprocess.check_output(['git','-C',str(R),'diff','61ed030098cc198019a45188210ef174cb2dc0d1','--','src','tests','tools/PrivateWpfHost','tools/ControlledIccWorkflow','tools/planning-r7/verified_runner.py'])==b'','frozenproducttestcodeunchanged')
ck(subprocess.check_output(['git','-C',str(R),'diff','5b07b0ad9af8b36b9c99c9f15a24eed808b484b1','--','tools/wpf-icc-gates'])==b'','frozenresearchcodeunchanged')
for r in read('HISTORICAL_EVIDENCE_PRESERVATION.json'):ck(hashlib.sha256((R/r['path']).read_bytes()).hexdigest()==r['baseSha256'],'historicalrawbytes'+r['path'])
v2=read('02_V2_FROZEN_POLICY_SUITE.json');ck(len(v2)==5 and all(x['reopenExact'] and x['TIFF16PNG16PreviewCodesExact'] for x in v2),'V2fivereopencases');ck(all(x['productAdmission']==False for x in v2),'V2notproduct')
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip') as z:ck(z.testzip() is None,'rawarchiveCRC')
ck(read('03_ENTRY_GATE.json')['defaultEnabled']==False,'ordinaryentryclosed');ck(not read('04_NATIVE_PREPARED.json')['started'],'nativepreparedonly')
m=E/'FILES_AND_TYPES.json'
if m.exists():
 files=read('FILES_AND_TYPES.json')['files'];ck(len(files)>70,'nonemptyinventory')
 for r in files:ck(hashlib.sha256((R/r['path']).read_bytes()).hexdigest()==r['sha256'],'evidence'+r['path'])
result={'status':'PASSED' if not errors else 'FAILED','count':len(checks),'errors':errors,'checks':checks,'productAcceptance':'BLOCKED','fullWpf':'FAILED_3_AND23','native':'NOT_RUN'};(E/'VALIDATION.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'status':result['status'],'count':len(checks),'errors':errors}));sys.exit(bool(errors))
