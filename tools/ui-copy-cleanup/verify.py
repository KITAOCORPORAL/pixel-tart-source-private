import pathlib,json,hashlib
r=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026");d=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");app=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026/Guangjing-UI-Cleanup-Final-Diagnostic-win-x64/app")
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
files=[dict(path=f.relative_to(app).as_posix(),bytes=f.stat().st_size,sha256=h(f)) for f in sorted(app.rglob('*')) if f.is_file()];deps=json.loads((app/'KitaoPhotoSelector.deps.json').read_text());assets=[]
for libs in deps['targets'].values():
 for lib,info in libs.items():
  for kind in ['runtime','native','resources','runtimeTargets']:
   for name,v in info.get(kind,{}).items():
    if name.endswith('/_._') or (kind=='runtimeTargets' and v.get('rid') not in [None,'win-x64','win']):continue
    choices=[app/name,app/pathlib.PurePosixPath(name).name]
    if kind=='resources':choices.append(app/v.get('locale','')/pathlib.PurePosixPath(name).name)
    assets.append(dict(library=lib,path=name,present=any(p.is_file() for p in choices)))
assert all(x['present'] for x in assets)
b=json.loads((d/'SEALED_BINDINGS.json').read_text());assert all(h(x['published'])==h(x['test'])==x['sha256'] for x in b['bindings'])
for name,value in [('APP_FILES.json',files),('DEPENDENCIES.json',assets),('BUILD_READBACK.json',dict(productSha='b9cb1e25e811903b117aef3afeb203aadf7d0790',testSha='0505216f8aacbcc67ea10f7584a8469767afe923',appFiles=len(files),assets=len(assets),missing=0,publishTestBindings=len(b['bindings']),allMatch=True))]:(d/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(appFiles=len(files),assets=len(assets),bindings=len(b['bindings']))))
