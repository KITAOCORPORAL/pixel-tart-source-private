"""Fetch exact local research dependencies; never installs or bundles them in the product.
Use only in a dedicated artifacts directory. License approval is not implied by download.
"""
import urllib.request,pathlib,hashlib,json,zipfile,tarfile,io,subprocess,sys
root=pathlib.Path(__file__).resolve().parents[2]/'artifacts/company-validation/icc';root.mkdir(parents=True,exist_ok=True)
items=[('https://api.anaconda.org/download/conda-forge/lcms2/2.16/win-64/lcms2-2.16-h67d730c_0.conda','lcms2-2.16.conda','f9fd9e80e46358a57d9bb97b1e37a03da4022143b019aa3c4476d8a7795de290')]
manifest=pathlib.Path(__file__).resolve().parents[2]/'docs/evidence/company-validation-20261015/icc/REFERENCE_COMPONENTS.json'
if manifest.exists():
 for x in json.loads(manifest.read_text(encoding='utf-8'))['files']:
  name=pathlib.Path(x['localPath']).name
  if name in ['argyll-3.5.0-bin.zip','argyll-3.5.0-src.zip']:
   url='https://www.argyllcms.com/Argyll_V3.5.0_'+('win64_exe.zip' if name.endswith('bin.zip') else 'src.zip');items.append((url,name,x['sha256']))
for url,name,expected in items:
 p=root/name
 if not p.exists():p.write_bytes(urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Kitao-isolated-ICC-research'}),timeout=90).read())
 actual=hashlib.sha256(p.read_bytes()).hexdigest()
 if actual!=expected:raise RuntimeError('Identity mismatch; do not use '+str(p))
 print(name,actual)
 if name.startswith('argyll'):
  with zipfile.ZipFile(p) as z:
   for n in z.namelist():
    if n.endswith(('/xicclu.exe','/icclu.exe','/License.txt','/License2.txt','/License4.txt','/icc/icc.c','/icc/icc.h','/xicc/xicclu.c','/doc/xicclu.html')):z.extract(n,root/'argyll35')
# zstandard must be installed into artifacts/icc/python-deps, not global environment.
sys.path.insert(0,str(root/'python-deps'));import zstandard
with zipfile.ZipFile(root/'lcms2-2.16.conda') as z:
 for n in z.namelist():
  if n.endswith('.tar.zst'):
   with zstandard.ZstdDecompressor().stream_reader(io.BytesIO(z.read(n))) as reader:
    archive=tarfile.open(fileobj=reader,mode='r|')
    for m in archive:
     if m.isfile() and ('lcms2.dll' in m.name or m.name.endswith(('lcms2.h','LICENSE'))):
      p=root/'lcms'/m.name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(archive.extractfile(m).read())
repo=root/'skcms';sha='97adcbb67e98d633a65b29fd405941970f3b476d'
if not repo.exists():subprocess.run(['git','clone','https://skia.googlesource.com/skcms',str(repo)],check=True)
subprocess.run(['git','-C',str(repo),'checkout','--detach',sha],check=True)
if subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()!=sha:raise RuntimeError('skcms identity mismatch')
print('Research references verified. Read licenses; binaries/source must stay out of product and Git.')
