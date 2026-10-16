"""Summarize observed case/process chain. Historical absent telemetry stays UNAVAILABLE."""
import pathlib,json,collections,sys
root=pathlib.Path(sys.argv[1]);rows=[]
for folder in root.iterdir():
 if not folder.is_dir():continue
 def load(n):
  p=folder/n
  return [json.loads(x) for x in p.read_text(encoding='utf-8-sig').splitlines() if x.strip()] if p.exists() else []
 cases=load('case-events.jsonl');events=load('subprocess-events.jsonl')
 if not cases and not events:continue
 starts=[e for e in events if e['phase']=='start'];ends=[e for e in events if e['phase']=='end'];samples=[e['detail'] for e in events if e['phase']=='sample'];complete=collections.Counter(e['detail']['file'] for e in starts);waits=collections.Counter(t.get('wait') for s in samples for t in s['threads'] if t.get('wait'))
 rows.append(dict(directory=str(folder),directoryLength=len(str(folder)),tempLength=max([c.get('tempLength',0) for c in cases]+[0]),caseStarts=len([c for c in cases if c['phase']=='start']),caseEnds=len([c for c in cases if c['phase']=='end']),sumCaseSeconds=sum(c['elapsedMs']/1000 for c in cases if c['phase']=='end'),cases=cases,childStarts=len(starts),childEnds=len(ends),childrenByExecutable=dict(complete),sumChildSeconds=sum(e['detail']['elapsedMs']/1000 for e in ends),maxChildSeconds=max([e['detail']['elapsedMs']/1000 for e in ends]+[0]),maxChildWorkingSet=max([s['workingSet'] for s in samples]+[0]),maxChildPrivateBytes=max([s['privateBytes'] for s in samples]+[0]),threadWaitSamples=dict(waits),pipeEofWaitSnapshots=[dict(pid=s['pid'],elapsedMs=s['elapsedMs'],output=s['outputTask'],exit=s['exitTask'],error=s['errorTask']) for s in samples if s['exitTask']=='RanToCompletion' and (s['outputTask']!='RanToCompletion' or s['errorTask']!='RanToCompletion')],timeouts=[e for e in events if e['phase']=='timeout'],workingDirectories=sorted(set(e['detail']['cwd'] for e in starts))))
print(json.dumps(rows,indent=2))
