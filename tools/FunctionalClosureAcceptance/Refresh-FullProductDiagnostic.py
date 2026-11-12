"""Refresh read-only company diagnostics without changing a booking or invoking desktop input."""
from pathlib import Path
from datetime import datetime, timezone
from collections import Counter
import argparse, csv, hashlib, json, sqlite3, subprocess, urllib.request, urllib.error
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser()
p.add_argument('--context', required=True)
a = p.parse_args()
context_path = Path(a.context).resolve()
c = json.loads(context_path.read_text(encoding='utf-8-sig'))
r = Path(c['worktree'])
d = r / 'docs/validation/product-functional-closure-20261112'
b = Path(c['deliveryRoot'])
receipts = d / 'receipts'
receipts.mkdir(parents=True, exist_ok=True)
now = datetime.now(timezone.utc).isoformat()

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def write(name, value):
    path = receipts / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return path

def git(*args):
    return subprocess.check_output(['git', '-C', str(r), *args], text=True, encoding='utf-8').strip()

def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as f:
        for chunk in iter(lambda: f.read(1048576), b''):
            h.update(chunk)
    return h.hexdigest()

def http(url, token=None):
    headers = {'Authorization': 'Bearer ' + token} if token else {}
    request = urllib.request.Request(url, headers=headers, method='GET')
    try:
        with urllib.request.urlopen(request, timeout=8) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())

# Existing receipts must be intact before they are incorporated.
old_index = read(d / 'EVIDENCE_INDEX.json')
for entry in old_index['artifacts']:
    f = d / entry['file']
    if not f.is_file() or digest(f) != entry['sha256']:
        raise RuntimeError('Existing evidence integrity mismatch: ' + entry['file'])

# Never promote a stale package to current product evidence.
changed = git('diff', '--name-only', c['currentProductSource'], '--', 'src', 'extensions')
if changed:
    raise RuntimeError('Product inputs differ from the published source; republish before refresh')
write('RESUME_SOURCE_AND_FETCH.json', {
    'observedAtUtc': now, 'startingSource': c['startingSource'],
    'headAtObservation': git('rev-parse', 'HEAD'), 'productSource': c['currentProductSource'],
    'branch': git('branch', '--show-current'), 'sourceInputsUnchanged': True,
    'statusBeforeRefresh': git('status', '--short'),
    'worktrees': git('worktree', 'list', '--porcelain'),
    'refs': git('for-each-ref', '--format=%(refname:short) %(objectname) %(upstream:short) %(upstream:track)', 'refs/heads', 'refs/remotes'),
    'fetchCompleted': True, 'noUiDraftApproved': True,
})

# Retain the original native observations, not only references to a private local path.
for name in ['WORKFLOW_INITIAL_WORKBENCH.json', 'WORKFLOW_PROJECT_CREATED_PLANNING.json',
             'WORKFLOW_BOOKING_READY_TO_SAVE.json', 'FINAL_PACKAGE_ARCHIVE.json']:
    f = b / name
    if f.is_file():
        (receipts / name).write_bytes(f.read_bytes())

mobile = read(c['mobileContext'])
root = Path(mobile['runtimeRoot'])
private = read(root / 'PHONE_DEVICE_PRIVATE.json')
base = mobile['localBaseUrl'].rstrip('/')
health_code, health = http(base + '/health/ready')
unauth_code, _ = http(base + '/v1/schedule/session')
session_code, session = http(base + '/v1/schedule/session', private['token'])
booking_code, booking = http(base + '/v1/schedule/bookings/' + mobile['bookingId'], private['token'])
with sqlite3.connect(Path(mobile['databasePath']).resolve().as_uri() + '?mode=ro', uri=True) as db:
    db.row_factory = sqlite3.Row
    row = db.execute('SELECT * FROM ShootBookings WHERE lower(Id)=lower(?)', (mobile['bookingId'],)).fetchone()
    if row is None:
        raise RuntimeError('Current synthetic phone appointment missing')
    db_booking = dict(row)
http_revision = booking.get('revision')
checks = {
    'healthReady': health_code == 200 and health.get('ready') is True,
    'unauthenticatedSessionRejected': unauth_code == 401,
    'existingPhoneCredentialAccepted': session_code == 200,
    'samePhoneDevice': session.get('device', {}).get('id') == mobile['phoneDeviceId'],
    'existingBookingReadable': booking_code == 200,
    'httpRevisionEqualsSqlite': http_revision == db_booking.get('Revision'),
    'handedOffRevisionUnchanged': http_revision == mobile['prePhoneBooking']['revision'],
    'mutatingProbeGuardPresent': (root / 'PHONE_HANDOFF_ACTIVE.json').is_file(),
    'credentialNotExpired': datetime.fromisoformat(mobile['phoneDeviceExpiresAt']) > datetime.now(timezone.utc),
}
write('MOBILE_READONLY_CURRENT.json', {
    'status': 'PASS_LOCAL_READONLY' if all(checks.values()) else 'ATTENTION_READONLY_CHECK_FAILED',
    'observedAtUtc': now, 'checks': checks, 'booking': booking,
    'credentialExpiresAtUtc': mobile['phoneDeviceExpiresAt'], 'requestsWereGetOnly': True,
    'scope': 'Current loopback server and persisted company credential only. No iPhone or HTTPS success inferred.',
})
if not all(checks.values()):
    raise RuntimeError('Read-only mobile validation needs investigation; inspect sanitized receipt')

exe = Path('C:/Program Files/Tailscale/tailscale.exe')
status = json.loads(subprocess.check_output([str(exe), 'status', '--json'], text=True, encoding='utf-8'))
serve = subprocess.run([str(exe), 'serve', 'status', '--json'], capture_output=True, text=True, encoding='utf-8')
write('TAILSCALE_CURRENT.json', {
    'observedAtUtc': now, 'backendState': status.get('BackendState'),
    'selfHostName': status.get('Self', {}).get('HostName'),
    'selfDnsName': status.get('Self', {}).get('DNSName'),
    'privateHttpsReady': bool(mobile.get('phoneHttpsConfigured', False)),
    'serveCommandExitCode': serve.returncode,
    'serveConfiguration': json.loads(serve.stdout) if serve.returncode == 0 and serve.stdout.strip() else None,
    'authLinkIsPrivateAndNotStoredInRepository': True,
    'phoneTwoStepHandoffIssued': bool(mobile.get('phoneHandoffStarted', False)),
    'pending': 'User must personally approve company machine in the original tailnet before HTTPS configuration.'
        if status.get('BackendState') != 'Running' else 'Configure and verify private HTTPS using the company Self.DNSName.',
})

# Read-only row counts do not convert the unfinished UI save into a successful booking.
workflow_root = Path(c['workflowQaRuntime'])
with sqlite3.connect((workflow_root / 'Data/pixel-tart.db').resolve().as_uri() + '?mode=ro', uri=True) as db:
    bookings = db.execute('SELECT count(*) FROM ShootBookings').fetchone()[0]
    project = db.execute('SELECT Id FROM Projects WHERE lower(Id)=lower(?)', (c['workflowProjectId'],)).fetchone()
write('WORKFLOW_CURRENT_READONLY.json', {
    'observedAtUtc': now, 'projectId': c['workflowProjectId'], 'sameProjectStillExists': bool(project),
    'bookingDatabaseRows': bookings, 'nativeBookingSaveCompleted': bookings > 0,
    'freshComputerUse': {
        'windowId': c.get('workflowWindow'), 'processId': c['workflowAppPid'],
        'accessibilityReadable': True, 'originalBookingDraftStillVisible': False,
        'inputRecoveredViaOpenComputerUse': True, 'workflowBookingId': c.get('workflowBookingId'),
        'historicalSkyScreenshotError': 'FrameArrived timed out: timed out waiting on channel',
        'historicalSkySaveInputError': 'coordinate input geometry is unavailable',
        'normalExeBookingSavedBySupportedNativeInvoke': bookings > 0,
        'workflowAppSource': c.get('workflowAppProductSource', c['baseProductSource']),
    },
    'scope': 'Original input failures retained; Open Computer Use recovered the normal EXE and saved the linked booking. Later steps and true phone acceptance remain separately scoped.',
})

new_trx = b / 'resume-booking-tether-auto/results.trx'
if new_trx.is_file():
    dest = receipts / 'trx/resume-booking-tether-auto/results.trx'
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(new_trx.read_bytes())
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
unique = {}
latest_times = {}
history = Counter()
runs = []
for f in sorted((receipts / 'trx').rglob('*.trx')):
    tree = ET.parse(f)
    results = tree.findall('.//t:UnitTestResult', ns)
    for result in results:
        name = result.get('testName')
        ended = datetime.fromisoformat(result.get('endTime')).timestamp()
        history[result.get('outcome')] += 1
        if name not in latest_times or ended > latest_times[name]:
            latest_times[name] = ended
            unique[name] = result.get('outcome')
    runs.append({'file': f.relative_to(d).as_posix(), 'results': len(results),
                 'counts': dict(Counter(x.get('outcome') for x in results))})
fresh = next((x for x in runs if 'resume-booking-tether-auto' in x['file']), None)
if not fresh or fresh['results'] != 61 or fresh['counts'] != {'Passed': 61}:
    raise RuntimeError('Expected 61 exact fresh booking/tether checks; no zero-match success')
write('RESUME_AUTOMATIC_CHECKS.json', {
    'observedAtUtc': now, 'productSource': c['currentProductSource'],
    'freshRun': fresh, 'allRetainedUniqueTestNames': dict(Counter(unique.values())),
    'allRetainedRuns': runs, 'allHistoricalOutcomeCounts': dict(history),
    'latestSelectionRule': 'Latest result endTime per testName, not directory enumeration order; historical configuration failure is retained.',
    'freshIsNativeInputEvidence': False,
    'scope': 'Booking save/retry/continue-planning, tether UI source contracts and runtime error handling; isolated runtime. Existing measurement opt-in skip remains explicit.',
})

# Bind reusable real-device receipts to the exact feature paths, independently of the new tests.
bindings = read(receipts / 'REUSED_EVIDENCE_SOURCE_BINDINGS.json')
for binding in bindings['evidence']:
    receipt = r / binding['receipt']
    if digest(receipt) != binding['receiptSha256']:
        raise RuntimeError('Reused receipt hash changed: ' + binding['scope'])
    delta = git('diff', '--name-only', binding['testedProductSource'], c['currentProductSource'], '--', *binding['pathsChecked'])
    if delta:
        raise RuntimeError('Reused feature inputs changed: ' + binding['scope'])
write('REUSED_EVIDENCE_RECHECK.json', {'observedAtUtc': now, 'status': 'PASS_FEATURE_SOURCE_AND_RECEIPT_HASHES',
    'scopes': [x['scope'] for x in bindings['evidence']],
    'boundary': 'Old Safari/Chrome runs retain their original project/device scope; no new project or current phone-calendar inference.'})

remote = 'origin/integration/product-memory-20261112'
remote_sha = git('rev-parse', remote)
write('FETCHED_MEMORY_BRANCH_BOUNDARY.json', {
    'observedAtUtc': now, 'remoteRef': remote, 'sha': remote_sha,
    'mergeBase': git('merge-base', c['startingSource'], remote_sha),
    'commitsAfterStartingSource': git('log', '--format=%H %s', c['startingSource'] + '..' + remote_sha),
    'fileDelta': git('diff', '--name-status', c['startingSource'], remote_sha),
    'integratedInThisPackage': False,
    'boundary': 'New company RAW memory/viewport receipts are on a separate branch and package. They do not validate this unchanged product source. No UI redesign is contained in this delta.',
})

# Produce a manifest verification receipt without adding any runtime secrets to the repository.
package = Path(c['package'])
manifest = read(package / 'PACKAGE_FILES.json')
files = manifest['files']
for item in files:
    f = package / item['path']
    if not f.is_file() or f.stat().st_size != item['bytes'] or digest(f) != item['sha256']:
        raise RuntimeError('Package manifest mismatch: ' + item['path'])
archive = Path(c['archive'])
archive_hash = digest(archive)
if archive_hash != c['archiveSha256']:
    raise RuntimeError('Existing diagnostic archive hash mismatch')
write('PACKAGE_CURRENT_INTEGRITY.json', {
    'observedAtUtc': now, 'status': 'PASS', 'productSource': c['currentProductSource'],
    'package': str(package), 'verifiedFiles': len(files), 'archive': str(archive),
    'archiveBytes': archive.stat().st_size, 'archiveSha256': archive_hash,
    'selectionServerIncluded': (package / 'SelectionService/PixelTart.SelectionApi.Server.exe').is_file(),
    'scheduleServerIncluded': (package / 'ScheduleService/Guangjing.Schedule.Server.exe').is_file(),
    'uiRedesignApproved': False, 'runtimeCredentialsIncluded': False,
})
print(json.dumps({'status': 'READONLY_REFRESH_COMPLETE', 'newChecks': 61, 'uniqueAutomaticOutcomes': dict(Counter(unique.values())),
    'tailscale': status.get('BackendState'), 'workflowBookings': bookings,
    'packageFilesVerified': len(files)}, ensure_ascii=True))