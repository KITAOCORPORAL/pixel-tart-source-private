"""Company acceptance. Only against a new isolated QA instance; never run on a reviewer project.

Run with the pilot Playwright venv, --chromium headless shell, --local/--lan sharing
one NEW port, --owner private QA credential file, --project QA GUID, --output NEW local
proof directory and --profiles NEW private directory. Never pass credentials on argv.
"""
import argparse
import asyncio
import datetime as dt
import hashlib
import json
import re
import ssl
import sys
from pathlib import Path
from urllib.parse import urlsplit
from playwright.async_api import async_playwright, expect


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def safe_error(exc):
    return re.sub(r'(?:member|invite)\.[0-9a-f]+\.[0-9a-f]+','[private]',str(exc).splitlines()[0])[:300]


async def run(args):
    out=Path(args.output); profiles=Path(args.profiles); local=urlsplit(args.local); lan=urlsplit(args.lan)
    assert local.scheme==lan.scheme=='https' and local.hostname=='127.0.0.1' and local.port==lan.port and local.port not in (None,443,45731,45732), 'Refusing non-QA listener'
    assert lan.hostname not in ('localhost','127.0.0.1') and args.project!='a276b086-36dd-441e-b730-86ab876ac112'
    assert not out.exists() and not profiles.exists(), 'Fresh proof/profiles required'
    out.mkdir(parents=True); profiles.mkdir(parents=True)
    record={'status':'RUNNING','startedUtc':utc(),'project':args.project,'physicalMachines':1,'clients':{'A':args.local,'B':args.lan},'actions':[],'pageErrors':[]}; stage='start'; current_page=None
    def persist():
        (out/'BROWSER-E2E.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    def mark(check,**evidence):
        record['actions'].append({'check':check,'timeUtc':utc(),**evidence});persist();print('PASS',check,flush=True)
    async def shot(p,name):
        file=out/(name+'.png');await p.screenshot(path=file,animations='disabled');return {'file':file.name,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
    async def state(p):
        s=await p.evaluate("async()=>{const t=JSON.parse(localStorage.getItem('workbench-session')).token;const r=await fetch('/api/state',{headers:{Authorization:'Bearer '+t},cache:'no-store'});if(!r.ok)throw Error('state '+r.status);return await r.json()}");assert s['project']==args.project;return s
    async def until(p,fn):
        for _ in range(100):
            s=await state(p)
            if fn(s):return s
            await asyncio.sleep(.16)
        raise AssertionError('Committed state did not satisfy '+stage)
    def pg(s,id):return next(z for z in s['workbench']['pages'] if z['id']==id)
    def objects(s,id):return pg(s,id)['data']['objects']
    def picobj(s,id,obj):return next(o for o in objects(s,id) if o['id']==obj)
    def rect(o):return [o[k] for k in ('x','y','w','h','rotate')]
    def byname(p,n):return p.get_by_role('button',name=n,exact=True)
    async def btn(p,n):await byname(p,n).click()
    async def region(p,loc,ax=.22,ay=.22,bx=.76,by=.75):
        await loc.scroll_into_view_if_needed();q=await loc.bounding_box();assert q and q['width']>1 and q['height']>1
        await p.mouse.move(q['x']+ax*q['width'],q['y']+ay*q['height']);await p.mouse.down();await p.mouse.move(q['x']+bx*q['width'],q['y']+by*q['height'],steps=10);await p.mouse.up()
    async def select_part(p,loc,start,length):
        await loc.focus();await p.keyboard.press('Control+Home');
        for _ in range(start):await p.keyboard.press('ArrowRight')
        for _ in range(length):await p.keyboard.press('Shift+ArrowRight')
        return await p.evaluate('getSelection().toString()')
    async def image_hash(p,id):
        return await p.evaluate("""async id=>{const t=JSON.parse(localStorage.getItem('workbench-session')).token;const r=await fetch('/api/image/'+id,{headers:{Authorization:'Bearer '+t}});if(r.status!==200)throw Error('image status '+r.status);const a=await r.arrayBuffer();const h=await crypto.subtle.digest('SHA-256',a);return {bytes:a.byteLength,sha256:Array.from(new Uint8Array(h),v=>v.toString(16).padStart(2,'0')).join('')}}""",id)
    async def save(p):await p.locator('#save').click();await expect(p.locator('#sync')).to_have_text('已保存')
    async def jump(p,target):await p.locator('#pages [data-page="'+target+'"]').click();await expect(p.locator('.we-board')).to_be_visible()
    def anchor_view(t):return {k:t['anchor'].get(k) for k in ('target','pageId','objectId','start','quote','hash','rect','detached')}
    async with async_playwright() as pw:
        contexts=[]
        async def client(n,base,code=None):
            c=await pw.chromium.launch_persistent_context(str(profiles/n),executable_path=args.chromium,headless=True,ignore_https_errors=True,viewport={'width':1700,'height':1060},args=['--no-proxy-server','--no-first-run','--disable-background-networking']);contexts.append(c);p=c.pages[0];p.set_default_timeout(11500);p.on('pageerror',lambda e:record['pageErrors'].append(safe_error(e)));await p.goto(base.rstrip('/')+'/workbench.html'+('#code='+code if code else ''));await expect(p.locator('#library')).to_be_visible();return c,p
        try:
            owner=Path(args.owner).read_text('utf-8-sig').strip(); stage='open Basic';ca,a=await client('A',args.local,owner);current_page=a;ss=await state(a);assert ss['me']['owner'] and ss['workbench']['tier']=='basic';assert len(ss['order'])>=6
            initial={z['id'] for z in ss['workbench']['pages']};source={z['id']:z for z in ss['blocks'] if z['kind']=='image' and not z['removed']};assert len(source)>=3;record['browserUA']=await a.evaluate('navigator.userAgent');record['memberA']=ss['me']['id'];mark(stage,project=ss['project'],tier=ss['workbench']['tier'],assets=len(source),shots=len(ss['order']),screenshot=await shot(a,'01-basic-list'))
            stage='mood template';await a.locator('.wb-list-row').get_by_role('button',name='打开',exact=True).click();await a.locator('#pages').get_by_role('button',name='模板',exact=True).click();await btn(a,'套用「情绪板」模板');await expect(a.locator('.we-board')).to_be_visible();ss=await until(a,lambda s:len(s['workbench']['pages'])==len(initial)+1);page=next(z['id'] for z in ss['workbench']['pages'] if z['id'] not in initial);mark(stage,page=page,title=pg(ss,page)['title'])
            stage='drop three project assets';thumb=a.locator('#asideBody .wb-asset[data-asset]');await expect(thumb.first).to_be_visible();thumb_ids=await thumb.evaluate_all('nodes=>nodes.map(n=>n.dataset.asset)');pool=[i for i in thumb_ids if i in source];assert len(set(pool))>=3;collar=next((i for i in pool if '衣领' in source[i]['text'] or 'collar' in source[i]['text'].lower()),pool[1]);others=[i for i in dict.fromkeys(pool) if i!=collar];chosen=[others[0],collar,others[1]];board=a.locator('.we-board');bbs=await board.bounding_box();assert bbs
            async def drag_asset(id,u,v):
                src=a.locator('#asideBody [data-asset="'+id+'"]');await src.drag_to(board,target_position={'x':bbs['width']*u,'y':bbs['height']*v})
            for n,(id,u) in enumerate(zip(chosen,[.10,.39,.69]),1):
                await drag_asset(id,u,.31);await expect(board.locator('[data-kind=image]')).to_have_count(n)
            await save(a);ss=await until(a,lambda s:len([o for o in objects(s,page) if o['kind']=='image'])==3);imgs=[next(o for o in objects(ss,page) if o.get('assetId')==id) for id in chosen];mark(stage,assetIds=chosen,objectIds=[o['id'] for o in imgs],screenshot=await shot(a,'02-dragged-three'))
            stage='crop original and replace preserving bounds';oid=imgs[0]['id'];obj=a.locator('[data-object="'+oid+'"]');await obj.click();await btn(a,'裁切图片');await region(a,obj.locator('.we-imgclip'));await btn(a,'应用裁切');await save(a);ss=await until(a,lambda s:picobj(s,page,oid).get('crop',{}).get('w',1)<.95);cropped=picobj(ss,page,oid);crop=cropped['crop'];assert 0<crop['w']<1 and 0<crop['h']<1
            async def choose_asset(p,id):
                choice=p.locator('#panelBody .wb-asset').filter(has=p.get_by_text(source[id]['text'],exact=True));await choice.click();await asyncio.sleep(.15)
            await btn(a,'替换图片');await choose_asset(a,chosen[2]);await save(a);ss=await until(a,lambda s:picobj(s,page,oid)['assetId']==chosen[2]);new=picobj(ss,page,oid);assert rect(new)==rect(cropped) and new['id']==oid and new['crop']=={'x':0,'y':0,'w':1,'h':1};mark(stage,objectId=oid,beforeAsset=chosen[0],afterAsset=new['assetId'],cropBeforeReplacement=crop,bounds=rect(new))
            stage='canvas title Chinese composition and partial formatting';title_obj=next(o for o in objects(ss,page) if o['kind']=='text');tid=title_obj['id'];tloc=a.locator('[data-object="'+tid+'"]');await tloc.dblclick();ed=a.get_by_role('textbox',name='编辑画布标题');await ed.focus();await a.keyboard.press('Control+a');session=await ca.new_cdp_session(a);await ed.evaluate("n=>{window.__qaIme=[];for(const ev of ['compositionstart','compositionupdate','compositionend'])n.addEventListener(ev,e=>window.__qaIme.push([e.type,e.data]))}");phr='秋冬衣领';await session.send('Input.imeSetComposition',{'text':phr,'selectionStart':len(phr),'selectionEnd':len(phr)});await session.send('Input.insertText',{'text':phr});await a.keyboard.insert_text(' · 侧逆光');full=phr+' · 侧逆光';await expect(ed).to_have_text(full);assert await select_part(a,ed,2,2)=='衣领';await a.keyboard.press('Control+u');await a.keyboard.press('Escape');await save(a);ss=await until(a,lambda s:picobj(s,page,tid)['text']==full);styled=picobj(ss,page,tid);assert any(m.get('underline') and m['start']==2 and m['end']==4 for m in styled['marks']);ime=await a.evaluate('window.__qaIme');assert {'compositionstart','compositionend'}<=set(x[0] for x in ime);mark(stage,text=full,styledRange=[2,4],marks=styled['marks'],compositionEvents=ime,method='Chromium CDP IME composition; not OS input method',screenshot=await shot(a,'03-canvas-title'))
            # Remaining flow is implemented below; each write goes through a member-facing control.
            stage='bind clothing photo to third shot';await a.locator('#sections button').filter(has_text='镜头清单与分镜').click();old_order=list(ss['order']);third=old_order[2];sixth=old_order[5];third_card=a.locator('[data-shot="'+third+'"]');await third_card.get_by_role('button',name='编辑分镜').click();await btn(a,'关联参考图');await choose_asset(a,collar);await btn(a,'保存分镜');ss=await until(a,lambda s:next(d for d in s['workbench']['shotDetails'] if d['id']==third)['assetId']==collar);mark(stage,shot=third,originalRank=3,asset=collar,screenshot=await shot(a,'04-third-shot'))
            stage='professional selected text comment and prefix reanchor';await a.locator('#people').click();await btn(a,'启用专业版批注');await until(a,lambda s:s['workbench']['tier']=='pro');await a.get_by_label('关闭对话框').click();await jump(a,page);await a.locator('[data-object="'+tid+'"]').dblclick();ed=a.get_by_role('textbox',name='编辑画布标题');assert await select_part(a,ed,2,2)=='衣领';await a.get_by_role('button',name='为选中文字批注').click();text_note='保留衣领二字的指向';
            async def send_comment(p,note):
                await p.get_by_label('批注内容').fill(note);await p.get_by_role('button',name='发布批注',exact=True).click();return await until(p,lambda s:any(t['notes'][0]['text']==note for t in s['threads']))
            def thread(s,note):return next(t for t in s['threads'] if t['notes'][0]['text']==note)
            ss=await send_comment(a,text_note);txt_thread=thread(ss,text_note);assert txt_thread['anchor']['start']==2 and txt_thread['anchor']['quote']=='衣领';await a.locator('#pages [data-page="'+page+'"]').click();await a.locator('[data-object="'+tid+'"]').dblclick();ed=a.get_by_role('textbox',name='编辑画布标题');await ed.focus();await a.keyboard.press('Control+Home');await a.keyboard.insert_text('定稿：');await a.keyboard.press('Escape');await save(a);ss=await until(a,lambda s:thread(s,text_note)['anchor']['start']==5);moved=thread(ss,text_note);assert not moved['anchor']['detached'] and moved['anchor']['quote']=='衣领' and '定稿：'+full==picobj(ss,page,tid)['text'];mark(stage,setup='owner used actual professional-tier control',beforeStart=2,after=anchor_view(moved),screenshot=await shot(a,'05-prefix-retained'))
            stage='B signs in with individual team invitation';await a.locator('#people').click();await btn(a,'升级为团队项目');await a.get_by_label('受邀成员署名').fill('造型师 B');await btn(a,'生成成员邀请');ss=await until(a,lambda s:any(i['label']=='造型师 B' for i in s['invitations']));await a.get_by_label('关闭对话框').click();inv=next(x for x in ss['invitations'] if x['label']=='造型师 B');cb,b=await client('B',args.lan,inv['code']);current_page=b;bs=await state(b);assert bs['me']['name']=='造型师 B' and bs['me']['id']!=record['memberA'] and bs['team'] and bs['workbench']['tier']=='team';record['memberB']=bs['me']['id'];await byname(b,'打开策划').click();await jump(b,page);await expect(b.locator('.we-board .we-img').first).to_be_visible();fresh={}
            for id in chosen:
                fresh[id]=await image_hash(b,id);assert fresh[id]['sha256']==source[id]['hash'].lower()
            mark(stage,setup='owner used actual upgrade and invitation buttons; B used one-time code in link fragment',visiblePhotos=await b.locator('.we-board [data-kind=image] .we-img').count(),verifiedOriginals=fresh)
            stage='B marks collar on canvas, A locates replies resolves';collar_obj=imgs[1]['id'];bc=b.locator('[data-object="'+collar_obj+'"]');await bc.click();await btn(b,'框选图片批注');await region(b,bc.locator('.we-imgclip'),.43,.69,.71,.805);await btn(b,'添加区域批注');note='衣领留住织纹，补一张侧逆光';bs=await send_comment(b,note);t=thread(bs,note);r=t['anchor']['rect'];assert t['anchor']['target']==collar and t['anchor']['objectId']==collar_obj and t['anchor']['pageId']==page and t['anchor']['hash'].lower()==source[collar]['hash'].lower() and max(abs(x-y)for x,y in zip(r,[.43,.69,.28,.115],strict=True))<.04 and not t['anchor']['detached'];current_page=a;await btn(a,'讨论');card=a.locator('.wb-thread').filter(has_text=note);await expect(card).to_be_visible();await card.get_by_role('button',name='定位',exact=True).click();await expect(a.locator('[data-object="'+collar_obj+'"] .we-commentbox')).to_be_visible();primary_overlay=await shot(a,'06-owner-collar-location');await card.get_by_role('button',name='回复',exact=True).click();reply='已补侧逆光，领面保留纹理。';await a.get_by_label('回复内容').fill(reply);await btn(a,'发送回复');await card.get_by_role('button',name='标为已解决',exact=True).click();ss=await until(a,lambda s:thread(s,note)['resolved']);bs=await until(b,lambda s:thread(s,note)['resolved']);assert [n['name'] for n in thread(bs,note)['notes']]==[bs['me']['name'],ss['me']['name']] and thread(bs,note)['notes'][1]['text']==reply;mark(stage,thread=t['id'],anchor=anchor_view(t),authors=[n['name'] for n in thread(bs,note)['notes']],screenshot=primary_overlay)
            stage='B reorders third shot after sixth, keeps reference and shot comment';current_page=b;await b.locator('#sections button').filter(has_text='镜头清单与分镜').click();sc=b.locator('[data-shot="'+third+'"]');await sc.get_by_role('button',name='分镜批注').click();snote='第三镜请从现场参考核对衣领';bs=await send_comment(b,snote);assert thread(bs,snote)['anchor']['target']==third;await sc.drag_to(b.locator('[data-shot="'+sixth+'"]'),source_position={'x':12,'y':15},target_position={'x':20,'y':25});bs=await until(b,lambda s:s['order'].index(third)==5 and s['order'].index(sixth)==4);await expect(sc.locator('.wb-num')).to_have_text('06');await sc.get_by_role('button',name='打开现场参考').click();await expect(b.locator('.wb-review-figure img')).to_be_visible();await expect(b.locator('.wb-review-figure img')).to_have_js_property('naturalWidth',source[collar].get('width') or 960);await b.get_by_label('关闭对话框').click();current_page=a;await btn(a,'讨论');ac=a.locator('.wb-thread').filter(has_text=snote);await ac.get_by_role('button',name='定位',exact=True).click();await expect(a.locator('[data-shot="'+third+'"] .wb-num')).to_have_text('06');mark(stage,before=old_order,after=bs['order'],referenceId=collar,threadId=thread(bs,snote)['id'],screenshot=await shot(a,'07-reordered-sixth'))
            stage='old placement comment detaches after replacement';current_page=b;await jump(b,page);test_obj=imgs[2]['id'];lc=b.locator('[data-object="'+test_obj+'"]');await lc.click();await btn(b,'框选图片批注');await region(b,lc.locator('.we-imgclip'));await btn(b,'添加区域批注');old_note='这条仅属于替换前的图';bs=await send_comment(b,old_note);old_thread=thread(bs,old_note);assert not old_thread['anchor']['detached'];current_page=a;await jump(a,page);await a.locator('[data-object="'+test_obj+'"]').click();await btn(a,'替换图片');await choose_asset(a,chosen[0]);await save(a);ss=await until(a,lambda s:thread(s,old_note)['anchor']['detached']);assert thread(ss,old_note)['anchor']['target']==chosen[2] and not thread(ss,note)['anchor']['detached'];mark(stage,old=anchor_view(thread(ss,old_note)),replacement=picobj(ss,page,test_obj)['assetId'],unaffectedCollar=anchor_view(thread(ss,note)))
            stage='reopen independent profiles';before=await state(a);refs=before['order'];expected_page=pg(before,page)['data'];await ca.close();contexts.remove(ca);await cb.close();contexts.remove(cb);ca,a=await client('A',args.local);cb,b=await client('B',args.lan);current_page=a
            for p,owner_id in ((a,record['memberA']),(b,record['memberB'])):
                await btn(p,'打开策划');await jump(p,page);s=await state(p);assert s['me']['id']==owner_id and pg(s,page)['data']==expected_page and s['order']==refs and thread(s,note)['resolved'] and thread(s,note)['anchor']['rect']==r and thread(s,old_note)['anchor']['detached'] and thread(s,text_note)['anchor']['start']==5;await expect(p.locator('[data-object="'+collar_obj+'"] .we-img')).to_be_visible()
            mark(stage,revision=before['revision'],order=refs,page=page,collar=anchor_view(thread(before,note)),screenshot=await shot(b,'08-member-reopened'))
            stage='clean vs review delivery previews';await a.locator('#deliver').click();await preview_checks(a,out,record,shot,btn,note,byname);mark(stage,**record.pop('previewEvidence'));assert not record['pageErrors'],record['pageErrors'];record['status']='PASS';record['completedUtc']=utc();persist()
        except Exception as exc:
            record.update(status='PARTIAL',failedStep=stage,errorType=type(exc).__name__,reason=safe_error(exc),completedUtc=utc());persist()
            if current_page and not current_page.is_closed():
                # No screenshot in an invitation dialog: codes are private even on a QA instance.
                try:
                    scrub=await current_page.locator('#panelBody').inner_text(timeout=500)
                    if not re.search(r'迁移码|查看邀请码|invite\.|member\.',scrub):record['failureScreenshot']=await shot(current_page,'failure')
                except Exception:pass
            persist();raise
        finally:
            for c in contexts:await c.close()

async def preview_checks(p,out,record,shot,btn,note,byname):
    for css,label,k in [('.wb-preview','干净交付预览','clean'),('.wb-preview','含批注审阅预览','review')]:
        await btn(p,label);surface=p.locator(css);await expect(surface).to_be_visible();content=await surface.inner_text();assert ('秋冬' in content or '衣领' in content) and (note in content)==(k=='review');rec=record.setdefault('previewEvidence',{});rec[k]={'containsThisDiscussion':note in content,'visibleImages':await surface.locator('img').count(),'screenshot':await shot(p,'09-'+k+'-preview')};await p.get_by_label('关闭对话框').click();
        if k=='clean':await p.locator('#deliver').click()

if __name__=='__main__':
    args=argparse.ArgumentParser(description=__doc__)
    for k in ('local','lan','project','chromium','owner','profiles','output'):args.add_argument('--'+k,required=True)
    params=args.parse_args()
    try:asyncio.run(run(params))
    except Exception as err:sys.exit('Acceptance stopped: '+type(err).__name__+': '+safe_error(err))
