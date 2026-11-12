from pathlib import Path
import argparse,subprocess,json,datetime
p=argparse.ArgumentParser();p.add_argument('--output',required=True);a=p.parse_args()
command="Get-PnpDevice -PresentOnly | Where-Object {$_.Class -in @('Camera','Image','WPD') -or $_.FriendlyName -match 'Canon|Nikon|Sony.*Camera|FUJIFILM|Panasonic.*Camera|Olympus|OM Digital|EOS'} | Select-Object Status,Class,FriendlyName | ConvertTo-Json -Compress"
r=subprocess.run(['powershell.exe','-NoProfile','-Command',command],capture_output=True,text=True,encoding='utf-8')
if r.returncode:raise RuntimeError('Device enumeration failed; absence cannot be concluded')
text=r.stdout.strip();devices=json.loads(text)if text else[]
if isinstance(devices,dict):devices=[devices]
result={'status':'OS_DEVICE_ENUMERATION_COMPLETE','observedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'deviceCount':len(devices),'devices':devices,'currentProductProvider':'WatchFolder','vendorSdkCaptureImplemented':False,'physicalCaptureAttempted':False,'realCameraConnectionVerified':False,'feedback':'No present camera/image/WPD device found. The product can still monitor an explicit folder.'if not devices else'Device presence only; current product has no vendor SDK or verified camera model support.','scope':'Windows present-device metadata without serial numbers; device presence is not a connected capture session.'}
Path(a.output).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
