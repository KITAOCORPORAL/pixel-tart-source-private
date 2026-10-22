import pathlib,json,hashlib,subprocess
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4"
def git(*args):return subprocess.check_output(['git',*args],cwd=root,text=True,encoding='utf-8').strip()
refs=json.loads((d/'BRANCH_AUDIT_CURRENT.json').read_text());rows=[]
current={x.split('\t',1)[-1]:x.split('\t',1)[0] for x in git('ls-tree','-r',sha).splitlines()}
for b in refs['refs']:
 if b['isAncestor'] or b['isDescendant'] or not b['mergeBase']:continue
 files=git('diff','--name-only',b['mergeBase'],b['sha'],'--','src','tests').splitlines();same=[];different=[];missing=[]
 for name in files:
  blob=git('ls-tree',b['sha'],'--',name)
  if not blob:continue
  if name not in current:missing.append(name)
  elif blob.split('\t',1)[0].split()[2]==current[name].split()[2]:same.append(name)
  else:different.append(name)
 rows.append(dict(ref=b['ref'],sha=b['sha'],sameCurrentBlobs=same,differentCurrentBlobs=different,absentCurrentFiles=missing,claims='Blob comparison only; different code may contain equivalent functionality, not inferred.'))
(d/'BRANCH_CURRENT_EQUIVALENCE.json').write_text(json.dumps(dict(sourceSha=sha,branches=rows),indent=2));print('checked',len(rows))
