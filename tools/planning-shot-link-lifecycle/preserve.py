import pathlib,subprocess,json,hashlib
r=pathlib.Path("E:/Kitao-Worktrees/planning-shot-link-lifecycle-company-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-shot-link-lifecycle-company-20261023");start=json.loads((d/'START.json').read_text(encoding='utf-8'));rows=[];changes=[]
for w in start['worktrees']:
 head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=w['path'],text=True).strip();status=subprocess.check_output(['git','-c','core.quotepath=false','status','--porcelain=v1','-uall'],cwd=w['path'],text=True,encoding='utf-8');rows.append(dict(path=w['path'],headUnchanged=head==w['head'],statusUnchanged=status==w['status']))
 for f in w['files']:
  try:actual=hashlib.sha256((pathlib.Path(w['path'])/f['path']).read_bytes()).hexdigest()
  except OSError:actual=None
  if actual!=f['sha256']:changes.append(dict(worktree=w['path'],path=f['path'],actual=actual))
old=start['oldPackage'];result=dict(worktrees=rows,dirtyFiles=sum(len(x['files']) for x in start['worktrees']),dirtyChanges=changes,allHeadsPreserved=all(x['headUnchanged'] for x in rows),allStatusesPreserved=all(x['statusUnchanged'] for x in rows),oldPackageUnchanged=hashlib.sha256(pathlib.Path(old['path']).read_bytes()).hexdigest()==old['sha256'],timezone='Europe/Berlin')
(d/'PRESERVATION.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='worktrees'}))
