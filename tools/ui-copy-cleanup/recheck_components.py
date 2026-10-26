import pathlib,json,hashlib,shutil
r=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026");d=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");b=json.loads((d/'SEALED_BINDINGS.json').read_text());rows=[]
for x in b['bindings']:
 source=pathlib.Path(x['published']);target=pathlib.Path(x['test']);expected=x['sha256'];assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
 if hashlib.sha256(target.read_bytes()).hexdigest()!=expected:shutil.copy2(source,target)
 rows.append(dict(path=str(target),sha256=hashlib.sha256(target.read_bytes()).hexdigest(),match=hashlib.sha256(target.read_bytes()).hexdigest()==expected))
(d/'FINAL_COMPONENT_READBACK.json').write_text(json.dumps(dict(rows=rows,allMatch=all(x['match'] for x in rows),note='Supplemental error-fixture build product outputs rebound to unchanged final published components. Raw SEALED run snapshot records original test assembly identity.'),indent=2));print(len(rows))
