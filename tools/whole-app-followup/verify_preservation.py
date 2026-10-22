import pathlib,json,subprocess,hashlib
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");before=json.loads((d/'INITIAL_STATE.json').read_text());rows=[]
for w in before['states']:
 p=w['path'];head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=p,text=True).strip();status=subprocess.check_output(['git','status','--porcelain=v1'],cwd=p,text=True,encoding='utf-8');rows.append(dict(path=p,head=head,headUnchanged=head==w['head'],statusUnchanged=status==w.get('status',''),status=status))
content=json.loads((d/'DIRTY_CONTENT_BEFORE.json').read_text());changed=[]
for x in content['files']:
 if 'sha256' not in x:continue
 try:actual=hashlib.sha256((pathlib.Path(x['worktree'])/x['path']).read_bytes()).hexdigest()
 except OSError:actual=None
 if actual!=x['sha256']:changed.append(dict(**x,actualSha256=actual))
old=json.loads((d/'OLD_PACKAGE_READBACK.json').read_text());digest=hashlib.sha256(pathlib.Path(old['path']).read_bytes()).hexdigest();result=dict(worktrees=rows,headsPreserved=all(x['headUnchanged'] for x in rows),statusesPreserved=all(x['statusUnchanged'] for x in rows),dirtyFilesChecked=len(content['files']),dirtyContentChanges=changed,oldPackageUnchanged=digest==old['sha256'],note='Changes outside this task, if any, are reported rather than reset/stashed/reverted.')
(d/'PRESERVATION_AFTER.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ['worktrees','dirtyContentChanges']}));print('dirtyChanges',len(changed))
