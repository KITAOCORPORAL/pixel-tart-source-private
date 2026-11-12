"""Prepare the existing company phone device after personal tailnet approval.
Default mode is read-only. --apply configures only an unused company HTTPS port;
existing unrelated Serve listeners, phone credentials and bookings are preserved.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse, json, subprocess, sys, urllib.request, urllib.error

p = argparse.ArgumentParser()
p.add_argument('--context', required=True)
p.add_argument('--apply', action='store_true')
p.add_argument('--https-port', type=int, default=8444)
p.add_argument('--observer-port', type=int, default=17663)
a = p.parse_args()
context = Path(a.context).resolve()
c = json.loads(context.read_text(encoding='utf-8-sig'))
root = Path(c['runtimeRoot']).resolve()
exe = 'C:/Program Files/Tailscale/tailscale.exe'
def call(*args):
    return subprocess.check_output([exe, *args], text=True, encoding='utf-8', timeout=30)
def save(path, obj):
    Path(path).write_text(json.dumps(obj, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
def get(url, token=None):
    headers = {'Authorization': 'Bearer '+token} if token else {}
    req = urllib.request.Request(url, headers=headers, method='GET')
    try:
        with urllib.request.urlopen(req, timeout=10) as res:
            return res.status, json.load(res)
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())
def require(ok, text):
    if not ok: raise RuntimeError(text)

state = json.loads(call('status', '--json'))
preflight = {'observedAtUtc': datetime.now(timezone.utc).isoformat(),
    'backendState': state.get('BackendState'), 'applyRequested': a.apply,
    'phoneHttpsConfigured': bool(c.get('phoneHttpsConfigured', False)),
    'phoneHandoffStarted': bool(c.get('phoneHandoffStarted', False)),
    'bookingMutations': False, 'credentialRotation': False}
if state.get('BackendState') != 'Running':
    preflight.update(status='WAITING_PERSONAL_COMPANY_TAILNET_APPROVAL',
        action='Use the fresh authorization link personally. Re-run this command with --apply after the company device is Running.')
    save(root/'PRIVATE_HTTPS_CONTINUATION_PREFLIGHT.json', preflight)
    print(json.dumps(preflight)); sys.exit(3)
dns = state.get('Self', {}).get('DNSName', '').rstrip('.')
require(bool(dns) and dns.endswith('.ts.net'), 'Company Self DNS name unavailable')
expected_suffix = c.get('expectedTailnetDnsSuffix')
require(bool(expected_suffix) and dns.lower().endswith('.'+expected_suffix.lower().lstrip('.')), 'Company device is not in the original authorized tailnet; HTTPS handoff withheld')
require(state['Self'].get('HostName', '').lower() == 'pc-20250309weiz', 'Unexpected machine; refusing to reuse another host')
require(1024 <= a.https_port <= 65535 and 1024 <= a.observer_port <= 65535, 'Invalid dedicated ports')
public = 'https://'+dns+':'+str(a.https_port)
private = json.loads((root/'PHONE_DEVICE_PRIVATE.json').read_text(encoding='utf-8-sig'))
require(datetime.fromisoformat(c['phoneDeviceExpiresAt']) > datetime.now(timezone.utc), 'Existing phone device expired; renew deliberately before handoff')
local = c['localBaseUrl'].rstrip('/')
code, session = get(local+'/v1/schedule/session', private['token'])
require(code == 200 and session['device']['id'] == c['phoneDeviceId'], 'Local service or current phone device does not match')
code, booking_before = get(local+'/v1/schedule/bookings/'+c['bookingId'], private['token'])
require(code == 200, 'Existing phone booking unreadable')
serve_before = json.loads(call('serve', 'status', '--json'))
endpoint = dns+':'+str(a.https_port)
existing = serve_before.get('Web', {}).get(endpoint)
if existing:
    require(existing.get('Handlers', {}).get('/', {}).get('Proxy') == local, 'Dedicated port already belongs to another route')
    for route, handler in existing.get('Handlers', {}).items():
        require(route in ('/', '/qa', '/qa/'), 'Unrelated route on dedicated port; preserve it')
        if route != '/': require(handler.get('Proxy', '').rstrip('/') == 'http://127.0.0.1:'+str(a.observer_port), 'Unrelated observer route; preserve it')
require(not serve_before.get('AllowFunnel', {}).get(endpoint), 'Selected listener has Funnel enabled; use another private port')
if not a.apply:
    preflight.update(status='READY_TO_CONFIGURE_AFTER_APPROVAL', companyHttpsBase=public,
        dedicatedPortAvailable=not existing, currentDeviceAccepted=True)
    save(root/'PRIVATE_HTTPS_CONTINUATION_PREFLIGHT.json', preflight)
    print(json.dumps(preflight)); sys.exit(0)

observer_config = root/'PHONE_BROWSER_OBSERVER_PRIVATE.json'
observer = json.loads(observer_config.read_text(encoding='utf-8-sig'))
observer['publicBaseUrl'] = public
observer_local = 'http://127.0.0.1:'+str(a.observer_port)
try:
    code, health = get(observer_local+'/qa/health/ready')
except (urllib.error.URLError, TimeoutError):
    health = None
if health:
    require(code == 200 and health.get('mode') == 'BrowserTimeZoneObserverOnly'
        and health.get('roundId') == observer['roundId'] and health.get('publicBaseUrl') == public, 'Observer port is occupied by another or older unbound observer')
else:
    save(observer_config, observer)
    observer_candidates = [Path(__file__).resolve().parents[1]/'MobileScheduleAcceptance/BrowserTimeZoneObserver.py', Path(__file__).resolve().parents[1]/'BrowserTimeZoneObserver.py']
    observer_script = next((candidate for candidate in observer_candidates if candidate.is_file()), None)
    require(observer_script is not None, 'Timezone observer tool missing from repository/package')
    log = (root/'browser-observer.stdout.log').open('ab')
    err = (root/'browser-observer.stderr.log').open('ab')
    process = subprocess.Popen([sys.executable, str(observer_script), '--config', str(observer_config), '--port', str(a.observer_port)],
        stdout=log, stderr=err, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    log.close(); err.close()
    c['browserObserverPid'] = process.pid
    import time
    health = None
    for _ in range(30):
        try:
            code, health = get(observer_local+'/qa/health/ready')
            if code == 200: break
        except (urllib.error.URLError, TimeoutError): time.sleep(.1)
    require(health is not None and health.get('roundId') == observer['roundId'], 'Owned timezone observer did not become ready')

# Both routes use this machine's tailnet-only HTTPS origin. No reset or Funnel.
call('serve', '--bg', '--yes', '--https='+str(a.https_port), local)
call('serve', '--bg', '--yes', '--https='+str(a.https_port), '--set-path=/qa', observer_local)
code, health = get(public+'/health/ready')
require(code == 200 and health.get('mode') == 'MobileScheduleV1', 'HTTPS certificate/service validation failed')
code, _ = get(public+'/v1/schedule/session')
require(code == 401, 'HTTPS must reject an unauthenticated session')
code, session = get(public+'/v1/schedule/session', private['token'])
require(code == 200 and session['device']['id'] == c['phoneDeviceId'], 'HTTPS device does not match current company device')
code, booking_after = get(public+'/v1/schedule/bookings/'+c['bookingId'], private['token'])
require(code == 200 and booking_after == booking_before, 'Booking changed during non-mutating setup; investigate before handoff')
code, health = get(public+'/qa/health/ready')
require(code == 200 and health.get('roundId') == observer['roundId'], 'HTTPS observer route mismatch')
serve_after = json.loads(call('serve', 'status', '--json'))
require(not serve_after.get('AllowFunnel', {}).get(endpoint), 'Unexpected public Funnel; handoff withheld')
url = public+'/qa/start/'+observer['nonce']+'#token='+private['token']
(root/'PHONE_URL.txt').write_text(url+'\n', encoding='utf-8')
c.update(phoneHttpsConfigured=True, phoneHandoffStarted=False, publicBaseUrl=public,
    phonePublicBaseUrl=public, browserObserverPort=a.observer_port, pythonExecutable=sys.executable,
    privateHttpsState='VERIFIED_COMPANY_HTTPS_CURRENT_DEVICE_NO_PHONE_ROUNDTRIP_YET')
save(context, c)
receipt = {'status': 'PASS_COMPANY_PRIVATE_HTTPS_CURRENT_DEVICE', 'observedAtUtc': datetime.now(timezone.utc).isoformat(),
    'companyHttpsBase': public, 'phoneDeviceId': c['phoneDeviceId'], 'bookingId': c['bookingId'],
    'unchangedRevision': booking_before['revision'], 'tlsCertificateVerified': True,
    'unauthenticatedRejected': True, 'sameObserverRound': True, 'funnelEnabled': False,
    'phoneUrlPrivateFile': str(root/'PHONE_URL.txt'), 'phoneRealDeviceRoundtrip': 'NOT_RUN',
    'bookingWrites': False, 'credentialsRotated': False}
save(root/'PRIVATE_HTTPS_CONTINUATION_VERIFIED.json', receipt)
print(json.dumps(receipt))