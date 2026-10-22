import pathlib,json,hashlib
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");src=d/'authorized-r3-fresh-output';files=list(src.rglob('TWO_PHOTO_RESULTS.json'))
rows=[]
for f in files:
 v=json.loads(f.read_text(encoding='utf-8-sig'));rows.append(dict(path=str(f),sha256=hashlib.sha256(f.read_bytes()).hexdigest(),photos=len(v),assertions=[{k:x.get(k) for k in ['scope','paperChanges','frameChanges','latestWins','developExact','variants','reopenExact']} for x in v],exports=[dict(format=e['format'],sha256=e['sha256']) for x in v for e in x['exports']]))
(d/'AUTHORIZED_FULL_RUN_READBACK.json').write_text(json.dumps(dict(sourceSha="13ec9ca1c678622e5c7b884d8f69882c409c33a4",results=rows,scope='Bounded 768px authorized derivatives, automatic view model flow, not RAW/native clicks'),indent=2));print(json.dumps(rows))
