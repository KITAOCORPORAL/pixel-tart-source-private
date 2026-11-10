"""Run only after the human reports completing the issued Safari steps.
Create PHONE_HUMAN_FEEDBACK.json in the private delivery root with success=true,
userText containing the user's actual report, and deviceBrowser="iPhone Safari".
Never use automated browser or DOM-adapter output as that report.
"""
from pathlib import Path
import datetime, hashlib, json, sqlite3, subprocess, sys, urllib.request, urllib.parse
root=Path.cwd(); delivery=Path(sys.argv[1] if len(sys.argv)>1 else "E:/Kitao-Deliveries/online-selection-converged-20261110")
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
context=read(delivery/"PHONE_CONTEXT.private.json")
feedback=read(Path(context["feedbackFile"]))
assert feedback.get("success") is True and feedback.get("userText","").strip(), "Human success feedback is required"
assert feedback.get("deviceBrowser")=="iPhone Safari", "Keep other browser evidence separate"
product=context["productSource"]
assert not subprocess.check_output(["git","diff",product,"HEAD","--","src"],text=True).strip()
ready=read(delivery/"PHONE_READINESS.json")
for name,expected in ready["files"].items():
    assert hashlib.sha256((Path(context["package"])/name).read_bytes()).hexdigest()==expected
issued=urllib.parse.urlsplit(context["protectedUrl"])
token=urllib.parse.parse_qs(issued.fragment)["token"][0]
request=urllib.request.Request(urllib.parse.urlunsplit((issued.scheme,issued.netloc,"/v1/client/selection/"+issued.path.rsplit("/",1)[1],"","")),headers={"Authorization":"Bearer "+token})
with urllib.request.urlopen(request,timeout=30)as response:view=json.load(response)
assert view["project"]["id"]==context["projectId"] and view["isLocked"]
receipt=view["confirmation"]
assert receipt["projectId"]==context["projectId"] and receipt["isLocked"] and len(receipt["items"])==1
assert receipt["items"][0]["imageId"]==context["assetId"] and receipt["items"][0]["selected"]
label=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
for name,method in [("collect","HttpPhase3RefreshConfirmedResultAndSyncWorkflow"),("reopen-workflow","HttpConvergedReopenReturnsToSameWorkflow")]:
    subprocess.run([sys.executable,"tools/online-converged/run_case.py","phone-"+name+"-"+label,"FullyQualifiedName=RAWSelectionAssistant.WpfTests.CanvasFavoriteAppAcceptanceTests."+method,"1",context["httpsConnection"],"phone01"],check=True)
evidence=Path(context["phoneEvidence"])
synced=read(evidence/"HTTP_SYNC.json");reopened=read(evidence/"HTTP_REOPEN_WORKFLOW.json")
assert synced["projectId"]==reopened["projectId"]==context["projectId"]
assert synced["revision"]==reopened["revision"]==receipt["revision"]
assert synced["workflowId"]==reopened["workflowId"] and reopened["returnedToWorkflow"]
db=Path(context["phoneRuntime"])/"Data/pixel-tart.db"
with sqlite3.connect("file:"+db.as_posix()+"?mode=ro",uri=True)as connection:
    links=connection.execute("SELECT Id FROM Projects WHERE SourceOnlineSelectionProjectId=?",(context["projectId"],)).fetchall()
assert links==[(synced["workflowId"],)]
result=dict(status="PASS_HUMAN_SAFARI_PC_COLLECT_REOPEN",productSource=product,
    projectId=context["projectId"],assetId=context["assetId"],humanFeedback=feedback,
    serverConfirmation=receipt,pcCollected=synced,pcReopened=reopened,
    uniqueWorkflowIdentityVerified=True,binaryBindings=ready["files"],
    browserExtensionWorkStarted=False)
out=delivery/("PHONE_FINAL_RECEIPT-"+label+".json");assert not out.exists()
out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(dict(status=result["status"],receipt=str(out),productSource=product)))
