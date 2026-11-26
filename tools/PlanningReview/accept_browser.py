import argparse
import hashlib
import json
import os
import time
from pathlib import Path
from playwright.sync_api import sync_playwright, expect


def run(args):
    out = Path(args.output); out.mkdir(parents=True, exist_ok=False)
    checks = []
    errors = []
    def mark(name, **details):
        checks.append(dict(check=name, **details)); (out/'steps.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf8')
    def state(page):
        return page.evaluate("async()=>{const token=JSON.parse(localStorage.getItem('review-session')).token;return (await fetch('/api/state',{headers:{Authorization:'Bearer '+token}})).json()}")
    def await_state(page, condition):
        limit=time.monotonic()+15
        while time.monotonic()<limit:
            value=state(page)
            if condition(value): return value
            page.wait_for_timeout(140)
        raise AssertionError('Scenario timed out waiting for committed state')
    def nav(page, kind): page.locator(f'[data-page={kind}]').click()
    def card(page, word): return page.locator('.thread').filter(has=page.get_by_text(word,exact=True))
    def screenshot(page,name): page.screenshot(path=str(out/(name+'.png')),animations='disabled')
    with sync_playwright() as runner:
        def client(label,url,code=None):
            context=runner.chromium.launch_persistent_context(str(Path(args.profiles)/label),executable_path=os.environ.get('PLANNING_QA_CHROMIUM'),headless=True,ignore_https_errors=True,viewport={'width':1370,'height':900},args=['--no-proxy-server','--no-first-run','--disable-background-networking'])
            page=context.pages[0];page.set_default_timeout(14000);page.on('pageerror',lambda err:errors.append(str(err)));page.goto(url)
            if code and page.locator('#code').is_visible():
                page.locator('#code').evaluate('(field,value)=>field.value=value',code);page.locator('#join button').click()
            expect(page.locator('#title')).to_have_text('秋冬服装型录'); return context,page
        owner=Path(args.owner).read_text(encoding='utf-8-sig').strip()
        first,a=client('A',args.local,owner)
        base=state(a); assert (args.resume or not base['team']) and len(base['order'])==6
        para=base['blocks'][0]; text=para['text']; start=text.index('秋冬外套'); quote_text='秋冬外套'
        paragraph=a.locator('.textblock p').first;box=paragraph.evaluate('''(el,range)=>{const r=document.createRange();r.setStart(el.firstChild,range[0]);r.setEnd(el.firstChild,range[1]);const b=r.getBoundingClientRect();return [b.left+0.2,b.right-0.2,b.top+b.height/2]}''',[start,start+len(quote_text)])
        if not args.resume:
            a.mouse.move(box[0],box[2]);a.mouse.down();a.mouse.move(box[1],box[2],steps=12);a.mouse.up();a.locator('.textblock [data-cmd=annotate-text]').first.click();a.locator('#note').fill('确认主角是秋冬外套');a.locator('#sendNote').click()
            text_card=card(a,'确认主角是秋冬外套');text_card.locator('[data-cmd=reply]').click();text_card.locator('textarea').fill('以外套为主。');text_card.locator('[data-cmd=send-reply]').click();expect(text_card).to_contain_text('以外套为主。');text_card.locator('[data-cmd=status]').click();expect(text_card.locator('.state')).to_have_text('已解决');text_card.locator('[data-cmd=status]').click();expect(text_card.locator('.state')).to_have_text('待处理');
        text_thread=state(a)['threads'][0];assert text_thread['anchor']['quote']==quote_text and text_thread['anchor']['start']==start
        mark('专业版文字选区、署名、回复、解决与重开',anchor=text_thread['anchor'],thread=text_thread['id'])
        nav(a,'image');
        if not args.resume:a.locator('#upload').set_input_files(str(Path(args.photo).resolve()))
        img=a.locator('.frame img');expect(img).to_be_visible();a.wait_for_function("document.querySelector('.frame img')?.naturalWidth>0");image_state=state(a);image=next(b for b in image_state['blocks'] if b['kind']=='image');digest=hashlib.sha256(Path(args.photo).read_bytes()).hexdigest().upper();assert image['hash']==digest
        if not args.resume:
            a.locator('#team').click();a.get_by_role('button',name='升级为团队项目').click();await_state(a,lambda s:s['team']);a.locator('#team').click();a.get_by_label('受邀成员署名').fill('造型师 B');a.get_by_role('button',name='生成成员邀请').click();up=await_state(a,lambda s:len(s['invitations'])==1)
        else: up=state(a)
        assert (up['project'],up['document'])==(base['project'],base['document']) and up['threads'][0]==text_thread and any(b['id']==image['id'] for b in up['blocks'])
        second,b=client('B',args.lan,None if args.resume else up['invitations'][0]['code']);nav(b,'image');b.wait_for_function("document.querySelector('.frame img')?.naturalWidth===960");assert b.evaluate("document.querySelector('.frame img').naturalHeight")==1440
        encoded_digest=b.evaluate("async id=>{const h={Authorization:'Bearer '+JSON.parse(localStorage.getItem('review-session')).token};const bytes=await(await fetch('/api/image/'+id,{headers:h})).arrayBuffer();return [...new Uint8Array(await crypto.subtle.digest('SHA-256',bytes))].map(n=>n.toString(16).padStart(2,'0')).join('').toUpperCase()}",image['id']);assert digest==encoded_digest
        mark('专业升级保留策划/分镜/原图/批注；B 经实体网卡地址取得同一图片',project=up['project'],document=up['document'],reference=image['id'],sha256=digest,dimensions=[960,1440],transport=args.lan)
        frame=b.locator('.frame');frame.locator('img').scroll_into_view_if_needed();coords=frame.evaluate('''el=>{const sc=el.closest('.photoScroll');sc.scrollTop=sc.scrollHeight;const r=el.getBoundingClientRect();return [r.x+r.width*.43,r.y+r.height*.69,r.x+r.width*.71,r.y+r.height*.805]}''');b.mouse.move(*coords[:2]);b.mouse.down();b.mouse.move(*coords[2:],steps=14);b.mouse.up();note='补一张侧逆光，保留衣领纹理';b.locator('#note').fill(note);b.locator('#sendNote').click();acol=card(a,note);expect(acol).to_be_visible();acol.locator('[data-cmd=jump]').click();s1=state(a);point_thread=next(t for t in s1['threads'] if t['notes'][0]['text']==note);r=point_thread['anchor']['rect'];assert all(abs(v-w)<.008 for v,w in zip(r,[.43,.69,.28,.115]));assert point_thread['notes'][0]['name']=='造型师 B'
        def norm(page):return page.locator('.selectedRect').first.evaluate("el=>{const v=el.getBoundingClientRect(),p=el.parentElement.getBoundingClientRect();return [(v.x-p.x)/p.width,(v.y-p.y)/p.height,v.width/p.width,v.height/p.height]}")
        before=norm(a);a.get_by_label('图片缩放').select_option('150');after=norm(a);assert max(abs(x-y) for x,y in zip(before,after))<.001
        expect(a.locator('#presence')).to_contain_text('造型师 B');screenshot(a,'01-collar-jump');acol.locator('[data-cmd=reply]').click();acol.locator('textarea').fill('收到，增加侧逆光机位，领面不过曝。');acol.locator('[data-cmd=send-reply]').click();expect(acol).to_contain_text('领面不过曝');acol.locator('[data-cmd=status]').click();expect(card(b,note).locator('.state')).to_have_text('已解决');mark('B 框选翻领，A 跳转、缩放、回复并解决，B 看见处理结果',thread=point_thread['id'],rectangle=r,screenBounds100=before,screenBounds150=after)
        nav(b,'shot');shots=b.locator('.shot');shot_id=base['order'][2];six=base['order'][5];shots.nth(2).locator('[data-cmd=annotate-shot]').click();b.locator('#note').fill('此镜检查领缘与下颌分离。');b.locator('#sendNote').click();expect(card(b,'此镜检查领缘与下颌分离。')).to_be_visible();shots.nth(2).drag_to(shots.nth(5));updated=await_state(b,lambda s:s['order'].index(shot_id)==s['order'].index(six)+1);card(a,'此镜检查领缘与下颌分离。').locator('[data-cmd=jump]').click();expect(a.locator('.shot.selected')).to_have_attribute('data-block',shot_id);expect(a.locator('.shot.selected .shotnum')).to_have_text('06');mark('B 将第三镜拖到第六镜之后；A 从原批注跳至新的第六位',originalOrder=base['order'],actualOrder=updated['order'],shotId=shot_id)
        first.close();second.close();first,a=client('A',args.local);second,b=client('B',args.lan);aa,bb=state(a),state(b);assert aa['project']==bb['project'] and aa['order']==bb['order']==updated['order'] and aa['threads']==bb['threads'];ret=next(t for t in aa['threads'] if t['id']==point_thread['id']);assert ret['resolved'] and ret['anchor']['rect']==r and [n['name'] for n in ret['notes']]==['造型师 B','负责人 A'];nav(b,'image');b.wait_for_function("document.querySelector('.frame img')?.naturalWidth===960");card(a,note).locator('[data-cmd=jump]').click();screenshot(a,'02-reopen-owner');screenshot(b,'03-reopen-member');mark('关闭两份独立 Chromium 进程并重开，身份/照片/区域/回复/状态/顺序保持一致',authors=[n['name'] for n in ret['notes']],times=[n['at'] for n in ret['notes']],stateVersion=aa['revision'],errors=errors);assert not errors
        first.close();second.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser();
    parser.add_argument('--resume',action='store_true')
    for option in ['local','lan','owner','photo','profiles','output']:parser.add_argument('--'+option,required=True)
    run(parser.parse_args())
