"""Read-only PE import names; does not load a binary."""
import pathlib,struct,json,sys,hashlib
p=pathlib.Path(sys.argv[1]);b=p.read_bytes();pe=struct.unpack_from('<I',b,0x3c)[0];assert b[pe:pe+4]==b'PE\0\0';count=struct.unpack_from('<H',b,pe+6)[0];size=struct.unpack_from('<H',b,pe+20)[0];opt=pe+24;magic=struct.unpack_from('<H',b,opt)[0];dirs=opt+(112 if magic==0x20b else 96);rva=struct.unpack_from('<I',b,dirs+8)[0];sections=[]
for i in range(count):
 off=opt+size+40*i;vs,va,rs,rp=struct.unpack_from('<IIII',b,off+8);sections.append((va,max(vs,rs),rp))
def pos(r):
 for va,n,rp in sections:
  if va<=r<va+n:return rp+r-va
 raise ValueError('unmapped rva')
imports=[]
if rva:
 off=pos(rva)
 while any(struct.unpack_from('<IIIII',b,off)):
  nr=struct.unpack_from('<I',b,off+12)[0];n=pos(nr);imports.append(b[n:b.index(0,n)].decode());off+=20
print(json.dumps(dict(path=str(p),sha256=hashlib.sha256(b).hexdigest(),imports=imports,delayImports='NOT_INSPECTED',notLicenseApproval=True),indent=2))
