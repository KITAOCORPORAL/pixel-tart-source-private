import argparse
import base64
import concurrent.futures
import hashlib
import json
import ssl
import threading
import urllib.error
import urllib.request
import uuid
from pathlib import Path


def run(args):
    results=[]; output=Path(args.output); output.mkdir(parents=True,exist_ok=False)
    tls=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT);tls.check_hostname=False;tls.verify_mode=ssl.CERT_NONE
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPSHandler(context=tls))
    def call(path,token=None,payload=None,status=200,binary=False):
        headers={'Content-Type':'application/json'}
        if token:headers['Authorization']='Bearer '+token
        req=urllib.request.Request(args.url.rstrip('/')+'/api/'+path,data=None if payload is None else json.dumps(payload).encode(),headers=headers)
        try:
            with opener.open(req,timeout=12) as resp:code=resp.status;raw=resp.read()
        except urllib.error.HTTPError as err:code=err.code;raw=err.read()
        if code!=status:raise AssertionError(f'{path.split("/")[0]}: expected {status}, got {code}')
        return raw if binary else json.loads(raw)
    def mark(name,**info):results.append(dict(check=name,**info));(output/'protocol.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
    def uid():return str(uuid.uuid4())
    owner=Path(args.owner).read_text(encoding='utf-8-sig').strip(); initial=call('state',owner);project=initial['project'];assert not initial['team']
    def op(who,action,status=200,**data):return call('op',who,dict(project=project,id=uid(),action=action,**data),status)
    def get(who):return call('state',who)
    def invite(name):
        op(owner,'invite',text=name);inv=next(i for i in get(owner)['invitations'] if i['label']==name);return inv['code']
    def join(code):return call('join',payload={'code':code})
    call('state',status=401);call('state','invalid',status=401);op(owner,'invite',422,text='专业版不可邀请');op(owner,'upgrade');code_b=invite('B 造型');member=join(code_b);bt=member['token'];call('join',payload={'code':code_b},status=403)
    member_c=join(invite('C 摄影助理'));code_d=invite('D 灯光');d=join(code_d);op(owner,'invite',409,text='满席新成员');op(bt,'removeMember',403,target=d['id']);op(bt,'invite',403,text='越权邀请');op(bt,'upgrade',403)
    moved=join(bt);assert moved['id']==member['id'] and get(bt)['used']==3 and get(bt)['invitations'] is None
    mark('满额、专业版限制、拒绝成员越权、一次性邀请、同一成员换端不新增席位',total=4,memberSeats=3,sameId=moved['id'])
    first=initial['blocks'][0];shot=next(b for b in initial['blocks'] if b['kind']=='shot');barrier=threading.Barrier(2)
    def edit_at_same_time(who,block,text):barrier.wait();return op(who,'text',target=block['id'],base=block['version'],text=text)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        a=pool.submit(edit_at_same_time,owner,first,'A：侧光拍出衣领的结构。');b=pool.submit(edit_at_same_time,bt,shot,'B：侧面剪裁');assert a.result()['outcome']==b.result()['outcome']=='saved'
    same=op(bt,'text',target=first['id'],base=first['version'],text='B：改用侧逆光、保留纹理。');assert same['outcome']=='conflict';snap=get(owner);clash=snap['conflicts'][-1];assert clash['current']=='A：侧光拍出衣领的结构。' and clash['submitted']=='B：改用侧逆光、保留纹理。'
    current=next(b for b in snap['blocks'] if b['id']==first['id']);op(owner,'ack',target=clash['id'],base=current['version'],flag=True,text='合并：侧逆光表现衣领结构与纹理。');mark('异块并发均保存；旧版本同块输入完整保留，可合并',conflict=clash['id'],current=clash['current'],submitted=clash['submitted'])
    original=get(owner)['blocks'][1];idx=original['text'].index('织纹'); comment=dict(project=project,id=uid(),action='comment',base=original['version'],text='复核织纹',anchor={'target':original['id'],'start':idx,'quote':'织纹'});r1=call('op',bt,comment);r2=call('op',bt,comment);assert r1['revision']==r2['revision'] and r2['replay'];call('op',bt,dict(comment,text='另一输入'),409)
    op(owner,'text',target=original['id'],base=original['version'],text='今天：'+original['text']);snap=get(owner);t=next(t for t in snap['threads'] if t['id']==comment['id']);assert t['anchor']['start']==idx+3 and not t['anchor']['detached'];op(owner,'text',target=original['id'],base=original['version']+1,text='替换这段简报。');assert next(t for t in get(owner)['threads'] if t['id']==comment['id'])['anchor']['detached'];op(owner,'delete',target=original['id'],base=original['version']+2);op(owner,'reply',target=comment['id'],text='旧位置已移除，讨论保留');snap=get(owner);t=next(t for t in snap['threads'] if t['id']==comment['id']);assert len(t['notes'])==2 and sum(i['id']==t['id'] for i in snap['threads'])==1
    mark('提交重试去重、同编号异内容拒绝；前插文字精确平移，删选区不误绑，删除后仍可回复',thread=t['id'],retainedNotes=len(t['notes']))
    img=Path(args.photo).read_bytes();img_id=uid();call('op',owner,dict(id=img_id,project=project,action='image',text='衣领参考',image=base64.b64encode(img).decode()));assert call('image/'+img_id,bt,binary=True)==img
    op(owner,'removeMember',target=member['id']);call('state',bt,status=403);call('op',bt,comment,403);call('image/'+img_id,bt,status=403,binary=True);call('presence',bt,{'device':uid()},403);call('join',payload={'code':bt},status=403);arch=get(owner);assert arch['used']==2 and any(n['name']=='B 造型' for t in arch['threads'] for n in t['notes']);replacement=join(invite('E 场务'));assert replacement['id']!=member['id'] and get(owner)['used']==3
    mark('移除保留署名、回收席位；旧设备和迁移码的读、写、重复请求、原图、在线心跳全部撤权',denied=5,newMember=replacement['id'])
    receipt=call('export',owner,{});native=Path(args.owner).parent/'exports'/receipt['folder'];plan=json.loads(next((native/'ProjectPlanning').glob('*.planning.json')).read_bytes());shots=json.loads(next((native/'ProjectShots').glob('*.shots.json')).read_bytes());assert plan['ProjectId']==project and plan['Document']['Package']['DocumentId']==initial['document'];assert '侧逆光' in plan['Document']['Body'];assert next(s for s in shots['Shots'] if s['ShotId']==shot['id'])['Name']=='B：侧面剪裁';copy=plan['Document']['References'][0]['ProjectCopy'];assert hashlib.sha256((native/'ProjectPlanning'/copy['RelativePath']).read_bytes()).digest()==hashlib.sha256(img).digest();assert plan['Document']['PixelTartReviewV1']['threads'][0]['id']==comment['id'];mark('导出独立原生项目并用 PlanningProjectStore 打开、照片重新核验，批注扩展保留',project=project,nativeRevision=plan['Revision'],export=receipt['folder'])

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for item in ['url','owner','photo','output']:parser.add_argument('--'+item,required=True)
    run(parser.parse_args())
