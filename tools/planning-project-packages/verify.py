import pathlib,json,hashlib,zipfile,xml.etree.ElementTree as E
r=pathlib.Path("E:/Kitao-Worktrees/planning-project-packages-20261022");d=pathlib.Path("E:/Kitao-Deliveries/planning-project-packages-20261022");app=pathlib.Path("E:/Kitao-Deliveries/planning-project-packages-20261022/Planning-ProjectPackages-Diagnostic-win-x64/app");sha="9040e320d871f05c1013da9ed45bb04d48257616"
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
s=json.loads((d/'synthetic-final/RESULT.json').read_text(encoding='utf-8-sig'));package=pathlib.Path(s['package']);m=None
with zipfile.ZipFile(package) as z:
 m=json.loads(z.read('manifest.json'));assert len(z.namelist())==len(m['Files'])+1
 for f in m['Files']:assert len(z.read(f['Path']))==f['Bytes'] and hashlib.sha256(z.read(f['Path'])).hexdigest().lower()==f['Sha256'].lower()
 imported=pathlib.Path(s['importedRoot']);copies=[f for f in m['Files'] if f['Path'].startswith('planning/')]
 for f in copies:assert h(imported/f['Path']).lower()==f['Sha256'].lower()
assert h(s['summary']).lower()==s['summarySha256'].lower();assert h(package).lower()==s['packageSha256'].lower()
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
with zipfile.ZipFile(s['summary']) as z:
 doc=E.fromstring(z.read('word/document.xml'));headings=[p for p in doc.findall('.//w:p',ns) if p.find('w:pPr/w:pStyle',ns) is not None and p.find('w:pPr/w:pStyle',ns).get('{'+ns['w']+'}val')=='Heading1'];assert len(headings)==15;assert len(doc.findall('.//w:drawing',ns))==1
files=[dict(path=f.relative_to(app).as_posix(),bytes=f.stat().st_size,sha256=h(f)) for f in sorted(app.rglob('*')) if f.is_file()];deps=json.loads((app/'KitaoPhotoSelector.deps.json').read_text());assets=[]
for libs in deps['targets'].values():
 for library,info in libs.items():
  for kind in ['runtime','native','resources','runtimeTargets']:
   for name,v in info.get(kind,{}).items():
    if name.endswith('/_._') or (kind=='runtimeTargets' and v.get('rid') not in [None,'win-x64','win']):continue
    candidates=[app/name,app/pathlib.PurePosixPath(name).name]
    if kind=='resources':candidates.append(app/v.get('locale','')/pathlib.PurePosixPath(name).name)
    assets.append(dict(library=library,kind=kind,path=name,present=any(p.is_file() for p in candidates)))
assert all(a['present'] for a in assets)
bind=json.loads((d/'FINAL_BINDINGS.json').read_text());assert all(h(x['test'])==h(x['published'])==x['sha256'] for x in bind['bindings'])
for name,value in [('APP_FILES.json',files),('DEPENDENCIES.json',assets),('VERIFIED_PACKAGE.json',dict(sourceSha=sha,manifest=m,packageSha256=h(package),importedRoot=str(imported),copyHashesMatch=True,summaryHeadings=15,summaryImages=1,sourceAndOldProjectUnchanged=s['sourceAndOldPlanningUnchanged'],applicationFiles=len(files),runtimeAssets=len(assets),missingDependencies=0,publishBindings=len(bind['bindings']),scope='Independent archive/XML/file readback of current synthetic production outputs, NOT native mouse/WPS'))]:
 (d/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(applicationFiles=len(files),assets=len(assets),packageFiles=len(m['Files'])+1,sha256=h(package),copiedPayloads=len(copies),headings=15)))
