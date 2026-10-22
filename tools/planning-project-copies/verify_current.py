import pathlib,json,hashlib,xml.etree.ElementTree as E,zipfile,subprocess
r=pathlib.Path("E:/Kitao-Worktrees/planning-project-copies-20261022");d=pathlib.Path("E:/Kitao-Deliveries/planning-project-copies-20261022");app=pathlib.Path("E:/Kitao-Deliveries/planning-project-copies-20261022/Planning-ProjectCopies-Diagnostic-win-x64/app");sha="3710317f8c2681141cb7d0e959e78ab9f9bb6037"
def hash(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
files=[dict(path=f.relative_to(app).as_posix(),bytes=f.stat().st_size,sha256=hash(f)) for f in sorted(app.rglob('*')) if f.is_file()]
(d/'APP_FILES.json').write_text(json.dumps(dict(sourceSha=sha,files=files),indent=2))
deps=json.loads((app/'KitaoPhotoSelector.deps.json').read_text());assets=[]
for libs in deps['targets'].values():
 for library,info in libs.items():
  for kind in ['runtime','native','resources','runtimeTargets']:
   for name,v in info.get(kind,{}).items():
    if name.endswith('/_._'):continue
    if kind=='runtimeTargets' and v.get('rid') not in [None,'win-x64','win']:continue
    candidates=[app/name,app/pathlib.PurePosixPath(name).name]
    if kind=='resources':candidates.append(app/v.get('locale','')/pathlib.PurePosixPath(name).name)
    assets.append(dict(library=library,kind=kind,asset=name,present=any(p.is_file() for p in candidates)))
(d/'DEPENDENCIES.json').write_text(json.dumps(dict(sourceSha=sha,assets=assets,missing=[x for x in assets if not x['present']]),indent=2))
assert all(x['present'] for x in assets)
bindings=json.loads((d/'FINAL_BINDINGS.json').read_text());assert all(hash(x['test'])==x['sha256']==hash(x['published']) for x in bindings['bindings'])
result=json.loads((d/'synthetic-final/RESULT.json').read_text(encoding='utf-8-sig'));moved=d/'synthetic-final/relocated-synthetic-project';copies=[]
for copy in result['copies']:
 path=moved/'planning'/copy['RelativePath'];assert hash(path).lower()==copy['Sha256'].lower();copies.append(dict(relative=copy['RelativePath'],sha256=hash(path),bytes=path.stat().st_size,originalPathAbsent=not pathlib.Path(copy['OriginalPath']).exists()))
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'};docx=d/'synthetic-final/relocated-project-summary.docx';headings=[];drawings=[]
with zipfile.ZipFile(docx) as z:
 doc=E.fromstring(z.read('word/document.xml'));text=''.join(doc.itertext());section=None
 for p in doc.find('w:body',ns):
  style=p.find('w:pPr/w:pStyle',ns)
  if style is not None and style.get('{'+ns['w']+'}val')=='Heading1':section=''.join(p.itertext());headings.append(section)
  for drawing in p.findall('.//w:drawing',ns):drawings.append(section)
 assert len(headings)==15 and len(drawings)==2 and drawings[0].startswith('04 ') and drawings[1].startswith('06 ')
 assert 'call-sheet.txt' in text and 'SHA-256已核对' in text and '缺失文件' in text
 assert len([n for n in z.namelist() if '/media/' in '/'+n])==2
current=dict(sourceSha=sha,scope='Independent ZIP/XML/hash readback of synthetic current outputs; not physical WPS/native acceptance',copies=copies,headings=headings,drawingSections=drawings,docxSha256=hash(docx),sourceHashesVerifiedDuringImport=True,externalOnlyNotEmbedded=True,applicationFiles=len(files),runtimeAssets=len(assets),missingDependencies=0,publishTestDllBindings=len(bindings['bindings']),dllBindingsMatch=True)
(d/'FINAL_READBACK.json').write_text(json.dumps(current,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(current,ensure_ascii=False))
