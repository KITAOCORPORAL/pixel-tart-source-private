"""Build instruction maps and an explicit measurement plan from fixed local artifacts."""
from pathlib import Path
import struct,json,hashlib,re,sys
ROOT=Path(sys.argv[1]);DLL=Path(sys.argv[2]);OLDOBJ=Path(sys.argv[3])
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rbytes(path,rva,size):
 b=path.read_bytes();pe=struct.unpack_from('<I',b,0x3c)[0];n=struct.unpack_from('<H',b,pe+6)[0];opt=struct.unpack_from('<H',b,pe+20)[0]
 for i in range(n):
  at=pe+24+opt+i*40;vs,va,rs,off=struct.unpack_from('<IIII',b,at+8)
  if va<=rva<va+max(vs,rs):return b[off+rva-va:off+rva-va+size].hex()
 raise ValueError(rva)
def textsections(path):
 b=path.read_bytes();n=struct.unpack_from('<H',b,2)[0];opt=struct.unpack_from('<H',b,16)[0];out=[]
 for i in range(n):
  at=20+opt+i*40;name=b[at:at+8].rstrip(b'\0');size,off=struct.unpack_from('<II',b,at+16)
  if name.startswith(b'.text'):out.append([name.decode(),size,hashlib.sha256(b[off:off+size]).hexdigest()])
 return out
assert sha(DLL)=='305eec498644f01ec707e8ac3e58a4b5670043d35fbc853626ff13ac6ff15484'
a=textsections(OLDOBJ);b=textsections(ROOT/'xtrans-listing.obj');assert a==b and len(a)==377
proof=dict(originalObjectSha256=sha(OLDOBJ),listingObjectSha256=sha(ROOT/'xtrans-listing.obj'),listingSha256=sha(ROOT/'xtrans-optimized.asm'),identicalTextSections=377,orderedTextSectionDigest=hashlib.sha256(json.dumps(a).encode()).hexdigest(),nativeDllSha256=sha(DLL))
(ROOT/'OPTIMIZED_IDENTITY.json').write_text(json.dumps(proof,indent=2)+'\n')
base=0x81cc0;sites=[]
for offset,size,reg,shift,phase,slots,source in [(0x31b,3,'rax',16,'tile-copy-consumed-G',[1],207),(0x5c0,5,'r8',0,'initial-LIM-upper',[3],232),(0x5c8,4,'rdx',0,'initial-LIM-lower',[1],232),(0x868,5,'rbx',0,'recalculation-LIM-upper',[3],257),(0x8ff,5,'rcx',0,'recalculation-LIM-lower',[1],257)]:
 sites.append(dict(rva=base+offset,nextRva=base+offset+size,bytes=rbytes(DLL,base+offset,size),register=reg,shift=shift,phase=phase,slots=slots,sourceLine=source))
sites.append(dict(rva=0x839bf,nextRva=0x839c4,bytes=rbytes(DLL,0x839bf,5),register='rax',shift=0,phase='final-G-store',slots=[1],sourceLine=417))
assert [s['bytes'] for s in sites]==['8b04d1','440fb74306','0fb75302','0fb75cf306','0fb74cf002','668944d102']
plan=dict(version='H1HardwareWindowPlanV1',validationCommit='4c322975c546ac90a568cc1301e520477a3ca8d4',researchCommit='0750a014a03ebf5a77a89f837bf29d2bc53c1775',nativeDllSha256=sha(DLL),readyRva=0x84870,readyByte=rbytes(DLL,0x84870,1),stopRva=0x848d7,stopByte=rbytes(DLL,0x848d7,1),sites=sites,watch=[dict(id='C1-lower',x=7460,y=507,slot=1,cfa=2),dict(id='C1-upper-control',x=7460,y=507,slot=3,cfa=2),dict(id='C2-lower',x=7461,y=506,slot=1,cfa=0),dict(id='C2-upper-control',x=7461,y=506,slot=3,cfa=0)],maxEvents=256,maximumRawProcesses=2,expectedWriterTiles={'C1':[499,7443],'C2':[3,7443]},expectedCrossReaders={'C1':[3,7443],'C2':[499,7443]},support='Actual mapped LIM/copy load consumed value different from own serial min witness and matching corresponding other-tile final-G store. This supports this execution mechanism, not original-DLL root-cause equivalence.',weaken='Complete mapped window loads retain serial bounds despite captured other-tile writes; weakens the mechanism for these points/schedule only. Absence under debugger cannot falsify all schedules.',insufficient='Missing ready/stop, truncation, wrong code/shape, no matching write or unknown required consumer site. Never substitute a peek.',perturbation='Two software boundaries outside the worker; per-thread hardware data traps stop debugger threads and perturb scheduling substantially. Debug delivery order is not memory happens-before. Worker instructions remain byte-identical.',noFullFramePayloads=True,wholeP5P6Comparison=False)
(ROOT/'PLAN.json').write_text(json.dumps(plan,indent=2)+'\n')
# Marked multi-thread self-test: same debugger arming, word and wide extraction, stop/restore protocol.
exe=ROOT/'widetest.exe';ls=(ROOT/'WIDETEST_DISASSEMBLY.txt').read_text(errors='replace').splitlines();ins=[]
for l in ls:
 m=re.match(r'  ([0-9A-F]{16}): (?:[0-9A-F]{2} )+\s+(\w+)\s*(.*)',l)
 if m:ins.append((int(m[1],16)-0x140000000,m[2],m[3]))
regs={'eax':'rax','edx':'rdx','ecx':'rcx','r8w':'r8','ax':'rax','cx':'rcx','dx':'rdx'};maps=[];wide=None
for i,(at,mn,oper) in enumerate(ins[:-1]):
 if any(x in oper for x in ['word ptr [0000000140003002h]','word ptr [0000000140003006h]','dword ptr [0000000140003000h]']):
  if mn=='movzx':reg=regs[oper.split(',')[0]];phase='read';shift=0
  elif mn=='mov' and oper.startswith('word ptr'):reg=regs[oper.split(',')[1]];phase='write';shift=0
  elif mn=='mov' and 'dword ptr [0000000140003000h]' in oper:reg=regs[oper.split(',')[0]];phase='wide-read';shift=16
  else:continue
  size=ins[i+1][0]-at;assert 0<size<=15;maps.append(dict(rva=at,nextRva=at+size,bytes=rbytes(exe,at,size),phase=phase,register=reg,shift=shift))
  if phase=='wide-read':wide=at+size
assert wide is not None
ready=int(re.search(r'\d+\s+\w+\s+([0-9A-F]{8}) h1_probe','\n'.join(ls))[1],16)
config=dict(mode='synthetic',useMarkers=True,argv=[str(exe)],executableSha256=sha(exe),maxEvents=32,timeoutSeconds=20,watch=[dict(rva=0x3002,id='lower'),dict(rva=0x3006,id='upper')],sites=maps,readyRva=ready,readyByte=rbytes(exe,ready,1),stopRva=wide,stopByte=rbytes(exe,wide,1))
(ROOT/'MARKED_SELFTEST_CONFIG.json').write_text(json.dumps(config,indent=2)+'\n')
print(json.dumps(dict(nativeIdentity=proof['nativeDllSha256'],identicalTextSections=377,selectedSites=len(sites),candidatePixels=2,markedSelfTestSites=len(maps)),indent=2))
