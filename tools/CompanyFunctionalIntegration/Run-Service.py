"""Start/reuse only the configured company's existing local diagnostic services."""
from pathlib import Path
import argparse,json,os,subprocess,time,urllib.request
p=argparse.ArgumentParser();p.add_argument('--case',choices=['Batch','Original','Phone'],required=True);a=p.parse_args();package=Path(__file__).resolve().parents[1];paths=json.loads((package/'company-paths.json').read_text('utf-8-sig'));c=json.loads(Path(paths['cases'][a.case]['context']).read_text('utf-8-sig'));env=os.environ.copy()
if a.case=='Phone':
 root=Path(c['workflowQaRuntime']);endpoint='http://127.0.0.1:17662/';mode='MobileScheduleV1';args=[str(package/'ScheduleService/Guangjing.Schedule.Server.exe'),'--Schedule:Root='+str(root),'--Schedule:OwnerKeyFile='+str(root/'schedule-owner.key'),'--Schedule:Port=17662','--Schedule:PublicBaseUrl='+endpoint.rstrip('/'),'--contentRoot='+str(package/'ScheduleService')];cwd=package/'ScheduleService';private=None
else:
 private_path=Path(c['selectionPrivatePath']);private=json.loads(private_path.read_text('utf-8-sig'));root=Path(private['root']);endpoint=private['endpoint'];mode='LocalDevHttpV1';from urllib.parse import urlsplit
 env.update(PIXELTART_SELECTION_LOCALDEV_ROOT=str(root),PIXELTART_SELECTION_LOCALDEV_PORT=str(urlsplit(endpoint).port),PIXELTART_SELECTION_OWNER_KEY=private['ownerKey'],PIXELTART_SELECTION_PUBLIC_BASE_URL=endpoint.rstrip('/'));args=[str(package/'SelectionService/PixelTart.SelectionApi.Server.exe')];cwd=package/'SelectionService'
def ready():
 with urllib.request.urlopen(endpoint+'health/ready',timeout=2) as response:return json.load(response)
try:h=ready()
except Exception:h=None
if h:
 assert h.get('mode')==mode
 if private:assert h.get('instanceId')==private['instanceId'],'Another selection instance owns the endpoint'
 print(json.dumps({'ready':True,'reusedService':True,'case':a.case}));raise SystemExit(0)
assert root.exists();logs=Path(paths['runRoot']);logs.mkdir(parents=True,exist_ok=True);stamp=str(time.time_ns());stdout=(logs/(a.case+'-service-'+stamp+'.stdout.log')).open('w');stderr=(logs/(a.case+'-service-'+stamp+'.stderr.log')).open('w');process=subprocess.Popen(args,cwd=cwd,env=env,stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW)
for _ in range(100):
 try:
  h=ready()
  if h.get('mode')==mode:break
 except Exception:time.sleep(.1)
else:raise RuntimeError('Configured service did not become ready')
if private:
 private.update(pid=process.pid,instanceId=h['instanceId']);private_path.write_text(json.dumps(private,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'ready':True,'startedServicePid':process.pid,'case':a.case,'source':paths['productSource'],'runtimePreserved':True}))
