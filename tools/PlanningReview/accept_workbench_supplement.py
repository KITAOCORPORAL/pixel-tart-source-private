"""Independent browser supplement on a disposable seeded company QA service only.

This creates one uniquely named document page, never modifies the original 13-case
receipt, and never saves credentials in the evidence. --output must not exist.
"""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlsplit
from uuid import UUID, uuid4
from xml.etree import ElementTree as ET
from zipfile import ZipFile

from playwright.sync_api import expect, sync_playwright


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main(args):
    base = urlsplit(args.local)
    assert base.scheme == 'https' and base.hostname == '127.0.0.1' and base.path in ('', '/')
    assert not base.query and not base.fragment and base.port and base.port not in (443, 45731, 45732)
    assert UUID(args.project) and args.project not in ('a276b086-36dd-441e-b730-86ab876ac112', '69fb923b-0863-4f04-9e9c-b670ffd289ab')
    uri = f'https://127.0.0.1:{base.port}/workbench.html'
    out = Path(args.output); out.mkdir(parents=True, exist_ok=False)
    receipt = {'status':'RUNNING','startedUtc':dt.datetime.now(dt.timezone.utc).isoformat(), 'project':args.project,'physicalMachines':1, 'checks':[], 'scriptSha256':sha(Path(__file__).read_bytes())}
    def persist():
        (out/'BROWSER-SUPPLEMENT.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    def check(name, passed, **data):
        receipt['checks'].append({'name':name,'passed':bool(passed),**data}); persist()
    def state():
        s = page.evaluate('''async()=>{const k=JSON.parse(localStorage.getItem('workbench-session'));return await (await fetch('/api/state',{headers:{Authorization:'Bearer '+k.token},cache:'no-store'})).json()}''')
        assert s['project']==args.project and s['me']['owner']; return s
    def find_page(s):
        return next(k for k in s['workbench']['pages'] if k['id']==pid)
    def find_row(s):return find_page(s)['data']['paragraphs'][0]
    def select_first_two():
        editor.focus();page.keyboard.press('Control+Home');page.keyboard.press('Shift+ArrowRight');page.keyboard.press('Shift+ArrowRight');assert page.evaluate('getSelection().toString()')=='衣领'
    def btn(txt):return page.get_by_role('button',name=txt,exact=True)
    def rich(n):
        return n.evaluate('''n=>{const s=[...n.querySelectorAll('span')].find(x=>x.textContent==='衣领');return {length:n.textContent.length,spanText:s?.textContent||null,font:s?.style.fontFamily||null,spacing:s?getComputedStyle(s).letterSpacing:null,leading:n.style.lineHeight,otherStyled:[...n.querySelectorAll('span')].some(x=>x!==s&&(x.style.letterSpacing||x.style.fontFamily))}}''')
    def rich_ok(v):return v['spanText']=='衣领' and v['spacing']=='3px' and 'mono' in (v['font'] or '').lower() and v['leading']=='1.8' and not v['otherStyled']
    def snap(n,name):
        f=out/(name+'.png');n.screenshot(path=f,animations='disabled');return {'file':f.name,'sha256':sha(f.read_bytes())}

    data='衣领'+('｜棚拍照明和面料纹理；甲乙独立署名，切换页面后保留草稿。'*90)+'｜试验结束。'
    persist()
    with sync_playwright() as pw:
        b=pw.chromium.launch(executable_path=args.chromium,args=['--no-proxy-server','--disable-background-networking']);ctx=b.new_context(ignore_https_errors=True,accept_downloads=True,viewport={'width':1700,'height':1060});page=ctx.new_page();page.set_default_timeout(9000);page.on('dialog',lambda d:d.accept());
        try:
            cred=Path(args.owner).read_text('utf-8-sig').strip();page.goto(uri+'#code='+cred);expect(page.locator('#library')).to_be_visible();s=state();assert len(s['order'])>=6;receipt['browserUA']=page.evaluate('navigator.userAgent');name='草稿排版补验 '+uuid4().hex[:8];page.locator('.wb-list-row').get_by_role('button',name='打开',exact=True).click();page.locator('#pages').get_by_role('button',name=re.compile('新建')).click();page.get_by_label('页面名称').fill(name);page.get_by_label('页面类型').select_option('doc');btn('创建页面').click();editor=page.locator('.we-paratext').first;expect(editor).to_be_visible();s=state();pid=next(k['id'] for k in s['workbench']['pages'] if k['title']==name);receipt['pageId']=pid;before=s['revision'];expected_before=find_row(s)['text']
            editor.focus();page.keyboard.press('Control+a');editor.evaluate('''(e,text)=>{const clip=new DataTransfer;clip.setData('text/plain',text);e.dispatchEvent(new ClipboardEvent('paste',{clipboardData:clip,bubbles:true,cancelable:true}))}''',data);expect(editor).to_have_text(data);observed=state();assert find_row(observed)['text']==expected_before and observed['revision']==before;page.reload();expect(editor).to_be_visible();s=state();rev_post=s['revision'];draft_text=editor.text_content();mode=page.locator('#sync').inner_text();restored=draft_text==data and editor.locator('xpath=ancestor::*[@data-editor-dirty][1]').get_attribute('data-editor-dirty')=='true' and '改动' in mode and before==rev_post and find_row(s)['text']==expected_before
            check('restore unsaved Chinese paste after reload',restored,restoredSha256=sha(draft_text.encode()),editorDirty=editor.locator('xpath=ancestor::*[@data-editor-dirty][1]').get_attribute('data-editor-dirty'),method='Renderer clipboard paste event; no OS clipboard or OS IME',utf16Units=len(data),sha256=sha(data.encode()),revisionBefore=before,revisionAfterReload=rev_post,restoredLength=len(draft_text),syncAfterReload=mode)
            if not restored:
                editor.focus();page.keyboard.press('Control+a');page.keyboard.insert_text(data)
            page.locator('#save').click();expect(page.locator('#sync')).to_have_text('已保存');s=state();check('explicit save commits restored document',restored and find_row(s)['text']==data and s['revision']>before,revisionBefore=before,revisionAfter=s['revision'])
            select_first_two();page.get_by_label('选中文字字体').select_option('mono');select_first_two();page.get_by_label('选中文字字距').select_option('3');editor.focus();page.get_by_label('段落行高').select_option('1.8');page.locator('#save').click();expect(page.locator('#sync')).to_have_text('已保存');page.reload();expect(editor).to_be_visible();r=find_row(state());v=rich(editor);selected=any(m.get('start')==0 and m.get('end')==2 and m.get('font')=='mono' and m.get('spacing')==3 for m in r.get('marks',[]));check('reopen selected font and spacing plus paragraph leading',selected and r.get('lineHeight')==1.8 and rich_ok(v),mark=r.get('marks'),lineHeight=r.get('lineHeight'),rendered=v)
            page.locator('#deliver').click();btn('干净交付预览').click();pane=page.locator('#panel .wb-preview');expect(pane).to_be_visible();v=page.evaluate('''()=>{const d=document.querySelector('#panel'),w=d.querySelector('.wb-table-wrap'),t=w.querySelector('table');return {viewport:innerWidth,dialog:d.offsetWidth,visible:w.clientWidth,table:t.offsetWidth,horizontalScroll:getComputedStyle(w).overflowX}}''');check('wide preview displays initial overview table at 1700px',v['visible']>=v['table']-1,geometry=v,screenshot=snap(page,'wide-preview-first-screen'));line=pane.get_by_text(data,exact=True).locator('xpath=ancestor-or-self::*[self::p or self::h1 or self::h2 or self::h3][1]').first;expect(line).to_have_count(1);v=rich(line);check('clean preview renders typography on exact selected text',rich_ok(v),rendered=v);page.get_by_label('关闭对话框').click();page.locator('#deliver').click()
            with page.expect_download() as info:btn('下载 Word（DOCX）').click()
            file=out/'browser-delivery.docx';download=info.value;download.save_as(file);assert download.failure() is None
            with ZipFile(file) as z:
                xml=ET.fromstring(z.read('word/document.xml'));ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'};paragraphs=xml.findall('.//w:p',ns);matches=[p for i,p in enumerate(paragraphs) if ''.join((n.text or '') for n in p.findall('.//w:t',ns))==data and any(''.join((n.text or '') for n in h.findall('.//w:t',ns))==name for h in paragraphs[max(0,i-2):i])];w='{'+ns['w']+'}';fonts=spacing=leading=None
                if len(matches)==1:
                    p=matches[0];sel=next((r for r in p.findall('w:r',ns) if ''.join((n.text or '') for n in r.findall('.//w:t',ns))=='衣领'),None);fmt=sel.find('w:rPr',ns) if sel is not None else None
                    if fmt is not None:
                        f=fmt.find('w:rFonts',ns);sp=fmt.find('w:spacing',ns);fonts=f.get(w+'ascii') if f is not None else None;spacing=sp.get(w+'val') if sp is not None else None
                    sp=p.find('w:pPr/w:spacing',ns);leading=sp.get(w+'line') if sp is not None else None
            check('owner downloads Word through browser',len(matches)==1 and fonts=='Consolas' and spacing=='45' and leading=='432',bytes=file.stat().st_size,sha256=sha(file.read_bytes()),matchingParagraphs=len(matches),subsetFont=fonts,subsetSpacingTwips=spacing,paragraphLeading=leading)
        except Exception as ex:
            msg=re.sub(r'(?:invite|member)\.[0-9a-f]+\.[0-9a-f]+','[redacted]',str(ex).splitlines()[0] if str(ex) else type(ex).__name__);receipt['error']=msg[:220]
        finally:ctx.close();b.close()
    receipt['status']='PASS' if 'error' not in receipt and len(receipt['checks'])==6 and all(c['passed'] for c in receipt['checks']) else 'PARTIAL';receipt['finishedUtc']=dt.datetime.now(dt.timezone.utc).isoformat();persist();print(receipt['status'],[(v['name'],v['passed']) for v in receipt['checks']],receipt.get('error',''));return 0 if receipt['status']=='PASS' else 1


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for k in ('local','project','owner','chromium','output'):p.add_argument('--'+k,required=True)
    sys.exit(main(p.parse_args()))
