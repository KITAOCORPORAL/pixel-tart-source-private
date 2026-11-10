"""Generate inspectable per-user native-host files. Does not write the registry."""
from pathlib import Path
import csv,hashlib,json,os,subprocess,sys
package=Path(sys.argv[1]).resolve();identity=json.loads((package/"extension/DEVELOPMENT_ID.json").read_text(encoding="utf-8-sig"))
user=next(csv.reader(subprocess.check_output(["whoami","/user","/fo","csv","/nh"],text=True).splitlines()))[1]
runtime=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else Path(os.environ["LOCALAPPDATA"])/"KitaoDiagnostic"/("ChromeCaptureV1-"+identity["extensionId"])
pipe="guangjing.capture."+hashlib.sha256((user+"|"+str(runtime)).encode()).hexdigest()[:24]
native=package/"native-host";config=native/"capture-host-config.json"
config.write_text(json.dumps(dict(extensionId=identity["extensionId"],pipeName=pipe,dataRoot=str(runtime)),indent=2)+"\n",encoding="utf-8")
manifest=native/"com.guangjing.capture.json";manifest.write_text(json.dumps(dict(name="com.guangjing.capture",description="Guangjing explicit single image capture",path=str(native/"Guangjing.Capture.NativeHost.exe"),type="stdio",allowed_origins=["chrome-extension://"+identity["extensionId"]+"/"]),indent=2)+"\n",encoding="utf-8")
backslash=chr(92)
key="HKEY_CURRENT_USER"+backslash+backslash.join(["Software","Google","Chrome","NativeMessagingHosts","com.guangjing.capture"])
value=str(manifest).replace(backslash,backslash+backslash).replace(chr(34),backslash+chr(34))
registration=package/"Register-Guangjing-NativeHost.reg";registration.write_text("Windows Registry Editor Version 5.00\n\n["+key+"]\n@=\""+value+"\"\n",encoding="utf-16")
print(json.dumps(dict(extensionId=identity["extensionId"],registrationFile=str(registration),registryWritten=False,runtime=str(runtime))))
