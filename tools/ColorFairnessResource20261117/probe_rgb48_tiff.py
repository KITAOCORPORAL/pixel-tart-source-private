from pathlib import Path
import json,hashlib,struct,sys
file=Path(sys.argv[1]);expected=sys.argv[2].upper();f=file.open('rb');head=f.read(8);assert head[:4]==b'II*\0';off=struct.unpack_from('<I',head,4)[0];f.seek(off);n=struct.unpack('<H',f.read(2))[0];tags={}
for _ in range(n):
 tag,typ,count,value=struct.unpack('<HHII',f.read(12));tags[tag]=(typ,count,value)
def vals(tag):
 typ,n,v=tags[tag];size={1:1,2:1,3:2,4:4}[typ];pos=f.tell();f.seek(v) if n*size>4 else None;b=f.read(n*size) if n*size>4 else struct.pack('<I',v)[:n*size];f.seek(pos);return list(struct.unpack('<'+{1:'B',3:'H',4:'I'}.get(typ,'B')*n,b))
w=vals(256)[0];h=vals(257)[0];bits=vals(258);compression=vals(259)[0];assert compression==1 and bits==[16,16,16];strip=vals(273);counts=vals(279);pixelhash=hashlib.sha256();bytesRead=0
for at,length in zip(strip,counts):
 f.seek(at)
 while length:
  b=f.read(min(length,1024*1024));assert b;pixelhash.update(b);bytesRead+=len(b);length-=len(b)
pixel=pixelhash.hexdigest().upper();assert pixel==expected,(pixel,expected)
icc=None
if 34675 in tags:
 typ,n,at=tags[34675];f.seek(at);profile=f.read(n);icc={'bytes':len(profile),'sha256':hashlib.sha256(profile).hexdigest()}
orientation=vals(274)[0] if 274 in tags else 1;f.close();report={'status':'PASS_STREAMED_STORED_RGB48_EXPORT_PARITY','file':str(file),'width':w,'height':h,'bits':bits,'orientation':orientation,'pixelBytes':bytesRead,'allRgbPayloadSha256':pixel,'icc':icc,'noRawOrNativeDecoder':True,'streamBlockMaxBytes':1048576,'fileSha256':None};Path(sys.argv[3]).write_text(json.dumps(report,indent=2)+chr(10));print(json.dumps({k:v for k,v in report.items() if k not in ['fileSha256','file']}))
