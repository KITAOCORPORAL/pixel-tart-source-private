import pathlib,json,hashlib,struct,subprocess
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");app=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022/Guangjing-FullScope-Followup-Diagnostic-win-x64/app");sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4"
files=[]
for f in sorted(app.rglob('*')):
 if f.is_file():files.append(dict(path=f.relative_to(app).as_posix(),bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest(),type=f.suffix))
(d/'APP_FILES.json').write_text(json.dumps(dict(sourceSha=sha,files=files),indent=2))
deps=json.loads((app/'KitaoPhotoSelector.deps.json').read_text());rows=[]
for target,libs in deps['targets'].items():
 for lib,meta in libs.items():
  for kind in ['runtime','native','resources','runtimeTargets']:
   for asset,details in meta.get(kind,{}).items():
    if asset.endswith('/_._') or asset=='_._':continue
    candidates=[app/asset,app/pathlib.PurePosixPath(asset).name]
    if kind=='resources':candidates+=[app/(details.get('locale',''))/pathlib.PurePosixPath(asset).name]
    if kind=='runtimeTargets' and details.get('rid') not in [None,'win-x64','win']:continue
    present=next((p for p in candidates if p.is_file()),None)
    rows.append(dict(library=lib,type=kind,asset=asset,present=present is not None,resolved=str(present) if present else None))
(d/'DEPENDENCIES.json').write_text(json.dumps(dict(sourceSha=sha,rows=rows,missing=[x for x in rows if not x['present']]),indent=2))
exe=(app/'KitaoPhotoSelector.exe').read_bytes();off=struct.unpack_from('<I',exe,0x3c)[0];machine=struct.unpack_from('<H',exe,off+4)[0]
(d/'BUILD_IDENTITY.json').write_text(json.dumps(dict(sourceSha=sha,branch='validation/guangjing-whole-app-followup-20261022',build='Release win-x64 self-contained, WholeAppDiagnosticBuild=true, PhotographerTrialBuild=false',exeMachine=hex(machine),x64=machine==0x8664,files=len(files),runtimeAssets=len(rows),missing=len([x for x in rows if not x['present']]),components=[x for x in files if x['path'] in ['KitaoPhotoSelector.exe','KitaoPhotoSelector.dll','RAWSelectionAssistant.Core.dll','PixelTart.AppCore.dll','PixelTart.Modules.AssetLibrary.dll','PixelTart.Modules.TestModule.dll']],timezone='Europe/Berlin'),indent=2))
print(json.dumps(dict(files=len(files),runtimeAssets=len(rows),missing=len([x for x in rows if not x['present']]),exeMachine=hex(machine))))
