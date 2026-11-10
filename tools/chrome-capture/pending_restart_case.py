from pathlib import Path
import base64,datetime,hashlib,json,os,re,sqlite3,struct,subprocess,sys,time,uuid
root=Path.cwd();d=Path("E:/Kitao-Deliveries/chrome-capture-v1-20261110");package=d/"Guangjing-ChromeCaptureV1-Diagnostic-win-x64";out=d/sys.argv[1];out.mkdir(exist_ok=False)
runtime_name=sys.argv[2]if len(sys.argv)>2 else"machine03"
seed=json.loads((d/(runtime_name+"-DESTINATIONS.json")).read_text());config=json.loads((package/"native-host/capture-host-config.json").read_text());host=package/"native-host/Guangjing.Capture.NativeHost.exe";origin="chrome-extension://"+config["extensionId"]+"/"
def call(body):
 data=json.dumps(body).encode();p=subprocess.run([str(host),origin],input=struct.pack('<I',len(data))+data,capture_output=True,timeout=100,creationflags=subprocess.CREATE_NO_WINDOW);assert p.returncode==0,p.stderr.decode(errors='replace');n=struct.unpack('<I',p.stdout[:4])[0];return json.loads(p.stdout[4:4+n])
image=(d/"fixtures01/srcset-original-1600.jpg").read_bytes();sha=hashlib.sha256(image).hexdigest().upper();capture=str(uuid.uuid4())
body=dict(operation='capture',capture=dict(captureId=capture,destination=dict(libraryId=seed['library']['LibraryId'],projectId=seed['projectB'],boardId=None),pageUrl='http://127.0.0.1:51291/srcset.html',imageUrl='http://127.0.0.1:51291/srcset-original-1600.jpg',renderedImageUrl='http://127.0.0.1:51291/srcset-thumb-320.jpg',sourceSelection='srcset-1600w',pageTitle='Synthetic original pending while desktop unavailable',capturedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),fileName='srcset-original-1600.jpg',contentSha256=sha,imageBase64=base64.b64encode(image).decode()))
unavailable=call(body);assert not unavailable['ok']and unavailable['retryable']
staging=Path(seed['runtime'])/'BrowserCapture/originals'/sha;staging.mkdir(parents=True,exist_ok=True);partial=staging/('srcset-original-1600.jpg.'+capture.replace('-','')+'.part');assert not partial.exists();partial.write_bytes(b'incomplete synthetic crash cache')
env=os.environ.copy();env.update(PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=seed['runtime'],GUANGJING_CAPTURE_PIPE=config['pipeName'],GUANGJING_CAPTURE_EXTENSION_ID=config['extensionId'])
with(out/'desktop.log').open('wb')as log:desktop=subprocess.Popen([str(package/'app/KitaoPhotoSelector.exe')],env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
end=time.monotonic()+35
while time.monotonic()<end:
 if call(dict(operation='destinations'))['ok']:break
 time.sleep(.2)
else:raise TimeoutError('Restart desktop not ready')
result=call(body);assert result['ok'],result;assert result['receipt']['contentSha256'].upper()==sha
with sqlite3.connect('file:'+Path(seed['library']['DatabasePath']).as_posix()+'?mode=ro',uri=True)as con:rows=con.execute('SELECT AssetId,ManagedCopyPath,ContentHash FROM AssetItems WHERE ContentHash=?',(sha,)).fetchall()
assert len(rows)==1 and hashlib.sha256(Path(rows[0][1]).read_bytes()).hexdigest().upper()==sha
replay=call(body);assert replay['ok']and replay['receipt']['assetId']==result['receipt']['assetId']
dotnet=re.search(r'dotnet="([^"]+)"',(root/'tools/whole-app-followup/build_current.py').read_text(encoding='utf-8-sig')).group(1);env['DOTNET_ROOT']=str(Path(dotnet).parent)
subprocess.run([dotnet,str(root/'tools/ChromeCapture.Acceptance/bin/Release/net10.0-windows10.0.19041.0/ChromeCapture.Acceptance.dll'),'close',str(desktop.pid)],env=env,check=True,creationflags=subprocess.CREATE_NO_WINDOW)
receipt=dict(status='PASS_NEW_PENDING_IMAGE_AFTER_RESTART_AND_INTERRUPTED_CACHE',scope='Actual native host/full desktop. Extension IndexedDB and Chrome right-click remain separate manual acceptance.',captureId=capture,assetId=result['receipt']['assetId'],contentSha256=sha,desktopUnavailable=unavailable,importedAfterRestart=result,replay=replay,staleIncompleteCacheDidNotPoisonRetry=True,managedFileHashMatches=True,chromeContextMenu='NOT_RUN',desktopPid=desktop.pid)
(out/'RECEIPT.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(dict(status=receipt['status'],assetId=receipt['assetId'])))
