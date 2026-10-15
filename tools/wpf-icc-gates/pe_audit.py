import pathlib,struct,json,sys,hashlib
p=pathlib.Path(sys.argv[1]);b=p.read_bytes();pe=struct.unpack_from('<I',b,0x3c)[0];assert b[pe:pe+4]==b'PE\0\0';ns=struct.unpack_from('<H',b,pe+6)[0];os=struct.unpack_from('<H',b,pe+20)[0];opt=pe+24;magic=struct.unpack_from('<H',b,opt)[0];base=struct.unpack_from('<Q' if magic==0x20b else '<I',b,opt+(24 if magic==0x20b else 28))[0];dd=opt+(112 if magic==0x20b else 96);sections=[]
for i in range(ns):
 o=opt+os+40*i;vs,va,rs,rp=struct.unpack_from('<IIII',b,o+8);sections.append((va,max(vs,rs),rp))
def pos(r):
 for va,n,rp in sections:
  if va<=r<va+n:return rp+r-va
 raise ValueError('unmappedRVA')
def string(r):o=pos(r);return b[o:b.index(0,o)].decode()
imports=[];delay=[];rva,size=struct.unpack_from('<II',b,dd+8)
if rva:
 o=pos(rva)
 while any(struct.unpack_from('<IIIII',b,o)):imports.append(string(struct.unpack_from('<I',b,o+12)[0]));o+=20
rva,size=struct.unpack_from('<II',b,dd+13*8)
if rva:
 o=pos(rva)
 while any(struct.unpack_from('<IIIIIIII',b,o)):
  attr,name=struct.unpack_from('<II',b,o);delay.append(string(name if attr&1 else name-base));o+=32
print(json.dumps(dict(path=str(p),sha256=hashlib.sha256(b).hexdigest(),ordinaryImports=imports,delayImports=delay,peArchitecture=hex(magic),noTransitiveClaim=True),indent=2))
