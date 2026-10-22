import pathlib,json,csv,hashlib,zipfile,argparse
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);a=p.parse_args();d=pathlib.Path(a.delivery);r=d/'reports';valid={'PASS','FAIL','PARTIAL','NOT_RUN','BLOCKED'}
ops=json.loads((r/'OPERATIONS.json').read_text(encoding='utf-8'));pl=json.loads((r/'PLANNING_15.json').read_text(encoding='utf-8'));actions=json.loads((r/'ACTIONS.json').read_text(encoding='utf-8'));mods=json.loads((r/'MODULES.json').read_text(encoding='utf-8'));summary=json.loads((r/'TEST_SUMMARY.json').read_text(encoding='utf-8'))
assert len(pl)==15 and len(mods)==24 and len(ops)==122
for rows,key in [(ops,'ID'),(pl,'SectionID'),(actions,'ActionID')]:assert len({x[key] for x in rows})==len(rows)
for x in ops+pl+actions:
 for k in ['Status','BuildIncluded','ManualDesktop','Operation','Automated','AutoTest']:
  if k in x:assert x[k] in valid,(k,x[k])
for s in summary['suites']:assert not s['missing'] and not s['extra']
w=summary['wpfExactCoverage'];assert w['plansMatchDiscovery'] and w['sameAssembly'] and w['sameDiscovery']
for name in ['OPERATIONS','PLANNING_15','ACTIONS','MODULES']:
 with (r/(name+'.csv')).open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
 assert len(rows)==len(json.loads((r/(name+'.json')).read_text(encoding='utf-8')))
raw=json.loads((r/'RAW_TEST_EVIDENCE_MANIFEST.json').read_text(encoding='utf-8'))
with zipfile.ZipFile(r/'RAW_TEST_EVIDENCE.zip') as z:
 for x in raw['files']:assert hashlib.sha256(z.read(x['path'])).hexdigest()==x['sha256']
readback=json.loads((d/'FINAL_COMPONENT_READBACK.json').read_text());assert readback['allMatch']
print(json.dumps(dict(status='PASS',scope='Structural validation of evidence only, not product acceptance',modules=len(mods),operations=len(ops),planning=len(pl),actions=len(actions),rawFiles=len(raw['files']))))
