from pathlib import Path
import os,sys,subprocess,json,secrets,time,urllib.request,socket,hashlib,re
root=Path.cwd();out=Path('E:/Kitao-Deliveries/online-selection-http-v1-20261109')/sys.argv[1];out.mkdir(exist_ok=False,parents=True);port=int(sys.argv[2]);binary=Path(sys.argv[3]).resolve();dotnet=re.search(r'dotnet="([^"]+)"',Path('tools/whole-app-followup/build_current.py').read_text(encoding='utf-8-sig')).group(1)
with socket.socket()as s:s.bind(('127.0.0.1',port))
owner=secrets.token_hex(32).upper();env=os.environ.copy();env.update(DOTNET_ROOT=str(Path(dotnet).parent),PIXELTART_SELECTION_LOCALDEV_ROOT=str(out/'server-state'),PIXELTART_SELECTION_LOCALDEV_PORT=str(port),PIXELTART_SELECTION_OWNER_KEY=owner)
log=(out/'service.log').open('wb');p=subprocess.Popen([dotnet,str(binary)],cwd=binary.parent,env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW);log.close()
connection={'endpoint':f'http://127.0.0.1:{port}/','ownerKey':owner,'port':port,'root':str(out/'server-state'),'dll':str(binary),'pid':p.pid,'dotnet':dotnet};(out/'PRIVATE_CONNECTION.json').write_text(json.dumps(connection,indent=2))
for _ in range(150):
 if p.poll()is not None:raise RuntimeError('Service exited; see local log')
 try:
  with urllib.request.urlopen(connection['endpoint']+'health/ready',timeout=1)as r:ready=json.load(r)
  assert ready['ready'];break
 except Exception:time.sleep(.1)
else:raise TimeoutError('Service did not become ready')
(out/'READY.json').write_text(json.dumps({'ready':True,'pid':p.pid,'endpoint':connection['endpoint'],'ownerKeySha256':hashlib.sha256(owner.encode()).hexdigest(),'mode':ready['mode'],'production':ready['production'],'dllSha256':hashlib.sha256(binary.read_bytes()).hexdigest()},indent=2)+'\n');print(json.dumps({'pid':p.pid,'endpoint':connection['endpoint'],'ready':ready}))
