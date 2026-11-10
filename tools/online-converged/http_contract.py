from pathlib import Path
import json,uuid,urllib.request,urllib.error,hashlib,time,sys
from PIL import Image
base=Path('E:/Kitao-Deliveries/online-selection-converged-20261110');conn=json.loads(Path(sys.argv[1]if len(sys.argv)>1 else base/'dev-http01/PRIVATE_CONNECTION.json').read_text(encoding='utf-8-sig'));out=base/(sys.argv[2]if len(sys.argv)>2 else'http-contract01');out.mkdir(exist_ok=False)
p=out/'proxy.jpg';Image.new('RGB',(64,48),(225,140,60)).save(p,'JPEG',quality=85);image=p.read_bytes();requests=[]
def request(method,path,body=None,token=None,headers=None,expected=200):
 h=dict(headers or {});data=body
 if body is not None and not isinstance(body,bytes):data=json.dumps(body).encode();h['Content-Type']='application/json'
 if token:h['Authorization']='Bearer '+token
 req=urllib.request.Request(conn['endpoint'].rstrip('/')+path,data=data,headers=h,method=method)
 try:
  with urllib.request.urlopen(req,timeout=15)as r:status=r.status;b=r.read()
 except urllib.error.HTTPError as e:status=e.code;b=e.read()
 requests.append({'method':method,'path':path,'status':status,'expected':expected});assert status==expected,(method,path,status,b.decode(errors='replace'));return json.loads(b)if b else None
id=str(uuid.uuid4());project={'name':'HTTP contract synthetic','clientDisplayName':'test client','targetCount':1,'deadlineUtc':None,'clientProjectId':id};h={'X-Owner-Key':conn['ownerKey'],'Idempotency-Key':'project:'+id.replace('-','')}
a=request('POST','/v1/selection-projects',project,headers=h);b=request('POST','/v1/selection-projects',project,headers=h);assert a['project']['id']==b['project']['id']==id and a['devAccessToken']==b['devAccessToken'];owner=a['devAccessToken'];asset=str(uuid.uuid4());sha=hashlib.sha256(image).hexdigest().upper();upload={'Content-Type':'image/jpeg','X-Original-File-Name':'synthetic.jpg','X-Content-SHA256':sha,'Idempotency-Key':f'asset:{id.replace("-","")}:{asset.replace("-","")}:{sha}'}
a=request('POST',f'/v1/selection-projects/{id}/assets/{asset}/proxy',image,owner,upload);b=request('POST',f'/v1/selection-projects/{id}/assets/{asset}/proxy',image,owner,upload);assert a['selectionAssetId']==b['selectionAssetId']==asset and a['revision']==b['revision'];assert a['uploadSha256']==sha
(out/'PRIVATE_OWNER.json').write_text(json.dumps({'id':id,'asset':asset,'owner':owner}));(out/'REQUESTS.json').write_text(json.dumps({'requests':requests,'projectId':id,'assetId':asset,'proxySha256':sha,'idempotencyProject':True,'idempotencyUpload':True,'newRawCalls':0},indent=2)+'\n');print(json.dumps({'requests':len(requests),'project':id,'asset':asset,'idempotent':True}))
