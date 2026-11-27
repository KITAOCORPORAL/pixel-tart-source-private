"""Use the normal EXE's already published synthetic fixture, then edit via real web controls.

Only works against an isolated Start-ShareCandidate config with matching fixture IDs.
The QA browser trusts only this saved certificate public key and uses fresh profiles.
Outputs proof only; never print/share the control secret, owner ticket, or member links.
"""
import argparse, asyncio, base64, hashlib, http.client, json, re, ssl, subprocess
from pathlib import Path
from urllib.parse import urlsplit, urlunsplit
from datetime import datetime, timezone
from playwright.async_api import async_playwright, expect

NOTE='造型师意见：衣领内侧再补一张有织纹的近景。'
REPLY='已确认衣领织纹，按新通告补拍。'
UPDATE='北窗衣领复核 · 10:25'
DESKTOP_INPUT='桌面光镜验收 · 初次发布'
MEMBER='造型师 B · 验收'


def pinned_receipt(root, fixture):
    config=json.loads((root/'host.json').read_text(encoding='utf-8-sig'))
    assert config['purpose']=='guangjing-planning-share-candidate'
    origin=urlsplit(config['controlBase']); assert origin.scheme=='https' and origin.hostname=='127.0.0.1'
    tls=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT); tls.check_hostname=False; tls.verify_mode=ssl.CERT_NONE
    conn=http.client.HTTPSConnection('127.0.0.1',origin.port,context=tls,timeout=10)
    try:
        conn.connect()
        assert hashlib.sha256(conn.sock.getpeercert(binary_form=True)).hexdigest().upper()==config['certificateSha256'].upper()
        request=dict(destinationId=config['destinationId'],projectId=fixture['projectId'],documentId=fixture['documentId'])
        conn.request('POST','/api/share/status',json.dumps(request),{'Content-Type':'application/json','Authorization':'Bearer '+(root/'control.key').read_text().strip()})
        response=conn.getresponse(); assert response.status==200
        receipt=json.loads(response.read())
    finally:conn.close()
    assert all(receipt[k]==v for k,v in request.items()) and receipt['state']=='ready'
    owner=urlsplit(receipt['ownerUrl']); member=urlsplit(receipt['memberUrl'])
    assert owner.netloc==origin.netloc and member.netloc==urlsplit(config['memberBase']).netloc
    assert owner.path==member.path and owner.path==f'/r/{receipt["publicationId"].replace("-", "")}/workbench.html'
    assert owner.scheme==member.scheme=='https' and owner.fragment.startswith('code=owner-once.') and not member.fragment
    return receipt

async def run(args):
    root=Path(args.delivery).resolve()/'_private'; out=Path(args.output).resolve(); out.mkdir(parents=True,exist_ok=False)
    assert out.is_relative_to(root), 'Proof with real company profiles must stay private.'
    fixture=next(p for p in json.loads((root/'fixture.json').read_text())['projects'] if not p['control'])
    receipt=pinned_receipt(root,fixture)
    # The launcher exports only the public .cer to the delivery. Compute SPKI for Chromium.
    b64=subprocess.run(['pwsh','-NoProfile','-Command',"$path=$env:GUANGJING_QA_CERT; $c=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile($path); try { [Convert]::ToBase64String([Security.Cryptography.SHA256]::HashData($c.PublicKey.ExportSubjectPublicKeyInfo())) } finally {$c.Dispose()}"],
        env=__import__('os').environ|{'GUANGJING_QA_CERT':str(root.parent/'团队协作候选证书.cer')},capture_output=True,text=True,check=True).stdout.strip()
    assert len(base64.b64decode(b64,validate=True))==32
    actions=[]; errors=[]; stage='open candidate'
    result=dict(status='RUNNING',mode=args.phase,publicationId=receipt['publicationId'],projectId=fixture['projectId'],documentId=fixture['documentId'],
        physicalMachines=1,chromeClients=['owner loopback','member LAN'],checks=actions)
    def persist():
        (out/'BROWSER.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    def mark(check, **kw):
        actions.append(dict(check=check, **kw));persist(); print('PASS',check,flush=True)
    async def snap(page,name):
        file=out/(name+'.png');await page.screenshot(path=file,animations='disabled'); return {'file':file.name,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
    async def state(p):
        s=await p.evaluate("""async()=>{const key='workbench-session'+location.pathname.replace(/workbench\\.html$/,'');const identity=JSON.parse(localStorage.getItem(key)||'null');if(!identity)return null;const r=await fetch(new URL('./api/state',location.href),{headers:{Authorization:'Bearer '+identity.token},cache:'no-store'});return r.ok?await r.json():null}""")
        if s: assert s['project']==fixture['projectId'] and s['document']==fixture['documentId']
        return s
    async def until(p,fn):
        for _ in range(75):
            s=await state(p)
            if s and fn(s):return s
            await asyncio.sleep(.14)
        raise AssertionError('Saved state did not match: '+stage)
    def section(s,key):return next(p for p in s['workbench']['pages'] if p['section']==key)
    def thread(s):return next((t for t in s['threads'] if t['notes'][0]['text']==NOTE),None)
    async def click(p,name):await p.get_by_role('button',name=name,exact=True).click()
    async def nav(p,label):await p.locator('#sections button').filter(has_text=label).click();await expect(p.locator('#pageHead h1')).to_contain_text(label)
    async def save(p):await p.locator('#save').click();await expect(p.locator('#sync')).to_have_text('已保存')
    async def image_hash(p,asset):
        return await p.evaluate("""async id=>{const key='workbench-session'+location.pathname.replace(/workbench\\.html$/,'');const identity=JSON.parse(localStorage.getItem(key));const r=await fetch(new URL('./api/image/'+id,location.href),{headers:{Authorization:'Bearer '+identity.token}});if(!r.ok)throw Error('No image');const x=await r.arrayBuffer();return [...new Uint8Array(await crypto.subtle.digest('SHA-256',x))].map(x=>x.toString(16).padStart(2,'0')).join('').toUpperCase()}""",asset)
    profiles=root/'collaboration-browser-profiles';profiles.mkdir(exist_ok=True)
    async with async_playwright() as pw:
        clients=[]
        async def launch(name,url):
            context=await pw.chromium.launch_persistent_context(str(profiles/name),executable_path=args.chromium,headless=True,
                viewport={'width':1700,'height':1060},args=['--no-proxy-server','--no-first-run','--disable-background-networking','--ignore-certificate-errors-spki-list='+b64]);clients.append(context)
            p=context.pages[0];p.set_default_timeout(10000);p.on('pageerror',lambda e:errors.append(type(e).__name__));await p.goto(url);await expect(p.locator('#work')).to_be_visible();return p
        try:
            a=await launch('A',receipt['ownerUrl']); aa=await until(a,lambda s:s['me']['owner'] and s['team']);result['chromium']=await a.evaluate('navigator.userAgent')
            if args.phase=='member-edit':
                stage='publication content from desktop'
                assert len(aa['workbench']['pages'])==15 and aa['order']==fixture['shotOrder']
                overview=section(aa,'overview')['data']['rows'][0]['cells']; assert overview['subtitle']==DESKTOP_INPUT
                original=next(s for s in aa['workbench']['shotDetails'] if s['id']==fixture['originalThirdShot']);assert original['assetId']==fixture['collar']
                await nav(a,'文字'); await expect(a.locator('.we-nativetable')).to_be_visible()
                brief=section(aa,'brief')['data']['paragraphs']; selected=next(p for p in brief if '衣领织纹' in p.get('text','')); fragment=next(m for m in selected['marks'] if m.get('bold') and m.get('italic')); assert selected['text'][fragment['start']:fragment['end']]=='衣领织纹'
                table=next(p['nativeTable'] for p in brief if p.get('nativeTable')); assert table['repeatHeader'] and len(table['rows'])==3
                mark(stage, pages=[p['section'] for p in aa['workbench']['pages']],themedRun=['衣领织纹','bold','italic'], rows=len(table['rows']), shotOrder=aa['order'], link=original['assetId'], screenshot=await snap(a,'01-desktop-brief-projected'))
                stage='member link and independent session';await a.locator('#people').click();await a.get_by_label('受邀成员署名').fill(MEMBER);await click(a,'生成成员邀请')
                invited=await until(a,lambda s:any(i['label']==MEMBER for i in s['invitations']));invitation=next(i for i in invited['invitations'] if i['label']==MEMBER)
                await a.evaluate("""Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:async v=>{window.__qaCopied=v}}})""")
                await click(a,'复制成员链接')
                link=await a.evaluate('window.__qaCopied'); read=urlsplit(link);assert urlunsplit(read._replace(fragment=''))==receipt['memberUrl'] and read.fragment.startswith('code=invite.') and 'owner' not in read.fragment
                await a.get_by_label('关闭对话框').click();b=await launch('B',link); bb=await until(b,lambda s:s['me']['name']==MEMBER and not s['me']['owner']);assert bb['me']['id']!=aa['me']['id'] and await b.evaluate("location.hash==='#overview'")
                hashes={r['id']:await image_hash(b,r['id']) for r in fixture['assets']};assert all(hashes[r['id']]==r['sha256'] for r in fixture['assets'])
                await nav(b,'情绪板');await expect(b.locator('[data-kind=image] img')).to_have_count(4);assert await image_hash(b,fixture['fixedCanvas'])==next(z['hash'] for z in bb['blocks'] if z['id']==fixture['fixedCanvas']);
                mark(stage,images=hashes, fixedCanvas=True, invitationNotOwner=True, distinctIdentity=True, screenshot=await snap(b,'02-member-mood'))
                stage='B collar region anchored to native shot';await nav(b,'镜头清单与分镜'); card=b.locator(f'[data-shot="{fixture["originalThirdShot"]}"]');await expect(card.locator('.wb-num')).to_have_text('03');await card.get_by_role('button',name='打开现场参考',exact=True).click();await click(b,'框选图片批注')
                img=b.locator('.wb-review-figure img');await img.scroll_into_view_if_needed();g=await img.bounding_box();assert g
                await b.mouse.move(g['x']+.43*g['width'],g['y']+.69*g['height']);await b.mouse.down();await b.mouse.move(g['x']+.70*g['width'],g['y']+.80*g['height'],steps=8);await b.mouse.up()
                await click(b,'添加区域批注');await b.get_by_label('批注内容').fill(NOTE);await click(b,'发布批注');bb=await until(b,lambda s:thread(s)); t=thread(bb);an=t['anchor'];assert an['target']==fixture['collar'] and an['objectId']==fixture['originalThirdShot'] and an['hash']==hashes[fixture['collar']] and not an['detached']; assert t['notes'][0]['name']==MEMBER
                mark(stage,anchor=an,author=t['notes'][0]['name'], screenshot=await snap(b,'03-member-collar-comment'))
                stage='B saves actual native brief table and timeline row';await nav(b,'文字');cell=b.locator('.we-nativetable [contenteditable=true]').filter(has_text='10:00');await expect(cell).to_have_count(1);await cell.fill('10:25');await save(b)
                await nav(b,'当天时间表'); row=b.locator(f'[data-record="{fixture["timelineRow"]}"]');await row.get_by_label('名称',exact=True).fill(UPDATE);await row.get_by_label('开始',exact=True).fill('10:25');await save(b)
                bb=await until(b,lambda s: next(r for r in section(s,'timeline')['data']['rows'] if r['id']==fixture['timelineRow'])['cells']['name']==UPDATE)
                assert any(any(x['text']=='10:25' for c in r['cells'] for x in c['paragraphs']) for w in section(bb,'brief')['data']['paragraphs'] if w.get('nativeTable') for r in w['nativeTable']['rows']); mark(stage,revision=bb['revision'], timelineRow=fixture['timelineRow'], newName=UPDATE, briefingCell='10:25', screenshot=await snap(b,'04-member-updated-timeline'))
            else:
                stage='owner reads B’s saved timeline and discussion'; assert thread(aa) is not None
                assert next(r for r in section(aa,'timeline')['data']['rows'] if r['id']==fixture['timelineRow'])['cells']['name']==UPDATE
                await nav(a,'镜头清单与分镜');await click(a,'讨论'); c=a.locator('.wb-thread').filter(has_text=NOTE);await expect(c).to_be_visible()
                stage='owner locates collar, replies and resolves';await c.get_by_role('button',name='定位',exact=True).click();await expect(a.locator('.wb-review-figure i')).to_be_visible();await a.get_by_label('关闭对话框').click()
                if not any(n['text']==REPLY for n in thread(aa)['notes']):
                    await c.get_by_role('button',name='回复',exact=True).click();await a.get_by_label('回复内容').fill(REPLY);await click(a,'发送回复')
                if not thread(aa)['resolved']: await c.get_by_role('button',name='标为已解决',exact=True).click()
                aa=await until(a,lambda s:thread(s)['resolved'] and thread(s)['notes'][-1]['text']==REPLY)
                mark(stage, notes=[n['name'] for n in thread(aa)['notes']]);stage='owner moves original shot from third to sixth'
                old=fixture['shotOrder'];first=a.locator(f'[data-shot="{fixture["originalThirdShot"]}"]')
                if aa['order'][-1]!=fixture['originalThirdShot']:
                    await expect(first.locator('.wb-num')).to_have_text('03');await first.get_by_role('button',name='换序 / 更多',exact=True).click();await a.get_by_label('移动到指定镜头后').select_option(old[-1]);await click(a,'移动分镜')
                aa=await until(a,lambda s:s['order'][-1]==fixture['originalThirdShot'] and thread(s)['resolved']);await expect(first.locator('.wb-num')).to_have_text('06')
                mark(stage,originalOrder=old,newOrder=aa['order'],sourceControl='换序 / 更多',screenshot=await snap(a,'05-owner-reordered'))
                stage='B reopens and locates same marked collar';b=await launch('B',receipt['memberUrl']+'#shots');bb=await until(b,lambda s:s['me']['name']==MEMBER and s['order'][-1]==fixture['originalThirdShot']);assert thread(bb)['anchor']==thread(aa)['anchor'];await click(b,'讨论'); c=b.locator('.wb-thread').filter(has_text=NOTE);await expect(c).to_contain_text(REPLY);await c.get_by_role('button',name='定位',exact=True).click();await expect(b.locator('.wb-review-figure i')).to_be_visible();await expect(b.locator(f'[data-shot="{fixture["originalThirdShot"]}"] .wb-num')).to_have_text('06'); mark(stage,anchor=thread(bb)['anchor'], resolved=True,screenshot=await snap(b,'06-member-same-collar'))
            assert not errors;result['status']='PASS';persist()
        except Exception as error:
            result.update(status='PARTIAL', failedStage=stage, errorType=type(error).__name__);persist();raise RuntimeError('Candidate UI acceptance stopped at '+stage) from None
        finally:
            for ctx in reversed(clients):await ctx.close()

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--delivery',required=True);p.add_argument('--output',required=True);p.add_argument('--chromium',required=True)
    p.add_argument('--phase',choices=['member-edit','owner-reorder'],required=True)
    asyncio.run(run(p.parse_args()))
