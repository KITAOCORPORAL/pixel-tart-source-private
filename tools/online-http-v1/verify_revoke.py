"""Read-only HTTP and isolated-store verification after native desktop revoke.
Usage: verify_revoke.py <private-connection.json> <app-evidence> <workspace.json> <new-receipt.json>
Credentials and share links stay in the private local test root.
"""
from pathlib import Path
import hashlib
import json
import sys
import urllib.error
import urllib.parse
import urllib.request

connection_path, evidence_path, workspace_path, output_path = map(Path, sys.argv[1:])
assert not output_path.exists(), "Preserve previous receipts"
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
connection = read(connection_path)
published = read(evidence_path / "HTTP_PUBLISHED.json")
reopened = read(evidence_path / "HTTP_REOPEN.json")
workspace = read(workspace_path)
parts = urllib.parse.urlsplit((evidence_path / "SHARE_LINK.secret.txt").read_text().strip())
token = urllib.parse.parse_qs(parts.fragment)["token"][0]
public_id = parts.path.rsplit("/", 1)[1]
assert public_id == published["publicId"]
requests = []
for path, headers, expected in [
    (parts.path, {}, 404),
    (f"/v1/client/selection/{public_id}", {"Authorization": "Bearer " + token}, 401),
]:
    request = urllib.request.Request(connection["endpoint"].rstrip("/") + path, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            status = response.status
    except urllib.error.HTTPError as error:
        status = error.code
    requests.append(dict(method="GET", path=path, status=status, expected=expected))
    assert status == expected, (path, status, expected)
store_path = Path(connection["root"]) / "Data/selection-v1.json"
store_bytes = store_path.read_bytes()
store = json.loads(store_bytes.decode("utf-8-sig"))
records = [p for p in store.values() if p["id"] == published["projectId"]]
assert len(records) == 1
project = records[0]
assert len(project["assets"]) == 1
assert project["clientHash"] is None
assert project["locked"] and project["confirmedRevision"] == reopened["revision"]
assert reopened["projectId"] == published["projectId"]
assert reopened["persistedWithoutHttpRefresh"] and reopened["selected"] == 1
assert reopened["resultVersion"] == project["version"]
asset = next(a for a in workspace["Assets"] if a["Id"] == published["assetId"])
for key in ["OriginalPath", "OriginalFullPath", "SourcePath", "ProxyJpegPath"]:
    if asset.get(key):
        assert asset[key] not in store_bytes.decode("utf-8-sig")
result = dict(requests=requests, desktopProjectRecords=len(records),
    desktopAssetRecords=len(project["assets"]), totalIsolatedTestAlbums=len(store),
    confirmedRevision=project["confirmedRevision"], clientGrantRemoved=True,
    serviceStateSha256=hashlib.sha256(store_bytes).hexdigest(),
    storedSourcePaths=False, reopen=reopened, newRawCalls=0)
output_path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(dict(revokedRequests=len(requests), projectRecords=1, assetRecords=1,
    reopenedRevision=reopened["revision"], passed=True)))
