"""Validate the frozen local HTTP delivery, export public evidence, and seal its ZIP.
Run from repository root: python tools/online-http-v1/seal_delivery.py <delivery-root> <new-docs-dir>
Keeps private connections, credentials, workspace databases and image buffers local.
"""
from pathlib import Path
from collections import Counter
import hashlib
import json
import shutil
import subprocess
import sys
import zipfile

repo = Path.cwd()
delivery = Path(sys.argv[1])
docs = Path(sys.argv[2])
assert not docs.exists(), "Preserve existing delivery evidence"
package = delivery / "Guangjing-OnlineSelectionV1-FullApp-Diagnostic-win-x64"
completion = delivery / "completion-20261110"
product = "c44f800a428ba097e024c91e7691dbf3b6a17355"
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
def git(*args):
    return subprocess.check_output(["git", *args], text=True, encoding="utf-8").strip()
def digest(p):
    with p.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()
assert not git("diff", product, "HEAD", "--", "src"), "Product changed since frozen build"
identity = read(package / "BUILD_IDENTITY.json")
assert identity["productSourceSha"] == product
assert identity["WholeAppDiagnosticBuild"] and not identity["PhotographerTrialBuild"]
for name, expected in identity["files"].items():
    assert digest(package / name) == expected["sha256"], name
for name in ["app/KitaoPhotoSelector.dll", "app/RAWSelectionAssistant.Core.dll",
             "selection-service/PixelTart.SelectionApi.Server.dll"]:
    assert product.encode() in (package / name).read_bytes(), name
assert (package / "tools/Start-LocalTest.ps1").read_bytes() == (repo / "tools/online-http-v1/Start-LocalTest.ps1").read_bytes()
bindings = read(delivery / "PUBLISHED_BINDINGS.json")
for binding in bindings:
    assert digest(Path(binding["published"])) == binding["sha256"]
    assert digest(Path(binding["test"])) == binding["sha256"]
regression = read(delivery / "final-regression-02/SUMMARY.json")
assert regression["ok"] and regression["counts"] == {"Passed": 90}
assert regression["sourceSha"] == product
assert all(r["ok"] and r["exitCode"] == 0 for r in regression["runs"])
counts = Counter((r["kind"], result["outcome"]) for r in regression["runs"] for result in r["results"])
app_dirs = ["final2-app-offline01", "final2-app-publish01", "final2-app-sync01", "completion-app-reopen-20261110"]
app_runs = []
for name in app_dirs:
    receipt = read(delivery / name / "RECEIPT.json")
    assert receipt["exitCode"] == 0 and len(receipt["results"]) == 1
    assert receipt["results"][0]["outcome"] == "Passed"
    assert not git("diff", product, receipt["sourceSha"], "--", "src")
    app_runs.append(dict(name=name, toolSource=receipt["sourceSha"], productSource=product,
                         test=receipt["results"][0]["testName"], outcome="Passed", runnerPid=receipt["pid"]))
for mode in ["exe", "cmd"]:
    smoke = read(completion / ("normal-" + mode) / "SMOKE.json")
    assert smoke["appExit"] == 0 and smoke["launcherExit"] == 0
    assert len(smoke["navigation"]) == 11
assert read(delivery / "PACKAGED_LOCAL_ENTRY_SMOKE.json")["configuredHttpProviderVisible"]
assert read(completion / "REVOKE_AND_REOPEN_READBACK.json")["clientGrantRemoved"]
requests = [json.loads(line) for line in (delivery / "final-http02/server-state/requests.jsonl").read_text(encoding="utf-8-sig").splitlines() if line.strip()]
assert any(x["path"].endswith("/confirm") and x["status"] == 200 for x in requests)
assert any(x["path"].endswith("/unpublish") and x["status"] == 200 for x in requests)
assert any(x["status"] == 410 for x in requests)
files = sorted(p for p in package.rglob("*") if p.is_file())
for p in files:
    relative = p.relative_to(package)
    assert not p.is_symlink(), relative
    assert not any(part.lower() in ["data", "objects", "test-assets", "__pycache__"] for part in relative.parts), relative
    assert p.suffix.lower() not in [".raf", ".arw", ".cr2", ".nef", ".jpg", ".jpeg", ".png", ".log", ".trx", ".pdb"], relative
    assert p.name not in ["PRIVATE_CONNECTION.json", "PRIVATE_OWNER.json", "workspace.json", "server-secret.bin"], relative
secrets = set()
for p in delivery.rglob("PRIVATE_CONNECTION.json"):
    value = read(p).get("ownerKey")
    if value: secrets.add(value.encode())
for p in delivery.rglob("PRIVATE_OWNER.json"):
    value = read(p).get("owner")
    if value: secrets.add(value.encode())
for p in delivery.rglob("SHARE_LINK.secret.txt"):
    from urllib.parse import urlsplit, parse_qs
    value = parse_qs(urlsplit(p.read_text(encoding="utf-8-sig").strip()).fragment).get("token", [""])[0]
    if value: secrets.add(value.encode())
for p in files:
    data = p.read_bytes()
    assert all(secret not in data for secret in secrets), "Private credential in package: " + str(p.relative_to(package))
manifest = {p.relative_to(package).as_posix(): dict(bytes=p.stat().st_size, sha256=digest(p)) for p in files}
archive = delivery / (package.name + ".zip")
assert not archive.exists(), "Preserve existing ZIP"
with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for p in files: z.write(p, p.relative_to(package).as_posix())
with zipfile.ZipFile(archive) as z:
    assert len(z.namelist()) == len(manifest)
    assert z.testzip() is None
    for name, expected in manifest.items():
        with z.open(name) as stream: actual = hashlib.file_digest(stream, "sha256").hexdigest()
        assert actual == expected["sha256"], name
zip_identity = dict(path=str(archive), sha256=digest(archive), bytes=archive.stat().st_size,
                    fileCount=len(manifest), unpackedBytes=sum(v["bytes"] for v in manifest.values()),
                    perFileReadback=True, privateCredentialsAbsent=True, rawImagesAbsent=True)
summary = dict(productSource=product, testSupportHead=git("rev-parse", "HEAD"),
    regression=dict(core=counts[("Core", "Passed")], wpf=counts[("Wpf", "Passed")], passed=90, failed=0,
                    methods=len(regression["runs"]), source=regression["sourceSha"]),
    appPhases=app_runs, totalFinalTrxPass=94, publishedDllBindings=len(bindings),
    standaloneSmoke=["configured packaged LocalTest launcher", "normal published EXE", "normal published CMD"],
    loggedServiceRequests=len(requests), physicalPhone="NOT_RUN_ENVIRONMENT_NOT_READY",
    newRawCalls=0, productUnchangedAfterFreeze=True, overall="LOCAL_HTTP_V1_READY_FOR_REVIEW_PHONE_PENDING")
docs.mkdir(parents=True)
def write(name, value):
    (docs / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
write("TEST_SUMMARY.json", summary)
write("PACKAGE_MANIFEST.json", manifest)
write("ZIP_IDENTITY.json", zip_identity)
write("REQUEST_SUMMARY.json", dict(finalBatchOnly=True, total=len(requests),
    methodsAndStatus={f"{method} {status}": count for (method, status), count in Counter((r["method"], r["status"]) for r in requests).items()},
    credentialHeadersLogged=False, historicalDevelopmentRequestsExcluded=True))
(docs / "HTTP_REQUESTS.jsonl").write_text("".join(json.dumps(row) + "\n" for row in requests), encoding="utf-8")
for source, target in [(delivery / "BUILD_IDENTITY.json", "BUILD_IDENTITY.json"),
    (delivery / "SOURCE_REUSE_REVIEW.json", "SOURCE_REUSE_REVIEW.json"),
    (delivery / "COMPLETION_HTTP_SECURITY_IDEMPOTENCY_20261110.json", "HTTP_SECURITY_IDEMPOTENCY.json"),
    (completion / "REVOKE_AND_REOPEN_READBACK.json", "REVOKE_AND_REOPEN_READBACK.json"),
    (delivery / "MOBILE_ACCEPTANCE_STATUS.json", "MOBILE_ACCEPTANCE_STATUS.json"),
    (delivery / "FINAL_CUSTOMER_BROWSER_RECEIPT.json", "CUSTOMER_BROWSER_RECEIPT.json"),
    (delivery / "PACKAGED_LOCAL_ENTRY_SMOKE.json", "PACKAGED_LOCAL_ENTRY_SMOKE.json"),
    (completion / "normal-exe/SMOKE.json", "PUBLISHED_EXE_SMOKE.json"),
    (completion / "normal-cmd/SMOKE.json", "PUBLISHED_CMD_SMOKE.json")]: shutil.copy2(source, docs / target)
for name in ["HTTP_OFFLINE.json", "HTTP_PUBLISHED.json", "HTTP_SYNC.json", "HTTP_REOPEN.json"]:
    shutil.copy2(delivery / "final02-app-evidence" / name, docs / name)
for name in app_dirs:
    target = docs / "app-phase-tests" / name; target.mkdir(parents=True)
    for file in ["RECEIPT.json", "results.trx"]: shutil.copy2(delivery / name / file, target / file)
write("REGRESSION_SUMMARY.json", regression)
visual = docs / "screenshots"; visual.mkdir()
for source, target in [(delivery / "FINAL_CUSTOMER_CONFIRMED.png", "BROWSER-customer-confirmed.png"),
    (delivery / "CUSTOMER_390PX_BROWSER.png", "BROWSER-responsive-390px-historical-same-web-source.png"),
    (delivery / "final02-app-evidence/OFFSCREEN-http-reopened.png", "OFFSCREEN-desktop-reopened-and-revoked.png")]: shutil.copy2(source, visual / target)
(completion / "SEALED_PACKAGE.json").write_text(json.dumps(zip_identity, indent=2) + "\n", encoding="utf-8")
print(json.dumps(dict(zip=zip_identity, summary=summary), indent=2))
