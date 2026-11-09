"""Bounded architectural post-instruction hardware watchpoint recorder (Windows x64).
Never obtains a consumer operand by reading the watched memory after a trap.
ReadProcessMemory is restricted to executable-code verification, object pointers,
and a clearly separated serial-before-parallel initial witness.
"""
import ctypes as C, ctypes.wintypes as W, json,struct,subprocess,time,sys,hashlib
from pathlib import Path
K=C.WinDLL('kernel32',use_last_error=True)
class SI(C.Structure):
 _fields_=[('cb',W.DWORD),('reserved',W.LPWSTR),('desktop',W.LPWSTR),('title',W.LPWSTR),('x',W.DWORD),('y',W.DWORD),('xs',W.DWORD),('ys',W.DWORD),('xc',W.DWORD),('yc',W.DWORD),('fill',W.DWORD),('flags',W.DWORD),('show',W.WORD),('r2',W.WORD),('p2',C.c_void_p),('hin',W.HANDLE),('hout',W.HANDLE),('herr',W.HANDLE)]
class PI(C.Structure):_fields_=[('process',W.HANDLE),('thread',W.HANDLE),('pid',W.DWORD),('tid',W.DWORD)]
class DE(C.Structure):_fields_=[('code',W.DWORD),('pid',W.DWORD),('tid',W.DWORD),('pad',W.DWORD),('u',C.c_ubyte*160)]
for name,args,ret in [('CreateProcessW',[W.LPCWSTR,W.LPWSTR,C.c_void_p,C.c_void_p,W.BOOL,W.DWORD,C.c_void_p,W.LPCWSTR,C.POINTER(SI),C.POINTER(PI)],W.BOOL),('WaitForDebugEvent',[C.POINTER(DE),W.DWORD],W.BOOL),('ContinueDebugEvent',[W.DWORD,W.DWORD,W.DWORD],W.BOOL),('OpenThread',[W.DWORD,W.BOOL,W.DWORD],W.HANDLE),('GetThreadContext',[W.HANDLE,C.c_void_p],W.BOOL),('SetThreadContext',[W.HANDLE,C.c_void_p],W.BOOL),('ReadProcessMemory',[W.HANDLE,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)],W.BOOL),('WriteProcessMemory',[W.HANDLE,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)],W.BOOL),('FlushInstructionCache',[W.HANDLE,C.c_void_p,C.c_size_t],W.BOOL),('CloseHandle',[W.HANDLE],W.BOOL),('TerminateProcess',[W.HANDLE,W.UINT],W.BOOL),('GetFinalPathNameByHandleW',[W.HANDLE,W.LPWSTR,W.DWORD,W.DWORD],W.DWORD)]:
 f=getattr(K,name);f.argtypes=args;f.restype=ret
K.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];K.K32GetProcessMemoryInfo.restype=W.BOOL
REG={'rax':120,'rcx':128,'rdx':136,'rbx':144,'rsp':152,'rbp':160,'rsi':168,'rdi':176,**{'r'+str(i):184+(i-8)*8 for i in range(8,16)},'rip':248}
class Context:
 def __init__(self,tid):
  self.h=K.OpenThread(0x5a,False,tid);ok(self.h,'OpenThread');self.b=C.create_string_buffer(1248);self.p=(C.addressof(self.b)+15)&~15;self.flags(0x100013);ok(K.GetThreadContext(self.h,self.p),'GetThreadContext')
 def flags(self,v):C.c_uint32.from_address(self.p+48).value=v
 def get(self,n):return C.c_uint64.from_address(self.p+REG[n]).value
 def set(self,n,v):C.c_uint64.from_address(self.p+REG[n]).value=v
 def dr(self,i,v=None):
  field=C.c_uint64.from_address(self.p+(72+i*8 if i<4 else {6:104,7:112}[i]))
  if v is None:return field.value
  field.value=v
 def apply(self):ok(K.SetThreadContext(self.h,self.p),'SetThreadContext')
 def close(self):K.CloseHandle(self.h)
def ok(v,label):
 if not v:raise OSError(C.get_last_error(),label)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(config,out):
 cfg=json.loads(Path(config).read_text());out=Path(out);out.mkdir(exist_ok=False,parents=True)
 assert C.sizeof(C.c_void_p)==8 and C.sizeof(DE)==176
 exe=Path(cfg['argv'][0]);assert sha(exe)==cfg['executableSha256'];assert cfg['maxEvents']<=512
 si=SI();si.cb=C.sizeof(SI);pi=PI();cmd=C.create_unicode_buffer(subprocess.list2cmdline(cfg['argv']))
 ok(K.CreateProcessW(str(exe),cmd,None,None,False,0x08000002,None,cfg.get('workingDirectory',str(exe.parent)),C.byref(si),C.byref(pi)),'CreateProcessW')
 threads=set();events=[];armed=False;ready=False;base=None;watch=[];witness=[];unknown=0;truncated=False;initial=False;marker=None;marker_byte=None;stop_marker=None;stop_byte=None;window_closed=False;exitcode=None;start=time.monotonic();imageBase=None
 def read(addr,n):
  b=C.create_string_buffer(n);done=C.c_size_t();ok(K.ReadProcessMemory(pi.process,addr,b,n,C.byref(done)),'ReadProcessMemory');assert done.value==n;return b.raw
 def write(addr,b):
  done=C.c_size_t();ok(K.WriteProcessMemory(pi.process,addr,C.c_char_p(b),len(b),C.byref(done)),'WriteProcessMemory');assert done.value==len(b);ok(K.FlushInstructionCache(pi.process,addr,len(b)),'FlushInstructionCache')
 def arm(tid,enable=True):
  ctx=Context(tid)
  try:
   for i in range(4):ctx.dr(i,watch[i]['address'] if enable and i<len(watch) else 0)
   dr7=sum((1<<(i*2))|(3<<(16+i*4))|(1<<(18+i*4)) for i in range(len(watch))) if enable else 0
   ctx.dr(6,0);ctx.dr(7,dr7);ctx.flags(0x100010);ctx.apply()
  finally:ctx.close()
 try:
  while exitcode is None:
   if time.monotonic()-start>cfg.get('timeoutSeconds',600):raise TimeoutError('No automatic retry')
   memory=C.create_string_buffer(80);struct.pack_into('<I',memory,0,80);ok(K.K32GetProcessMemoryInfo(pi.process,memory,80),'GetProcessMemoryInfo');working=struct.unpack_from('<Q',memory.raw,16)[0]
   if working>cfg.get('memoryLimitBytes',2147483648):raise MemoryError('External working-set guard; no retry')
   e=DE()
   if not K.WaitForDebugEvent(C.byref(e),200):
    if C.get_last_error()==121:continue
    ok(False,'WaitForDebugEvent')
   began=time.monotonic_ns();u=bytes(e.u);cont=0x10002
   if e.code in [2,3]:
    threads.add(e.tid)
    if e.code==3:
     imageBase=struct.unpack_from('<Q',u,24)[0]
     if cfg['mode']=='synthetic':
      base=imageBase;watch=[{**r,'address':base+r['rva']} for r in cfg['watch']]
      if cfg.get('useMarkers'):
       marker=base+cfg['readyRva'];marker_byte=read(marker,1);assert marker_byte.hex()==cfg['readyByte'];write(marker,bytes([0xcc]));stop_marker=base+cfg['stopRva'];stop_byte=read(stop_marker,1);assert stop_byte.hex()==cfg['stopByte'];write(stop_marker,bytes([0xcc]))
      else:armed=True;ready=True
     hf=struct.unpack_from('<Q',u,0)[0]
     if hf:K.CloseHandle(hf)
    if armed:arm(e.tid)
   elif e.code==4:threads.discard(e.tid)
   elif e.code==5:exitcode=struct.unpack_from('<I',u,0)[0]
   elif e.code==6:
    hf,dllbase=struct.unpack_from('<QQ',u,0)
    if hf:
     buf=C.create_unicode_buffer(32768);n=K.GetFinalPathNameByHandleW(hf,buf,len(buf),0);module=buf.value if n else ''
     if cfg['mode']=='raw' and module.lower().endswith('raw_r.dll'):
      assert base is None and sha(module)==cfg['nativeSha256'];base=dllbase;marker=base+cfg['readyRva'];marker_byte=read(marker,1);assert marker_byte.hex()==cfg['readyByte'];write(marker,bytes([0xcc]));stop_marker=base+cfg['stopRva'];stop_byte=read(stop_marker,1);assert stop_byte.hex()==cfg['stopByte'];write(stop_marker,bytes([0xcc]))
     K.CloseHandle(hf)
   elif e.code==1:
    ex=struct.unpack_from('<I',u,0)[0];address=struct.unpack_from('<Q',u,16)[0]
    if ex==0x80000003 and marker is not None and address==marker:
     assert not ready;ctx=Context(e.tid)
     try:
      write(marker,marker_byte);ctx.set('rip',marker)
      if cfg['mode']=='raw':
       this=ctx.get('r14');img=struct.unpack('<Q',read(this+8,8))[0];height,width=struct.unpack('<HH',read(this+0x14,4));assert (width,height)==(7752,5184) and img%8==0
       for r in cfg['watch']:
        addr=img+(r['y']*width+r['x'])*8+r['slot']*2;watch.append({**r,'address':addr});witness.append({**r,'serialWitnessOnly':struct.unpack('<H',read(addr,2))[0]})
      else:
       witness=[{k:v for k,v in r.items() if k!='address'}|{'serialWitnessOnly':struct.unpack('<H',read(r['address'],2))[0]} for r in watch]
      ctx.apply()
     finally:ctx.close()
     for tid in threads:arm(tid)
     armed=True;ready=True
    elif ex==0x80000003 and stop_marker is not None and address==stop_marker:
     assert ready and not window_closed;ctx=Context(e.tid)
     try:write(stop_marker,stop_byte);ctx.set('rip',stop_marker);ctx.apply()
     finally:ctx.close()
     for tid in threads:arm(tid,False)
     armed=False;window_closed=True
    elif ex==0x80000003 and not initial:initial=True
    elif ex==0x80000004 and armed:
     ctx=Context(e.tid)
     try:
      mask=ctx.dr(6)&15
      if mask:
       nextRva=ctx.get('rip')-base;site=next((s for s in cfg['sites'] if s['nextRva']==nextRva),None)
       if site:assert read(base+site['rva'],len(bytes.fromhex(site['bytes']))).hex()==site['bytes']
       for i in range(len(watch)):
        if mask&(1<<i):
         item={'sequence':len(events),'threadId':e.tid,'watch':{k:v for k,v in watch[i].items() if k!='address'},'nextRva':nextRva,'consumerValueKnown':False,'phase':'UNMAPPED_HARDWARE_ACCESS'}
         item['postRegisters']={name:ctx.get(name) for name in REG};item['postEFlags']=C.c_uint32.from_address(ctx.p+68).value
         if site:
          item.update(site);
          if cfg['mode']=='raw':
           assert watch[i]['slot'] in site['slots'],'Hardware address hit wrong typed operand slot'
           phase=site['phase']
           if phase=='tile-copy-consumed-G':actual_address=ctx.get('rcx')+ctx.get('rdx')*8+2
           elif phase=='initial-LIM-lower':actual_address=ctx.get('rbx')+2
           elif phase=='initial-LIM-upper':actual_address=ctx.get('rbx')+6
           elif phase=='recalculation-LIM-lower':actual_address=ctx.get('rax')+ctx.get('rsi')*8+2
           elif phase=='recalculation-LIM-upper':actual_address=struct.unpack('<Q',read(ctx.get('rbp')-0x60,8))[0]+ctx.get('rsi')*8+6
           elif phase=='final-G-store':actual_address=ctx.get('rcx')+ctx.get('rdx')*8+2
           else:raise AssertionError('Unreviewed actual operand address')
           assert actual_address==watch[i]['address'],'Optimized effective address does not match watched cell'
           item['effectiveAddressVerified']=True
          item['consumerValueKnown']=True;item['value']=((ctx.get(site['register'])>>site.get('shift',0))&65535) if 'register' in site else site['immediate'];item['tileTop']=None
          if cfg['mode']=='raw':
           # Static optimized-stack locations from byte-matched source listing.
           rsp=ctx.get('rsp');item['tileTop']=struct.unpack('<i',read(rsp+0x40,4))[0];item['tileLeft']=struct.unpack('<i',read(rsp+0x30,4))[0]
         else:unknown+=1
         item['debuggerPauseProcessingNs']=time.monotonic_ns()-began;events.append(item)
       ctx.dr(6,0);ctx.flags(0x100010);ctx.apply()
       if len(events)>=cfg['maxEvents']:
        truncated=True
        for tid in threads:arm(tid,False)
        armed=False
      else:cont=0x80010001
     finally:ctx.close()
    elif ex==0x80000004 and truncated:cont=0x10002
    else:cont=0x80010001
   ok(K.ContinueDebugEvent(e.pid,e.tid,cont),'ContinueDebugEvent')
  result={'status':'completed' if exitcode==0 and ready and ((cfg['mode']=='synthetic' and not cfg.get('useMarkers')) or window_closed) else 'FAILED_NO_RETRY','exitCode':exitcode,'pid':pi.pid,'readyHit':ready,'windowClosedAfterJoin':window_closed,'events':events,'serialWitness':witness,'unknownAccesses':unknown,'truncated':truncated,'configSha256':sha(config),'mode':cfg['mode'],'elapsedSeconds':time.monotonic()-start,'readPolicy':'No watched-memory reread at any post-instruction event. Values are register/immediate operands of mapped executed instructions. Serial witness before OMP is separate.','observerEffect':'Debug events stop/schedule threads; bounded bookkeeping in another process substantially perturbs scheduling. Event order is debugger delivery order, not global memory order.'}
  (out/'RECEIPT.json').write_text(json.dumps(result,indent=2)+'\n');return result
 except BaseException as err:
  K.TerminateProcess(pi.process,90);(out/'FAILURE.json').write_text(json.dumps({'error':repr(err),'events':events,'noRetry':True,'mode':cfg['mode']},indent=2));raise
 finally:K.CloseHandle(pi.process);K.CloseHandle(pi.thread)
if __name__=='__main__':
 r=run(sys.argv[1],sys.argv[2]);print(json.dumps({k:v for k,v in r.items() if k not in ['events','serialWitness']},indent=2));sys.exit(0 if r['status']=='completed' else 1)
