from pathlib import Path
import os,subprocess,json,sys,re
r=Path(__file__).resolve().parents[2];a=r/'artifacts/home-20261115';env=dict(os.environ,AGENT_DESKTOP_HOME=str(a/'desktop-private'))
args=sys.argv[1:];label=None;match=None
if "--match" in args:i=args.index("--match");match=args[i+1];args=args[:i]+args[i+2:]
if '--save' in args:i=args.index('--save');label=args[i+1];args=args[:i]+args[i+2:]
if args and args[0]=='invoke-name':
 name=args[1];role=args[2] if len(args)>2 else 'button'
 snap=subprocess.run(['agent-desktop.cmd','snapshot','--app','KitaoPhotoSelector.exe','--compact','--max-depth','18','--timeout-ms','20000'],env=env,capture_output=True,text=True,encoding='utf-8');view=json.loads(snap.stdout);matches=[]
 def locate(n):
  if n.get('name')==name and n.get('role')==role and 'offscreen' not in n.get('states',[]) and 'disabled' not in n.get('states',[]):matches.append(n)
  for c in n.get('children',[]):locate(c)
 locate(view.get('data',{}).get('tree',{}))
 if len(matches)!=1:print(json.dumps({'matches':len(matches),'name':name}));sys.exit(2)
 args=['click',matches[0]['ref_id'],'--timeout-ms','20000']
x=subprocess.run(['agent-desktop.cmd',*args],env=env,capture_output=True,text=True,encoding='utf-8');result=json.loads(x.stdout) if x.stdout else dict(error=x.stderr)
if label:(a/'native').mkdir(exist_ok=True);(a/'native'/f'{label}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
if args and args[0]=='snapshot' and result.get('ok'):
 def walk(n):
  if 'offscreen' in n.get('states',[]):return
  name=n.get('name','');ref=n.get('ref_id','');value=n.get('value','')
  if (name or ref) and (match is None or re.search(match,name)):print(ref,n.get('role',''),name,value)
  for c in n.get('children',[]):walk(c)
 walk(result['data']['tree']);print('WINDOW',result['data'].get('window'))
else:print(json.dumps(result,ensure_ascii=False))
sys.exit(x.returncode)
