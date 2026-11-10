"""Run shipped JS against actual HTTP faults; the DOM adapter is not Safari evidence."""
from pathlib import Path
from PIL import Image
import datetime, hashlib, json, subprocess, sys, urllib.request, urllib.parse, uuid
root=Path.cwd(); delivery=Path("E:/Kitao-Deliveries/online-selection-converged-20261110")
connection=json.loads((delivery/"PRIVATE_ACTUAL_CONNECTION.json").read_text(encoding="utf-8-sig"))
out=delivery/sys.argv[1]; out.mkdir(exist_ok=False)
control=delivery/"fault-proxy02/control.json"
endpoint=connection["endpoint"].rstrip("/")
Image.new("RGB",(96,64),(221,141,45)).save(out/"SYNTHETIC.JPG","JPEG",quality=85)
image=(out/"SYNTHETIC.JPG").read_bytes(); sha=hashlib.sha256(image).hexdigest().upper()
def call(method,path,body=None,token=None,headers=None):
    h=dict(headers or {})
    if token:h["Authorization"]="Bearer "+token
    if body is not None and not isinstance(body,bytes):body=json.dumps(body).encode();h["Content-Type"]="application/json"
    with urllib.request.urlopen(urllib.request.Request(endpoint+path,data=body,headers=h,method=method),timeout=30)as response:
        assert response.status==200; return json.load(response)
summary=[]
for mode in ["normal","confirm-wrong-project","confirm-drop-after-commit","block-view-after-confirm"]:
    control.write_text('{"mode":"none"}')
    folder=out/mode; folder.mkdir()
    project=str(uuid.uuid4()); asset=str(uuid.uuid4())
    created=call("POST","/v1/selection-projects",dict(name="Converged "+mode,clientDisplayName="Synthetic test",targetCount=1,deadlineUtc=None,clientProjectId=project),headers={"X-Owner-Key":connection["ownerKey"],"Idempotency-Key":"project:"+project.replace("-","")})
    owner=created["devAccessToken"]
    call("POST",f"/v1/selection-projects/{project}/assets/{asset}/proxy",image,owner,{"Content-Type":"image/jpeg","X-Original-File-Name":"SYNTHETIC.JPG","X-Content-SHA256":sha,"Idempotency-Key":f'asset:{project.replace("-","")}:{asset.replace("-","")}:{sha}'})
    rule=dict(targetCount=1,minimumCount=0,maximumCount=1,allowExtraSelections=False,extraSelectionPriceMinor=0,allowComments=False,allowFavorites=True,allowDownload=False,showFileNames=True,applyWatermark=False,deadlineUtc=None,requirePin=False,lockAfterConfirmation=True)
    grant=call("POST",f"/v1/selection-projects/{project}/publish",dict(rule=rule,accessExpiresAtUtc=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=1)).isoformat()),owner)
    (folder/"SHARE_LINK.secret.txt").write_text(grant["protectedUrl"],encoding="utf-8")
    (folder/"PRIVATE_OWNER.json").write_text(json.dumps(dict(owner=owner,project=project,asset=asset)))
    subprocess.run(["node","tools/online-converged/probe_customer.mjs",str(folder/"SHARE_LINK.secret.txt"),"src/PixelTart.SelectionApi.Server/wwwroot/selection-client.js",str(folder/"CUSTOMER_JS_RECEIPT.json"),mode,str(control)],check=True)
    if mode!="confirm-wrong-project":
        owner_result=call("GET",f"/v1/selection-projects/{project}/final-selection",token=owner)
        assert owner_result["projectId"]==project and owner_result["isLocked"] and owner_result["items"][0]["imageId"]==asset
        (folder/"OWNER_STORED_RESULT.json").write_text(json.dumps(owner_result,indent=2)+"\n")
    else:
        token=urllib.parse.parse_qs(urllib.parse.urlsplit(grant["protectedUrl"]).fragment)["token"][0]
        view=call("GET",f'/v1/client/selection/{grant["publicId"]}',token=token)
        assert not view["isLocked"] and view["confirmation"] is None
    summary.append(dict(mode=mode,projectId=project,assetId=asset,passed=True))
(out/"SUMMARY.json").write_text(json.dumps(dict(actualHttp=True,browser=False,safari=False,cases=summary),indent=2)+"\n")
print(json.dumps(dict(cases=len(summary),passed=True)))
