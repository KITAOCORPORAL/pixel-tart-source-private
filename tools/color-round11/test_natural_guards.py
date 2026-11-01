"""Actual rejection paths; no image math or whole/raw processing."""
from pathlib import Path
import json,ctypes,sys,hashlib
import numpy as np
r=Path('N:/pixart/ColorResearchHome-20261030');root=r/'round11';libpath=root/'build-natural-v1/engine/spektrafilm_engine.dll';lib=ctypes.CDLL(str(libpath));m=json.loads((root/'natural-inputs-v1/manifest.json').read_text());src=m['sources'][0];resources=r/'resources-patch';repo=Path(__file__).resolve().parents[2];resolved=json.loads((repo/'tools/cross-device-diagnostic/resolved-portraA.json').read_text());hashes={p.relative_to(resources).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in resources.rglob('*') if p.is_file()};rs=hashlib.sha256(json.dumps(hashes,sort_keys=True,separators=(',',':')).encode()).hexdigest();READ=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_float));WRITE=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_uint16));POLL=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p)
records=[]
for name,symbol,sourcehash,pixelhash,w,h in [('new-wrong-source','spk_stream_natural_photo_v1','0'*64,src['pixelSha256'],src['proxyShape'][1],src['proxyShape'][0]),('new-wrong-pixel','spk_stream_natural_photo_v1',src['sha256'].lower(),'0'*64,src['proxyShape'][1],src['proxyShape'][0]),('new-wrong-shape','spk_stream_natural_photo_v1',src['sha256'].lower(),src['pixelSha256'],src['proxyShape'][1]-1,src['proxyShape'][0]),('original-entry-still-rejects-new-source','spk_stream_research_v1',src['sha256'].lower(),src['pixelSha256'],src['proxyShape'][1],src['proxyShape'][0])]:
 counts=[0]
 @READ
 def read(*args):counts[0]+=1;return 1
 @WRITE
 def write(*args):return 1
 @POLL
 def poll(*args):return 0
 fn=getattr(lib,symbol);fn.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint64,ctypes.c_uint64,ctypes.c_uint64,READ,WRITE,POLL,ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_int;lib.spk_string_free.argtypes=[ctypes.c_void_p];request={'version':'NaturalPhotographyStreamV1' if name.startswith('new-') else 'SpektraStreamingResearchV1','route':'A','domain':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','orientation':1,'sourceSha256':sourcehash,'inputPixelSha256':pixelhash,'backendSha256':hashlib.sha256(libpath.read_bytes()).hexdigest(),'resourceManifestSha256':rs,'resolved':resolved,'negativeStore':str(root/(name+'.negative.partial'))};reply=ctypes.c_void_p();status=fn(str(resources).encode(),json.dumps(request).encode(),w,h,17,512*1048576,256*1048576,64*1048576,read,write,poll,None,ctypes.byref(reply))
 try:j=json.loads(ctypes.string_at(reply).decode())
 finally:
  if reply.value:lib.spk_string_free(reply)
 if status==0 or counts[0]!=0 or j['deviceLiveAfter']!=0 or j['stagingLiveAfter']!=0:raise ValueError('Rejection failed '+name)
 records.append({'case':name,'status':status,'readCallbacks':counts[0],'deviceLive':j['deviceLiveAfter'],'stagingLive':j['stagingLiveAfter'],'error':j.get('error')})
print(json.dumps({'version':'NaturalCoverageExplicitGuardControlsV1','cases':records,'sourceWhitelistUnchanged':True},indent=2))
