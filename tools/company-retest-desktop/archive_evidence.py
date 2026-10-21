import pathlib,zipfile,hashlib,json,argparse
p=argparse.ArgumentParser();p.add_argument('--reports',required=True);p.add_argument('--output',required=True);a=p.parse_args();reports=pathlib.Path(a.reports).resolve();output=pathlib.Path(a.output).resolve()
if output.exists():raise FileExistsError('Use a new evidence archive; never overwrite a prior delivery')
files=[]
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(reports.iterdir()):
  if not f.is_file() or f.is_symlink():continue
  if f.suffix.lower() not in ['.json','.csv','.md','.txt','.zip']:raise ValueError('Unexpected evidence file '+f.name)
  b=f.read_bytes();z.writestr(f.name,b);files.append(dict(path=f.name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(output) as z:
 for row in files:
  if hashlib.sha256(z.read(row['path'])).hexdigest()!=row['sha256']:raise ValueError('Archive readback mismatch')
result=dict(path=str(output),bytes=output.stat().st_size,sha256=hashlib.sha256(output.read_bytes()).hexdigest(),files=files,readbackVerified=True,scope='Independent retest evidence only; not a new software release or Setup',privatePhotosIncluded=False,databasesIncluded=False,releaseBinariesIncluded=False)
(output.parent/(output.stem+'-identity.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='files'}))
