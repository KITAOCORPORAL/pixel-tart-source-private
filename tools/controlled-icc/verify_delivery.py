"""Integrity and classification checks, never substitutes native acceptance."""
import pathlib,json,hashlib,subprocess,zipfile,sys
R=pathlib.Path(__file__).resolve().parents[2];E=R/'docs/evidence/controlled-icc-20261015';checks=[];errors=[]
def ck(ok,name):
 checks.append({'name':name,'passed':bool(ok)})
 if not ok:errors.append(name)
def read(n):return json.loads((E/n).read_text(encoding='utf-8-sig'))
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as e:errors.append(str(p)+str(e))
base=read('01_BASELINE.json');ck(base['allMatches'] and len(base['recheckedFiles'])==290,'old payload290')
for f in base['recheckedFiles']:
 p=pathlib.Path(base['oldRelease']['directory'])/f['path'];ck(p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'],'old '+f['path'])
for m in read('RELEASE_FILES.json'):
 for f in m['files']:
  p=pathlib.Path(m['root'])/f['path'];ck(p.is_file() and p.stat().st_size==f['bytes'] and hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'],'release '+m['package']+'/'+f['path'])
ck(read('FINAL_publishedCore_SUMMARY.json')['counts']=={'Passed':1699,'NotExecuted':6},'published exact counts1699/6')
ck(read('FINAL_publishedCore_SUMMARY.json')['status']=='FAILED','strict remainsFAILED')
ck(read('FINAL_publishedWpfSafe_SUMMARY.json')['counts']=={'Passed':16},'safe WPF16')
ck(len(read('FINAL_WPF_NOT_RUN.json'))==1610,'whole WPF1610notrun')
ck(len(read('05_NATIVE_RESULTS.json')['tests'])==24,'native24notrun')
ck(all(x['unchanged'] for x in read('FINAL_FROZEN_GOLDEN_IDENTITY.json')),'golden source bytes unchanged')
for row in read('PUBLISHED_COMPONENT_BINDING.json'):ck(row['matches'],'component '+row['kind']+'/'+row['file'])
result=read('FINAL_ISOLATED_ICC_RESULT.json');ck(result['previewReopenExact'],'preview reopenexact');ck(result['inputBitDepth']==16,'input16bit');ck(result['intentEffective']==1 and not result['bpcEffective'],'relative BPCoff');ck(json.loads(read('FINAL_DIRECT_COMPARE.json')['stdout'])['exact'],'directLCMSallfloatsexact')
for row in read('04_PERFORMANCE.json')['aggregate']:ck(row['count']==3 and row['allHashesFrozen'],'3bounded '+row['mode'])
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip') as z:ck(z.testzip() is None,'raw archiveCRC')
ck(read('01_TIMEZONE_FIELDS.json')['requestedHistoricalAcceptanceZone']=='Asia/Shanghai','business timezone erratum')
ck(subprocess.check_output(['git','-C',str(R),'diff','e230038e2b882ef413a1b53972854efbd80e264e','--','src','tests/RAWSelectionAssistant.Tests/ControlledIccTests.cs','tools/ControlledIccWorkflow'])==b'','frozen product/tool source unchanged')
manifest=E/'FILES_AND_TYPES.json'
if manifest.exists():
 rows=json.loads(manifest.read_text(encoding='utf-8'))['files'];ck(len(rows)>100,'nonempty inventory')
 for f in rows:
  p=R/f['path'];ck(p.exists() and hashlib.sha256(p.read_bytes()).hexdigest()==f['workingSha256'],'evidence '+f['path'])
output={'status':'PASSED' if not errors else 'FAILED','count':len(checks),'errors':errors,'checks':checks,'isProductAcceptance':False,'fullWpf':'NOT_RUN','strictCore':'FAILED_NOTEXECUTED'}
(E/'VALIDATION.json').write_text(json.dumps(output,indent=2),encoding='utf-8');print(json.dumps({'status':output['status'],'count':len(checks),'errors':errors}));sys.exit(bool(errors))
