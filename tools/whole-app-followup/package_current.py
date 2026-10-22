import pathlib,json,hashlib,zipfile,shutil,csv,subprocess
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");pkg=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022/Guangjing-FullScope-Followup-Diagnostic-win-x64");reports=d/'reports';sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4"
def dump(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
# Current capture archive: exact manifest-selected files only, never profiles/assets/databases.
visual=d/'current-run-visual-recheck';vm=json.loads((visual/'current-run-visual-evidence.json').read_text(encoding='utf-8-sig'));manifest=[];archive=d/'Logical-Offscreen-106-current.zip'
if archive.exists():raise FileExistsError('Preserve previous archive')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for row in vm['captures']:
  image=pathlib.Path(row['path']);meta=pathlib.Path(row['metadata_path']) if 'metadata_path' in row else pathlib.Path(str(image)+'.json')
  for f in [image,meta]:
   b=f.read_bytes();rel=f.relative_to(visual).as_posix();z.writestr(rel,b);manifest.append(dict(path=rel,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 f=visual/'current-run-visual-evidence.json';b=f.read_bytes();z.writestr(f.name,b);manifest.append(dict(path=f.name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
 z.writestr('README-OFFSCREEN.txt','Automated logical simulation only. Separate UiReview build. Not actual desktop screenshots, native input or physical DPI approval. First attempt timeout preserved separately. Current manifest 106 captures, original limits unchanged.')
with zipfile.ZipFile(archive) as z:
 for f in manifest:assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
dump(d/'LOGICAL_ARCHIVE_IDENTITY.json',dict(sourceSha=sha,path=str(archive),sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),bytes=archive.stat().st_size,captures=106,files=len(manifest)+1,readbackVerified=True,scope='OFFSCREEN ONLY; no profile/database/private media'))
dump(d/'LOGICAL_ARCHIVE_FILES.json',manifest)
# Copy explicitly selected synthetic artifacts, not business roots containing databases.
samples=pkg/'synthetic-samples';samples.mkdir(exist_ok=True)
for src,name in [(d/'planning-samples-native/images/synthetic-owned-images-summary.docx','synthetic-owned-images-summary.docx'),(d/'planning-samples-native/images/synthetic-image-status-summary.docx','synthetic-image-status-summary.docx'),(d/'planning-samples-native/images/synthetic-owned-images-layout-review.pdf','product-readback-layout-review-NOT-WPS.pdf'),(d/'planning-samples-native/summary/synthetic-15-section-summary.docx','synthetic-15-section-summary.docx')]:shutil.copy2(src,samples/name)
(samples/'README.txt').write_text('Synthetic regression examples from current published components. Product readback PDF reviewed with Poppler. Independent LibreOffice renderer unavailable; NOT Word/WPS or full cross-machine project portability acceptance.',encoding='utf-8')
# Additional current business result identity only, no personal content.
for f in sorted((d/'planning-samples-native/business').rglob('RESULT.json')):
 label='BUSINESS_SYNTHETIC_RESULT.json' if f.parent.name.startswith('business-') else 'FORMATS_SYNTHETIC_RESULT.json';shutil.copy2(f,reports/label)
selected=['INITIAL_STATE.json','BASELINE_RELATIONSHIPS.json','INHERITED_COMMITS.json','BRANCH_AUDIT_CURRENT.json','BRANCH_CURRENT_EQUIVALENCE.json','BUILD_IDENTITY.json','APP_FILES.json','DEPENDENCIES.json','PUBLISH_FULL.json','BUILD_SOLUTION.json','BUILD_WPF_X64.json','COMPONENT_BINDINGS.json','FINAL_COMPONENT_READBACK.json','DESKTOP_FINAL_START.json','DESKTOP_FINAL_INITIAL.txt','DESKTOP_FINAL_OPERATIONS.json','DESKTOP_FINAL_COMPONENT_READBACK.json','DESKTOP_ORIGINAL_CLICK.json','DESKTOP_ORIGINAL_CAPTURE.json','PRESERVATION_AFTER.json','OLD_PACKAGE_READBACK.json','READING_INDEX.json','AUTHORIZED_FULL_RUN_READBACK.json','AUTHORIZATION_PREFLIGHT.json','SCENARIO11_CURRENT.json','CURRENT_SAMPLE_INSPECTION.json','SAMPLE_VISUAL_REVIEW.json','INDEPENDENT_DOCX_RENDER.json','INDEPENDENT_DOCX_RENDER.log','PLANNING_SAMPLE_PATH_FAILURE.json','LOGICAL_VISUAL_FIRST_FAILURE.json','LOGICAL_VISUAL_IDENTITY.json','LOGICAL_ARCHIVE_IDENTITY.json','LOGICAL_ARCHIVE_FILES.json','FULL_CORE_CORRECTED.json','DRIVER_SYNTAX_FULL_CORE_CORRECTED.json','DRIVER_SYNTAX_FULL_CORE_CORRECTED.log','RUN_CONFIGURATION.json','PRODUCE_LOGICAL_VISUAL.json','RECHECK_PRODUCE_LOGICAL_VISUAL.json','RECHECK_VALIDATE_LOGICAL_VISUAL.json']
for name in selected:shutil.copy2(d/name,reports/name)
# Rerun structural verifier, preserve exact output.
p=subprocess.run(["C:/Program Files/Python313/python.exe",str(root/'tools/whole-app-followup/verify_ledger.py'),'--delivery',str(d)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
(reports/'LEDGER_VALIDATION.log').write_text(p.stdout,encoding='utf-8')
if p.returncode:raise RuntimeError('Ledger verifier failed')
(pkg/'reports').mkdir(exist_ok=True)
for f in reports.iterdir():
 if f.is_file():shutil.copy2(f,pkg/'reports'/f.name)
(pkg/'evidence').mkdir(exist_ok=True);shutil.copy2(archive,pkg/'evidence'/archive.name)
# Final package contains app, selected evidence and synthetic examples only.
zipPath=d/(pkg.name+'.zip')
if zipPath.exists():raise FileExistsError('Never overwrite prior delivery')
files=[]
with zipfile.ZipFile(zipPath,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if any(x in rel.parts for x in ['UserData','runtime','isolated-profiles']):raise RuntimeError('Unexpected mutable data in package')
  if f.suffix.lower() in ['.db','.sqlite','.sqlite3','.raw','.raf','.cr2','.cr3','.arw','.nef']:raise RuntimeError('Unapproved data type')
  b=f.read_bytes();arc=pkg.name+'/'+rel.as_posix();z.writestr(arc,b);files.append(dict(path=arc,bytes=len(b),type=f.suffix,sha256=hashlib.sha256(b).hexdigest()))
with zipfile.ZipFile(zipPath) as z:
 assert len(z.namelist())==len(files)
 for f in files:
  if hashlib.sha256(z.read(f['path'])).hexdigest()!=f['sha256']:raise RuntimeError('ZIP readback mismatch')
identity=dict(sourceSha=sha,path=str(zipPath),bytes=zipPath.stat().st_size,sha256=hashlib.sha256(zipPath.read_bytes()).hexdigest(),files=len(files),readbackVerified=True,isSetup=False,diagnosticOnly=True,userDataIncluded=False,privateInputsIncluded=False,timezone='Europe/Berlin')
dump(d/'ZIP_IDENTITY.json',identity);dump(d/'ZIP_FILES.json',files);shutil.copy2(d/'ZIP_IDENTITY.json',reports/'ZIP_IDENTITY.json');shutil.copy2(d/'ZIP_FILES.json',reports/'ZIP_FILES.json');print(json.dumps(identity))
