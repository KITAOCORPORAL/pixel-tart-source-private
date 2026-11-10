"""Coordinate only the isolated real-Chrome QA root. Never reads extension private storage.
Commands: snapshot | desktop-down | desktop-up | invalidate-target | feedback <step> <verbatim-text>
Chrome UI operations remain human-operated under the existing URL policy.
"""
from pathlib import Path
import datetime,hashlib,json,os,re,sqlite3,struct,subprocess,sys,time
BASE=Path("E:/Kitao-Deliveries/chrome-capture-v1-20261110")
read=lambda p:json.loads(Path(p).read_text(encoding="utf-8-sig"))
context=read(BASE/"CHROME_FIVE_CASE_ROUND.json");repo=Path(context['worktree']);package=Path(context['actualPackage']);runtime=Path(context['runtime']);out=Path(context['evidence']);seed=read(context['seed'])
assert runtime.resolve()==Path(seed['runtime']).resolve()
config=read(package/"native-host/capture-host-config.json");assert Path(config['dataRoot']).resolve()==runtime.resolve()
identity=read(package/"BUILD_IDENTITY.json");assert identity['productSource']==context['productSource']
for name,value in identity['files'].items():
 with(package/name).open('rb')as stream:assert hashlib.file_digest(stream,'sha256').hexdigest()==value['sha256'],name
stamp=lambda:datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
def save(name,value):
 path=out/(name+'-'+stamp()+'.json');path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');return path

def native_probe():
 payload=json.dumps(dict(operation='destinations')).encode();p=subprocess.run([str(package/'native-host/Guangjing.Capture.NativeHost.exe'),'chrome-extension://'+context['extensionId']+'/'],input=struct.pack('<I',len(payload))+payload,capture_output=True,timeout=12,creationflags=subprocess.CREATE_NO_WINDOW)
 assert p.returncode==0 and len(p.stdout)>=4,p.stderr.decode(errors='replace');size=struct.unpack('<I',p.stdout[:4])[0];return json.loads(p.stdout[4:4+size])

def snapshot():
 allowed=[f'http://127.0.0.1:{port}/{page}'for port in[51291,51292]for page in['ordinary.html','srcset.html','unavailable.html']]
 if context.get('pendingFixture'):allowed.append(context['pendingFixture']['pageUrl'])
 records=[];journal=runtime/'BrowserCapture/captures.sqlite'
 if journal.exists():
  with sqlite3.connect('file:'+journal.as_posix()+'?mode=ro',uri=True)as con:
   sql="SELECT CaptureId,State,RecordJson FROM BrowserCaptures WHERE json_extract(RecordJson,'$.PageUrl') IN ("+','.join('?'for _ in allowed)+') ORDER BY CapturedAtUtc'
   records=[dict(id=x[0],state=x[1],record=json.loads(x[2]))for x in con.execute(sql,allowed)]
 with sqlite3.connect('file:'+Path(seed['library']['DatabasePath']).as_posix()+'?mode=ro',uri=True)as con:
  assets=con.execute('SELECT AssetId,DisplayName,ContentHash,Width,Height,ManagedCopyPath FROM AssetItems ORDER BY AssetId').fetchall();links=con.execute('SELECT ProjectId,AssetId,Role FROM ProjectAssetLinks ORDER BY ProjectId,AssetId,Role').fetchall()
 with sqlite3.connect('file:'+(runtime/'Data/pixel-tart.db').as_posix()+'?mode=ro',uri=True)as con:projects=con.execute('SELECT Id,Name,IsArchived FROM Projects ORDER BY Id').fetchall()
 with sqlite3.connect('file:'+(runtime/'InspirationTray/tray.sqlite').as_posix()+'?mode=ro',uri=True)as con:board_links=con.execute('SELECT ce.CollectionId,t.LibraryId,t.AssetId FROM InspirationCollectionEntries ce JOIN InspirationTrayEntries t ON t.TrayEntryId=ce.TrayEntryId ORDER BY ce.CollectionId,t.AssetId').fetchall()
 return dict(recordedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),productSource=context['productSource'],records=records,assets=assets,projectLinks=links,projects=projects,boardLinks=board_links,assetCount=len(assets),projectLinkCount=len(links),boardLinkCount=len(board_links))

def process_path(pid):
 cmd="(Get-CimInstance Win32_Process -Filter 'ProcessId="+str(int(pid))+"').ExecutablePath"
 return subprocess.check_output(['powershell.exe','-NoProfile','-Command',cmd],text=True,encoding='utf-8',creationflags=subprocess.CREATE_NO_WINDOW).strip()

def persist_context():
 (BASE/'CHROME_FIVE_CASE_ROUND.json').write_text(json.dumps(context,indent=2)+'\n',encoding='utf-8')

command=sys.argv[1]
if command=='snapshot':
 result=snapshot();path=save('SNAPSHOT',result);print(json.dumps(dict(snapshot=str(path),assetCount=result['assetCount'],projectLinkCount=result['projectLinkCount'],boardLinkCount=result['boardLinkCount'],completed=[dict(captureId=x['id'],projectId=x['record']['ProjectId'],assetId=x['record']['AssetId'],page=x['record']['PageUrl'])for x in result['records']if x['state']=='Completed']),ensure_ascii=False,indent=2))
elif command=='feedback':
 assert len(sys.argv)>3;step=sys.argv[2];text=sys.argv[3];assert text.strip();path=save('USER_FEEDBACK_'+step,dict(step=step,userText=text,recordedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),authority='Direct user feedback about actual Chrome UI; not a protocol probe'));print(path)
elif command=='desktop-down':
 pid=int(context['desktopPid']);assert Path(process_path(pid)).resolve()==(package/'app/KitaoPhotoSelector.exe').resolve()
 before=snapshot();save('CASE4_BEFORE_DESKTOP_CLOSE',before)
 dotnet=re.search(r'dotnet="([^"]+)"',(repo/'tools/whole-app-followup/build_current.py').read_text(encoding='utf-8-sig')).group(1);env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent)
 subprocess.run([dotnet,str(repo/'tools/ChromeCapture.Acceptance/bin/Release/net10.0-windows10.0.19041.0/ChromeCapture.Acceptance.dll'),'close',str(pid)],env=env,check=True,creationflags=subprocess.CREATE_NO_WINDOW)
 reply=native_probe();assert not reply['ok']and reply['retryable'];context.update(stage='CASE4_DESKTOP_OFF_WAITING_USER_NEW_CAPTURE',desktopStoppedPid=pid,desktopStoppedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat());persist_context();save('CASE4_DESKTOP_OFF_VERIFIED',dict(pid=pid,readinessProbe=reply,probeIsNotChromeCapture=True));print('Desktop closed normally; Chrome pages/queues untouched. Wait for the human to create and retain one new item.')
elif command=='desktop-up':
 before=native_probe()
 if before.get('ok'):raise RuntimeError('Desktop already ready; preserve current state and inspect case4 before another launch')
 with(out/('CASE4_DESKTOP_START-'+stamp()+'.log')).open('wb')as log:p=subprocess.run(['cmd.exe','/d','/c','Start-Guangjing-Capture.cmd'],cwd=package,stdout=log,stderr=subprocess.STDOUT,timeout=20,creationflags=subprocess.CREATE_NO_WINDOW)
 assert p.returncode==0;end=time.monotonic()+35
 while time.monotonic()<end:
  reply=native_probe()
  if reply.get('ok'):break
  time.sleep(.2)
 else:raise TimeoutError('Same-version desktop did not become ready')
 command="Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'KitaoPhotoSelector.exe'} | Select-Object ProcessId,ExecutablePath | ConvertTo-Json -Compress"
 data=json.loads(subprocess.check_output(['powershell.exe','-NoProfile','-Command',command],text=True,encoding='utf-8'));data=data if isinstance(data,list)else[data];matching=[x for x in data if x.get('ExecutablePath')and Path(x['ExecutablePath']).resolve()==(package/'app/KitaoPhotoSelector.exe').resolve()];assert len(matching)==1
 pid=matching[0]['ProcessId'];assert pid!=context.get('desktopStoppedPid');context.update(stage='CASE4_DESKTOP_REOPENED_WAITING_USER_SAME_ITEM_RETRY',desktopPid=pid,desktopReopenedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat());persist_context();save('CASE4_REOPENED_READY',dict(pid=pid,readinessProbe=reply,probeIsNotChromeCapture=True));print('Same frozen desktop ready. Human must refresh destinations and send the SAME retained item, not create another one.')
elif command=='invalidate-target':
 target=context['temporaryProjectId'];before=snapshot();row=next(x for x in before['projects']if x[0]==target);assert row[1]==context['temporaryProjectName']and row[2]==0;assert not any(x[0]==target for x in before['projectLinks']);save('CASE5_TARGET_BEFORE_INVALIDATION',dict(project=row,snapshot=before))
 with sqlite3.connect(runtime/'Data/pixel-tart.db')as con:
  count=con.execute('UPDATE Projects SET IsArchived=1,UpdatedAt=? WHERE Id=? AND Name=? AND IsArchived=0',(datetime.datetime.now(datetime.timezone.utc).isoformat(),target,context['temporaryProjectName'])).rowcount;assert count==1
 reply=native_probe();assert reply['ok']and all(x['id']!=target for x in reply['destinations']['projects']);context.update(stage='CASE5_TARGET_INVALID_WAITING_USER_SEND_WITHOUT_REFRESH',temporaryTargetInvalidatedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat());persist_context();save('CASE5_TARGET_INVALID_VERIFIED',dict(projectId=target,readinessProbe=reply,probeIsNotChromeCapture=True,snapshot=snapshot()));print('Temporary target archived, not deleted. Human must click SEND in the already selected popup WITHOUT refreshing targets.')
else:raise SystemExit('snapshot | feedback | desktop-down | desktop-up | invalidate-target')
