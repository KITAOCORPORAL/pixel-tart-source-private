import pathlib,json,hashlib,zipfile,shutil,subprocess
r=pathlib.Path("E:/Kitao-Worktrees/planning-project-packages-20261022");d=pathlib.Path("E:/Kitao-Deliveries/planning-project-packages-20261022");pkg=pathlib.Path("E:/Kitao-Deliveries/planning-project-packages-20261022/Planning-ProjectPackages-Diagnostic-win-x64");reports=d/'reports';sha="9040e320d871f05c1013da9ed45bb04d48257616"
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
for n in ['START.json','PRODUCT.json','FIRST_FAILURE.json','FINAL_BINDINGS.json','FINAL_RUN_PLAN.json','FINAL_PUBLISH.json','FINAL_BUILD_CORE.json','FINAL_BUILD_WPF.json','VERIFIED_PACKAGE.json','APP_FILES.json','DEPENDENCIES.json','PRESERVATION.json','DESKTOP_START.json','DESKTOP_INPUT.json','DESKTOP_CAPTURE.json','DESKTOP_BINDINGS.json']:shutil.copy2(d/n,reports/n)
shutil.copy2(d/'synthetic-final/RESULT.json',reports/'SYNTHETIC_RESULT.json')
# Only actual selected package/docx and evidence, never imported databases or profile directories.
(pkg/'samples').mkdir(exist_ok=True);shutil.copy2(d/'Synthetic-Planning-Project-v1.ptplan',pkg/'samples/Synthetic-Planning-Project-v1.ptplan');shutil.copy2(d/'synthetic-final/imported-summary.docx',pkg/'samples/imported-summary.docx')
dump(reports/'SAMPLE_IDENTITY.json',dict(path=str(d/'Synthetic-Planning-Project-v1.ptplan'),sha256=h(d/'Synthetic-Planning-Project-v1.ptplan'),bytes=(d/'Synthetic-Planning-Project-v1.ptplan').stat().st_size,syntheticOnly=True,containsDatabase=False))
(pkg/'reports').mkdir(exist_ok=True)
for f in reports.iterdir():
 if f.is_file():shutil.copy2(f,pkg/'reports'/f.name)
archive=d/(pkg.name+'.zip')
if archive.exists():raise FileExistsError('Preserve existing package')
files=[]
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts or f.suffix.lower() in ['.db','.sqlite','.sqlite3','.raw','.raf','.cr2','.arw']:raise RuntimeError('Unexpected data')
  b=f.read_bytes();name=pkg.name+'/'+rel.as_posix();z.writestr(name,b);files.append(dict(path=name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 for f in files:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
identity=dict(productSha=sha,path=str(archive),bytes=archive.stat().st_size,sha256=h(archive),files=len(files),readback=True,isSetup=False,diagnosticOnly=True,userDataIncluded=False,databaseIncluded=False)
dump(d/'ZIP_IDENTITY.json',identity);dump(d/'ZIP_FILES.json',files);shutil.copy2(d/'ZIP_IDENTITY.json',reports/'ZIP_IDENTITY.json');shutil.copy2(d/'ZIP_FILES.json',reports/'ZIP_FILES.json');print(json.dumps(identity))
