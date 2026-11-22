"""Resume the preserved home calendar over existing HTTPS. Never mutate bookings."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,sqlite3,socket,subprocess,time,urllib.request,urllib.parse,urllib.error,uuid
repo=Path(__file__).resolve().parents[2];a=repo/'artifacts/home-resume-final-20261122';c=json.loads((a/'BUILD_CONTEXT.json').read_text('utf-8-sig'))
prior=repo.parent/'guangjing-product-functional-home-20261115/artifacts/home-20261115';home=json.loads((prior/'CONTEXT.json').read_text('utf-8-sig'));runtime=Path(home['runtime']);marker=json.loads((prior/'PHONE_HANDOFF_ACTIVE.json').read_text('utf-8-sig'));booking=marker['bookingId']
private=a/'phone-private';private.mkdir(exist_ok=True);db=runtime/'Data/pixel-tart.db'
def load(p):return json.loads(p.read_text('utf-8-sig'))
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def tables():
 with sqlite3.connect(db.resolve().as_uri()+'?mode=ro',uri=True) as connection:
  names=[row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table' AND name != 'ScheduleDevices' AND name NOT LIKE 'sqlite_%'")]
  result={}
  for name in names:
   rows=connection.execute('SELECT * FROM "'+name.replace('"','""')+'"').fetchall();canonical=json.dumps(sorted(rows,key=repr),ensure_ascii=False,default=lambda x:x.hex() if isinstance(x,bytes) else str(x),separators=(',',':')).encode();result[name]={'rows':len(rows),'sha256':hashlib.sha256(canonical).hexdigest()}
  row=connection.execute('SELECT Revision,StartAtUtc,EndAtUtc,TimeZoneId FROM ShootBookings WHERE Id=?',(booking,)).fetchone();assert row,'Preserved home booking missing'
 return result,{'id':booking,'revision':row[0],'startAtUtc':row[1],'endAtUtc':row[2],'timeZoneId':row[3]}
def request(base,path,method='GET',body=None,headers=None):
 data=json.dumps(body).encode() if body is not None else None;h=dict(headers or {})
 if data is not None:h['Content-Type']='application/json'
 req=urllib.request.Request(base+path,data=data,headers=h,method=method)
 try:response=urllib.request.urlopen(req,timeout=15)
 except urllib.error.HTTPError as e:response=e
 with response:
  raw=response.read();value=json.loads(raw) if 'json' in response.headers.get('Content-Type','') else raw
  return response.status,value
state=json.loads(subprocess.check_output(['C:/Program Files/Tailscale/tailscale.exe','status','--json'],text=True,encoding='utf-8'));assert state['BackendState']=='Running' and state.get('Self',{}).get('Online'),'Tailnet is not online'
serve=json.loads(subprocess.check_output(['C:/Program Files/Tailscale/tailscale.exe','serve','status','--json'],text=True,encoding='utf-8'));targets=[host for host,cfg in serve.get('Web',{}).items() if cfg.get('Handlers',{}).get('/',{}).get('Proxy')=='http://127.0.0.1:17716'];assert len(targets)==1,'Existing home HTTPS route is not unique';public='https://'+targets[0];assert serve.get('TCP',{}).get('8444',{}).get('HTTPS'),'HTTPS not configured'
before,priorBooking=tables();save(private/'BUSINESS_BEFORE.json',before)
backup=private/'calendar-before.sqlite3'
if not backup.exists():
 with sqlite3.connect(db.resolve().as_uri()+'?mode=ro',uri=True) as origin,sqlite3.connect(backup) as destination:origin.backup(destination)
server=Path(c['package'])/'ScheduleService/Guangjing.Schedule.Server.exe';pidfile=private/'SERVICE.json'
try:status,health=request('http://127.0.0.1:17716','/health/ready')
except OSError:status,health=0,None
if not health:
 with socket.socket() as probe:probe.bind(('127.0.0.1',17716))
 process=subprocess.Popen([str(server),'--Schedule:Root='+str(runtime),'--Schedule:OwnerKeyFile='+str(runtime/'schedule-owner.key'),'--Schedule:Port=17716','--Schedule:PublicBaseUrl='+public,'--contentRoot='+str(server.parent)],cwd=server.parent,stdout=(private/'service.stdout.log').open('ab'),stderr=(private/'service.stderr.log').open('ab'),stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save(pidfile,{'pid':process.pid,'exe':str(server),'runtime':str(runtime),'productSource':c['productSource'],'exeSha256':hashlib.file_digest(server.open('rb'),'sha256').hexdigest()})
 until=time.monotonic()+20
 while time.monotonic()<until:
  if process.poll() is not None:raise RuntimeError('Home schedule service exited')
  try:
   status,health=request('http://127.0.0.1:17716','/health/ready')
   if status==200:break
  except OSError:pass
  time.sleep(.1)
else:assert pidfile.exists() and load(pidfile)['exe']==str(server),'An unowned schedule service occupies the existing route'
assert status==200 and health['mode']=='MobileScheduleV1'
status,health=request(public,'/health/ready');assert status==200 and health['mode']=='MobileScheduleV1','HTTPS route did not reach the home service'
status,page=request(public,'/calendar');assert status==200 and b'calendar-client.js' in page
status,js=request(public,'/calendar-client.js');assert status==200 and hashlib.sha256(js).digest()==hashlib.sha256((repo/'src/Guangjing.Schedule.Server/wwwroot/calendar-client.js').read_bytes()).digest(),'Served page is not the current source'
operation=private/'DEVICE_OPERATION.json'
if not operation.exists():save(operation,{'id':str(uuid.uuid4()),'name':'iPhone home continuation 20261122','validHours':48})
op=load(operation);owner=(runtime/'schedule-owner.key').read_text('utf-8-sig').strip()
status,issued=request(public,'/v1/schedule/devices','POST',{'name':op['name'],'validHours':op['validHours']},{'X-Owner-Key':owner,'Idempotency-Key':op['id']});assert status==200
save(private/'DEVICE_PRIVATE.json',issued);url=issued['url']+'&booking='+booking
(private/'PHONE_URL_PRIVATE.txt').write_text(url+chr(10),encoding='utf-8')
credential=urllib.parse.parse_qs(urllib.parse.urlsplit(url).fragment)['token'][0];headers={'Authorization':'Bearer '+credential}
status,session=request(public,'/v1/schedule/session',headers=headers);assert status==200 and session['device']['id']==issued['device']['id']
status,remote=request(public,'/v1/schedule/bookings/'+booking,headers=headers);assert status==200 and remote['revision']==priorBooking['revision']
status,without=request(public,'/v1/schedule/bookings/'+booking);assert status==401
save(private/'PHONE_HANDOFF_ACTIVE.json',{'bookingId':booking,'device':issued['device'],'urlFile':str(private/'PHONE_URL_PRIVATE.txt'),'mutatingAutomatedProbesPaused':True,'realDeviceResult':'NOT_RUN','priorMarkerPreserved':True})
after,postBooking=tables();assert before==after and priorBooking==postBooking,'Business rows changed during preparation'
receipt={'status':'HTTPS_DEVICE_AND_HISTORICAL_BOOKING_READY_PHONE_NOT_RUN','atUtc':datetime.now(timezone.utc).isoformat(),'productSource':c['productSource'],'tailnetOnline':True,'existingHttpsRouteReused':True,'newDeviceExpiresAtUtc':issued['device']['expiresAtUtc'],'homeBooking':priorBooking,'companyRevision4NotPresentInHome':True,'businessTablesUnchanged':after,'bookingMutations':0,'phoneBrowserNotOpened':True,'physicalIPhoneAcceptance':'NOT_RUN','privateUrlFile':str(private/'PHONE_URL_PRIVATE.txt'),'originalPhoneMarkerPreserved':True,'servedJsMatchesFrozenSource':True,'unauthenticatedReadStatus':status}
save(a/'PHONE_READINESS.json',receipt)
print(json.dumps({'status':receipt['status'],'homeRevision':priorBooking['revision'],'businessTablesVerified':len(after),'expiresAtUtc':receipt['newDeviceExpiresAtUtc']},ensure_ascii=False))
