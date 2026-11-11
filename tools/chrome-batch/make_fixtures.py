from pathlib import Path
from PIL import Image,ImageDraw
import hashlib,json,sys,shutil
out=Path(sys.argv[1]);out.mkdir(exist_ok=False,parents=True)
# Preserve known single-image fixtures to regress the already accepted right-click entry.
source=Path('E:/Kitao-Deliveries/11.10/Guangjing-ChromeCaptureV1-Diagnostic-win-x64/test-pages')
for p in source.iterdir():
    if p.suffix in ['.jpg','.html']or p.name=='FIXTURE_IDENTITY.json':shutil.copy2(p,out/p.name)
def image(name,size,color,kind='JPEG'):
    im=Image.new('RGB',size,color);draw=ImageDraw.Draw(im)
    for i in range(12):draw.rectangle((i*size[0]//12,20,i*size[0]//12+size[0]//25,size[1]-20),fill=(40+i*12,170-i*5,80+i*9))
    im.save(out/name,kind,**({'quality':92}if kind=='JPEG'else{}))
image('lazy-original-1400.jpg',(1400,900),'#b68465');image('unknown-original-900.jpg',(900,600),'#789a62');image('denied-original-1800.jpg',(1800,1200),'#b75c68');image('slow-original-1000.jpg',(1000,750),'#6479a1');image('mixed-480.png',(480,360),'#758b68','PNG');shutil.copy2(out/'ordinary-640.jpg',out/'ordinary-alias.jpg')
routes=[]
def route(path,file,**kwargs):routes.append(dict(path=path,file=file,**kwargs))
for file in ['lazy-original-1400.jpg','unknown-original-900.jpg','mixed-480.png','ordinary-alias.jpg']:route('/'+file,file)
route('/denied-original-1800.jpg','denied-original-1800.jpg',allowFlag='originalAllowed')
route('/slow-original-1000.jpg','slow-original-1000.jpg',delayMs=2500)
route('/mixed.html','mixed.html');route('/cancel.html','cancel.html')
head='<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>光镜 当前页批量采集验收</title><style>body{font:16px system-ui;background:#f4f3ef;padding:28px;color:#29382f}.grid{display:grid;grid-template-columns:repeat(3,220px);gap:18px}.card{background:white;padding:12px;border-radius:9px}img{max-width:196px;height:130px;object-fit:contain}h1{font-size:26px}p{max-width:840px}</style><body><h1>当前页批量采集合成页</h1><p>工具栏光镜→扫描当前页。扫描不应请求1600原图、data-src原图或故意403原图。所有图片均为授权合成资料。</p><div class="grid">'
items=[('<img id="ordinary" src="/ordinary-640.jpg">','普通JPEG，重复URL×2'),('<img id="srcset" src="/srcset-thumb-320.jpg" srcset="/srcset-thumb-320.jpg 320w, /srcset-original-1600.jpg 1600w" sizes="80px">','srcset：应取1600原图'),('<img id="lazy" src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7" data-src="/lazy-original-1400.jpg" data-width="1400" data-height="900">','懒加载，明确data-src'),('<img id="duplicate" src="/ordinary-640.jpg">','与普通图同URL，应合并'),('<img id="alias" src="/ordinary-alias.jpg">','不同URL相同字节，应已有素材'),('<img id="forbidden" src="/unavailable-thumb.jpg" srcset="/unavailable-thumb.jpg 320w, /denied-original-1800.jpg 1800w" sizes="80px">','最高原图先403，可恢复后重试'),('<img id="unknown" src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7" data-original="/unknown-original-900.jpg">','原图尺寸未知，应明确标记'),('<img id="png" src="/mixed-480.png">','PNG格式筛选'),('<img id="slow" src="/srcset-thumb-320.jpg" data-original="/slow-original-1000.jpg" data-width="1000" data-height="750">','延时原图，供取消/恢复')]
(out/'mixed.html').write_text(head+''.join('<section class="card">'+img+'<p>'+title+'</p></section>'for img,title in items)+'</div></body></html>',encoding='utf-8')
(out/'cancel.html').write_text(head.replace('批量采集合成页','取消与重开恢复')+''.join('<section class="card"><img src="/srcset-thumb-320.jpg" data-original="/slow-original-1000.jpg?item='+str(i)+'" data-width="1000" data-height="750"><p>延时项 '+str(i+1)+'</p></section>'for i in range(6))+'</div></body></html>',encoding='utf-8')
identity={p.name:dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size)for p in out.iterdir()if p.suffix in ['.jpg','.png']}
(out/'FIXTURE_IDENTITY.json').write_text(json.dumps(identity,indent=2)+'\n');(out/'FIXTURE_ROUTES.json').write_text(json.dumps(dict(routes=routes),indent=2)+'\n');(out/'FIXTURE_CONTROL.runtime.json').write_text('{"originalAllowed":false}\n')
print(json.dumps(dict(fixtureRoot=str(out),images=len(identity),routes=len(routes))))
