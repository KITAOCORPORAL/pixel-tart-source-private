from pathlib import Path
import struct,hashlib,json,argparse
p=argparse.ArgumentParser();p.add_argument('file',type=Path);p.add_argument('out',type=Path);a=p.parse_args()
with a.file.open('rb') as f:
 head=f.read(8);assert head[:4]==b'II*\0';off=struct.unpack_from('<I',head,4)[0];f.seek(off);n=struct.unpack('<H',f.read(2))[0];tags={}
 for _ in range(n):
  tag,typ,count,val=struct.unpack('<HHII',f.read(12));tags[tag]=(typ,count,val)
 def raw(tag):
  typ,n,val=tags[tag];size={1:1,2:1,3:2,4:4,7:1}[typ]
  if size*n<=4:return struct.pack('<I',val)[:size*n]
  f.seek(val);return f.read(n*size)
 def vals(tag):
  typ,n,_=tags[tag];return list(struct.unpack('<'+{1:'B',3:'H',4:'I'}[typ]*n,raw(tag)))
 assert vals(258)==[16,16,16] and vals(259)==[1]
 pixel=hashlib.sha256();count=0
 for at,length in zip(vals(273),vals(279)):
  f.seek(at)
  while length:
   data=f.read(min(length,1048576));assert data;pixel.update(data);count+=len(data);length-=len(data)
 profile=raw(34675) if 34675 in tags else b''
 result={'file':str(a.file),'width':vals(256)[0],'height':vals(257)[0],'bits':vals(258),'orientation':vals(274)[0] if 274 in tags else 1,'pixelBytes':count,'pixelSha256':pixel.hexdigest().upper(),'icc':{'bytes':len(profile),'sha256':hashlib.sha256(profile).hexdigest().upper()},'newRawDecodes':0}
with a.file.open('rb') as f:result['fileSha256']=hashlib.file_digest(f,'sha256').hexdigest().upper()
a.out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='file'}))
