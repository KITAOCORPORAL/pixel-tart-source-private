import pathlib,zipfile,json,hashlib
r=pathlib.Path("E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023");records=json.loads((r/'FINAL_PERFORMANCE_RUNS.json').read_text());assert len(records)==6 and all(x['exitCode']==0 for x in records);assert len({x['testSha256'] for x in records})==1
files=[]
with zipfile.ZipFile(r/'reports/PERFORMANCE_RAW.zip','w',zipfile.ZIP_DEFLATED) as z:
 for mode in ['before','after']:
  for i in range(3):
   root=r/('perf-final-'+mode+'-'+str(i))
   for name in ['run.log','result.trx','MEASUREMENT.json']:
    f=root/name;b=f.read_bytes();arc=root.name+'/'+name;z.writestr(arc,b);files.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 (r/'reports/PERFORMANCE_RAW_MANIFEST.json').write_text(json.dumps(files,indent=2))
with zipfile.ZipFile(r/'reports/PERFORMANCE_RAW.zip') as z:
 for f in files:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
print(json.dumps(dict(runs=len(records),sameTestBinary=True,rawFiles=len(files))))
