import argparse
import json
import os
import time
import uuid
from pathlib import Path
from playwright.sync_api import sync_playwright,expect


def run(args):
    out=Path(args.output);out.mkdir(parents=True,exist_ok=False);events=[]
    def mark(name,**info):events.append(dict(check=name,**info));(out/'network.json').write_text(json.dumps(events,ensure_ascii=False,indent=2),encoding='utf8')
    with sync_playwright() as runner:
        def open_client(profile,code):
            context=runner.chromium.launch_persistent_context(str(Path(args.profiles)/profile),executable_path=os.environ.get('PLANNING_QA_CHROMIUM'),headless=True,ignore_https_errors=True,viewport={'width':1360,'height':920},args=['--no-proxy-server','--no-first-run']);p=context.pages[0];p.set_default_timeout(13000);p.goto(args.url);p.locator('#code').evaluate('(el,value)=>el.value=value',code);p.locator('#join button').click();expect(p.locator('#title')).to_have_text('秋冬服装型录');return context,p
        def state(p):return p.evaluate("async()=>{const id=JSON.parse(localStorage.getItem('review-session'));return(await fetch('/api/state',{headers:{Authorization:'Bearer '+id.token}})).json()}")
        def op(p,action,**kwargs):return p.evaluate("async op=>{const id=JSON.parse(localStorage.getItem('review-session'));return(await fetch('/api/op',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+id.token},body:JSON.stringify({...op,project:id.project})})).json()}",dict(id=str(uuid.uuid4()),action=action,**kwargs))
        def wait(p,fn):
            until=time.monotonic()+15
            while time.monotonic()<until:
                s=state(p)
                if fn(s):return s
                p.wait_for_timeout(120)
            raise AssertionError('State did not reach expected condition')
        first,a=open_client('owner',Path(args.owner).read_text(encoding='utf-8-sig').strip());s=state(a);d=next(p for p in s['people'] if p['active'] and p['name']=='D 灯光');op(a,'removeMember',target=d['id']);op(a,'invite',text='网络验收 B');s=state(a);second,b=open_client('member',next(inv['code'] for inv in s['invitations'] if inv['label']=='网络验收 B'));actor=state(b)['me']['id'];shot=s['order'][0];shotcard=b.locator(f'[data-block="{shot}"]');shotcard.locator('[data-cmd=annotate-shot]').click();b.locator('#note').fill('断线期间：侧光补衣领纹理');second.set_offline(True);b.locator('#sendNote').click();expect(b.locator('#sync')).to_have_text('离线有改动');b.screenshot(path=str(out/'01-offline.png'));assert not any(t['notes'][0]['text']=='断线期间：侧光补衣领纹理' for t in state(a)['threads']);mark('真实浏览器断网后评论保留在成员持久队列，服务尚未收到')
        fail_once=[True]
        def lose_ack(route):
            data=route.request.post_data_json
            if fail_once[0] and data.get('action')=='comment':
                fail_once[0]=False;res=route.fetch();assert res.ok;route.abort('failed')
            else:route.continue_()
        b.route('**/api/op',lose_ack);second.set_offline(False);expect(b.locator('#sync')).to_have_text('已保存',timeout=18000);s=wait(a,lambda v:any(t['notes'][0]['text']=='断线期间：侧光补衣领纹理' for t in v['threads']));assert not fail_once[0];found=[t for t in s['threads'] if t['notes'][0]['text']=='断线期间：侧光补衣领纹理'];assert len(found)==1;assert b.evaluate("JSON.parse(localStorage.getItem('review:'+JSON.parse(localStorage['review-session']).project+':'+JSON.parse(localStorage['review-session']).id+':queue')).length")==0;mark('重连后丢弃第一次成功回执，客户端重试，同一批注仍只有一条',thread=found[0]['id'])
        target=next(k for k in s['blocks'] if k['kind']=='text' and not k['removed']);node=b.locator(f'[data-block="{target["id"]}"]');node.locator('[data-cmd=edit]').click();node.get_by_label('编辑内容').fill('B 的离线输入：侧逆光保留纹理。');second.set_offline(True);node.locator('[data-cmd=save-edit]').click();expect(b.locator('#sync')).to_have_text('离线有改动');op(a,'text',target=target['id'],base=target['version'],text='A 在线改稿：灰卡测光。');second.set_offline(False);expect(b.locator('#sync')).to_have_text('发生冲突');expect(b.locator('#issues')).to_contain_text('B 的离线输入：侧逆光保留纹理。');expect(b.locator('#issues')).to_contain_text('A 在线改稿：灰卡测光。');b.screenshot(path=str(out/'02-conflict.png'));mark('离线旧版本与在线编辑碰撞，界面显示双方输入；未静默覆盖')
        auth_transfer=b.evaluate("JSON.parse(localStorage['review-session']).token");third,c=open_client('replacement-computer',auth_transfer);assert state(c)['me']['id']==actor and state(a)['used']==3
        target_shot=state(a)['order'][1];op(a,'comment',anchor={'target':target_shot},text='保留被移除分镜的讨论');block=next(bl for bl in state(a)['blocks'] if bl['id']==target_shot);op(a,'delete',target=target_shot,base=block['version']);orphan=b.locator('.thread').filter(has_text='保留被移除分镜的讨论');expect(orphan).to_contain_text('原位置已移除');orphan.locator('[data-cmd=jump]').click();expect(b.locator('#toast')).to_contain_text('原位置已移除');mark('删除内容后仍看到讨论与缺失提示；成员在第三份设备资料中重入，未多占席位',member=actor)
        op(a,'removeMember',target=actor);expect(b.locator('#login')).to_be_visible(timeout=12000);expect(c.locator('#login')).to_be_visible(timeout=12000);assert state(a)['used']==2;mark('移除后两个在线设备均退出项目，回收席位',seatsLeft=1)
        first.close();second.close();third.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for item in ['url','owner','profiles','output']:parser.add_argument('--'+item,required=True)
    run(parser.parse_args())
