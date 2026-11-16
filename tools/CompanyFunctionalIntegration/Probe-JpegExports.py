from pathlib import Path
import hashlib,json,struct,sys
base=Path('E:/Kitao-Deliveries/company-functional-20261116');root=base/'batch-01';session=json.loads((root/'output/company-batch.ptstudio.json').read_text('utf-8-sig'));workspace=json.loads((root/'workflow-runtime/OnlineSelection/workspace.json').read_text('utf-8-sig'));result=workspace['FinalResults'][0]
assert result['IsLocked'] and result['SelectionVersion']==1 and result['Revision']==6 and len([x for x in result['Items'] if x['Selected']])==2
def dimensions(path):
 data=Path(path).read_bytes();at=2
 while at<len(data):
  if data[at]!=255:at+=1;continue
  while data[at]==255:at+=1
  m=data[at];at+=1
  if m in (0xd9,0xda):break
  n=struct.unpack('>H',data[at:at+2])[0];chunk=data[at+2:at+n];at+=n
  if m in (0xc0,0xc1,0xc2):h,w=struct.unpack('>HH',chunk[1:5]);return w,h
 raise ValueError('No JPEG dimensions')
rows=[]
for p in sorted((root/'output/edited-delivery').glob('*.jpg')):
 data=p.read_bytes();assert data[:2]==b'\xff\xd8';at=2;width=height=precision=None;icc={}
 while at<len(data):
  if data[at]!=255:at+=1;continue
  while data[at]==255:at+=1
  marker=data[at];at+=1
  if marker in (0xd9,0xda):break
  if marker in range(0xd0,0xd8) or marker==1:continue
  length=struct.unpack('>H',data[at:at+2])[0];chunk=data[at+2:at+length];at+=length
  if marker in (0xc0,0xc1,0xc2):precision=chunk[0];height,width=struct.unpack('>HH',chunk[1:5])
  if marker==0xe2 and chunk[:12]==b'ICC_PROFILE\0':icc[chunk[12]]=chunk[14:]
 side=json.loads(Path(str(p)+'.kitao-export.json').read_text('utf-8-sig'));h=hashlib.sha256(data).hexdigest();assert side['OutputHash'].lower()==h
 source=next(t for t in session['targets'] if Path(t['path']).stem+'_仿色'==p.stem);sourcehash=hashlib.sha256(Path(source['path']).read_bytes()).hexdigest();assert side['SourceHash'].lower()==sourcehash and side['EditVersionId']==source['activeEditVersionId']
 asset=next(a for a in workspace['Assets'] if Path(a['OriginalFileName']).stem==Path(source['path']).stem);assert asset['Id'] in [x['ImageId'] for x in result['Items'] if x['Selected']]
 profile=b''.join(v for k,v in sorted(icc.items()));assert (width,height)==dimensions(source['path']) and precision==8 and len(profile)>0
 rows.append({'file':str(p),'bytes':len(data),'sha256':h,'width':width,'height':height,'bitsPerChannel':precision,'iccBytes':len(profile),'iccSha256':hashlib.sha256(profile).hexdigest(),'source':source['path'],'sourceHash':sourcehash,'editVersionId':side['EditVersionId'],'exportId':side['ExportId'],'assetId':asset['Id'],'numericParameters':[n['numericParameters'] for n in source.get('stack',{}).get('nodes',[])] if source.get('stack') else [],'sidecarOutputHashVerified':True})
assert len(rows)==2
r={'source':'49fbc2f6d47e3bacabb34878e89e37e33523939e','workflowProjectId':session['workflowProjectId'],'albumId':result['SelectionProjectId'],'selectionVersion':1,'resultRevision':6,'actualProductEncodedOutput':True,'originalInputLayoutUnchanged':True,'rows':rows,'notClaimed':'JPEG output differs from PNG selection intermediate due encoding; no byte equality across formats claimed'}
output=Path(sys.argv[1]) if len(sys.argv)>1 else base/'BATCH_ACTUAL_EXPORTS.json';assert not output.exists(),'Use a fresh receipt path';output.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n');print(json.dumps({'count':len(rows),'outputs':[dict(file=x['file'],bytes=x['bytes'],size=[x['width'],x['height']],iccBytes=x['iccBytes']) for x in rows]},ensure_ascii=True))
