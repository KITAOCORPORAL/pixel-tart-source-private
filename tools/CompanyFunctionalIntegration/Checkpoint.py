from pathlib import Path
from datetime import datetime,timezone
import argparse,sqlite3,json,hashlib
p=argparse.ArgumentParser();p.add_argument('context');p.add_argument('output');a=p.parse_args();c=json.loads(Path(a.context).read_text('utf-8-sig'));root=Path(c['workflowQaRuntime']);out={'atUtc':datetime.now(timezone.utc).isoformat(),'source':c['workflowAppProductSource'],'pid':c.get('workflowAppPid'),'projectId':c['workflowProjectId'],'root':str(root)}
with sqlite3.connect((root/'Data/pixel-tart.db').resolve().as_uri()+'?mode=ro',uri=True) as db:
 db.row_factory=sqlite3.Row;pid=c['workflowProjectId'];row=db.execute('SELECT Id,Name,ProjectType,RootPath,SourceOnlineSelectionProjectId FROM Projects WHERE lower(Id)=lower(?)',(pid,)).fetchone();out['project']=dict(row) if row else None;out['sources']=[dict(r) for r in db.execute('SELECT Path,Priority FROM ProjectSources WHERE lower(ProjectId)=lower(?)',(pid,))];out['inputs']=[dict(r) for r in db.execute('SELECT OriginalInput FROM SelectionInputs WHERE lower(ProjectId)=lower(?)',(pid,))];out['projectCount']=db.execute('SELECT COUNT(*) FROM Projects').fetchone()[0]
 if c.get('workflowBookingId'):
  row=db.execute('SELECT Id,ProjectId,Revision FROM ShootBookings WHERE lower(Id)=lower(?)',(c['workflowBookingId'],)).fetchone();out['booking']=dict(row) if row else None
w=root/'OnlineSelection/workspace.json'
if w.exists():
 d=json.loads(w.read_text('utf-8-sig'));out['albums']=[dict(id=x['Id'],name=x['Name'],workflowId=x.get('WorkflowProjectId'),status=x['Status']) for x in d['Projects'] if x.get('WorkflowProjectId')==c['workflowProjectId']];out['results']=[dict(albumId=x['SelectionProjectId'],version=x['SelectionVersion'],revision=x['Revision'],locked=x['IsLocked'],selected=[i['ImageId'] for i in x['Items'] if i['Selected']]) for x in d['FinalResults'] if x['SelectionProjectId']==out['project']['SourceOnlineSelectionProjectId']];out['assets']=[dict(id=x['Id'],projectId=x['ProjectId'],file=x['OriginalFileName'],path=x['LocalSourcePath'],cloudId=x.get('CloudAssetId')) for x in d['Assets'] if x['ProjectId']==out['project']['SourceOnlineSelectionProjectId']]
original=Path(c.get('sourceDirectory','E:/Kitao-Deliveries/product-functional-closure-20261112/synthetic-inputs/source-jpegs'));out['originalFiles']=[dict(path=str(f),bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in original.glob('*.jpg')];out['newRawDecodes']=0
path=Path(a.output);assert not path.exists();path.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(out,ensure_ascii=True))
