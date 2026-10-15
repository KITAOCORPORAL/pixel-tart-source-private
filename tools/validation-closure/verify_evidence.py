"""Fail closed validation for research evidence; not a product acceptance substitute."""
import pathlib,json,hashlib,subprocess,sys
R=pathlib.Path(__file__).resolve().parents[2];E=R/'docs/evidence/validation-closure-20261015';errors=[];checks=[]
def check(ok,label):
 checks.append({'name':label,'passed':bool(ok)})
 if not ok:errors.append(label)
for p in E.rglob('*.json'):
 try:json.loads(p.read_text(encoding='utf-8-sig'))
 except Exception as ex:errors.append(str(p)+str(ex))
a=json.loads((E/'01_FIXED_IDENTITIES.json').read_text(encoding='utf-8-sig'));check(a['allMatches'],'fixed references');check(len(a['sevenProfiles'])==7,'seven profiles');check(len(json.loads((E/'01_CELLS.json').read_text(encoding='utf-8-sig')))==56,'56 cells')
r=json.loads((E/'03_RELEASE_RECHECK.json').read_text(encoding='utf-8-sig'));check(r['allMatches'] and r['count']==290,'290 sealed files')
for f in r['files']:
 p=pathlib.Path(r['identity']['directory'])/f['path'];check(p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'],'sealed '+f['path'])
s=json.loads((E/'04_RELEASE_CORE_SUMMARY.json').read_text(encoding='utf-8-sig'));check(s['counts']=={'Passed':1696,'NotExecuted':6},'exact core counts');check(s['selected']==1702 and s['discovered']==1702,'discovery coverage');check(s['status']=='FAILED','strict not green')
a=json.loads((E/'04_full.json').read_text(encoding='utf-8-sig'));b=json.loads((E/'04_tile.json').read_text(encoding='utf-8-sig'));check(a['pixelHash']==b['pixelHash']=='CC85E4AA22C9750B3AF0C50DA9BC28BE6C5F507E75645EC318062974D63D2421','full tile historic pixel hash')
for mode in ['cancel','full-cancel']:check(json.loads((E/('04_'+mode+'.json')).read_text(encoding='utf-8-sig'))['status']=='CANCELLED',mode)
check(subprocess.check_output(['git','-C',str(R),'diff','af78bb56b1eeec4e7c1a0b2de6f320dbbe33d458','--','src','tests'])==b'','src tests unchanged')
for row in json.loads((E/'02_INPUT_AFTER.json').read_text(encoding='utf-8-sig')):check(row['unchanged'],'source '+row['file'])
m=E/'FILES_AND_TYPES.json'
if m.exists():
 check(len(json.loads(m.read_text(encoding='utf-8-sig'))['files'])>=80,'nonempty complete manifest')
 for row in json.loads(m.read_text(encoding='utf-8-sig'))['files']:
  p=R/row['path'];check(p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'],'manifest '+row['path'])
result={'status':'FAILED' if errors else 'PASSED','checks':checks,'errors':errors,'notNativeAcceptance':True};(E/'VALIDATION.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'status':result['status'],'checks':len(checks),'errors':errors},indent=2));sys.exit(bool(errors))
