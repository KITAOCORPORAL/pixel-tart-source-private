"""Read-only dual-provider intake. Network assets never enter the repository/release."""
import argparse, csv, hashlib, io, json, zipfile
from pathlib import Path
from datetime import date
from PIL import Image

UNKNOWN = {"", "unknown", "未下载", "未知"}
def sha(data): return hashlib.sha256(data).hexdigest()
def known(value): return str(value or "").strip().lower() not in UNKNOWN
def rows(data): return list(csv.DictReader(io.StringIO(data.decode("utf-8-sig"))))
def audit(grok, muse=None, audit_date="2026-10-16"):
    result = dict(schemaVersion=1, auditDate=audit_date, calibrationEligible=False,
                  packages=[], records=[], sources=[], images=[], relationships=[], observations=[], permissions=[], gaps=[])
    providers=[]
    if grok:
        data=Path(grok).read_bytes()
        package=dict(provider="GROK",bytes=len(data),sha256=sha(data),integrity="InvalidOrTruncated")
        result["packages"].append(package)
        try:
            with zipfile.ZipFile(io.BytesIO(data)) as z:
                bad=z.testzip()
                if bad: raise ValueError("ZIP CRC failed: "+bad)
                package["integrity"]="CompleteZipCrcVerified"
                entries=[n for n in z.namelist() if not n.endswith("/")]
                package["entries"]=[dict(path=n,bytes=len(z.read(n)),sha256=sha(z.read(n))) for n in entries]
                j=next(n for n in entries if n.endswith("/samples.json")); prefix=j[:-len("samples.json")]
                raw=json.loads(z.read(j)); csv_rows=rows(z.read(prefix+"samples.csv"))
                package["jsonRecords"]=len(raw); package["csvRecords"]=len(csv_rows)
                package["csvJsonEqual"]=csv_rows==raw
                providers.append(("GROK",raw,lambda n,z=z,prefix=prefix:z.read(prefix+n), z))
                # Consume while archive is open; no extractall or path traversal.
                consume(result,*providers.pop()[:3],audit_date)
        except (zipfile.BadZipFile,ValueError,KeyError,StopIteration) as e:
            package["error"]=str(e);result["gaps"].append("GROK package incomplete; never treated as a complete library")
    if muse:
        root=Path(muse)
        required=[root/n for n in ["samples.csv","samples.json","ENGINE_HANDOFF.md"]]
        if all(p.is_file() for p in required):
            raw=json.loads(required[1].read_text(encoding="utf-8-sig"))
            result["packages"].append(dict(provider="MUSE",integrity="TextFilesRead",entries=[dict(path=p.name,bytes=p.stat().st_size,sha256=sha(p.read_bytes())) for p in required],jsonRecords=len(raw),csvRecords=len(rows(required[0].read_bytes())),csvJsonEqual=rows(required[0].read_bytes())==raw))
            def read_muse(n):
                p=(root/n).resolve()
                if root.resolve() not in p.parents: raise ValueError("Asset outside provider root")
                return p.read_bytes()
            consume(result,"MUSE",raw,read_muse,audit_date)
        else: result["gaps"].append("MUSE required files incomplete")
    else: result["gaps"].append("MUSE independent samples.csv/samples.json/ENGINE_HANDOFF.md not found in scoped local search; not imported from GROK archive")
    by_hash={}
    for image in result["images"]: by_hash.setdefault(image["sha256"],[]).append(image["id"])
    result["duplicates"]=[dict(sha256=h,imageIds=ids) for h,ids in by_hash.items() if len(ids)>1]
    result["counts"]=dict(rawRecords=len(result["records"]),imageRecords=sum(r["kind"]=="Image" for r in result["records"]),webOrLiteratureRecords=sum(r["kind"]=="WebOrLiterature" for r in result["records"]),downloadedImages=len(result["images"]),uniqueImageBytes=len(by_hash),exactUniqueSourceUrls=len({r["url"] for r in result["sources"] if r["url"]}),verifiedScenePairs=0)
    result["gaps"].append("User's 41 original-record expectation is not an observed merged count; counts above reflect actual received providers only")
    return result

def consume(out,provider,records,read,audit_date):
    seen=set()
    for r in records:
        sid=r.get("sample_id",r.get("id",""))
        if not sid.startswith(provider+"-POL-") or sid in seen: raise ValueError("Invalid/duplicate provider ID: "+sid)
        seen.add(sid); local=r.get("local_filename",""); image=known(local)
        # A missing asset is still an image record if the manifest gives an image file name.
        kind="Image" if image else "WebOrLiterature"
        issues=["No controlled color-managed paired measurement; not eligible for modern-film calibration"]
        if r.get("retrieval_date","")>audit_date: issues.append("Provider retrieval date is in the future relative to audit date; retained as claim only")
        if "linked_pair_ids" in r and known(r["linked_pair_ids"]): issues.append("Claimed pair retained in original; same author/workflow is not a verified same-scene pair")
        if "Photoshop" in json.dumps(r,ensure_ascii=False): issues.append("Software tag is not evidence that color was edited")
        film=r.get("exact_film_type","")
        if any(t in film for t in ["55", "665", "669", "实验", "20x24", "Time-Zero", "过期"]): issues.append("Historical/experimental/aged material excluded from modern i-Type characterization")
        out["records"].append(dict(id=sid,provider=provider,kind=kind,original=r,reviewStatus="ContextOnlyPendingProvenance",reviewReasons=issues,useAssessments=[dict(use="ModernFilmCalibration",grade="D",reason=issues[0]),dict(use="VisualInspiration" if image else "LiteratureContext",grade="C",reason="Traceable provider claim; original source and workflow not independently verified"),dict(use="ControlledScanComparison",grade="D",reason="No verified same-original multi-scan pair")]))
        source_id=sid+":source"
        out["sources"].append(dict(id=source_id,recordId=sid,url=r.get("original_post_url",""),kind=kind,verification="ProviderClaimNotPageVerified"))
        if known(r.get("source_group_id")):
            out["relationships"].append(dict(id=sid+":source-group",kind="SourceGroupOnly",left=sid,right=r["source_group_id"],verifiedScenePair=False))
        out["observations"].append(dict(id=sid+":observation",recordId=sid,claim=r.get("observed_traits",""),authority="ProviderObservationUnverified",quantitativeFitAllowed=False,contraryEvidence="Unknown; uncontrolled exposure, scan, postprocessing and aging"))
        out["permissions"].append(dict(recordId=sid,status="Unknown",imageRedistribution="Unknown",codeLicense="NotApplicableToImage",claimedLicenseUrl=r.get("license_url",""),providerClaim=r.get("redistribution_allowed",""),reason="License claim retained; source/license scope not independently verified; unknown is neither denied nor allowed"))
        if image:
            try:
                b=read(local)
                with Image.open(io.BytesIO(b)) as im:
                    im.load(); icc=im.info.get("icc_profile"); exif=im.getexif()
                    out["images"].append(dict(id=sid+":scan:1",recordId=sid,sourceId=source_id,localRelativePath=local,bytes=len(b),sha256=sha(b),claimedSha256=r.get("sha256"),hashMatchesProvider=sha(b).lower()==str(r.get("sha256","")).lower(),width=im.width,height=im.height,mode=im.mode,icc=dict(status="BytesRead" if icc else "NotEmbedded",bytes=len(icc) if icc else 0,sha256=sha(icc) if icc else None,interpretation="NotParsed; profile presence alone does not explain a cast"),software=str(exif.get(305,"Unknown")),scanWorkflow="Unknown",releaseBundling="NotApproved"))
            except (OSError,KeyError,ValueError) as e: out["gaps"].append(sid+": asset unreadable: "+str(e))

def main():
    p=argparse.ArgumentParser();p.add_argument("--grok");p.add_argument("--muse");p.add_argument("--output",required=True);a=p.parse_args()
    dest=Path(a.output)
    if dest.exists(): raise FileExistsError("Audit evidence is append-only; choose a new output")
    report=audit(a.grok,a.muse);dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(report["counts"],ensure_ascii=False)); print("calibrationEligible=false")
if __name__=="__main__":main()
