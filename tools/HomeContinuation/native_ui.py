from pathlib import Path
import json,os,re,subprocess,sys
repo=Path(__file__).resolve().parents[2];artifacts=repo/'artifacts/home-native-final-20261122';env=dict(os.environ,AGENT_DESKTOP_HOME=str(repo/'artifacts/home-20261122/desktop-private'))
args=sys.argv[1:];label=None;pattern=None
for flag in ['--save','--match']:
 if flag in args:
  i=args.index(flag);value=args[i+1];args=args[:i]+args[i+2:]
  if flag=='--save':label=value
  else:pattern=value
if args[0] in ['click-id','set-id','click-name']:
 operation,target=args[:2];wid=args[args.index('--window-id')+1]
 observed=subprocess.run(['agent-desktop.cmd','snapshot','--app','KitaoPhotoSelector.exe','--window-id',wid,'--compact','--max-depth','18','--timeout-ms','15000'],env=env,capture_output=True,text=True,encoding='utf-8')
 snap=json.loads(observed.stdout)
 if label:(artifacts/'native'/(label+'-before.json')).write_text(json.dumps(snap,ensure_ascii=False,indent=2),encoding='utf-8')
 matches=[]
 def collect(n):
  if 'offscreen' in n.get('states',[]):return
  match=n.get('native_id',{}).get('value')==target if operation!='click-name' else n.get('name')==target and n.get('role')=='button'
  if match and (n.get('role')=='textfield' if operation=='set-id' else n.get('role')=='button'):matches.append(n)
  for ch in n.get('children',[]):collect(ch)
 if snap.get('ok'):collect(snap['data']['tree'])
 if len(matches)!=1 or not matches[0].get('ref_id'):print(json.dumps({'error':'Control identity is not unique/actionable','matches':len(matches),'target':target}));sys.exit(2)
 args=['set-value',matches[0]['ref_id'],args[2],'--timeout-ms','12000'] if operation=='set-id' else ['click',matches[0]['ref_id'],'--timeout-ms','12000']
p=subprocess.run(['agent-desktop.cmd',*args],env=env,capture_output=True,text=True,encoding='utf-8')
try:v=json.loads(p.stdout)
except ValueError:print(p.stdout,p.stderr);sys.exit(p.returncode)
if label:(artifacts/'native'/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def sanitize(text):return re.sub('([#&](?:token|key)=)[^&\\s"\'<>]+',lambda m:m.group(1)+'[PRIVATE]',str(text))
if args[0]=='snapshot' and v.get('ok'):
 def walk(n):
  if 'offscreen' in n.get('states',[]):return
  name=n.get('name','');ref=n.get('ref_id','')
  if (name or ref) and (pattern is None or re.search(pattern,name)):print(ref,n.get('role'),sanitize(name),sanitize(n.get('value','')),n.get('native_id',''),n.get('bounds',''))
  for c in n.get('children',[]):walk(c)
 walk(v['data']['tree']);print('WINDOW',v['data'].get('window'))
else:print(sanitize(json.dumps(v,ensure_ascii=False)))
sys.exit(p.returncode)
