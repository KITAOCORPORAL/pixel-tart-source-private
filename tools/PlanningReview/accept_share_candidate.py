"""Candidate Share to Team acceptance; starts and stops only its own isolated host.

Run from the repository with the built PlanningReview DLL, an isolated QA HTTPS
certificate valid for loopback+LAN, and a fresh output directory. The only secret
is random, generated for the test, and never sent until after the TLS pin check.
"""
import argparse
import concurrent.futures
import hashlib
import http.client
import json
import os
import secrets
import socket
import ssl
import subprocess
import time
from pathlib import Path
from urllib.parse import parse_qs, urlencode, urlsplit
from uuid import UUID, uuid4


def encode(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode('utf-8')


def digest(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def run(args):
    lan = args.lan
    port = args.port
    assert lan not in ('127.0.0.1', 'localhost') and (1024 <= port <= 65535 and port not in (45731, 45732, 45751, 45761))
    for host in ('127.0.0.1', lan):
        with socket.socket(socket.AF_INET) as s:
            if os.name == 'nt':
                s.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            s.bind((host, port))
    service = Path(args.dll).resolve(strict=True)
    dotnet = Path(args.dotnet).resolve(strict=True)
    cert = Path(args.cert).resolve(strict=True)
    root = Path(args.output).resolve()
    root.mkdir(parents=True, exist_ok=False)
    data = root / 'synthetic-host'
    data.mkdir()
    checks = []
    receipt = root / 'SHARE-PROTOCOL.json'
    destination = str(uuid4())
    secret = secrets.token_hex(32)
    secret_file = data / 'control.key'
    secret_file.write_text(secret, encoding='ascii')
    tls = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    tls.check_hostname = False
    tls.verify_mode = ssl.CERT_NONE
    loopback = f'https://127.0.0.1:{port}'
    member_base = f'https://{lan}:{port}'
    photo = (Path(__file__).parent / 'fixtures' / 'autumn-collar.jpg').read_bytes()
    running = None
    server_log = None
    starts = 0
    stage = 'startup'
    timeline_name = '08:45 进场校灯'
    legacy_project = str(uuid4())

    def save(status):
        receipt.write_text(json.dumps(dict(status=status, destinationId=destination, port=port, checks=checks,
            scope='Windows HTTPS protocol with synthetic native bytes and one physical host'), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

    def mark(tag, **evidence):
        checks.append(dict(check=tag, **evidence))
        save('RUNNING')
        print('PASS', tag, flush=True)

    def call(path, *, who=None, host='127.0.0.1', payload=None, method=None, expected=200, raw=False, discard=False, origin=None, declared_length=None):
        connection = http.client.HTTPSConnection(host, port, timeout=20, context=tls)
        try:
            connection.connect()
            assert digest(connection.sock.getpeercert(binary_form=True)) == args.pin.upper(), 'QA certificate pin mismatch'
            headers = {}
            if who:
                headers['Authorization'] = 'Bearer ' + who
            if origin:
                headers['Origin'] = origin
            body = payload
            if payload is not None:
                if isinstance(payload, bytes):
                    headers['Content-Type'] = 'application/octet-stream'
                else:
                    headers['Content-Type'] = 'application/json'
                    body = encode(payload)
            if declared_length is not None:
                headers['Content-Length'] = str(declared_length)
            connection.request(method or ('POST' if body is not None else 'GET'), path, body, headers)
            with connection.getresponse() as response:
                assert response.status == expected, f'{(method or path.split("/")[-1])}: expected {expected}, got {response.status}'
                if discard:
                    return
                contents = response.read()
                return contents if raw or not contents else json.loads(contents)
        finally:
            connection.close()

    def start(legacy=False):
        nonlocal running, server_log, starts
        starts += 1
        server_log = (root / f'host-{starts}.log').open('wb')
        current_process = os.environ.copy()
        current_process['DOTNET_ROOT'] = str(dotnet.parent)
        current_process['DOTNET_ROOT_X64'] = str(dotnet.parent)
        host_args = [str(dotnet), str(service), '--data', str(root/'legacy-host' if legacy else data), '--lan', lan, '--port', str(port), '--cert', str(cert)]
        if legacy:
            host_args.extend(['--workbench', 'true', '--project', legacy_project])
        else:
            host_args.extend(['--share-destination-id', destination, '--share-public-base', member_base, '--share-control-secret-file', str(secret_file)])
        running = subprocess.Popen(host_args, cwd=Path(__file__).parent, env=current_process, stdout=server_log, stderr=subprocess.STDOUT,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        deadline = time.monotonic() + 15
        while True:
            assert running.poll() is None, 'Candidate process exited during startup. See test host log.'
            try:
                status = call('/health')
                if legacy:
                    assert 'mode' not in status and status['version'] == 1
                else:
                    assert status['destinationId'] == destination and status['mode'] == 'planning-share-candidate'
                    assert call('/health', host=lan)['destinationId'] == destination
                return
            except (ConnectionError, OSError):
                if time.monotonic() > deadline:
                    raise
                time.sleep(.10)

    def stop():
        nonlocal running, server_log
        if running is not None:
            if running.poll() is None:
                running.terminate()
                running.wait(timeout=15)
            running = None
        if server_log is not None:
            server_log.close()
            server_log = None

    def share(path, payload, **kw):
        return call('/api/share/'+path, who=secret, payload=payload, **kw)

    def fixture(project, document, body, asset=None):
        pname, dname = project.replace('-', ''), document.replace('-', '')
        state = {'ProjectId': project, 'Document': {'Title': '同名计划', 'Body': body,
            'Package': {'ProjectId': project, 'DocumentId': document, 'Body': body},
            'Resources': {'Items': [{'$kind':'schedule', 'Id':asset or document, 'Name':timeline_name, 'StartLocal':'08:45', 'EndLocal':'09:15'}]}}}
        files = {}
        if asset:
            sha = digest(photo)
            synthetic_path = r'C:\PlanningShare\assets\sample.jpg'
            storage_path = f'project-copies/{pname}/{dname}/reference/{asset.replace("-", "")}/{sha.lower()}.jpg'
            state['Document']['References'] = [{'Source': {'ReferenceId': asset, 'Kind': 3, 'ExternalReference': synthetic_path, 'Title': '服装图片'},
                'IsMoodboard': True, 'ProjectCopy': {'ProjectId': project, 'DocumentId': document, 'ReferenceId': asset, 'Kind': 'reference', 'OriginalPath': synthetic_path,
                'RelativePath': storage_path, 'Sha256': sha, 'Bytes': len(photo), 'ImportedAt': '2026-11-27T00:00:00Z'}}]
            files[f'ProjectPlanning/{storage_path}'] = photo
        files[f'ProjectPlanning/{pname}.planning.json'] = encode(state)
        identity = dict(destinationId=destination, projectId=project, documentId=document)
        manifest = dict(identity, sourceFingerprint=digest(encode(state)), files=[dict(path=path, length=len(raw), sha256=digest(raw)) for path, raw in files.items()])
        return identity, manifest, files

    def room_path(published):
        return '/r/' + UUID(published).hex

    def send_file(published, raw, expected=204, claimed=None):
        return share(published + '/files/' + (claimed or digest(raw)), raw, method='PUT', expected=expected)

    def send_seal(identity, partial, **kw):
        return share(partial['publicationId']+'/seal', dict(identity, manifestSha256=partial['manifestSha256']), **kw)

    def member_call(published, action, **kw):
        return call(room_path(published) + '/api/' + action, **kw)

    def owner_code(ready):
        owner = urlsplit(ready['ownerUrl'])
        member = urlsplit(ready['memberUrl'])
        path = room_path(ready['publicationId'])+'/workbench.html'
        assert owner._replace(fragment='').geturl() == loopback+path
        assert ready['memberUrl'] == member_base+path and not member.fragment and not member.query
        code = parse_qs(owner.fragment)['code'][0]
        assert code.startswith('owner-once.') and 'member.' not in ready['ownerUrl']
        return code

    def begin_complete(identity, manifest, files):
        part = share('begin', manifest)
        for raw in files.values():
            send_file(part['publicationId'], raw)
        return send_seal(identity, part)

    def op(published, token, project, action, *, host='127.0.0.1', expected=200, **rest):
        return member_call(published, 'op', who=token, host=host, expected=expected,
            payload=dict(id=str(uuid4()), project=project, action=action, **rest))

    try:
        start()
        p1, d1, asset = (str(uuid4()) for _ in range(3))
        identity, manifest, files = fixture(p1, d1, '已保存的本地正文 A', asset)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            first, second = [f.result() for f in [pool.submit(share,'begin',manifest), pool.submit(share,'begin',manifest)]]
        pub = first['publicationId']
        stage = 'PARTIAL_AND_HASH_GATE'
        assert first['state'] == second['state'] == 'partial' and pub == second['publicationId'] and len(first['missing']) == 2
        assert not first['ownerUrl'] and not first['memberUrl']
        member_call(pub, 'state', expected=404)
        call('/api/share/status', host=lan, who=secret, payload=identity, expected=403)
        call('/api/share/status', payload=identity, expected=403)
        call('/api/share/status', who=secret, payload=identity, origin=loopback, expected=403)
        share('status', dict(identity,destinationId=str(uuid4())), expected=409)
        assert share('status', dict(identity, documentId=str(uuid4())))['state'] == 'notPublished'
        unsafe_manifest = dict(manifest, files=manifest['files'] + [dict(path='ProjectPlanning/../identity.key', length=1, sha256=digest(b'x'))])
        share('begin', unsafe_manifest, expected=422)
        plan = next(raw for path,raw in files.items() if path.endswith('.planning.json'))
        send_file(pub, plan)
        send_file(pub, bytes([photo[0] ^ 1]) + photo[1:], expected=422, claimed=digest(photo))
        waiting = send_seal(identity, first)
        assert waiting['state'] == 'partial' and [f['sha256'] for f in waiting['missing']] == [digest(photo)]
        mark(stage, publicationId=pub, missingAssets=1, dismissedWrongHash=True, unauthorizedControlDenied=True)

        stage = 'PARTIAL_RESTART_AND_RESTAGE'
        stop()
        start()
        assert share('status', identity)['missing'] == waiting['missing']
        new_ident, restaged, new_files = fixture(p1, d1, '刷新了已保存的本地正文 A', asset)
        assert new_ident == identity
        moved = share('begin', restaged)
        assert moved['publicationId'] == pub and moved['manifestSha256'] != first['manifestSha256']
        send_seal(identity, first, expected=409)
        share(pub+'/seal', identity, expected=409)
        for raw in new_files.values():
            send_file(pub, raw)
        mark(stage, publicationId=pub, staleSealRejected=True, bindingUnchanged=True)

        stage = 'ALL_BYTES_WITH_INVALID_NATIVE_PLAN'
        invalid_identity, invalid_manifest, invalid_files = fixture(str(uuid4()), str(uuid4()), '身份错配计划')
        invalid_path = next(iter(invalid_files))
        bad_state = json.loads(invalid_files[invalid_path])
        bad_state['Document']['Package']['DocumentId'] = str(uuid4())
        invalid_raw = encode(bad_state)
        invalid_manifest['files'] = [dict(path=invalid_path, length=len(invalid_raw), sha256=digest(invalid_raw))]
        invalid_part = share('begin', invalid_manifest)
        invalid_pub = invalid_part['publicationId']
        send_file(invalid_pub, invalid_raw)
        assert not share('status', invalid_identity)['missing']
        send_seal(invalid_identity, invalid_part, expected=422)
        member_call(invalid_pub, 'state', expected=404)
        status_after = share('status', invalid_identity)
        assert status_after['state'] == 'partial' and status_after['publicationId'] == invalid_pub and not status_after['ownerUrl']
        mark(stage, publicationId=invalid_pub, noReadyLink=True, uploadedAllBytes=True)

        stage = 'LOST_ACK_AND_REOPEN'
        send_seal(identity, moved, discard=True)  # no receipt body reaches the desktop state machine
        stop()
        start()
        ready = share('status', identity)
        assert ready['state'] == 'ready' and ready['publicationId'] == pub and ready['publishedFingerprint'] == restaged['sourceFingerprint']
        code = owner_code(ready)
        member_call(pub, 'join', host=lan, payload={'code':code}, expected=403)
        owner = member_call(pub, 'join', payload={'code':code})['token']
        member_call(pub, 'join', payload={'code':code}, expected=401)
        member_call(pub,'join',payload={'code':owner},expected=403)
        fresh = owner_code(share('status', identity))
        assert fresh != code and member_call(pub, 'join', payload={'code':fresh})['token'] == owner
        member_call(pub, 'state', who=owner, host=lan, expected=403)
        assert member_call(pub,'state',who=owner)['workbench']['tier'] == 'team'
        assert call(room_path(pub)+'/workbench.html', raw=True).find(b'workbench.js') > 0
        assert b"fetch('api/'" in call(room_path(pub)+'/workbench.js', raw=True)
        call('/workbench.html', expected=404)
        call('/api/state', who=owner, expected=404)
        call(room_path(pub)+'/api/share/status', who=secret, payload=identity, expected=404)
        call(room_path(pub)+'/api/SHARE/status', host=lan, expected=404, method='POST')
        call('/API/SHARE/status', host=lan, expected=403, method='POST')
        call(room_path(pub)+'/notebook.json', who=owner, expected=404)
        assert member_call(pub,'image/'+asset,who=owner,raw=True) == photo
        mark(stage, publicationId=pub, originalFingerprint=ready['publishedFingerprint'], ownerTicketSingleUse=True,
             projectionRestored=True, sha256=digest(photo))

        stage = 'MEMBER_INVITE_ARTIFACT_AND_PERMISSIONS'
        op(pub, owner, p1, 'invite', text='合成成员')
        owner_state = member_call(pub,'state',who=owner)
        invitation = owner_state['invitations'][0]['code']
        assert invitation.startswith('invite.') and owner_state['memberUrl'] == ready['memberUrl']
        assert member_call(pub, 'state', host=lan, expected=401)
        member = member_call(pub,'join',host=lan,payload={'code':invitation})['token']
        member_call(pub,'join',host=lan,payload={'code':invitation},expected=403)
        member_state = member_call(pub,'state',who=member,host=lan)
        assert not member_state['me']['owner'] and member_state['invitations'] is None
        assert member_call(pub,'image/'+asset,who=member,host=lan,raw=True) == photo
        op(pub,member,p1,'invite',host=lan,expected=403,text='越权')
        op(pub,member,p1,'removeMember',host=lan,expected=403,target=member_state['me']['id'])
        member_call(pub,'export',host=lan,who=member,payload={},expected=403)
        member_call(pub,'workbench/docx',host=lan,who=member,expected=403)
        member_call(pub,'image/'+asset,host=lan,expected=401)
        member_call(pub,'presence',host=lan,expected=401,payload={'device':str(uuid4())})
        b = next(block for block in member_state['blocks'] if block['kind'] == 'text')
        op(pub, member, p1, 'comment', host=lan, base=b['version'], text='保留网上批注',
            anchor=dict(target=b['id'],quote=b['text'],start=0))
        mark(stage, publicationId=pub, team=True, ownerActionsDenied=True, memberImageSha256=digest(photo))

        if args.browser:
            stage = 'BROWSER_MEMBER_TIMELINE'
            op(pub, owner, p1, 'invite', text='真实浏览器成员')
            browser_code = next(i['code'] for i in member_call(pub, 'state', who=owner)['invitations'] if i['label'] == '真实浏览器成员')
            browser_path = room_path(pub) + '/workbench.html'
            member_link = ready['memberUrl'] + '#' + urlencode(dict(code=browser_code, route='timeline'))
            browser_input = encode(dict(browser=args.browser, profile=str(root/'member-browser'), spki=args.browser_spki,
                memberLink=member_link, roomPath=browser_path, timelineName=timeline_name))
            proc = subprocess.run([args.node, str(Path(__file__).parent/'accept_share_browser.mjs')], input=browser_input,
                capture_output=True, timeout=55, check=False)
            if proc.returncode != 0:
                raise AssertionError('Private headless browser timeline probe failed: ' + proc.stderr.decode('utf-8', errors='replace').splitlines()[0])
            mark(stage, **json.loads(proc.stdout))

        stage = 'READY_IMMUTABLE_AND_ISOLATED'
        other_project, other_doc = str(uuid4()),str(uuid4())
        alternate, alternate_manifest, alternate_files = fixture(p1,d1,'不得覆盖已有在线工作台',asset)
        again = share('begin', alternate_manifest)
        assert alternate == identity and again['publicationId'] == pub and again['publishedFingerprint'] == ready['publishedFingerprint']
        for raw in alternate_files.values():
            send_file(pub, raw)
        assert send_seal(identity, again)['publicationId'] == pub
        assert share(pub+'/seal', identity)['publicationId'] == pub
        a_state = member_call(pub,'state',who=owner)
        assert a_state['blocks'][0]['text'] == '刷新了已保存的本地正文 A' and a_state['threads'][0]['notes'][0]['text'] == '保留网上批注'
        second_room = begin_complete(*fixture(other_project, other_doc, '同名不同项目'))
        third_room = begin_complete(*fixture(p1, str(uuid4()), '同名同项目不同文档'))
        room_b, room_c = second_room['publicationId'],third_room['publicationId']
        assert len({pub,room_b,room_c}) == 3
        assert member_call(room_b,'join',payload={'code':owner_code(second_room)})['project'] == other_project
        member_call(room_b,'state',who=owner,expected=401)
        member_call(room_c,'state',who=member,host=lan,expected=401)
        assert member_call(room_c,'join',payload={'code':owner_code(third_room)})['project'] == p1
        assert not (data/'owner.local').exists() and not (data/'notebook.json').exists()
        mark(stage, rooms=[pub, room_b, room_c], sameTitle=a_state['title'], originalRevision=a_state['revision'], notesRetained=True)
        stage = 'LEGACY_WORKBENCH_REGRESSION'
        stop()
        start(legacy=True)
        legacy_owner = (root/'legacy-host'/'owner.local').read_text(encoding='utf-8')
        legacy_state = call('/api/state', who=legacy_owner)
        assert legacy_state['me']['owner'] and legacy_state['workbench']['tier'] == 'basic'
        call('/api/share/status', who=secret, payload=identity, expected=404)
        assert call('/workbench.html', raw=True).find(b'workbench.js') > 0
        call('/api/op', who=legacy_owner, payload=b'', declared_length=7400001, expected=400)
        mark(stage, singleProjectRoot=True, candidateApiUnmapped=True, oldUploadCap=7400000)
        save('PASS')
    except BaseException:
        checks.append({'failedAt': stage})
        save('FAIL')
        raise
    finally:
        stop()


if __name__ == '__main__':
    cli = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'dll', 'cert', 'pin', 'lan', 'output'):
        cli.add_argument('--'+name, required=True)
    cli.add_argument('--port', type=int, default=45763)
    cli.add_argument('--browser', help='Optional Chromium executable for the member timeline route')
    cli.add_argument('--browser-spki', help='Base64 SHA-256 hash of the QA certificate SPKI')
    cli.add_argument('--node', default='node')
    run(cli.parse_args())
