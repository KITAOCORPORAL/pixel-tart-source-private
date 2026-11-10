from pathlib import Path
from http.server import ThreadingHTTPServer,SimpleHTTPRequestHandler
from PIL import Image,ImageDraw
import datetime,json,sys,hashlib,urllib.parse
root=Path(sys.argv[1]);port=int(sys.argv[2] if len(sys.argv)>2 else 51291);root.mkdir(exist_ok=False,parents=True)
def image(name,size):
    im=Image.new('RGB',size,'#d98c34');draw=ImageDraw.Draw(im)
    for x in range(0,size[0],max(1,size[0]//16)):draw.rectangle((x,0,x+size[0]//32,size[1]),fill=(40+x*170//size[0],120,155))
    im.save(root/name,'JPEG',quality=94)
image('ordinary-640.jpg',(640,480));image('srcset-original-1600.jpg',(1600,1200));image('srcset-thumb-320.jpg',(320,240));image('unavailable-thumb.jpg',(320,240))
for name,title,body in [('ordinary.html','光镜 JPEG 右键采集验收','<img id="test-image" src="/ordinary-640.jpg" width="480">'),('srcset.html','光镜 srcset 原图验收','<img id="test-image" src="/srcset-thumb-320.jpg" srcset="/srcset-thumb-320.jpg 320w, /srcset-original-1600.jpg 1600w" sizes="80px" width="240">'),('unavailable.html','光镜 原图不可取得验收','<img id="test-image" src="/unavailable-thumb.jpg" srcset="/unavailable-thumb.jpg 320w, /denied-original-2400.jpg 2400w" sizes="80px" width="240">')]:
 (root/name).write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>'+title+'</title><body style="font:18px system-ui;padding:40px;background:#eee"><h1>'+title+'</h1><p>仅使用合成 JPEG。在图片上右键，选择“发送到光镜”。</p>'+body+'<p><a href="/ordinary.html">JPEG</a> · <a href="/srcset.html">srcset</a> · <a href="/unavailable.html">原图失败</a></p></body></html>',encoding='utf-8')
(root/'FIXTURE_IDENTITY.json').write_text(json.dumps({p.name:{'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}for p in root.glob('*.jpg')},indent=2)+'\n')
class Handler(SimpleHTTPRequestHandler):
 def __init__(self,*args,**kwargs):super().__init__(*args,directory=str(root),**kwargs)
 def do_GET(self):
  if urllib.parse.urlsplit(self.path).path=='/denied-original-2400.jpg':self.send_error(403,'Original deliberately unavailable');return
  super().do_GET()
 def log_message(self,format,*args):
  with(root/'requests.jsonl').open('a',encoding='utf-8')as stream:stream.write(json.dumps(dict(timeUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),path=self.path,status=str(args[1])if len(args)>1 else''))+'\n')
(root/'READY.json').write_text(json.dumps(dict(port=port,root=str(root),pid=__import__('os').getpid()),indent=2)+'\n');ThreadingHTTPServer(('127.0.0.1',port),Handler).serve_forever()
