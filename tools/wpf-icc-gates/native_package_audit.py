import pathlib,zipfile,tarfile,io,json,hashlib,sys,struct
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/wpf-icc-gates';old=pathlib.Path(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc');sys.path.insert(0,str(A/'python-deps'));import zstandard
package=old/'lcms2-2.16.conda';records=[];info={}
with zipfile.ZipFile(package) as z:
 for n in z.namelist():
  if n.startswith('info-') and n.endswith('.tar.zst'):
   raw=zstandard.ZstdDecompressor().decompress(z.read(n),max_output_size=64*1024*1024)
   with tarfile.open(fileobj=io.BytesIO(raw)) as t:
    for m in t:
     if m.isfile() and (m.name.endswith(('.json','meta.yaml','build.bat','conda_build_config.yaml','LICENSE')) or 'recipe' in m.name):
      b=t.extractfile(m).read();records.append(dict(file=m.name,sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
      if m.name in ['info/index.json','info/about.json','info/recipe/meta.yaml','info/recipe/build.bat','info/recipe/bld.bat','info/recipe/win_jpegturbo.patch','info/hash_input.json','info/recipe/conda_build_config.yaml']:info[m.name]=b.decode(errors='replace')
(A/'native-package-info.json').write_text(json.dumps({'packageSha256':hashlib.sha256(package.read_bytes()).hexdigest(),'records':records,'metadata':info},indent=2));print(json.dumps({'records':len(records),'metadata':info},indent=2))
