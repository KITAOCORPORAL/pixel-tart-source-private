import pathlib,json,hashlib,subprocess,collections,time,sys
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4"
def git(*args,cwd=root):return subprocess.check_output(['git','-c','core.quotepath=false',*args],cwd=cwd,text=True,encoding='utf-8')
start=json.loads((d/'INITIAL_STATE.json').read_text());dirty=[]
for w in start['states']:
 if not w.get('status'):continue
 entries=git('status','--porcelain=v1','-uall',cwd=w['path']).splitlines()
 for line in entries:
  rel=line[3:]
  if ' -> ' in rel:rel=rel.split(' -> ')[1]
  p=pathlib.Path(w['path'])/rel
  try:
   if p.is_file():dirty.append(dict(worktree=w['path'],path=rel,bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
  except OSError as ex:dirty.append(dict(worktree=w['path'],path=rel,error=str(ex)))
(d/'DIRTY_CONTENT_BEFORE.json').write_text(json.dumps(dict(scope='Captured after build setup; other worktrees not edited by this task',files=dirty),indent=2))
old=pathlib.Path('E:/Kitao-Deliveries/guangjing-whole-app-validation-20261020/Guangjing-FullScope-Diagnostic-final-win-x64.zip');identity=json.loads((old.parent/'ZIP_IDENTITY.json').read_text());digest=hashlib.sha256(old.read_bytes()).hexdigest()
(d/'OLD_PACKAGE_READBACK.json').write_text(json.dumps(dict(path=str(old),sha256=digest,recorded=identity,match=identity['sha256']==digest),indent=2))
refs=git('for-each-ref','--format=%(refname:short)','refs/remotes/origin').splitlines();branches=[]
for ref in refs:
 if ref=='origin/HEAD':continue
 head=git('rev-parse',ref).strip()
 try:base=git('merge-base',sha,head).strip()
 except subprocess.CalledProcessError:base=None
 names=git('diff','--name-status',base,head,'--','src','tests','tools').splitlines() if base else []
 current=git('diff','--name-status',sha,head,'--','src','tests','tools').splitlines()
 branches.append(dict(ref=ref,sha=head,mergeBase=base,isAncestor=head==base,isDescendant=sha==base,branchOnlyChanges=names,fullDiffFromCurrent=current,claims='Not merged or cherry-picked by this followup. Presence is not inferred from branch names.'))
(d/'BRANCH_AUDIT_CURRENT.json').write_text(json.dumps(dict(sourceSha=sha,refs=branches),indent=2))
oldsha='46ba7e29d93660f47f836bd4f60169c894f371ca'
(d/'INHERITED_COMMITS.json').write_text(json.dumps(dict(base=oldsha,sourceSha=sha,commits=git('log','--format=%H %s',oldsha+'..'+sha).splitlines(),productDiff=git('diff','--name-status',oldsha,sha,'--','src','tests','tools').splitlines(),selection='Existing single descendant of original validation and all reviewed company/home fixes; no merge/cherry-pick. Dirty owned-resource/desktop-entry branches excluded.'),indent=2))
print(json.dumps(dict(dirtyFiles=len(dirty),branches=len(branches),oldPackageUnchanged=digest==identity['sha256'])))
