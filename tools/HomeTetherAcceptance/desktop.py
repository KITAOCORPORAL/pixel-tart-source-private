from pathlib import Path
import json,os,subprocess,sys,re
repo=Path(__file__).resolve().parents[2];art=repo/'artifacts/home-tether-20261123';env=dict(os.environ,AGENT_DESKTOP_HOME=str(art/'desktop-private'));args=sys.argv[1:];label=None
if '--save' in args:i=args.index('--save');label=args[i+1];args=args[:i]+args[i+2:]
p=subprocess.run(['agent-desktop.cmd',*args],env=env,capture_output=True,text=True,encoding='utf-8');
try:data=json.loads(p.stdout)
except Exception:print(p.stdout,p.stderr);raise SystemExit(p.returncode)
if label:(art/'native'/(label+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
if args[0]=='snapshot' and data.get('ok'):
 def walk(n):
  if 'offscreen' in n.get('states',[]):return
  if n.get('role') in ['button','textbox','combobox','checkbox','radiobutton','tab','tabitem','menuitem'] or (n.get('role')=='statictext' and re.search('项目|相机|会话|看守|连接|设备|快门|目录|照片|无|张|联机|选片|启动|存储|准备|运行',n.get('name',''))):print(n.get('ref_id',''),n.get('role'),n.get('name',''),n.get('value',''),n.get('states',[]))
  for x in n.get('children',[]):walk(x)
 walk(data['data']['tree'])
else:print(json.dumps(data,ensure_ascii=False))
raise SystemExit(p.returncode)
