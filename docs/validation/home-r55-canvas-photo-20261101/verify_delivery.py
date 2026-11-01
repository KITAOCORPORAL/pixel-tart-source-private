"""Read-only release verifier: validates ZIP/directory/test DLL bindings and reports every numerical gate."""
import argparse,hashlib,json,sys,zipfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('delivery',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
def sha(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as stream:
  for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()
z=json.loads((a.delivery/'ZIP_IDENTITY.json').read_text(encoding='utf-8-sig'));archive=a.delivery/Path(z['zip']).name;pub=a.delivery/'Guangjing-R55-CanvasPhoto-Diagnostic-win-x64'
assert sha(archive)==z['sha256']
with zipfile.ZipFile(archive) as f:
 assert len(f.infolist())==z['fileCount']
 for item in z['files']:
  assert sha(pub/item['path'])==item['sha256'],item['path']
  h=hashlib.sha256()
  with f.open(pub.name+'/'+item['path']) as stream:
   for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
  assert h.hexdigest()==item['sha256'],item['path']
checks=[]
for receipt,host,key in [('PUBLISHED_BINDING.json','release-testhost','sharedDlls'),('CORE_BINDING.json','release-core-testhost','files'),('RESUME_BINDING.json','resume-testhost','sharedDlls')]:
 data=json.loads((a.delivery/receipt).read_text(encoding='utf-8-sig'))
 for row in data[key]:
  assert sha(pub/row['file'])==row['releaseSha256']==row['boundSha256']==sha(a.delivery/host/row['file']),row['file']
 checks.append({'receipt':receipt,'matchedDllCount':len(data[key])})
identity=json.loads((pub/'PRODUCT_IDENTITY.json').read_text(encoding='utf-8-sig'));assert identity['WholeAppDiagnosticBuild'] is True and identity['PhotographerTrialBuild'] is False
for row in z['files']:
 path=row['path'].lower();assert not path.endswith(('.ptplan','.ptlibrary','.ptstudio.json'));assert not any(x in path for x in ['spektralab','ffmpeg.exe','photo24.jpg','photo33.jpg'])
result={'packageIntegrity':'PASS','sha256':z['sha256'],'fileCount':z['fileCount'],'bytes':archive.stat().st_size,'bindings':checks,'productSource':identity['productSource'],'scope':'Archive and DLL integrity only; numerical/physical acceptance is separate'}
a.output.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
