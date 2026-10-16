import pathlib,json,hashlib,subprocess,time,ctypes,os
root=pathlib.Path.cwd();p=pathlib.Path('N:/pixart/kitao-photo-engine-home-20261016/artifacts/home-photo/candidate-local/extracted/LibRaw-0.22.2');out=root/'artifacts/followup/03-candidate';exe=p/'bin/dcraw_emu.exe';dll=p/'bin/libraw.dll';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();assert sha(exe)=='8DF090A80C784FA03182B91AB1679FD08E5FC139D54B0BFB8B003024EF741129';assert sha(dll)=='6A459C22039ABF0EAC4D263673337C8ED5F223ACBD372FCF77610DEBF80AC8CD'
class PMC(ctypes.Structure):_fields_=[('cb',ctypes.c_ulong),('PageFaultCount',ctypes.c_ulong),('PeakWorkingSetSize',ctypes.c_size_t),('WorkingSetSize',ctypes.c_size_t),('QuotaPeakPagedPoolUsage',ctypes.c_size_t),('QuotaPagedPoolUsage',ctypes.c_size_t),('QuotaPeakNonPagedPoolUsage',ctypes.c_size_t),('QuotaNonPagedPoolUsage',ctypes.c_size_t),('PagefileUsage',ctypes.c_size_t),('PeakPagefileUsage',ctypes.c_size_t),('PrivateUsage',ctypes.c_size_t)]
get=ctypes.WinDLL('psapi').GetProcessMemoryInfo;get.argtypes=[ctypes.c_void_p,ctypes.POINTER(PMC),ctypes.c_ulong]
rows=[]
for name,file in [('Sony_DSC08015','Sony_DSC08015.ARW'),('Fuji_DSCF0347','Fuji_DSCF0347.RAF'),('Canon_TNAN8886','Canon_TNAN8886.CR3')]:
 for repeat in [1,2]:
  output=out/f'{name}-{repeat}.private.ppm'
  if output.exists():raise RuntimeError('no overwrite')
  source=root/'artifacts/followup/authorized'/file;cmd=[str(exe),'-w','-6','-o','4','-g','1','1','-W','-q','3','-t','0','-Z',str(output),str(source)];clock=time.perf_counter();samples=[]
  with (out/f'{name}-{repeat}.private.log').open('wb') as log:
   proc=subprocess.Popen(cmd,stdout=log,stderr=log,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
   while proc.poll() is None:
    mem=PMC();mem.cb=ctypes.sizeof(mem)
    if get(int(proc._handle),ctypes.byref(mem),mem.cb):samples.append(dict(ms=(time.perf_counter()-clock)*1000,workingSet=mem.WorkingSetSize,peakWorkingSet=mem.PeakWorkingSetSize,private=mem.PrivateUsage))
    time.sleep(.02)
  row=dict(identity=name,repeat=repeat,exitCode=proc.returncode,milliseconds=(time.perf_counter()-clock)*1000,sourceSha256=sha(source),executableSha256=sha(exe),nativeSha256=sha(dll),flags=cmd[1:-1],contract='PhotoRaw0222Research1: cameraWB baked, gamma1/1, ProPhoto primaries D65 convention; processed quantized RGB16 not sensor raw, not ICC ProPhotoD50',orientationApplied=False,icc='PPM has no embedded ICC; explicit research contract only',memorySamples=samples,distributionApproved=False)
  if proc.returncode==0:
   with output.open('rb') as f:
    tokens=[]
    while len(tokens)<4:
     line=f.readline()
     if not line:raise RuntimeError('bad ppm')
     if not line.startswith(b'#'):tokens+=line.split()
    assert tokens[0]==b'P6' and tokens[3]==b'65535';w,h=map(int,tokens[1:3]);assert output.stat().st_size-f.tell()==w*h*6;digest=hashlib.sha256()
    while block:=f.read(1048576):digest.update(block)
   row.update(width=w,height=h,bits=16,pixelSha256=digest.hexdigest().upper(),status='RESEARCH_PREBUILT_DECODED')
  else:row['status']='FAILED'
  rows.append(row);print(name,repeat,row['status'],row.get('width'),row.get('height'),row.get('pixelSha256'),flush=True)
(out/'CANDIDATE_RESULTS.json').write_text(json.dumps(dict(status='RESEARCH_NOT_PRODUCT',rows=rows,cleanWindowsRebuild='NOT_RUN',cleanVMLoad='NOT_RUN',productEntryEnabled=False),indent=2)+'\n')
