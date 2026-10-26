import subprocess,urllib.request,json,base64,pathlib
cwd="E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026"
p=subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,cwd=cwd)
v=dict(l.split('=',1) for l in p.stdout.splitlines() if '=' in l);headers={'User-Agent':'Kitao-UI-cleanup','Accept':'application/vnd.github+json'}
if v.get('password'):headers['Authorization']='Basic '+base64.b64encode((v.get('username','')+':'+v['password']).encode()).decode()
rows=[]
try:
 for state in ['open','closed']:
  for page in range(1,5):
   u=f'https://api.github.com/repos/KITAOCORPORAL/pixel-tart-source-private/issues?state={state}&per_page=100&page={page}';data=json.load(urllib.request.urlopen(urllib.request.Request(u,headers=headers),timeout=30));rows.extend(dict(number=x['number'],title=x['title'],state=x['state'],body=(x.get('body') or '')[:3000],isPr='pull_request' in x) for x in data)
   if len(data)<100:break
 result=dict(status='PASS',issues=rows)
except Exception as e:result=dict(status='BLOCKED',error=str(e),issues=rows)
pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026/ISSUES_READ.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
