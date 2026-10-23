import pathlib,json,hashlib,zipfile,shutil
r=pathlib.Path("E:/Kitao-Worktrees/planning-owned-jpeg-company-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023");pkg=pathlib.Path("E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/Planning-OwnedJPEG-Diagnostic-win-x64");reports=d/'reports';sha="4997c3059c5737405921d88ba6fd74eb2be2496e"
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
for n in ['START.json','PRODUCT.json','FIRST_FAILURE.json','FINAL_BINDINGS.json','FINAL_RUN_PLAN.json','FINAL_PUBLISH.json','FINAL_BUILD_CORE.json','FINAL_BUILD_WPF.json','DOCX_READBACK.json','VERIFIED_OUTPUTS.json','APP_FILES.json','DEPENDENCIES.json','PRESERVATION.json']:shutil.copy2(d/n,reports/n)
for source,target in [('START.json','DESKTOP_START.json'),('DESKTOP_STATUS.json','DESKTOP_STATUS.json'),('INPUT.json','DESKTOP_INPUT.json'),('CAPTURE.json','DESKTOP_CAPTURE.json'),('BINDINGS.json','DESKTOP_BINDINGS.json')]:shutil.copy2(d/'native-jpeg'/source,reports/target)
(pkg/'samples').mkdir(exist_ok=True)
for src,target in [('jpeg-shot-missing-v1.ptplan','jpeg-shot-missing-v1.ptplan'),('jpeg-owned-v1.ptplan','jpeg-owned-v1.ptplan'),('jpeg-png-15-section-summary.docx','jpeg-png-15-section-summary.docx')]:shutil.copy2(d/'synthetic-final'/src,pkg/'samples'/target)
shutil.copy2(r/'tests/fixtures/planning-owned-jpeg/orientation-6.jpg',pkg/'samples/replacement-shot.jpg');shutil.copy2(r/'tests/fixtures/planning-owned-jpeg/plain.png',pkg/'samples/synthetic-reference.png')
dump(reports/'SAMPLE_IDENTITY.json',[dict(path=str(f),sha256=h(f),bytes=f.stat().st_size,syntheticOnly=True) for f in sorted((pkg/'samples').iterdir())])
(pkg/'reports').mkdir(exist_ok=True)
for f in reports.iterdir():
 if f.is_file():shutil.copy2(f,pkg/'reports'/f.name)
archive=d/(pkg.name+'.zip')
if archive.exists():raise FileExistsError('Preserve previous ZIP')
files=[]
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts or f.suffix.lower() in ['.db','.sqlite','.sqlite3','.raf','.raw','.cr2','.arw']:raise RuntimeError('Unexpected private/mutable data')
  b=f.read_bytes();name=pkg.name+'/'+rel.as_posix();z.writestr(name,b);files.append(dict(path=name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 for f in files:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
identity=dict(productSha=sha,path=str(archive),bytes=archive.stat().st_size,files=len(files),sha256=h(archive),readback=True,isSetup=False,diagnosticOnly=True,userDataIncluded=False,databaseIncluded=False)
dump(d/'ZIP_IDENTITY.json',identity);dump(d/'ZIP_FILES.json',files);shutil.copy2(d/'ZIP_IDENTITY.json',reports/'ZIP_IDENTITY.json');shutil.copy2(d/'ZIP_FILES.json',reports/'ZIP_FILES.json');print(json.dumps(identity))
