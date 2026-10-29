"""Hash inventories only; no external source/resource bytes copied into product repo."""
import json,subprocess,sys,hashlib
from pathlib import Path
from run_matrix import sha
root=Path(sys.argv[1]);build=Path(sys.argv[2]);resources=Path(sys.argv[3]);out=Path(sys.argv[4])
files=subprocess.check_output(['git','-C',str(root),'ls-files','-z']).decode().split('\0');inventory=[]
for name in files:
 if name:
  p=root/name
  if p.is_file():inventory.append({'path':name,'bytes':p.stat().st_size,'sha256':sha(p)})
sourceRootHash=hashlib.sha256(json.dumps(inventory,sort_keys=True,separators=(',',':')).encode()).hexdigest();res=[{'path':str(p.relative_to(resources)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(resources.rglob('*')) if p.is_file()];lic=[x for x in inventory if 'license' in x['path'].lower() or 'notice' in x['path'].lower()];bins=[{'path':str(p.relative_to(build)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':sha(p)} for p in build.rglob('*') if p.is_file() and p.suffix.lower() in ('.dll','.exe','.spv')]
report={'sourceCommit':subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD']).decode().strip(),'sourceTree':subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD^{tree}']).decode().strip(),'gitStatus':subprocess.check_output(['git','-C',str(root),'status','--porcelain']).decode(),'gitSubmodules':subprocess.check_output(['git','-C',str(root),'submodule','status']).decode(),'sourceFileManifestSha256':sourceRootHash,'sourceFiles':inventory,'licenseFiles':lic,'runtimeResources':res,'buildBinaries':bins}
with out.open('x',encoding='utf-8') as f:json.dump(report,f,indent=2)
print(json.dumps({'trackedFiles':len(inventory),'resources':len(res),'binaries':len(bins),'manifest':str(out)}))
