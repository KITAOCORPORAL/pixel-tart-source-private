import pathlib,json,hashlib,zipfile
r=pathlib.Path("E:/Kitao-Worktrees/planning-shot-link-lifecycle-company-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-shot-link-lifecycle-company-20261023");app=pathlib.Path("E:/Kitao-Deliveries/planning-shot-link-lifecycle-company-20261023/Planning-ShotLinks-Verified-Diagnostic-win-x64/app");sha="3cd7543050722b2449a75ec7115e70aa29dcff6b"
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
files=[dict(path=f.relative_to(app).as_posix(),bytes=f.stat().st_size,sha256=h(f)) for f in sorted(app.rglob('*')) if f.is_file()];deps=json.loads((app/'KitaoPhotoSelector.deps.json').read_text());assets=[]
for libs in deps['targets'].values():
 for library,info in libs.items():
  for kind in ['runtime','native','resources','runtimeTargets']:
   for name,v in info.get(kind,{}).items():
    if name.endswith('/_._') or (kind=='runtimeTargets' and v.get('rid') not in [None,'win-x64','win']):continue
    candidates=[app/name,app/pathlib.PurePosixPath(name).name]
    if kind=='resources':candidates.append(app/v.get('locale','')/pathlib.PurePosixPath(name).name)
    assets.append(dict(library=library,path=name,present=any(p.is_file() for p in candidates)))
assert all(x['present'] for x in assets)
b=json.loads((d/'VERIFIED_BINDINGS.json').read_text());assert all(h(x['published'])==h(x['test'])==x['sha256'] for x in b['bindings'])
s=json.loads((d/'synthetic-verified/RESULT.json').read_text(encoding='utf-8-sig'))
with zipfile.ZipFile(s['owned']) as z:
 m=json.loads(z.read('manifest.json'))
 for f in m['Files']:assert hashlib.sha256(z.read(f['Path'])).hexdigest().lower()==f['Sha256'].lower()
 state=json.loads(z.read('planning.json'));links=state['Document']['ShotExternalLinks'];assert len(links)==1 and links[0]['ProjectCopy'] is not None
 imported=json.loads((pathlib.Path(s['isolatedRoot'])/'planning'/(s['project'].replace('-','')+'.planning.json')).read_text(encoding='utf-8-sig'));assert imported['Document']['ShotExternalLinks'][0]['ReconnectedFile'] is None
 copy=links[0]['ProjectCopy'];assert h(pathlib.Path(s['isolatedRoot'])/'planning'/copy['RelativePath']).lower()==copy['Sha256'].lower();assert copy['Sha256']==s['selectedHash']
for name,value in [('APP_FILES.json',files),('DEPENDENCIES.json',assets),('VERIFIED_OUTPUTS.json',dict(productSha=sha,appFiles=len(files),runtimeAssets=len(assets),missing=0,bindingCount=len(b['bindings']),ownedPackageSha256=h(s['owned']),selectedSha256=h(s['selected']),machineApprovalStripped=True,isolatedCopyVerified=True,result=s))]:(d/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(files=len(files),assets=len(assets),bindings=len(b['bindings']),ownedPackageSha=h(s['owned']))))
