from pathlib import Path
import argparse,json,numpy as np
from PIL import Image,ImageDraw,ImageFont
from rgb16_io import sha,tiff_info,pixels,decode,encode
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('identity_manifest',type=Path);a=p.parse_args();a.root.mkdir(exist_ok=True)
meta=json.loads(a.identity_manifest.read_text('utf-8-sig'))
selected=[v for v in meta['outputs'] if v['phase']=='default']
records=[];thumbs=[]
for v in selected:
 path=Path(v['path']);assert sha(path)==v['sha256'].lower();info=tiff_info(path);image=pixels(path,info);step=max(1,int(np.ceil(max(image.shape[:2])/900)));small=np.asarray(image[::step,::step],dtype=np.float32)/np.float32(65535);del image
 for ev in (0,2):
  display=encode(np.minimum(1,decode(small.astype(np.float64))*2**ev));thumb=Image.fromarray(np.rint(np.clip(display,0,1)*255).astype('uint8'));thumb.thumbnail((900,620));thumbs.append((v['id']+' | stored E0 + reference '+str(ev)+' EV | preview only',thumb))
 records.append(dict(id=v['id'],path=str(path),sha256=v['sha256'].lower(),width=info['width'],height=info['height'],orientation=info['orientation'],iccSha256=info['iccSha256'],domain='Already rendered sRGB16, no sensor reconstruction',authorization='company-validation-20261015/AUTHORIZED_RAW_INPUTS plus RawColorChain default output identity',rawSha256={'company-02':'52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865','company-05':'B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401'}[v['id']]))
canvas=Image.new('RGB',(1800,1300),'#ffffff');draw=ImageDraw.Draw(canvas);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21)
for i,(label,image) in enumerate(thumbs):
 x=(i%2)*900;yy=(i//2)*650;draw.text((x+8,yy+5),label,font=font,fill='black');canvas.paste(image,(x,yy+30))
canvas.save(a.root/'COMPANY_INPUT_CONTACT_SHEET.png');(a.root/'COMPANY_INPUTS.json').write_bytes((json.dumps(records,indent=2)+'\n').encode());print(json.dumps([dict(id=r['id'],width=r['width'],height=r['height'],sha256=r['sha256']) for r in records],indent=2))
