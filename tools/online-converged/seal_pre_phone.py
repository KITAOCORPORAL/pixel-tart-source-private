from pathlib import Path
from collections import Counter
import hashlib, json, shutil, sqlite3, subprocess, zipfile
root=Path.cwd(); delivery=Path("E:/Kitao-Deliveries/online-selection-converged-20261110")
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
freeze=read(delivery/"FINAL_PRODUCT_FREEZE.json");product=freeze["productSource"];package=Path(freeze["package"])
docs=root/"docs/validation/online-selection-converged-20261110"
assert not subprocess.check_output(["git","diff",product,"HEAD","--","src"],text=True).strip()
def digest(path):
    with path.open("rb")as stream:return hashlib.file_digest(stream,"sha256").hexdigest()
assert digest(package/"app/raw_r.dll")=="f500c0732feb21b188d5b52cea05fd824d5b3c8016eb2ca68d8312acc9f914b9"
for name in ["app/KitaoPhotoSelector.dll","app/RAWSelectionAssistant.Core.dll","selection-service/PixelTart.SelectionApi.Server.dll"]:
    assert product.encode()in(package/name).read_bytes()
for row in read(delivery/"FINAL_PUBLISHED_BINDINGS.json"):
    assert digest(Path(row["published"]))==row["sha256"]==digest(Path(row["test"]))
initial=read(delivery/"regression01/SUMMARY.json");recheck=read(delivery/"schema-recheck01/RECEIPT.json")
assert len(recheck["results"])==3 and all(x["outcome"]=="Passed"for x in recheck["results"])
fixed={x["testName"]:x for x in recheck["results"]};final=[]
for run in initial["runs"]:
    for result in run["results"]:
        value=fixed.get(result["testName"],result)
        final.append(dict(kind=run["kind"],fqn=run["fqn"],testName=value["testName"],outcome=value["outcome"],rechecked=result["testName"]in fixed))
assert all(x["outcome"]=="Passed"for x in final)
counts=Counter(x["kind"]for x in final)
app_dirs=["final-offline01","final-publish01","final-wrong-project01","final-sync01","final-reopen01","final-reopen-workflow01"]
for name in app_dirs:
    receipt=read(delivery/name/"RECEIPT.json");assert receipt["exitCode"]==0 and len(receipt["results"])==1 and receipt["results"][0]["outcome"]=="Passed"
for mode in ["exe","cmd"]:
    smoke=read(delivery/("final-normal-"+mode)/"SMOKE.json");assert smoke["appExit"]==smoke["launcherExit"]==0
cases=read(delivery/"customer-cases-final01/SUMMARY.json");assert len(cases["cases"])==4 and all(x["passed"]for x in cases["cases"])
phone=read(delivery/"PHONE_READINESS.json");assert phone["productSource"]==product and not phone["isLocked"]and phone["selected"]==0 and phone["humanFeedback"]is None
assert not(delivery/"PHONE_HUMAN_FEEDBACK.json").exists()
reopened=read(delivery/"actual02-app-evidence/HTTP_REOPEN_WORKFLOW.json")
with sqlite3.connect("file:"+(delivery/"actual02-app-runtime/Data/pixel-tart.db").as_posix()+"?mode=ro",uri=True)as connection:
    matches=connection.execute("SELECT Id,SourceOnlineSelectionProjectId FROM Projects WHERE SourceOnlineSelectionProjectId=?",(reopened["projectId"],)).fetchall()
    schema=connection.execute("SELECT max(Version) FROM SchemaInfo").fetchone()[0]
assert matches==[(reopened["workflowId"],reopened["projectId"])] and schema==8
sql_receipt=dict(schemaVersion=schema,selectionProjectId=reopened["projectId"],workflowProjectId=reopened["workflowId"],uniqueReverseBinding=True,reopened=reopened)
identity=read(package/"BUILD_IDENTITY.json");identity.update(planningSections=15,tutorial="RETIRED",installer=False,rawNativeUnchanged=True)
identity["files"]={name:dict(sha256=digest(package/name),bytes=(package/name).stat().st_size)for name in ["app/KitaoPhotoSelector.exe","app/KitaoPhotoSelector.dll","app/RAWSelectionAssistant.Core.dll","app/raw_r.dll","selection-service/PixelTart.SelectionApi.Server.exe","selection-service/PixelTart.SelectionApi.Server.dll","selection-service/wwwroot/selection-client.js"]}
(package/"BUILD_IDENTITY.json").write_text(json.dumps(identity,indent=2)+"\n",encoding="utf-8")
(package/"README.txt").write_text("光镜 在线选片整合版 · 全模块诊断包\n\n产品源码："+product+"\n基线8e447075；选择性移植c36e1f38确认回执校验及响应丢失恢复。\n\n运行Start-OnlineSelection-LocalTest.cmd，启动包内自包含LocalDev和完整桌面，并生成合成JPEG。在线选片：创建并导入照片→上传/重试代理→等服务确认→带规则发布→复制受保护链接。在本机浏览器勾选和确认后，PC刷新客户结果→交付结果→同步归片工作区。\n确认回执校验项目/素材/版本/修订/锁定/时间；响应丢失从服务已保存回执恢复。schema8保存唯一反向归片身份；普通保存和重开不丢失。旧库升级保留原备份，旧缺失身份记录不按名字猜测绑定。\n停止本机测试服务用Stop-OnlineSelection-LocalTest.cmd；普通Start-Guangjing-Diagnostic.cmd保留None provider模式。配置和凭据仅在私有测试根。\n待用户操作的HTTPS相册和连接凭据不打入本包；本机测试与手机待测相册使用独立数据根。收到真实Safari反馈后才收回并出最终人机回执。\nWholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全部模块/策划15项/教程退役保持，无安装器。本轮无RAW解码或native替换。浏览器扩展继续等待首轮验收。\n",encoding="utf-8")
secrets=set()
for p in delivery.rglob("PRIVATE*CONNECTION*.json"):
    value=read(p).get("ownerKey")
    if value:secrets.add(value.encode())
for p in delivery.rglob("PRIVATE_OWNER.json"):
    value=read(p).get("owner")
    if value:secrets.add(value.encode())
from urllib.parse import urlsplit,parse_qs
for p in delivery.rglob("SHARE_LINK.secret.txt"):
    value=parse_qs(urlsplit(p.read_text(encoding="utf-8-sig").strip()).fragment).get("token",[""])[0]
    if value:secrets.add(value.encode())
files=sorted(p for p in package.rglob("*")if p.is_file())
for p in files:
    assert not p.is_symlink()
    assert not any(part.lower()in ["data","objects","test-assets","__pycache__"]for part in p.relative_to(package).parts)
    assert p.suffix.lower()not in [".raf",".arw",".jpg",".jpeg",".png",".pdb",".log",".trx"]
    data=p.read_bytes();assert all(secret not in data for secret in secrets),p
manifest={p.relative_to(package).as_posix():dict(bytes=p.stat().st_size,sha256=digest(p))for p in files}
archive=delivery/(package.name+".zip");assert not archive.exists()
with zipfile.ZipFile(archive,"x",zipfile.ZIP_DEFLATED,compresslevel=6)as z:
    for p in files:z.write(p,p.relative_to(package).as_posix())
with zipfile.ZipFile(archive)as z:
    assert len(z.namelist())==len(manifest) and z.testzip()is None
    for name,expected in manifest.items():
        with z.open(name)as stream:assert hashlib.file_digest(stream,"sha256").hexdigest()==expected["sha256"]
zip_identity=dict(path=str(archive),sha256=digest(archive),bytes=archive.stat().st_size,fileCount=len(files),perFileReadback=True,knownCredentialsExcluded=len(secrets),privatePhoneLinkExcluded=True)
summary=dict(productSource=product,testSupportSha=subprocess.check_output(["git","rev-parse","HEAD"],text=True).strip(),regressionFinal=dict(counts=dict(counts),passed=len(final),failed=0,originalFailures=3,rechecked=3),nativePhases=len(app_dirs),totalSelectedTrxPass=len(final)+len(app_dirs),customerJsActualHttpCases=4,realBrowserConfirmation=True,publishedDllBindings=30,rawCalls=0,phoneStatus="WAITING_FOR_USER",phonePostHumanPcCollect="NOT_RUN",browserExtension="DEFERRED")
def write(name,value):(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
write("BUILD_IDENTITY.json",identity);write("PACKAGE_MANIFEST.json",manifest);write("ZIP_IDENTITY.json",zip_identity);write("TEST_SUMMARY.json",summary);write("REGRESSION_FINAL.json",final);write("FINAL_SQLITE_IDENTITY.json",sql_receipt)
for source,target in [(delivery/"regression01/SUMMARY.json","REGRESSION_INITIAL_SUMMARY.json"),(delivery/"schema-recheck01/RECEIPT.json","SCHEMA_RECHECK.json"),(delivery/"PHONE_READINESS.json","PHONE_READINESS.json"),(delivery/"FAMILY_ALBUM_SEPARATE_RECEIPT.json","FAMILY_ALBUM_SEPARATE_RECEIPT.json"),(delivery/"REOPEN_FAILURE_CAUSE_181DE897.json","CANDIDATE_REOPEN_FAILURE_CAUSE.json"),(delivery/"FINAL_CUSTOMER_BROWSER_RECEIPT.json","CUSTOMER_BROWSER_RECEIPT.json")]:shutil.copy2(source,docs/target)
for source,target in [(delivery/"final-service/server-state/requests.jsonl","FINAL_SERVICE_REQUESTS.jsonl"),(delivery/"fault-proxy-final/requests.jsonl","FAULT_BOUNDARY_REQUESTS.jsonl")]:shutil.copy2(source,docs/target)
for name in ["HTTP_OFFLINE.json","HTTP_PUBLISHED.json","HTTP_WRONG_PROJECT_REJECTED.json","HTTP_SYNC.json","HTTP_REOPEN.json","HTTP_REOPEN_WORKFLOW.json"]:shutil.copy2(delivery/"actual02-app-evidence"/name,docs/name)
for name in app_dirs:
    target=docs/"native-phases"/name;target.mkdir(parents=True,exist_ok=True)
    for file in ["RECEIPT.json","results.trx"]:shutil.copy2(delivery/name/file,target/file)
for mode in ["normal","confirm-wrong-project","confirm-drop-after-commit","block-view-after-confirm"]:shutil.copy2(delivery/"customer-cases-final01"/mode/"CUSTOMER_JS_RECEIPT.json",docs/("JS_"+mode+".json"))
images=docs/"screenshots";images.mkdir(exist_ok=True)
shutil.copy2(delivery/"FINAL_CUSTOMER_BROWSER_CONFIRMED.png",images/"BROWSER-confirmed-final.png")
shutil.copy2(delivery/"actual02-app-evidence/OFFSCREEN-converged-reopened-workflow.png",images/"OFFSCREEN-reopened-correct-workflow.png")
print(json.dumps(dict(summary=summary,package=zip_identity),indent=2))
