"""Run against one newly minted, empty company QA project; reviewer project is prohibited."""
import asyncio, json, hashlib, datetime, sys, time, os
from pathlib import Path
from playwright.async_api import async_playwright, expect
E=Path(__file__).resolve().parent
I=json.loads((E/'qa-instance.json').read_text('utf-8-sig')); D=Path(I['delivery']); PRIVATE=D/('private-'+I['instance']); PIC=Path(sys.argv[1]).resolve()
assert I['instance'].startswith('gap-') and I['port']!=45731 and I['project']!='a276b086-36dd-441e-b730-86ab876ac112'
assert hashlib.sha256(PIC.read_bytes()).hexdigest()=='0884ecc7c547fa1f8c6505fac4dc5ec18ef08e437b224e6937a17d124ebea169'
checked={}; record={'project':I['project'],'lan':I['lan'],'qaInstance':I['instance'],'kind':'Two independently launched persistent Chromium clients, one physical Windows host. Only UI interactions perform writes.','crossPhysical':'NOT_RUN','checks':checked}
def save(): (E/'acceptance.json').write_text(json.dumps(record,indent=2,ensure_ascii=False),encoding='utf8')
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
async def state(p):return await p.evaluate("async()=>{const s=JSON.parse(localStorage.getItem('review-session'));const r=await fetch('/api/state',{headers:{Authorization:'Bearer '+s.token}});if(!r.ok)throw Error(r.status);return await r.json()}")
async def find(p,pred):
 for _ in range(125):
  s=await state(p)
  if pred(s):return s
  await asyncio.sleep(.12)
 raise AssertionError('expected server state not observed')
async def main():
 exe=next((D/'qa-browser').rglob('chrome-headless-shell.exe'));contexts=[];record['startedUtc']=utc();save()
 async with async_playwright() as pw:
  async def client(label,url,secret=None):
   ctx=await pw.chromium.launch_persistent_context(str(PRIVATE/'gap-client-profiles'/label),executable_path=str(exe),headless=True,ignore_https_errors=True,args=['--no-proxy-server','--no-first-run','--disable-background-networking'],viewport={'width':1370,'height':900});contexts.append(ctx);p=ctx.pages[0];p.set_default_timeout(18000);await p.goto(url)
   if secret:
    await p.locator('#code').fill(secret);await p.locator('#join button').click()
   await expect(p.locator('#title')).to_have_text('秋冬服装型录');return ctx,p
  try:
   ca,a=await client('A',I['local'],(PRIVATE/'owner.local').read_text('utf-8-sig').strip());one=await state(a);assert one['project']==I['project'] and not one['team'] and len(one['threads'])==0 and one['me']['owner'];record['browser']=await a.evaluate('navigator.userAgent');record['ownerId']=one['me']['id'];ids=one['order'][:2];assert len(set(ids))==2;shot_note='专业版：检查第一镜衣领与下颌的间距';image_note='专业版：保留参考图衣领纹理'
   async def nav(p,kind):await p.locator('[data-page='+kind+']').click()
   async def note(p,word):await p.locator('#note').fill(word);await p.locator('#sendNote').click();await expect(p.locator('.thread').filter(has_text=word)).to_be_visible()
   await nav(a,'shot');await a.locator(f'.shot[data-block="{ids[0]}"] [data-cmd=annotate-shot]').click();await note(a,shot_note);await nav(a,'image');await a.locator('#upload').set_input_files(str(PIC));await expect(a.locator('.frame img')).to_have_js_property('naturalWidth',960);frame=a.locator('.frame');await frame.locator('img').scroll_into_view_if_needed();coords=await frame.evaluate('''el=>{const s=el.closest('.photoScroll');s.scrollTop=s.scrollHeight;const r=el.getBoundingClientRect();return [r.x+r.width*.43,r.y+r.height*.69,r.x+r.width*.71,r.y+r.height*.805]}''');await a.mouse.move(*coords[:2]);await a.mouse.down();await a.mouse.move(*coords[2:],steps=14);await a.mouse.up();await note(a,image_note);now=await find(a,lambda s:len(s['threads'])==2);assert not now['team'];image=next(b for b in now['blocks'] if b['kind']=='image');st=next(t for t in now['threads'] if t['notes'][0]['text']==shot_note);it=next(t for t in now['threads'] if t['notes'][0]['text']==image_note);assert st['anchor']['target']==ids[0] and it['anchor']['target']==image['id'] and it['anchor']['hash']==image['hash'] and max(abs(i-j) for i,j in zip(it['anchor']['rect'],[.43,.69,.28,.115],strict=True))<.008;assert st['notes'][0]['name']==it['notes'][0]['name']==one['me']['name'];await a.locator(f'.thread[data-tid="{it["id"]}"] [data-cmd=jump]').click();await expect(a.locator('.selectedRect').first).to_be_visible();await a.screenshot(path=E/'01-professional-collar.png');checked['professionalShotAndRegionBeforeUpgrade']={'team':False,'revision':now['revision'],'shotThread':st['id'],'shotId':ids[0],'imageThread':it['id'],'imageId':image['id'],'imageSha256':image['hash'],'rectangle':it['anchor']['rect'],'author':one['me']['name']};save()
   await a.locator('#team').click();await a.get_by_role('button',name='升级为团队项目').click();await find(a,lambda s:s['team']);await a.locator('#team').click();await a.get_by_label('受邀成员署名').fill('造型师 B');await a.get_by_role('button',name='生成成员邀请').click();up=await find(a,lambda s:len(s['invitations'])==1);cb,b=await client('B',I['lan'],up['invitations'][0]['code']);bs=await state(b);assert bs['project']==I['project'] and set(t['id'] for t in bs['threads'])=={st['id'],it['id']} and bs['me']['id']!=one['me']['id'] and bs['me']['name']=='造型师 B';record['memberId']=bs['me']['id'];record['seats']={'owner':1,'availableMemberSeats':bs['memberSeats']};assert bs['memberSeats']==3;save()
   async def edit(p,id,value):
    await nav(p,'shot');card=p.locator(f'.shot[data-block="{id}"]');await card.locator('[data-cmd=edit]').click();await card.get_by_label('编辑内容').fill(value);return card.locator('[data-cmd=save-edit]')
   values=['负责人 A：第一镜正面拍摄，检查衣领间距','造型师 B：第二镜采用侧逆光，保留衣领织纹'];buttons=await asyncio.gather(edit(a,ids[0],values[0]),edit(b,ids[1],values[1]));before=await state(a);versions={x['id']:x['version']for x in before['blocks'] if x['id'] in ids};arrived={};responses={};gate=asyncio.Event()
   def intercept(label):
    async def handle(route):
     op=route.request.post_data_json
     if op['action']=='text':
      arrived[label]={'target':op['target'],'base':op['base'],'operationId':op['id'],'queuedUtc':utc()};assert arrived[label]['base']==versions[op['target']]
      if len(arrived)==2:gate.set()
      await asyncio.wait_for(gate.wait(),15);arrived[label]['forwardedUtc']=utc()
     await route.continue_()
    return handle
   async def capture(p,label):
    async with p.expect_response(lambda r:r.request.method=='POST' and r.url.endswith('/api/op')) as evt:
     await buttons[0 if label=='A' else 1].click()
    r=await evt.value;v=await r.json();assert r.status==200 and v['outcome']=='saved' and not v.get('replay');responses[label]={'status':r.status,'outcome':v['outcome'],'revision':v['revision']}
   await asyncio.gather(a.route('**/api/op',intercept('A')),b.route('**/api/op',intercept('B')));await asyncio.gather(capture(a,'A'),capture(b,'B'));assert [arrived[label]['target'] for label in ('A','B')]==ids;assert len(set(q['revision'] for q in responses.values()))==2
   def valid(s):return not s['conflicts'] and all(any(x['id']==id and x['text']==word and x['version']==versions[id]+1 for x in s['blocks'])for id,word in zip(ids,values))
   await asyncio.gather(find(a,valid),find(b,valid));await ca.close();contexts.remove(ca);await cb.close();contexts.remove(cb);ca,a=await client('A',I['local']);cb,b=await client('B',I['lan']);states=await asyncio.gather(find(a,valid),find(b,valid));assert states[0]['me']['id']==record['ownerId'] and states[1]['me']['id']==record['memberId'];assert states[0]['order']==states[1]['order']==before['order'];assert all({t['id'] for t in s['threads']}=={st['id'],it['id']} for s in states)
   for p in (a,b):
    await nav(p,'shot');await expect(p.locator(f'.shot[data-block="{ids[0]}"] .shotname')).to_have_text(values[0]);await expect(p.locator(f'.shot[data-block="{ids[1]}"] .shotname')).to_have_text(values[1]);await p.screenshot(path=E/('02-owner-reopened.png' if p==a else '03-member-reopened.png'))
   checked['concurrentDifferentShotCards']={'baselineGlobalRevision':before['revision'],'overlap':'both real UI-generated save POSTs intercepted pending; neither forwarded until both arrived; released together to real pilot','requests':arrived,'responses':responses,'reopened': [{'actor':s['me']['name'],'actorId':s['me']['id'],'globalRevision':s['revision'],'conflicts':len(s['conflicts']),'shots':[{'id':x['id'],'text':x['text'],'version':x['version']} for x in s['blocks'] if x['id'] in ids]} for s in states]};record['status']='PASS';record['finishedUtc']=utc();save();print('PASS professional shot and region; two different shot cards saved, both clients reopened.')
  finally:
   for c in contexts:await c.close()
try:asyncio.run(main())
except Exception as e:
 record['status']='FAILED'; record['failureType']=type(e).__name__; record['failureSummary']=str(e).splitlines()[0][:250];save();raise