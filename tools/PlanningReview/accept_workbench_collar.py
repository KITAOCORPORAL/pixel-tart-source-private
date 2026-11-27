"""After the 13-case test, crop and resize the existing resolved collar placement on QA."""
import argparse
import hashlib
import json
from pathlib import Path
from urllib.parse import urlsplit
from playwright.sync_api import expect, sync_playwright


def run(a):
    old=json.loads(Path(a.main_receipt).read_text('utf-8-sig'));stage=next(x for x in old['actions'] if x['check'].startswith('B marks collar'));base=urlsplit(a.local);assert old['status']=='PASS' and old['project']==a.project and a.project not in ('69fb923b-0863-4f04-9e9c-b670ffd289ab','a276b086-36dd-441e-b730-86ab876ac112');assert base.scheme=='https' and base.hostname=='127.0.0.1' and base.port not in (None,443,45731,45732) and not base.query and not base.fragment
    out=Path(a.output);out.mkdir(parents=True,exist_ok=False);anchor=stage['anchor'];pid=anchor['pageId'];oid=anchor['objectId'];tid=stage['thread'];report={'status':'RUNNING','project':a.project,'method':'Browser interactions on the existing resolved B→A collar thread; predicted CSS pixels use rendered full image bounds times stored original normalized anchor, independently clipped to the visible crop.','thread':tid,'anchor':anchor,'phases':[],'scriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    def store():(out/'BROWSER-COLLAR-GEOMETRY.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    def state():
        s=p.evaluate('''async()=>{const k=JSON.parse(localStorage.getItem('workbench-session'));return await (await fetch('/api/state',{headers:{Authorization:'Bearer '+k.token},cache:'no-store'})).json()}''');assert s['project']==a.project and s['me']['owner'];return s
    def button(n):return p.get_by_role('button',name=n,exact=True)
    def locate():p.locator('[data-thread="'+tid+'"]').get_by_role('button',name='定位').click();expect(obj.locator('.we-commentbox')).to_be_visible()
    def phase(name):
        s=state();t=next(x for x in s['threads'] if x['id']==tid);assert t['resolved'] and all(t['anchor'].get(k)==v for k,v in anchor.items());o=next(x for x in next(g for g in s['workbench']['pages'] if g['id']==pid)['data']['objects'] if x['id']==oid);actual,full,clip=(obj.locator(selector).bounding_box() for selector in ('.we-commentbox','.we-img','.we-imgclip'));rx,ry,rw,rh=anchor['rect'];x=max(full['x']+rx*full['width'],clip['x']);y=max(full['y']+ry*full['height'],clip['y']);right=min(full['x']+(rx+rw)*full['width'],clip['x']+clip['width']);bottom=min(full['y']+(ry+rh)*full['height'],clip['y']+clip['height']);expected=dict(x=x,y=y,width=right-x,height=bottom-y);error=max(abs(expected[k]-actual[k]) for k in expected);f=out/(name+'.png');obj.screenshot(path=f,animations='disabled');rec={'name':name,'placement':{k:o.get(k) for k in ('x','y','w','h','rotate','assetId','crop')},'revision':s['revision'],'storedOriginalHash':t['anchor']['hash'],'storedOriginalRect':t['anchor']['rect'],'fullImageCssPx':full,'visibleImageCssPx':clip,'expectedOverlayCssPx':expected,'actualOverlayCssPx':actual,'maxErrorCssPx':round(error,4),'screenshot':{'file':f.name,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}};report['phases'].append(rec);store();assert error<2.5 and right>x and bottom>y;return rec
    def save():p.locator('#save').click();expect(p.locator('#sync')).to_have_text('已保存')
    def drag(b,x1,y1,x2,y2):p.mouse.move(b['x']+b['width']*x1,b['y']+b['height']*y1);p.mouse.down();p.mouse.move(b['x']+b['width']*x2,b['y']+b['height']*y2,steps=15);p.mouse.up()
    with sync_playwright() as q:
        browser=q.chromium.launch(executable_path=a.chromium,args=['--no-proxy-server']);ctx=browser.new_context(ignore_https_errors=True,viewport={'width':1700,'height':1060});p=ctx.new_page();p.set_default_timeout(8000);obj=p.locator('[data-object="'+oid+'"]');store()
        try:
            p.goto('https://127.0.0.1:'+str(base.port)+'/workbench.html#code='+Path(a.owner).read_text('utf-8-sig').strip());expect(p.locator('#library')).to_be_visible();state();p.locator('.wb-list-row').get_by_role('button',name='打开',exact=True).click();button('讨论').click();locate();before=phase('01-before');assert before['placement']['crop'] in (None,{'x':0,'y':0,'w':1,'h':1});obj.click();button('裁切图片').click();drag(obj.locator('.we-imgclip').bounding_box(),.17,.25,.9,.94);button('应用裁切').click();save();locate();cropped=phase('02-after-crop');c=cropped['placement']['crop'];assert .57<c['h']<.76 and .63<c['w']<.8
            obj.click();handle=obj.get_by_label('拖动调整对象大小');expect(handle).to_be_visible();bb=handle.bounding_box();cx=bb['x']+bb['width']/2;cy=bb['y']+bb['height']/2;p.mouse.move(cx,cy);p.mouse.down();p.mouse.move(cx+65,cy+65,steps=15);p.mouse.up();save();locate();resized=phase('03-after-resize');assert resized['placement']['w']>cropped['placement']['w']+25 and resized['placement']['crop']==c and resized['actualOverlayCssPx']['width']>cropped['actualOverlayCssPx']['width']+4
            p.reload();expect(obj).to_be_visible();button('讨论').click();locate();reopened=phase('04-after-reopen');assert resized['placement']==reopened['placement'];report['status']='PASS'
        except Exception as exc:report['status']='PARTIAL';report['failedOperation']=type(exc).__name__;raise
        finally:store();ctx.close();browser.close()
    print('PASS',[(v['name'],v['maxErrorCssPx']) for v in report['phases']])


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for k in ('main-receipt','local','project','owner','output','chromium'):p.add_argument('--'+k,required=True)
    run(p.parse_args())
