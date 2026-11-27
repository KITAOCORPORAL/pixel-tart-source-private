"""Seed or upgrade an explicitly fresh company demo. No credentials or files are printed."""
import argparse, base64, hashlib, http.client, json, ssl, uuid
from pathlib import Path
from urllib.parse import urlsplit

ALLOW = {
    '01_Fashion_model.jpg': ('白底样张 · 轮廓参考', '1c07f7b45e9e0593a6cdb9f0f95b60f9e2f983262daf7e570b67249205a7cb46'),
    '02_Autumn_fashion.jpg': ('秋冬衣领 · 造型参考', '0884ecc7c547fa1f8c6505fac4dc5ec18ef08e437b224e6937a17d124ebea169'),
    '03_Hair_in_wind.jpg': ('风中发丝 · 姿态参考', '1212a14c1dc4dd873c81fd3fe76b370fac4be95f8a11d84eaec68fc9f5400107'),
}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root',type=Path);parser.add_argument('--photos',type=Path);parser.add_argument('--team',action='store_true');parser.add_argument('--pro',action='store_true');parser.add_argument('--upgrade-existing',action='store_true');parser.add_argument('--baseline-invite-seed',action='store_true')
    args=parser.parse_args();r=args.root.resolve(strict=True)
    assert r.parent.name=='planning-workbench-pilot-20261127' and r.name.startswith('private-'), 'use the isolated workbench delivery'
    entry=json.loads((r/'ENTRY_RECEIPT.json').read_bytes());address=urlsplit(entry['loopback']);port=address.port;expected=entry['certificateSHA256'].upper();project=entry['project'];owner=(r/'owner.local').read_text(encoding='utf-8-sig').strip(); assert address.hostname=='127.0.0.1' and port in (45751,45752,45753)
    ctx=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT);ctx.check_hostname=False;ctx.verify_mode=ssl.CERT_NONE
    def call(path,fields=None):
        with_conn=http.client.HTTPSConnection('127.0.0.1',port,context=ctx,timeout=15)
        try:
            with_conn.connect();assert hashlib.sha256(with_conn.sock.getpeercert(binary_form=True)).hexdigest().upper()==expected
            with_conn.request('POST' if fields is not None else 'GET','/api/'+path,json.dumps(fields).encode() if fields is not None else None,{'Authorization':'Bearer '+owner,'Content-Type':'application/json'});res=with_conn.getresponse();blob=res.read(); val=json.loads(blob)
            if res.status!=200:raise RuntimeError(f'{path}: {res.status} {val.get("error", "")[:170]}')
            return val
        finally:with_conn.close()
    snap=call('state');assert snap['project']==project and snap['me']['owner'] and 'workbench' in snap
    def id():return str(uuid.uuid4())
    def op(action,**fields):
        q=call('op',dict(id=id(),project=project,action=action)|fields);assert q['outcome']=='saved',(action,q.get('outcome'));return q
    if args.upgrade_existing:
        assert len(snap['workbench']['pages'])>=15
        if snap['workbench']['tier']=='basic' and (args.pro or args.team): op('wbTier',text='pro')
        if not snap['team'] and args.team:op('upgrade')
        return
    assert snap['revision']==0 and [p['section'] for p in snap['workbench']['pages']]==['brief','reference','shots'] and not snap['team'], 'no preexisting project writes'
    assert args.photos
    raw=[]
    for name,(title,digest) in ALLOW.items():
        source=(args.photos/name).read_bytes();assert hashlib.sha256(source).hexdigest()==digest;raw.append((name,title,source))
    asset_ids=[]
    for _,title,source in raw:
        i=id();op('image',id=i,text=title,image=base64.b64encode(source).decode('ascii'));asset_ids.append(i)
    portrait,collar,wind=asset_ids
    def para(kind,text,marks=None,align=None):return dict(id=id(),type=kind,text=text,marks=marks or [])|({'align':align} if align else {})
    def obj(kind,x,y,w,h,**fields):return dict(id=id(),kind=kind,x=x,y=y,w=w,h=h,rotate=0)|fields
    def note(text,size=28,bold=False,**fields):return {'text':text,'marks':[{'start':0,'end':len(text),'size':size,'bold':bold}|fields]}
    def shot(x,y,w,h,asset):return obj('image',x,y,w,h,assetId=asset,crop={'x':0,'y':0,'w':1,'h':1})
    def canvas(*items):return {'width':1000,'height':670,'objects':list(items)}
    def table(columns,rows):return {'columns':[dict(key=k,label=v) for k,v in columns],'rows':[dict(id=id(),cells=dict(zip((k for k,_ in columns),fields,strict=True))) for fields in rows]}
    def add(section,title,kind,data):op('wbPageAdd',section=section,text=title,kind=kind,data=data)
    brief=snap['workbench']['pages'][0];reference=snap['workbench']['pages'][1]; shots=snap['order'];assert len(shots)==6
    op('wbPageSet',target=brief['id'],base=brief['version'],text='项目简报',data={'paragraphs':[
        para('h1','秋冬服装型录'),para('p','LOOKBOOK  /  品牌团队拍摄策划 · 2026.12.02'),
        para('h2','客户需求'),para('p','拍摄 6 套秋冬外套。保留深色面料的厚度、翻领衣领的结构与边缘层次；人物动作自然，突出剪裁而非表情。'),
        para('h2','创意方向'),para('p','松针绿、深棕与暖灰构成基础色组。窗边侧逆光保留织纹，正面辅光只抬暗面，不填平领口。'),
        para('quote','衣领边缘须和下颌分开，现场检查面料高光不过曝。'),
        para('h2','交付规格'),para('ul','6 套 × 3 张主图；补 6 张衣领和纹理近景。'),para('ul','交付 sRGB JPG：4:5 竖版主图、3:2 横版备选以及衣领细节裁片。'),para('p','首轮选片于拍摄次日 18:00 前提交，统一命名 LookXX_镜号。')], 'assetIds':[collar]})
    op('wbPageSet',target=reference['id'],base=reference['version'],text='参考图',data=canvas(obj('shape',32,32,936,606,shape='rect',fill='#EFF0E6'),obj('text',67,55,610,59,**note('参考图与素材来源',44,True)),shot(72,156,210,315,portrait),shot(337,155,210,315,collar),shot(608,155,290,315,wind),obj('text',74,494,850,60,**note('同一项目内共享 ｜ CC0 样片 ｜ 均经策划项目副本存储',22))))
    add('overview','项目概览','table',table([('aim','拍摄目标'),('keywords','视觉关键词'),('requirements','客户要求'),('must','必拍内容'),('care','注意事项'),('delivery','交付用途'),('date','拍摄日期'),('location','拍摄地点'),('people','参与人员'),('status','项目状态')],[('为秋冬外套制作有触感、可用于电商的图像','侧逆光、衣领织纹、松针绿、留白','正面服装结构要完整；深色织纹可辨','全身、侧身、走动、领口近景','蒸汽熨平，衣领与下颌分离；备同款 L 码','品牌商详、型录、社媒','2026-12-02','北窗摄影棚','林澄 · 周宁 · 陈谙','进行中')]))
    add('call','拍摄通告','table',table([('time','时间'),('where','地点'),('team','人员'),('wardrobe','服装'),('scene','场景安排')],[('07:45','北窗摄影棚 · 化妆区','周宁、造型助理','Look 01–03','通告签到，蒸汽熨烫，检查扣子'),('08:30','A 棚 · 暖灰无缝背景','摄影、模特、造型','Look 01：高领毛呢','全身到半身，定主光'),('10:00','A 棚 · 北窗左侧','摄影、灯光、模特','Look 03：棕褐翻领','衣领纹理与侧逆光'),('13:30','B 棚 · 浅灰墙','摄影、造型、数字助理','Look 04–06','动态、侧身与收尾')]))
    add('mood','情绪板','canvas',canvas(obj('shape',30,26,938,612,shape='rect',fill='#EFEBDC'),obj('text',69,48,840,85,**note('领口、织纹与冬日侧光',55,True,color='#2D473B')),obj('text',70,137,838,57,**note('A/W LOOKBOOK / 北窗光线；收敛色彩，保留衣服的体积',19)),shot(69,221,187,282,portrait),shot(313,191,200,300,collar),shot(581,214,301,274,wind),obj('text',71,516,844,41,**note('白底轮廓  ／  秋冬翻领细节  ／  动态发丝（Petr Kratochvil · CC0）',19)),*(obj('shape',71+i*73,572,61,38,fill=fill,shape='rect') for i,fill in enumerate(['#34493F','#926F55','#A4B0A3','#D6CCB5']))))
    add('lighting','灯位图','canvas',canvas(obj('shape',48,58,885,570,shape='rect',fill='#E4EBDD'),obj('text',76,78,520,59,**note('A 棚 · 衣领侧逆光 / 俯视',37,True)),obj('shape',451,270,102,102,shape='ellipse',fill='#A9917B'),obj('text',400,389,220,49,**note('人物朝北窗',23,True)),obj('arrow',204,220,175,33,rotate=24,fill='#37604E'),obj('text',153,171,275,59,**note('主光 · 北窗加柔光布',26)),obj('arrow',607,266,156,31,rotate=169,fill='#936B50'),obj('text',644,209,288,59,**note('辅光 · 白反光板',23)),obj('line',462,445,70,32,rotate=90,fill='#3D634F'),obj('text',443,503,232,49,**note('相机 · 85 mm',24)),shot(733,397,132,197,collar)))
    add('props','服化道与道具 · 造型清单','table',table([('thumb','缩略图'),('look','Look 编号'),('name','名称'),('type','分类'),('quantity','数量'),('owner','负责人'),('status','准备状态'),('source','来源'),('archive','归档')],[(portrait,'01','炭灰高领毛呢','服装','2','周宁','已备齐','摄影棚样衣','活动'),(collar,'03','棕褐翻领外套','服装','2','周宁','待熨烫','品牌样衣','活动'),(wind,'05','无金属发夹','道具','4','吴晴','已备齐','造型工具包','活动')]))
    add('gear','设备','table',table([('name','名称'),('model','型号'),('qty','数量'),('source','来源目录'),('archive','归档')],[('主机','全画幅 A','1','摄影工作室设备包','活动'),('便携闪光','600 Ws','2','A 棚出租登记','活动'),('稳定器','可调三脚架','1','摄影包','活动')]))
    add('crew','人员','table',table([('name','名称'),('role','角色'),('contact','联系方式'),('arrive','到场时间'),('archive','归档')],[('林澄','摄影','由制作人联络','07:45','活动'),('周宁','造型','项目群联络','07:45','活动'),('陈谙','灯光','项目群联络','08:00','活动'),('吴晴','数字助理','项目群联络','08:00','活动')]))
    add('venue','场地','table',table([('name','名称'),('address','地址'),('contact','联系方式'),('availability','可用时段'),('permit','报备许可'),('archive','归档'),('zone','时区'),('sunset','手录日落'),('remarks','备注')],[('北窗摄影棚','示例：工作室 A / B 棚','工作室接待','07:30–18:30','拍摄当日访客备案','活动','Asia/Shanghai','手记 15:16','换景保持背景干净')]))
    add('schedule','档期','table',table([('name','档期标题'),('date','日期'),('where','地点'),('status','状态'),('source','资料来源')],[('服装型录主拍','2026-12-02','北窗摄影棚','已排期','项目手录 · 没有连接工作日历'),('备选下雨场地','2026-12-03','B 棚','候选','制作人手录')]))
    add('timeline','当天时间表','table',table([('name','名称'),('date','日期'),('start','开始时间'),('end','结束时间'),('zone','时区 ID'),('place','地点'),('lead','负责人'),('archive','归档')],[('对色与走位','2026-12-02','08:30','09:00','Asia/Shanghai','A 棚','林澄','活动'),('衣领近景','2026-12-02','10:00','10:45','Asia/Shanghai','北窗','陈谙','活动'),('场景 B 落版','2026-12-02','13:30','16:00','Asia/Shanghai','B 棚','林澄','活动')]))
    add('budget','预算','table',table([('name','名称'),('amount','预算金额'),('currency','币种'),('archive','归档')],[('摄影棚','450','EUR','活动'),('灯光器材','180','EUR','活动'),('服装蒸烫与耗材','65','EUR','活动')]))
    add('files','文件','table',table([('name','名称'),('status','状态'),('type','来源类型'),('details','文件详情')],[('品牌型录交付说明','待确认','资料清单','交付规格正文见项目简报'),('拍摄通告 v1','项目内编辑','工作台表格','打开左栏拍摄通告')]))
    add('document','文档内容','doc',{'paragraphs':[para('h1','现场备注 / 交付留档'),para('h2','到场检查'),para('check','拍摄电池已充电、原始文件双盘备份。'),para('check','对比样衣扣子与领面方向，确认相机色卡。'),para('h2','客户备注'),para('p','衣领近景优先，黑衣不得吞没织纹。静物照片保留台面留白。')]})
    add('lights','灯具清单','table',table([('name','名称'),('x','横向位置 0–1'),('y','纵向位置 0–1'),('angle','方向角度'),('power','功率百分比'),('modifier','修饰器'),('archive','归档')],[('北窗主光','0.2','0.2','25','65','一层柔光布','活动'),('白板辅光','0.75','0.36','170','15','白色反光板','活动')]))
    shot_values=[('建立全身轮廓','A 棚暖灰背景','全身','50 mm · 平视','高领毛呢','北窗柔光','12'),('直走一来一回，捕捉衣摆','A 棚中轴','全身','50 mm · 低位','炭灰外套','侧光','12'),('近拍衣领翻折和双层织纹：稳定肩位，留住暗部纹理。','A 棚北窗','特写','85 mm · 肩高','棕褐翻领','侧逆光＋白板','18'),('转侧身展示剪裁线','A 棚暖灰背景','半身','85 mm · 平视','翻领外套','柔光','12'),('袖口、内搭与主面料同框','B 棚浅灰墙','中近景','85 mm · 平视','围巾','主光低比','15'),('模特回身，背面轮廓入画','B 棚浅灰墙','全身','50 mm · 侧后','长款外套','侧逆光','14')]
    for i,(shot_id,vals) in enumerate(zip(shots,shot_values,strict=True)):
        req,scene,sz,camera,props,light,mins=vals
        op('wbShotSet',target=shot_id,base=1,data={'requirements':req,'fields':{'status':'待拍','scene':scene,'shotSize':sz,'cameraAndLens':camera,'props':props,'lighting':light,'estimatedMinutes':mins,'remarks':'检查衣领边缘' if i==2 else ''},'assetId':None if i==2 else (portrait if i<2 else wind)})
    after=call('state');overview=next(p['id'] for p in after['workbench']['pages'] if p['section']=='overview');op('wbPageMove',target=overview,after='00000000-0000-0000-0000-000000000000',base=after['workbench']['pageOrderVersion'])
    if args.pro or args.team:op('wbTier',text='pro')
    if args.team:op('upgrade')
    if args.baseline_invite_seed:
        assert args.team
        after=call('state');p=next(p for p in after['workbench']['pages'] if p['section']=='shots');b=after['order'][2]
        op('wbShotSet',target=b,base=next(d['version'] for d in after['workbench']['shotDetails'] if d['id']==b),data={'assetId':collar})
        op('comment',base=p['version'],text='第 3 镜请检查衣领的侧逆光和深色织纹。可从「打开现场参考」查看原图。',anchor={'pageId':p['id'],'target':b})
        op('comment',base=p['version'],text='衣领这里保留翻折的暗面和织纹；点「定位」可打开带框选的原图。',anchor={'pageId':p['id'],'objectId':b,'target':collar,'hash':hashlib.sha256(raw[1][2]).hexdigest().upper(),'rect':[0.43,0.69,0.28,0.115]})
    result=call('state');print(json.dumps({'project':project,'revision':result['revision'],'pages':len(result['workbench']['pages']),'assets':len(asset_ids),'shots':len(shots),'tier':result['workbench']['tier']},ensure_ascii=True))
if __name__=='__main__':main()
