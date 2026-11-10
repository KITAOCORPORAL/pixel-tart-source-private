"""Read-only export of only this task's synthetic company-Chrome captures. No unrelated URLs/assets."""
from pathlib import Path
import hashlib,json,sqlite3,sys,datetime
root=Path(sys.argv[1] if len(sys.argv)>1 else "E:/Kitao-Deliveries/chrome-capture-v1-20261110")
context=json.loads((root/"CHROME_CAPTURE_CONTEXT.json").read_text(encoding="utf-8-sig"));seed=json.loads(Path(context["companyDestinations"]).read_text(encoding="utf-8-sig"))
base=context["testBaseUrl"];fixture_bases={base,"http://127.0.0.1:51291"};allowed={origin+"/"+page for origin in fixture_bases for page in ["ordinary.html","srcset.html","unavailable.html"]};path=Path(seed["runtime"])/"BrowserCapture/captures.sqlite"
if not path.exists():print(json.dumps(dict(status="WAITING_FOR_REAL_CHROME_CAPTURE",records=0)));sys.exit(0)
with sqlite3.connect("file:"+path.as_posix()+"?mode=ro",uri=True)as connection:
    records=[json.loads(x[0])for x in connection.execute("SELECT RecordJson FROM BrowserCaptures WHERE State='Completed'")]
records=[x for x in records if x["PageUrl"]in allowed]
assert not any(x["PageUrl"].endswith("/unavailable.html")for x in records),"Unavailable original unexpectedly produced a completed capture"
with sqlite3.connect("file:"+Path(seed["library"]["DatabasePath"]).as_posix()+"?mode=ro",uri=True)as connection:
    assets=[dict(assetId=x[0],contentHash=x[1],managedFile=x[2])for x in connection.execute("SELECT AssetId,ContentHash,ManagedCopyPath FROM AssetItems")]
known={x['assetId']:x for x in assets}
for record in records:
    asset=known.get(record["AssetId"]);assert asset
    with Path(asset['managedFile']).open('rb')as stream:actual=hashlib.file_digest(stream,'sha256').hexdigest()
    assert actual.upper()==record['ContentHash'].upper()
label=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ');output=root/("COMPANY_CHROME_CAPTURE_RECEIPTS-"+label+".json");assert not output.exists()
result=dict(status="REAL_CHROME_CAPTURE_RECORDS_FOUND"if records else"WAITING_FOR_REAL_CHROME_CAPTURE",productSource=context["productSource"],scope="Only own synthetic test-page URLs; requires actual Chrome action evidence before declaring full acceptance",records=records,assetCount=len(assets),chromeContextMenuUserEvidenceRequired=True)
output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(status=result['status'],records=len(records),output=str(output))))
