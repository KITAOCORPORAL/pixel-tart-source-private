from pathlib import Path
import argparse,json,struct,array
p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]/'artifacts/legacy-export-memory-20261122');p.add_argument('--output',required=True,type=Path);a=p.parse_args();assert not a.output.exists()
def blocks(path):
 with path.open('rb') as f:
  header=f.read(8);assert header[:4]==b'II*\0';f.seek(struct.unpack_from('<I',header,4)[0]);n=struct.unpack('<H',f.read(2))[0];tags={}
  for _ in range(n):
   t,k,c,v=struct.unpack('<HHII',f.read(12));tags[t]=(k,c,v)
  def values(tag):
   kind,count,value=tags[tag];assert kind==4
   if count==1:return [value]
   f.seek(value);return list(struct.unpack('<'+'I'*count,f.read(count*4)))
  for offset,count in zip(values(273),values(279),strict=True):
   f.seek(offset)
   while count:
    data=f.read(min(count,1048576));assert data;count-=len(data);yield data
reports=json.loads((a.root/'PIXEL_AND_TIFF_COMPARISON.json').read_text());result={}
for label,report in reports.items():
 result[label]={};probes=report['probes']
 for name,left,right in [('before-after','before','after'),('after-reopen','after','reopen')]:
  count=diff=0
  for x,y in zip(blocks(Path(probes[left]['file'])),blocks(Path(probes[right]['file'])),strict=True):
   assert len(x)==len(y);count+=len(x)//2
   if x!=y:diff+=sum(l!=r for l,r in zip(array.array('H',x),array.array('H',y),strict=True))
  result[label][name]={'samplesCompared':count,'differentSamples':diff};assert diff==0
sony=json.loads((a.root/'SONY_PARTIAL_COMPARISON.json').read_text())['probes'];count=0
for x,y in zip(blocks(Path(sony['before']['file'])),blocks(Path(sony['after']['file'])),strict=True):assert x==y;count+=len(x)//2
result['sony-first-only']={'before-after':{'samplesCompared':count,'differentSamples':0}}
a.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(result))
