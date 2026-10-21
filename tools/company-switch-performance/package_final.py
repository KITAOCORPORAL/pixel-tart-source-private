import pathlib,zipfile,json,hashlib,argparse,shutil
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);p.add_argument('--package',required=True);a=p.parse_args();d=pathlib.Path(a.delivery);pkg=pathlib.Path(a.package);reports=d/'final-report';manifest=[]
with zipfile.ZipFile(reports/'RAW_PERFORMANCE.zip','w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted((d/'performance').rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(d/'performance')
  if any(x in rel.parts for x in ['shared-fixture','temp','runtime','evidence']):continue
  if f.suffix not in ['.json','.csv','.log','.trx']:continue
  if len(rel.parts)>4:continue
  b=f.read_bytes();arc=rel.as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(reports/'RAW_PERFORMANCE_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
shutil.copy2(d/'phase1-report/RAW_COMPANY_TESTS.zip',reports/'RAW_PHASE1_TESTS.zip')
for f in reports.iterdir():
 if f.is_file():(pkg/'reports').mkdir(exist_ok=True);shutil.copy2(f,pkg/'reports'/f.name)
files=[];archive=d/'Company-SwitchFix-Diagnostic-win-x64.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts:continue
  b=f.read_bytes();name=pkg.name+'/'+rel.as_posix();z.writestr(name,b);files.append(dict(path=name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 for f in files:
  if hashlib.sha256(z.read(f['path'])).hexdigest()!=f['sha256']:raise RuntimeError('Readback mismatch')
result=dict(path=str(archive),bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),files=len(files),readbackVerified=True,containsUserData=False,isSetup=False)
(d/'FINAL_ZIP_IDENTITY.json').write_text(json.dumps(result,indent=2),encoding='utf-8');(d/'FINAL_ZIP_FILES.json').write_text(json.dumps(files,indent=2),encoding='utf-8');print(json.dumps(result))
