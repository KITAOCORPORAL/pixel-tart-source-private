import pathlib,json,hashlib,zipfile,subprocess,sys
R=pathlib.Path(__file__).resolve().parents[2];E=R/'docs/evidence/icc-reliability-20261015';A=R/'artifacts/icc-reliability'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
runrows=[]
for name in ['roots-A-core','roots-A-wpf','roots-B-core','roots-B-wpf','roots-fixed-A-core','roots-fixed-A-wpf','roots-fixed-B-core','roots-fixed-B-wpf','final-core','published-core','published-wpf-safe']:
 d=A/name
 if d.exists():
  for run in d.glob('run-*'):runrows.append((name,run))
with zipfile.ZipFile(E/'RAW_TEST_RESULTS.zip','w',zipfile.ZIP_DEFLATED) as z:
 for name,root in runrows:
  for p in root.rglob('*'):
   if p.is_file() and p.suffix.lower() in ['.json','.trx','.log','.xml']:z.write(p,name+'/'+str(p.relative_to(root)))
 for p in A.glob('*.log'):z.write(p,'process-logs/'+p.name)
 for d in ['containers','input-transport-final']:
  for p in (A/d).glob('*.json'):z.write(p,d+'/'+p.name)
  for p in (A/d).glob('*.trx'):z.write(p,d+'/'+p.name)
print(json.dumps({'archiveSha256':digest(E/'RAW_TEST_RESULTS.zip'),'runCount':len(runrows)}))
