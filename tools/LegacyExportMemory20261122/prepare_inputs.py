"""Freeze existing uncompressed rendered TIFF samples, never read camera RAW."""
from pathlib import Path
import struct,json,hashlib
root=Path(__file__).resolve().parents[2];out=root/'artifacts/legacy-export-memory-20261122/inputs';out.mkdir(parents=True,exist_ok=True)
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def freeze(name,path,expected,kind):
 assert sha(path)==expected.upper(),str(path)
 with path.open('rb') as f:
  endian,magic,off=struct.unpack('<2sHI',f.read(8));assert endian==b'II' and magic==42
  f.seek(off);n=struct.unpack('<H',f.read(2))[0];tags={}
  for _ in range(n):
   tag,typ,count,val=struct.unpack('<HHII',f.read(12));tags[tag]=(typ,count,val)
  def raw(tag):
   typ,n,val=tags[tag];size={1:1,2:1,3:2,4:4,7:1}[typ]
   if size*n<=4:return struct.pack('<I',val)[:size*n]
   f.seek(val);return f.read(size*n)
  def vals(tag):
   typ,n,_=tags[tag];return list(struct.unpack('<'+{1:'B',3:'H',4:'I'}[typ]*n,raw(tag)))
  w,h=vals(256)[0],vals(257)[0];assert vals(258)==[16,16,16] and vals(259)==[1] and vals(277)==[3]
  payload=out/(name+'.rgb48');profile=out/(name+'.icc');assert not payload.exists()
  with payload.open('xb') as dest:
   for at,length in zip(vals(273),vals(279)):
    f.seek(at)
    while length:
     block=f.read(min(length,1048576));assert block;dest.write(block);length-=len(block)
  profile.write_bytes(raw(34675));orientation=vals(274)[0] if 274 in tags else 1
  assert payload.stat().st_size==w*h*6
 task={'name':name,'inputKind':kind,'renderedSource':str(path),'renderedSourceSha256':expected.upper(),'payload':str(payload),'pixelSha256':sha(payload),'width':w,'height':h,'orientation':orientation,'icc':str(profile),'iccSha256':sha(profile),'newRawDecodes':0,'guardBytes':2**31,'inputDifference':'Original company 0806D680 payload unavailable; frozen from existing rendered TIFF. No sensor-linear claim.'}
 (out/(name+'.json')).write_text(json.dumps(task,indent=2)+'\n',encoding='utf-8');return task
synthetic=root.parent/'guangjing-tiff-viewport-memory-20261115/artifacts/tiff-memory-20261115/synthetic-rgb16-7752x5184.tif'
sony=root.parent/'kitao-instant-film-evidence-v3-20261016/.validation/instant-v3/visual-review-final/sony-dsc08015-isolated-frozen.tif'
inputs=[freeze('synthetic40',synthetic,'5D3AEE67B02348F670E47EB94BBB44A16BAC3480311D38300F9C8D690DB49A0E','existing synthetic rendered RGB16, same 7752x5184 dimensions'),freeze('sony33',sony,'5c06a14160c9b57015cfebd9cd1e882620ffc817cfdca061596746cdd82d1ca7','existing authorized Sony rendered RGB16')]
(out/'INPUTS.json').write_text(json.dumps(inputs,indent=2)+'\n',encoding='utf-8');print(json.dumps([{k:x[k] for k in ['name','width','height','orientation','pixelSha256']} for x in inputs]))
