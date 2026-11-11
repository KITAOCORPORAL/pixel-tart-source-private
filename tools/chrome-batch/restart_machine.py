from pathlib import Path
import json,os,subprocess,sys,datetime
package=Path(sys.argv[1]);config=json.loads((package/"native-host/capture-host-config.json").read_text(encoding="utf-8-sig"))
env=os.environ.copy();env.update(PIXEL_TART_ISOLATED_RUNTIME="1",PIXEL_TART_ISOLATED_RUNTIME_ROOT=config['dataRoot'],GUANGJING_CAPTURE_PIPE=config['pipeName'],GUANGJING_CAPTURE_EXTENSION_ID=config['extensionId'])
log=Path(config['dataRoot'])/("restart-"+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+".log")
with log.open('wb')as stream:p=subprocess.Popen([str(package/'app/KitaoPhotoSelector.exe')],env=env,stdout=stream,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
print(json.dumps(dict(pid=p.pid,log=str(log))))
