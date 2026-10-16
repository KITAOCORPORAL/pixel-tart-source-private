from pathlib import Path
import struct,json,hashlib
root=Path('artifacts/home-photo/candidate-local/extracted/LibRaw-0.22.2')
def inspect(path):
 data=path.read_bytes();pe=struct.unpack_from('<I',data,60)[0];assert data[pe:pe+4]==b'PE\0\0';machine,nsec=struct.unpack_from('<HH',data,pe+4);optsize=struct.unpack_from('<H',data,pe+20)[0];opt=pe+24;magic=struct.unpack_from('<H',data,opt)[0];assert magic==0x20b
 sec=opt+optsize;sections=[]
 for i in range(nsec):
  at=sec+40*i;virtual,va,rawsize,raw=struct.unpack_from('<IIII',data,at+8);sections.append((va,max(virtual,rawsize),raw))
 def offset(rva):
  for va,size,raw in sections:
   if va<=rva<va+size:return raw+rva-va
  raise ValueError('UnmappedRVA')
 imports=[];rva,size=struct.unpack_from('<II',data,opt+112+8)
 if rva:
  at=offset(rva)
  for i in range(512):
   values=struct.unpack_from('<IIIII',data,at+20*i)
   if not any(values):break
   start=offset(values[3]);imports.append(data[start:data.index(0,start)].decode('ascii'))
 return dict(file=str(path),sha256=hashlib.sha256(data).hexdigest(),machine=hex(machine),architecture='AMD64' if machine==0x8664 else 'UNKNOWN',imports=imports)
rows=[inspect(root/'bin/libraw.dll'),inspect(root/'bin/dcraw_emu.exe')]
source=[]
for name in ['Makefile.msvc','samples/dcraw_emu.cpp','src/decoders/sonycc.cpp','src/postprocessing/dcraw_process.cpp','libraw/libraw_version.h','LICENSE.LGPL','LICENSE.CDDL','COPYRIGHT']:
 p=root/name;source.append(dict(file=name,exists=p.exists(),sha256=hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None))
Path('docs/evidence/photo-engine-home-20261016/05_candidate_gates/HOME_DEPENDENCIES_AND_SOURCE.json').write_text(json.dumps(dict(rows=rows,source=source,compilerAvailableFromKnownPaths=False,cleanRebuild='NOT_RUN',distributionElection='OPEN_NOT_APPROVED'),indent=2),encoding='utf-8')
print(rows)
