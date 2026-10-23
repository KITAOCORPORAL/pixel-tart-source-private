import pathlib,json,hashlib,zipfile,shutil
r=pathlib.Path("E:/Kitao-Worktrees/planning-shot-link-lifecycle-company-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-shot-link-lifecycle-company-20261023");pkg=pathlib.Path("E:/Kitao-Deliveries/planning-shot-link-lifecycle-company-20261023/Planning-ShotLinks-Verified-Diagnostic-win-x64");reports=d/'reports';sha="3cd7543050722b2449a75ec7115e70aa29dcff6b"
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
selected=['START.json','PRODUCT.json','FIRST_FAILURE.json','REGRESSION_FAILURE.json','PROVENANCE_CORRECTION.json','VERIFIED_BINDINGS.json','VERIFIED_RUN_PLAN.json','VERIFIED_PUBLISH.json','VERIFIED_BUILD_CORE.json','VERIFIED_BUILD_WPF.json','VERIFIED_OUTPUTS.json','APP_FILES.json','DEPENDENCIES.json','PRESERVATION.json']
for n in selected:shutil.copy2(d/n,reports/n)
for name,src in [('DESKTOP_STATUS.json','DESKTOP_STATUS.json'),('DESKTOP_INPUT_FIRST.json','INPUT_FIRST.json'),('DESKTOP_RECOVERY_FAILURE.json','RECOVERY_FAILURE.json'),('DESKTOP_BINDINGS.json','BINDINGS.json'),('DESKTOP_START.json','START.json')]:shutil.copy2(d/'native-owned-copy'/src,reports/name)
shutil.copy2(d/'synthetic-verified/RESULT.json',reports/'SYNTHETIC_RESULT.json')
(pkg/'samples').mkdir(exist_ok=True)
for src,dest in [('shot-missing-v1.ptplan','shot-missing-v1.ptplan'),('shot-owned-v1.ptplan','shot-owned-v1.ptplan'),('selected-shot.png','replacement-shot.png')]:shutil.copy2(d/'synthetic-verified'/src,pkg/'samples'/dest)
dump(reports/'SAMPLE_IDENTITY.json',[dict(path=str(f),sha256=h(f),bytes=f.stat().st_size,syntheticOnly=True) for f in sorted((pkg/'samples').iterdir())])
(pkg/'reports').mkdir(exist_ok=True)
for f in reports.iterdir():
 if f.is_file():shutil.copy2(f,pkg/'reports'/f.name)
archive=d/(pkg.name+'.zip')
if archive.exists():raise FileExistsError('Preserve old package')
files=[]
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts or f.suffix.lower() in ['.db','.sqlite','.sqlite3','.raf','.raw','.cr2','.arw']:raise RuntimeError('Unexpected personal/mutable data')
  b=f.read_bytes();name=pkg.name+'/'+rel.as_posix();z.writestr(name,b);files.append(dict(path=name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 for f in files:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
identity=dict(productSha=sha,path=str(archive),bytes=archive.stat().st_size,files=len(files),sha256=h(archive),readback=True,isSetup=False,diagnosticOnly=True,userDataIncluded=False,databaseIncluded=False)
dump(d/'ZIP_IDENTITY.json',identity);dump(d/'ZIP_FILES.json',files);shutil.copy2(d/'ZIP_IDENTITY.json',reports/'ZIP_IDENTITY.json');shutil.copy2(d/'ZIP_FILES.json',reports/'ZIP_FILES.json');print(json.dumps(identity))
