import pathlib,json,hashlib,zipfile,shutil
r=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026");d=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");pkg=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026/Guangjing-UI-Cleanup-Final-Diagnostic-win-x64");out=d/'reports'
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
for n in ['FINAL_IDENTITY.json','PRODUCT.json','BUILD_READBACK.json','NATIVE_FINAL_EQUIVALENCE.json','PRESERVATION.json','CORE_COPY_FAILURE.json','CANDIDATE_FAILURE.json','CANVAS_QUEUE_COMPARISON.json','BEFORE_IMAGE_PROVENANCE.json']:
 shutil.copy2(d/n,out/n)
# No repeated full-scope historical inventories. Keep a small command/hash set and the current raw archive.
shutil.copy2(r/'docs/validation/home-planning-owned-canvas-20261025/HISTORICAL_HOME_SCENARIO11.json',out/'HISTORICAL_HOME_SCENARIO11.json')
dump(out/'BUILD_COMMAND.json',json.loads((d/'SEALED_PUBLISH.json').read_text()));dump(out/'COMPONENT_BINDINGS.json',json.loads((d/'SEALED_BINDINGS.json').read_text()));dump(out/'DEPENDENCIES.json',json.loads((d/'DEPENDENCIES.json').read_text()));dump(out/'APP_FILES.json',json.loads((d/'APP_FILES.json').read_text()));
(pkg/'reports').mkdir(exist_ok=True)
for f in out.iterdir():
 if f.is_file():shutil.copy2(f,pkg/'reports'/f.name)
(pkg/'samples').mkdir(exist_ok=True)
for name in ['native-project-a-v1.ptplan','native-project-b-v1.ptplan','project-b.png']:shutil.copy2(d/'synthetic-ab-sealed'/name,pkg/'samples'/name)
shutil.copy2(r/'tests/fixtures/planning-owned-jpeg/orientation-6.jpg',pkg/'samples/replacement-shot.jpg');shutil.copy2(d/'synthetic-sealed/jpeg-png-15-section-summary.docx',pkg/'samples/synthetic-15-section-summary.docx')
(pkg/'visual-review').mkdir(exist_ok=True);shutil.copy2(d/'BEFORE_AFTER_FINAL.png',pkg/'visual-review/BEFORE_AFTER_OFFSCREEN.png')
for name in ['planning-missing-owned-OFFSCREEN.png','canvas-missing-owned-OFFSCREEN.png']:shutil.copy2(d/'error-evidence'/name,pkg/'visual-review'/name)
(pkg/'visual-review/README.txt').write_text('IN_PROCESS_WPF_RENDER synthetic data only, not native desktop screenshots. Before selected UI files source-equivalent to inherited baseline; old app not claimed to include owned-canvas fixes. Final after generated with published product DLL binding.',encoding='utf-8')
archive=d/(pkg.name+'.zip')
if archive.exists():raise FileExistsError('Never overwrite previous delivery')
manifest=[]
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts or f.suffix.lower() in ['.db','.sqlite','.sqlite3','.raf','.raw','.cr2','.arw']:raise RuntimeError('Unexpected mutable/private data')
  b=f.read_bytes();name=pkg.name+'/'+rel.as_posix();z.writestr(name,b);manifest.append(dict(path=name,bytes=len(b),type=f.suffix,sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(archive) as z:
 for f in manifest:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
identity=dict(productSha='b9cb1e25e811903b117aef3afeb203aadf7d0790',testRevisionSha='0505216f8aacbcc67ea10f7584a8469767afe923',path=str(archive),sha256=h(archive),bytes=archive.stat().st_size,files=len(manifest),readback=True,diagnosticOnly=True,isSetup=False,UserDataIncluded=False,databaseIncluded=False)
dump(d/'ZIP_IDENTITY.json',identity);dump(d/'ZIP_FILES.json',manifest);print(json.dumps(identity))
