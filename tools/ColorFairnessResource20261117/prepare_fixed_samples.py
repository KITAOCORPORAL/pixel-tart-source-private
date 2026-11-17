from pathlib import Path
import sys,json,numpy as np,hashlib
root=Path(sys.argv[1]);original=Path(sys.argv[2]);plan=json.loads((root/'FROZEN_PLAN.json').read_text('utf-8'));target=root/'samples';target.mkdir(exist_ok=False)
rows=[]
for i in plan['inputs']:
 p=original/i['file'];a=np.fromfile(p,dtype='<f4').reshape(384,384,3);assert hashlib.sha256(p.read_bytes()).hexdigest()==i['hash'];small=a[::4,::4].copy();q=target/i['file'];q.write_bytes(small.tobytes());rows.append({'id':i['id'],'kind':'roi','path':str(q),'source':str(p),'sourceSha256':i['hash'],'bytes':q.stat().st_size,'sha256':hashlib.sha256(q.read_bytes()).hexdigest()})
for k,v in plan['anchors'].items():
 p=original/v['file'];a=np.fromfile(p,dtype='<f4').reshape(v['shape']);q=target/v['file'];q.write_bytes(a[::4,::4].copy().tobytes());rows.append({'id':k,'kind':'anchor','path':str(q),'source':str(p),'sourceSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':q.stat().st_size,'sha256':hashlib.sha256(q.read_bytes()).hexdigest()})
(root/'SAMPLED_INPUTS.json').write_text(json.dumps(rows,indent=2)+chr(10),encoding='utf-8');print(json.dumps({'fixedSamples':len(rows),'allEvery4':True}))
