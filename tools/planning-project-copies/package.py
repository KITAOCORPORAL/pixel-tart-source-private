import pathlib,json,hashlib,zipfile,shutil
root=pathlib.Path("E:/Kitao-Worktrees/planning-project-copies-20261022");d=pathlib.Path("E:/Kitao-Deliveries/planning-project-copies-20261022");pkg=pathlib.Path("E:/Kitao-Deliveries/planning-project-copies-20261022/Planning-ProjectCopies-Diagnostic-win-x64");reports=d/'reports';sha="3710317f8c2681141cb7d0e959e78ab9f9bb6037"
def hash(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(path,x):path.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
selected=['START.json','PRODUCT_COMMIT.json','FIRST_FAILURES.json','FINAL_BINDINGS.json','FINAL_RUN_PLAN.json','APP_FILES.json','DEPENDENCIES.json','FINAL_READBACK.json','PRESERVATION.json','DESKTOP_START.json','DESKTOP_OBSERVATION.json','DESKTOP_COMPONENT_READBACK.json','FINAL_BUILD_CORE.json','FINAL_BUILD_WPF.json','FINAL_PUBLISH.json']
for n in selected:shutil.copy2(d/n,reports/n)
shutil.copy2(d/'synthetic-final/RESULT.json',reports/'SYNTHETIC_RESULT.json')
changed=subprocess_output=None
import subprocess
files=subprocess.check_output(['git','diff','--name-status',"6cdb3f7bab745dc28df9797f278c85967d130a29",sha,'--','src','tests'],cwd=root,text=True,encoding='utf-8').splitlines();dump(reports/'PRODUCT_CHANGED_FILES.json',dict(sourceSha=sha,base="6cdb3f7bab745dc28df9797f278c85967d130a29",files=files,types=['Product C#','Core/WPF test C#']))
# Keep full synthetic sample separately; explicitly synthetic SQLite does not enter Git.
sample=d/'Synthetic-ProjectCopies-Sample.zip'
if sample.exists():raise FileExistsError('Preserve existing sample')
sampleFiles=[]
with zipfile.ZipFile(sample,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted((d/'synthetic-final').rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  b=f.read_bytes();arc='Synthetic-ProjectCopies/'+f.relative_to(d/'synthetic-final').as_posix();z.writestr(arc,b);sampleFiles.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 z.writestr('README.txt','Synthetic SQLite and project stores only. Complete-directory relocation regression sample, NOT a general project import route. Never replace personal UserData. Owned copies resolve under planning/project-copies. Missing external-only source intentionally retained.')
with zipfile.ZipFile(sample) as z:
 for f in sampleFiles:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
dump(d/'SAMPLE_ZIP_FILES.json',sampleFiles);dump(d/'SAMPLE_ZIP_IDENTITY.json',dict(sourceSha=sha,path=str(sample),sha256=hash(sample),bytes=sample.stat().st_size,files=len(sampleFiles)+1,readback=True,containsSyntheticDatabase=True,privateData=False))
shutil.copy2(d/'SAMPLE_ZIP_IDENTITY.json',reports/'SAMPLE_ZIP_IDENTITY.json');shutil.copy2(d/'SAMPLE_ZIP_FILES.json',reports/'SAMPLE_ZIP_FILES.json')
(pkg/'reports').mkdir(exist_ok=True)
for f in reports.iterdir():
 if f.is_file():shutil.copy2(f,pkg/'reports'/f.name)
(pkg/'synthetic-examples').mkdir(exist_ok=True)
for name in ['relocated-project-summary.docx','summary-product-layout.pdf','RESULT.json']:shutil.copy2(d/'synthetic-final'/name,pkg/'synthetic-examples'/name)
(pkg/'synthetic-examples/README.txt').write_text('Synthetic current output; product-rendered PDF checked by independent Poppler. Not WPS/native desktop acceptance. Full synthetic directory/database delivered separately.',encoding='utf-8')
archive=d/(pkg.name+'.zip')
if archive.exists():raise FileExistsError('Preserve old package')
manifest=[]
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts or f.suffix.lower() in ['.db','.sqlite','.sqlite3','.raw','.raf','.arw','.cr2','.cr3']:raise RuntimeError('Unexpected mutable/private data in diagnostic package')
  b=f.read_bytes();arc=pkg.name+'/'+rel.as_posix();z.writestr(arc,b);manifest.append(dict(path=arc,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 assert len(z.namelist())==len(manifest)
 for f in manifest:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
identity=dict(sourceSha=sha,path=str(archive),sha256=hash(archive),bytes=archive.stat().st_size,files=len(manifest),readback=True,diagnosticOnly=True,isSetup=False,containsUserData=False,containsDatabase=False)
dump(d/'ZIP_FILES.json',manifest);dump(d/'ZIP_IDENTITY.json',identity);print(json.dumps(identity));print((d/'SAMPLE_ZIP_IDENTITY.json').read_text())
