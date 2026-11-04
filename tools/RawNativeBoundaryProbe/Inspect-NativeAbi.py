import sys,struct,json,hashlib,pathlib
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
p=pathlib.Path(sys.argv[1]); b=p.read_bytes(); nt=struct.unpack_from('<I',b,0x3c)[0]; opt=nt+24; base=struct.unpack_from('<Q',b,opt+24)[0]; ns=struct.unpack_from('<H',b,nt+6)[0]; os=struct.unpack_from('<H',b,nt+20)[0]
sections=[struct.unpack_from('<8sIIIIIIHHI',b,opt+os+i*40) for i in range(ns)]
def off(rva):
 for s in sections:
  if s[2]<=rva<s[2]+s[1]: return s[4]+rva-s[2]
 raise ValueError(hex(rva))
def cstr(o): return b[o:b.index(0,o)].decode('ascii')
er=struct.unpack_from('<I',b,opt+112)[0]; e=struct.unpack_from('<IIHHIIIIIII',b,off(er)); nf,nn,fa,na,oa=e[6:]; exports={}
for i in range(nn):
 name=cstr(off(struct.unpack_from('<I',b,off(na)+i*4)[0])); ordinal=struct.unpack_from('<H',b,off(oa)+i*2)[0]; rva=struct.unpack_from('<I',b,off(fa)+ordinal*4)[0]; exports[name]=base+rva
md=Cs(CS_ARCH_X86,CS_MODE_64)
def disasm(addr,length=250):
 o=off(addr-base); return [{'address':hex(x.address),'bytes':x.bytes.hex(),'mnemonic':x.mnemonic,'operands':x.op_str} for x in md.disasm(b[o:o+length],addr)]
sel=sys.argv[3:]; selected={n:disasm(exports[n]) if n in exports else disasm(int(n,0),12000) for n in sel}
result={'path':str(p),'sha256':hashlib.sha256(b).hexdigest(),'imageBase':hex(base),'exports':{n:hex(v) for n,v in exports.items()},'instructions':selected,'rawDecodes':0}
pathlib.Path(sys.argv[2]).write_text(json.dumps(result,indent=2)+'\n')
for n,ins in selected.items():
 print('\n===',n,'===')
 for i in ins[:60]: print(i['address'],i['mnemonic'],i['operands'])
