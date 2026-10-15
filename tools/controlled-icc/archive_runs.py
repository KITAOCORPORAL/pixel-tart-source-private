import pathlib,zipfile,json,hashlib
R=pathlib.Path(__file__).resolve().parents[2];E=R/'docs/evidence/controlled-icc-20261015';items=json.loads((E/'RAW_TEST_RUNS.json').read_text());out=E/'RAW_TEST_RESULTS.zip'
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for item in items:
  root=pathlib.Path(item['path'])
  for p in root.rglob('*'):
   if p.is_file() and p.suffix.lower() in ['.trx','.json','.xml','.log']:z.write(p,item['name']+'/'+str(p.relative_to(root)))
print(json.dumps({'file':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()}))
