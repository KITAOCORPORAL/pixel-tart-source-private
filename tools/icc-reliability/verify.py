import pathlib,json,hashlib,subprocess,zipfile,sys
R=pathlib.Path(__file__).resolve().parents[2];E=R/'docs/evidence/icc-reliability-20261015';errors=[];checks=[]
def ck(ok,n):
 checks.append({'name':n,'passed':bool(ok)})
 if not ok:errors.append(n)
def read(n):return json.loads((E/n).read_text(encoding='utf-8-sig'))
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append(str(p)+str(e))
for root in ['A','B']:
 ck(read('01_FINAL_ROOT_'+root+'_core.json')['counts']=={'Passed':1701,'NotExecuted':7},'root'+root+'core')
 ck(read('01_FINAL_ROOT_'+root+'_wpf.json')['counts']=={'Passed':16},'root'+root+'wpf')
ck(read('FINAL_publishedCore_SUMMARY.json')['counts']=={'Passed':1702,'NotExecuted':6},'published1702/6')
ck(read('FINAL_publishedCore_SUMMARY.json')['status']=='FAILED','strictnotgreen')
ck(read('FINAL_publishedWpfSafe_SUMMARY.json')['counts']=={'Passed':16},'safeWPF16')
ck(len(read('FINAL_WPF_CLASSIFICATION.json'))==1626,'WPFfullnames1626')
ck(sum(t['status']=='NOT_RUN' for t in read('FINAL_WPF_CLASSIFICATION.json'))==1610,'WPFunrun1610')
ck(all(x['unchanged'] for x in read('FINAL_FROZEN_CONTRACTS.json')),'goldenP3bytesunchanged')
ck(json.loads(read('FINAL_PUBLISHED_PIXELS.json')['stdout'])['tiffExact'],'publishedTIFFexact')
ck(json.loads(read('FINAL_PUBLISHED_PIXELS.json')['stdout'])['pngBytesExact'],'publishedPNGexact')
for b in read('PUBLISHED_COMPONENT_BINDING.json'):ck(b['matches'],'component'+b['kind']+b['name'])
for pack in read('RELEASE_FILES.json'):
 for f in pack['files']:
  p=pathlib.Path(pack['root'])/f['path'];ck(p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'],'release'+pack['name']+f['path'])
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip') as z:ck(z.testzip() is None,'rawarchiveCRC')
ck(subprocess.check_output(['git','-C',str(R),'diff','6cea361464e27c4fc224bc0cc019b55cf600a884','--','src','tests','tools/ControlledIccWorkflow','tools/StructureScratchExperiment'])==b'','frozenSourceUnchanged')
m=E/'FILES_AND_TYPES.json'
if m.exists():
 files=read('FILES_AND_TYPES.json')['files'];ck(len(files)>150,'inventorynonempty')
 for f in files:ck(hashlib.sha256((R/f['path']).read_bytes()).hexdigest()==f['sha256'],'manifest'+f['path'])
result={'status':'PASSED' if not errors else 'FAILED','count':len(checks),'errors':errors,'checks':checks,'nativeAcceptance':False};(E/'VALIDATION.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'status':result['status'],'count':len(checks),'errors':errors}));sys.exit(bool(errors))
