"""Loopback-only, non-mutating phone/browser timezone observation for real-device QA.
The published calendar files and booking database are never changed by this sidecar.
Route the private HTTPS /qa prefix here, and / to the frozen calendar service.
Config and observed metadata remain in the explicitly owned runtime, outside the package.
"""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse
import argparse,datetime,json,secrets,re,threading
parser=argparse.ArgumentParser();parser.add_argument('--config',required=True);parser.add_argument('--port',type=int,default=17663)
args=parser.parse_args();config=json.loads(Path(args.config).read_text(encoding='utf-8-sig'))
nonce=config['nonce'];assert re.fullmatch(r'[a-zA-Z0-9_-]{24,80}',nonce)
public=config['publicBaseUrl'].rstrip('/');origin=urlparse(public);assert origin.scheme=='https' or(origin.scheme=='http'and origin.hostname in['127.0.0.1','localhost'])
output=Path(config['outputFile']).resolve();output.parent.mkdir(parents=True,exist_ok=True);gate=threading.Lock()
class Handler(BaseHTTPRequestHandler):
 def log_message(self,*values):pass
 def normalized(self):
  value=urlparse(self.path).path
  return value[3:] if value.startswith('/qa/') else value
 def reply(self,status,body,kind='application/json',csp=None):
  payload=body if isinstance(body,bytes)else json.dumps(body,ensure_ascii=False).encode('utf-8')
  self.send_response(status);self.send_header('Content-Type',kind);self.send_header('Content-Length',str(len(payload)));self.send_header('Cache-Control','no-store');self.send_header('Referrer-Policy','no-referrer');self.send_header('X-Content-Type-Options','nosniff')
  if csp:self.send_header('Content-Security-Policy',csp)
  self.end_headers();self.wfile.write(payload)
 def do_GET(self):
  path=self.normalized()
  if path=='/health/ready':return self.reply(200,{'ready':True,'mode':'BrowserTimeZoneObserverOnly','roundId':config['roundId'],'publicBaseUrl':public,'bookingWrites':False})
  if path!='/start/'+nonce:return self.reply(404,{'code':'NotFound'})
  script_nonce=secrets.token_urlsafe(18)
  template="""<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>光镜 · 真机验收连接</title><body><h1>连接公司合成档期</h1><p id="state">正在记录浏览器时区，随后打开预约。</p><script nonce="SCRIPT_NONCE">
(async()=>{const note=document.getElementById('state');try{const token=new URLSearchParams(location.hash.slice(1)).get('token');if(!token)throw new Error('请使用电脑签发的完整授权链接。');history.replaceState(null,'',location.pathname);const details={roundId:ROUND_ID,deviceId:DEVICE_ID,browserTimeZone:Intl.DateTimeFormat().resolvedOptions().timeZone,browserUtcOffsetMinutes:-new Date().getTimezoneOffset(),browserNowUtc:new Date().toISOString(),language:navigator.language,userAgent:navigator.userAgent};const response=await fetch('/qa/observe/ROUND_NONCE',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(details),cache:'no-store',credentials:'omit'});if(!response.ok)throw new Error('浏览器信息未得到验收服务确认，请联系电脑端。');note.textContent='预约时区 Asia/Shanghai；浏览器时区 '+details.browserTimeZone+'。正在打开档期…';location.replace('/calendar#token='+encodeURIComponent(token));}catch(error){note.textContent=error.message;}})();
</script></body></html>"""
  html=template.replace('SCRIPT_NONCE',script_nonce).replace('ROUND_NONCE',nonce).replace('ROUND_ID',json.dumps(config['roundId'])).replace('DEVICE_ID',json.dumps(config['deviceId'])).encode('utf-8')
  return self.reply(200,html,'text/html; charset=utf-8',"default-src 'none'; script-src 'nonce-"+script_nonce+"'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'")
 def do_POST(self):
  if self.normalized()!='/observe/'+nonce:return self.reply(404,{'code':'NotFound'})
  if self.headers.get('Origin')not in[None,public]:return self.reply(403,{'code':'WrongOrigin'})
  try:
   size=int(self.headers.get('Content-Length','0'));assert 0<size<=4096;value=json.loads(self.rfile.read(size));assert value.get('roundId')==config['roundId']and value.get('deviceId')==config['deviceId'];tz=value['browserTimeZone'];assert isinstance(tz,str)and len(tz)<=80 and re.fullmatch(r'[A-Za-z0-9_+/-]+',tz);ua=str(value['userAgent']);assert len(ua)<=1024 and ua==self.headers.get('User-Agent','');offset=value['browserUtcOffsetMinutes'];assert isinstance(offset,(int,float))and -840<=offset<=840
  except(Exception):return self.reply(400,{'code':'InvalidObservation'})
  is_iphone='iPhone'in ua and 'Safari/'in ua and not any(x in ua for x in['CriOS/','FxiOS/','EdgiOS/','OPiOS/'])
  record={'serverObservedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'roundId':config['roundId'],'deviceId':config['deviceId'],'appointmentTimeZone':'Asia/Shanghai','browserTimeZone':tz,'browserUtcOffsetMinutes':offset,'browserNowUtc':str(value.get('browserNowUtc',''))[:40],'language':str(value.get('language',''))[:40],'userAgent':ua,'iPhoneSafariUserAgentObserved':is_iphone,'authority':'Browser metadata only; requires human phone-save feedback and calendar device mutation receipt for real-device acceptance','bookingWrites':False}
  with gate:
   with output.open('a',encoding='utf-8')as stream:stream.write(json.dumps(record,ensure_ascii=False)+'\n')
  return self.reply(200,{'recorded':True,'browserTimeZone':tz,'appointmentTimeZone':'Asia/Shanghai'})
ThreadingHTTPServer(('127.0.0.1',args.port),Handler).serve_forever()
