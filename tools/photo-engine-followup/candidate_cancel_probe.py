import ctypes as C,ctypes.wintypes as W,pathlib,json,time,hashlib
output=pathlib.Path('artifacts/followup/03-candidate/CALLBACK_CANCEL.json')
if output.exists():raise RuntimeError('New cancellation evidence path required; never overwrite')
source=pathlib.Path('artifacts/followup/authorized/Sony_DSC08015.ARW')
if hashlib.sha256(source.read_bytes()).hexdigest().upper()!='1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93':raise RuntimeError('Unadmitted Sony copy')
p=pathlib.Path('N:/pixart/kitao-photo-engine-home-20261016/artifacts/home-photo/candidate-local/extracted/LibRaw-0.22.2/bin/libraw.dll')
if hashlib.sha256(p.read_bytes()).hexdigest().lower()!='6a459c22039abf0eac4d263673337c8ed5f223acbd372fcf77610debf80ac8cd':raise RuntimeError('Unadmitted candidate native')
add=__import__('os').add_dll_directory(str(p.parent));dll=C.CDLL(str(p));P=C.c_void_p;I=C.c_int;U=C.c_uint;CB=C.CFUNCTYPE(I,P,I,I,I)
for n,r,a in [('libraw_init',P,[U]),('libraw_open_wfile',I,[P,C.c_wchar_p]),('libraw_unpack',I,[P]),('libraw_dcraw_process',I,[P]),('libraw_close',None,[P]),('libraw_set_progress_handler',None,[P,CB,P])]:f=getattr(dll,n);f.restype=r;f.argtypes=a
started=time.perf_counter();events=[]
def callback(_,stage,iteration,expected):
 events.append(dict(ms=(time.perf_counter()-started)*1000,stage=stage,iteration=iteration,expected=expected));return int((time.perf_counter()-started)>.05)
cb=CB(callback);raw=dll.libraw_init(0);open_code=unpack_code=process_code=None
try:
 dll.libraw_set_progress_handler(raw,cb,None);open_code=dll.libraw_open_wfile(raw,str(pathlib.Path('artifacts/followup/authorized/Sony_DSC08015.ARW').resolve()))
 if open_code==0:
  unpack_code=dll.libraw_unpack(raw)
  if unpack_code==0:process_code=dll.libraw_dcraw_process(raw)
finally:dll.libraw_close(raw)
result=dict(identity='Sony_DSC08015',nativeSha256=__import__('hashlib').sha256(p.read_bytes()).hexdigest(),status='CANCELLED_BY_NATIVE_CALLBACK' if -100010 in [open_code,unpack_code,process_code] else 'NOT_CANCELLED',openCode=open_code,unpackCode=unpack_code,processCode=process_code,elapsedMs=(time.perf_counter()-started)*1000,callbacks=events,outputProduced=False,scope='Direct fixed-candidate C API cancellation on isolated copy, not product switch/UI cancellation',nativeClosed=True)
output.write_text(json.dumps(result,indent=2)+'\n');print({k:v for k,v in result.items() if k!='callbacks'})
