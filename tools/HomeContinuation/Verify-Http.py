"""Real HTTP acceptance against the frozen packaged service; no browser or device claim."""
from pathlib import Path
from datetime import datetime, timezone
import argparse,hashlib,json,os,secrets,socket,subprocess,time,urllib.request,urllib.error,urllib.parse,uuid
repo=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--artifact-root',type=Path,default=repo/'artifacts/home-resume-final-20261122');args=parser.parse_args()
a=args.artifact_root.resolve()
c=json.loads((a/'BUILD_CONTEXT.json').read_text('utf-8-sig'))
run=a/'http-acceptance';index=1
while run.exists():run=a/('http-acceptance-'+str(index));index+=1
run.mkdir()
server=Path(c['package'])/'SelectionService/PixelTart.SelectionApi.Server.exe'
with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
base=f'http://127.0.0.1:{port}';owner=secrets.token_hex(32)
env=dict(os.environ,PIXELTART_SELECTION_LOCALDEV_ROOT=str(run/'service-state'),PIXELTART_SELECTION_LOCALDEV_PORT=str(port),PIXELTART_SELECTION_OWNER_KEY=owner,PIXELTART_SELECTION_PUBLIC_BASE_URL=base)
process=None;checks={};journal=[]
def start():
 global process
 process=subprocess.Popen([str(server)],cwd=server.parent,env=env,stdout=(run/'service.stdout.log').open('ab'),stderr=(run/'service.stderr.log').open('ab'),creationflags=subprocess.CREATE_NO_WINDOW)
 until=time.monotonic()+20
 while time.monotonic()<until:
  if process.poll() is not None:raise RuntimeError('Packaged service exited before readiness')
  try:
   status,value=request('GET','/health/ready')
   if status==200 and value['mode']=='LocalDevHttpV1':return value
  except (OSError,ValueError):pass
  time.sleep(.1)
 raise RuntimeError('Service readiness timed out')
def stop():
 if process is not None and process.poll() is None:process.terminate();process.wait(timeout=10)
def request(method,path,data=None,token=None,headers=None):
 h=dict(headers or {})
 if token:h['Authorization']='Bearer '+token
 if data is not None and not isinstance(data,bytes):data=json.dumps(data).encode();h['Content-Type']='application/json'
 req=urllib.request.Request(base+path,data=data,headers=h,method=method)
 try:response=urllib.request.urlopen(req,timeout=20)
 except urllib.error.HTTPError as e:response=e
 with response:
  raw=response.read();status=response.status
  value=json.loads(raw) if 'json' in response.headers.get('Content-Type','') else raw
 journal.append({'method':method,'path':path,'status':status})
 return status,value
def check(name,condition):
 checks[name]=bool(condition)
 if not condition:raise AssertionError(name)
def create(name):
 id=uuid.uuid4();data={'clientProjectId':str(id),'name':name,'clientDisplayName':'Synthetic acceptance','targetCount':1,'deadlineUtc':None}
 h={'X-Owner-Key':owner,'Idempotency-Key':'project:'+id.hex}
 status,first=request('POST','/v1/selection-projects',data,headers=h)
 check('create_'+name,status==200 and first['project']['id']==str(id))
 return id,data,h,first
try:
 ready=start()
 for blank in ['','   ']:
  id=uuid.uuid4();status,_=request('POST','/v1/selection-projects',{'clientProjectId':str(id),'name':blank,'clientDisplayName':'Synthetic','targetCount':1},headers={'X-Owner-Key':owner,'Idempotency-Key':'project:'+id.hex})
  check('blank_rejected_'+str(len(blank)),status==400)
 id,data,h,first=create('HomeResume-A');token=first['devAccessToken'];project='/v1/selection-projects/'+str(id)
 status,retry=request('POST','/v1/selection-projects',data,headers=h);check('create_retry_same_identity',status==200 and retry==first)
 other,_,_,second=create('HomeResume-B')
 status,_=request('GET',project,token=second['devAccessToken']);check('wrong_project_credential_rejected',status in (401,403,404))
 photo=repo/'artifacts/home-20261122/jpeg-inputs/HOME_0001.jpg';pixels=photo.read_bytes();sha=hashlib.sha256(pixels).hexdigest().upper();asset=uuid.uuid4()
 uploadHeaders={'Content-Type':'image/jpeg','X-Content-SHA256':sha,'Idempotency-Key':f'asset:{id.hex}:{asset.hex}:{sha}','X-Original-File-Name':photo.name}
 path=project+f'/assets/{asset}/proxy'
 status,uploaded=request('POST',path,pixels,token,uploadHeaders);check('jpeg_uploaded',status==200 and uploaded['selectionAssetId']==str(asset))
 status,retry=request('POST',path,pixels,token,uploadHeaders);check('upload_retry_same_revision',status==200 and retry==uploaded)
 rule={'targetCount':1,'minimumCount':1,'maximumCount':1,'allowExtraSelections':False,'extraSelectionPriceMinor':0,'allowComments':False,'allowFavorites':True,'allowDownload':False,'showFileNames':True,'applyWatermark':False,'deadlineUtc':None,'requirePin':False,'lockAfterConfirmation':True}
 status,published=request('POST',project+'/publish',{'rule':rule,'accessExpiresAtUtc':None},token);check('protected_link_published',status==200)
 url=urllib.parse.urlsplit(published['protectedUrl']);client=urllib.parse.parse_qs(url.fragment)['token'][0];public=first['project']['publicId'];clientPath='/v1/client/selection/'+public
 status,_=request('GET',url.path);check('page_available_http',status==200)
 status,_=request('GET',clientPath);check('missing_customer_token_rejected',status==401)
 status,view=request('GET',clientPath,token=client);check('customer_reads_own_album',status==200 and view['project']['id']==str(id))
 status,assets=request('GET',clientPath+'/assets',token=client);check('exactly_one_uploaded_asset',status==200 and len(assets['items'])==1)
 for variant in ['thumb','preview']:
  status,image=request('GET',clientPath+f'/assets/{asset}/{variant}',token=client);check(variant+'_bytes_readable',status==200 and image.startswith(b'\xff\xd8'))
 choice={'selected':True,'favorite':False,'expectedSelectionVersion':view['project']['selectionVersion'],'expectedRevision':view['project']['revision'],'operationId':str(uuid.uuid4())}
 status,chosen=request('PUT',clientPath+f'/choices/{asset}',choice,client);check('customer_chosen',status==200)
 status,retry=request('PUT',clientPath+f'/choices/{asset}',choice,client);check('choice_retry_same_revision',status==200 and all(retry.get(k)==chosen.get(k) for k in ['projectId','selectionAssetId','selectionVersion','revision','isLocked']))
 confirm={'confirmed':True,'confirmationNonce':str(uuid.uuid4()),'expectedSelectionVersion':chosen['selectionVersion'],'expectedRevision':chosen['revision']}
 status,final=request('POST',clientPath+'/confirm',confirm,client);check('customer_confirmed_locked',status==200 and final['isLocked'] and final['items'][0]['selected'])
 status,retry=request('POST',clientPath+'/confirm',confirm,client);check('confirm_retry_same_snapshot',status==200 and retry==final)
 status,desktop=request('GET',project+'/final-selection',token=token);check('desktop_receives_exact_snapshot',status==200 and desktop==final)
 stop();ready2=start();status,reopened=request('GET',project+'/final-selection',token=token);check('service_restart_persists_snapshot',status==200 and reopened==final)
 status,_=request('POST',project+'/unpublish',{},token);check('revoke_succeeded',status==200)
 status,_=request('GET',clientPath,token=client);check('revoked_token_rejected',status in (401,403,404,410))
 status,_=request('GET',url.path);check('revoked_page_invalid',status in (404,410))
 check('input_unchanged',hashlib.sha256(photo.read_bytes()).hexdigest().upper()==sha)
 receipt={'status':'PASS_REAL_PACKAGED_HTTP','atUtc':datetime.now(timezone.utc).isoformat(),'productSource':c['productSource'],'serviceExeSha256':hashlib.file_digest(server.open('rb'),'sha256').hexdigest(),'checks':checks,'projectId':str(id),'assetId':str(asset),'confirmedSelectionVersion':final['selectionVersion'],'confirmedRevision':final['revision'],'authority':'Real packaged HTTP service and clients; no browser, ordinary desktop or physical iPhone acceptance','newRawDecodes':0}
 (run/'RESULT.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
 print(json.dumps({'status':receipt['status'],'checks':len(checks),'confirmedRevision':final['revision']}))
finally:
 stop();(run/'REQUESTS.json').write_text(json.dumps(journal,indent=2),encoding='utf-8')
