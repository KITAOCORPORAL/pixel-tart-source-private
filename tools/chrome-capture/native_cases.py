"""Actual published Native Host + full desktop + SQLite. Does not claim Chrome context-menu acceptance."""
from pathlib import Path
import base64,copy,datetime,hashlib,json,os,re,sqlite3,struct,subprocess,sys,time,uuid
root=Path.cwd();delivery=Path("E:/Kitao-Deliveries/chrome-capture-v1-20261110");package=delivery/"Guangjing-ChromeCaptureV1-Diagnostic-win-x64"
out=delivery/sys.argv[1];out.mkdir(exist_ok=False)
runtime_name=sys.argv[2]if len(sys.argv)>2 else"machine01"
seed=json.loads((delivery/(runtime_name+"-DESTINATIONS.json")).read_text(encoding="utf-8-sig"));native=package/"native-host/Guangjing.Capture.NativeHost.exe"
subprocess.run([str(native),"--prepare",str(package),seed["runtime"]],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
config=json.loads((package/"native-host/capture-host-config.json").read_text(encoding="utf-8-sig"));(out/"HOST_CONFIG.json").write_text(json.dumps(config,indent=2)+"\n")
origin="chrome-extension://"+config["extensionId"]+"/";rows=[]
def native_call(request,caller=origin,raw=None):
 data=json.dumps(request,separators=(",",":"),ensure_ascii=False).encode()
 wire=raw if raw is not None else struct.pack("<I",len(data))+data
 process=subprocess.run([str(native),caller],input=wire,capture_output=True,timeout=100,creationflags=subprocess.CREATE_NO_WINDOW)
 assert process.returncode==0,(process.returncode,process.stderr.decode(errors="replace"))
 assert len(process.stdout)>=4,process.stderr.decode(errors="replace")
 count=struct.unpack("<I",process.stdout[:4])[0];assert count<=1024*1024 and len(process.stdout)==count+4
 return json.loads(process.stdout[4:])
def start():
 env=os.environ.copy();env.update(PIXEL_TART_ISOLATED_RUNTIME="1",PIXEL_TART_ISOLATED_RUNTIME_ROOT=seed["runtime"],GUANGJING_CAPTURE_PIPE=config["pipeName"],GUANGJING_CAPTURE_EXTENSION_ID=config["extensionId"])
 with(out/("desktop-"+str(len(rows))+".log")).open("wb")as log:p=subprocess.Popen([str(package/"app/KitaoPhotoSelector.exe")],env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
 end=time.monotonic()+35
 while time.monotonic()<end:
  if p.poll()is not None:raise RuntimeError("Full desktop exited: "+str(p.returncode))
  result=native_call(dict(operation="destinations"))
  if result["ok"]:return p,result
  time.sleep(.2)
 raise TimeoutError("Published desktop pipe did not become ready")
dotnet=re.search(r'dotnet="([^"]+)"',(root/"tools/whole-app-followup/build_current.py").read_text(encoding="utf-8-sig")).group(1)
env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent);env['PATH']=str(Path(dotnet).parent)+os.pathsep+env.get('PATH','')
driver=root/"tools/ChromeCapture.Acceptance/bin/Release/net10.0-windows10.0.19041.0/ChromeCapture.Acceptance.dll"
def close(p):subprocess.run([dotnet,str(driver),"close",str(p.pid)],check=True,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
def state():
 with sqlite3.connect("file:"+Path(seed["library"]["DatabasePath"]).as_posix()+"?mode=ro",uri=True)as con:
  return dict(assets=con.execute("SELECT AssetId,ContentHash,ManagedCopyPath FROM AssetItems").fetchall(),links=con.execute("SELECT ProjectId,AssetId FROM ProjectAssetLinks ORDER BY ProjectId").fetchall())
app,dest=start();assert any(x['id']==seed['library']['LibraryId']for x in dest['destinations']['libraries']);assert len(dest['destinations']['projects'])==3
image=(delivery/"fixtures01/ordinary-640.jpg").read_bytes();sha=hashlib.sha256(image).hexdigest()
request=dict(operation="capture",capture=dict(captureId=str(uuid.uuid4()),destination=dict(libraryId=seed['library']['LibraryId'],projectId=seed['projectA'],boardId=seed['board']),pageUrl="http://127.0.0.1:51291/ordinary.html",imageUrl="http://127.0.0.1:51291/ordinary-640.jpg",renderedImageUrl="http://127.0.0.1:51291/ordinary-640.jpg",pageTitle="Synthetic JPEG native bridge machine acceptance",capturedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),fileName="ordinary-640.jpg",contentSha256=sha,imageBase64=base64.b64encode(image).decode(),sourceSelection="direct"))
def check(name,request):
 result=native_call(request);rows.append(dict(name=name,result=result));(out/"PROGRESS.json").write_text(json.dumps(rows,indent=2)+"\n");return result
first=check("original_import_and_project_board_association",request);assert first['ok'],first;asset=first['receipt']['assetId'];assert first['receipt']['contentSha256'].lower()==sha
snapshot=state();assert len(snapshot['assets'])==1 and snapshot['links']==[(seed['projectA'],asset)];assert hashlib.sha256(Path(snapshot['assets'][0][2]).read_bytes()).hexdigest()==sha
replay=check("same_capture_replayed",request);assert replay['ok']and replay['receipt']['assetId']==asset and len(state()['assets'])==1
other=copy.deepcopy(request);other['capture']['captureId']=str(uuid.uuid4());other['capture']['destination'].update(projectId=seed['projectB'],boardId=None)
reused=check("same_image_other_project_reuses_asset",other);assert reused['ok']and reused['receipt']['assetId']==asset and reused['receipt']['outcome']=='Reused';assert len(state()['assets'])==1 and set(tuple(x)for x in state()['links'])=={(seed['projectA'],asset),(seed['projectB'],asset)}
subprocess.run([dotnet,str(driver),'ui',str(app.pid),str(out/'NATIVE_SOURCE_ACTION.json'),'open-source'],env=env,check=True,creationflags=subprocess.CREATE_NO_WINDOW)
close(app)
unavailable=check("desktop_closed_keeps_request_retryable",request);assert not unavailable['ok']and unavailable['retryable']
app2,_=start();restored=check("same_capture_after_full_desktop_restart",request);assert app2.pid!=app.pid and restored['ok']and restored['receipt']['assetId']==asset and len(state()['assets'])==1
with sqlite3.connect(Path(seed['runtime'])/'Data/pixel-tart.db')as con:con.execute('UPDATE Projects SET IsArchived=1 WHERE Id=?',(seed['invalidProject'],))
bad=copy.deepcopy(request);bad['capture']['captureId']=str(uuid.uuid4());bad['capture']['destination'].update(projectId=seed['invalidProject'],boardId=None)
invalid=check("invalid_destination_rejected_without_reroute",bad);assert not invalid['ok']and invalid['code']=='DestinationInvalid'and len(state()['assets'])==1
malformed=check("missing_capture_fields_rejected",dict(operation='capture',capture=dict(captureId=str(uuid.uuid4()),destination=None)));assert not malformed['ok'];assert native_call(dict(operation='destinations'))['ok']
for label,caller,raw in [('wrong_extension_origin','chrome-extension://'+'a'*32+'/',None),('oversize_native_frame',origin,struct.pack('<I',24*1024*1024+1))]:
 result=native_call(dict(operation='destinations'),caller,raw);assert not result['ok'];rows.append(dict(name=label,result=result))
close(app2)
result=dict(status='PASS_NATIVE_PROTOCOL_DESKTOP_MACHINE_CHECKS',chromeContextMenu='NOT_RUN_REQUIRES_MANUAL_EXTENSION_LOAD',productSource=__import__('subprocess').check_output(['git','rev-parse','HEAD'],text=True).strip(),firstDesktopPid=app.pid,reopenedDesktopPid=app2.pid,assetId=asset,contentSha256=sha,libraryId=seed['library']['LibraryId'],projectA=seed['projectA'],projectB=seed['projectB'],rows=rows,finalDatabaseState=state(),newRawCalls=0)
(out/'NATIVE_MACHINE_RECEIPT.json').write_text(json.dumps(result,indent=2)+"\n");print(json.dumps({k:result[k]for k in ['status','chromeContextMenu','assetId','firstDesktopPid','reopenedDesktopPid']}))
