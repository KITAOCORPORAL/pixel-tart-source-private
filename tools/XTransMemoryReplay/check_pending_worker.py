"""Prove a pending successor worker rejects before input/reservation/native access."""
from pathlib import Path
import argparse,ctypes as C,ctypes.wintypes as W,hashlib,json,subprocess,time
p=argparse.ArgumentParser();p.add_argument('dotnet');p.add_argument('worker',type=Path);p.add_argument('output',type=Path);a=p.parse_args();a.output.mkdir(exist_ok=False)
plan=a.output/'pending-plan.json';plan.write_bytes((json.dumps(dict(Version=2,Task='RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111',AuthorizedForHeavyDecode=False,Files=[]))+'\n').encode())
response=a.output/'worker-output';response.mkdir();request=a.output/'request.json'
# input and reservation are deliberately absent. Passing task admission would
# therefore fail with a different error before it could reach any native code.
request.write_bytes((json.dumps(dict(task='RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111',planPath=str(plan.resolve()),planSha=hashlib.sha256(plan.read_bytes()).hexdigest().upper(),outputRoot=str(response.resolve())))+'\n').encode())
with (a.output/'stdout.log').open('w') as out,(a.output/'stderr.log').open('w') as err:
 child=subprocess.Popen([a.dotnet,str(a.worker.resolve()),'--decode-once',str(request.resolve())],cwd=a.worker.resolve().parent,stdout=out,stderr=err,creationflags=0x08000000)
 k=C.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD];k.K32GetProcessMemoryInfo.restype=W.BOOL;k.CloseHandle.argtypes=[W.HANDLE];handle=k.OpenProcess(0x410,False,child.pid)
 peak=0;start=time.monotonic();stopped=False
 while child.poll() is None:
  buffer=C.create_string_buffer(80);C.c_uint32.from_buffer(buffer).value=80
  if handle and k.K32GetProcessMemoryInfo(handle,buffer,80):
   import struct
   working=struct.unpack_from('<Q',buffer.raw,8)[0];private=struct.unpack_from('<Q',buffer.raw,72)[0];peak=max(peak,working,private)
   if peak>2**31 or time.monotonic()-start>20:child.kill();stopped=True;break
  time.sleep(.05)
 if handle:k.CloseHandle(handle)
 child.wait()
result=json.loads((response/'result.json').read_text('utf-8'))
assert child.returncode==1 and not stopped and 'requires explicit new-batch authorization' in result['error']
assert not list(a.output.glob('**/*.consumed')) and not (response/'decoded.rgb48').exists()
receipt=dict(status='PASS_PENDING_AUTHORIZATION_REJECTED_BEFORE_INPUT',exitCode=child.returncode,guardBytes=2**31,peakProcessBytes50ms=peak,newRawDecodes=0,nativeProcessingReached=False,inputAndReservationAbsent=True,error=result['error'])
(a.output/'RECEIPT.json').write_bytes((json.dumps(receipt,indent=2)+'\n').encode());print(json.dumps({k:v for k,v in receipt.items() if k!='error'}))
