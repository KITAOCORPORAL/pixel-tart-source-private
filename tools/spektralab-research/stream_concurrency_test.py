"""Actual native busy/cancel control, not another image/full render."""
from pathlib import Path
import ctypes,json,hashlib,threading,time
import numpy as np
from natural_admission import load_json,sha
from host_environment import environment
host=environment();root=Path(host['researchRoot']);engine=root;saved=load_json(root/'exact-321-r37/report.json');image=np.load(Path(host['smallRoot'])/'whole-scene-321x215.npy',mmap_mode='r');lib=ctypes.CDLL(str(root/'build-stream-v1/engine/spektrafilm_engine.dll'));resource_map={x['path']:x['sha256'] for x in saved['resourceIdentity']};request={'version':'SpektraStreamingResearchV1','route':'A','domain':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','orientation':1,'sourceSha256':saved['sourceSha256'].lower(),'inputPixelSha256':saved['inputPixelSha256'],'backendSha256':saved['librarySha256'],'resourceManifestSha256':hashlib.sha256(json.dumps(resource_map,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'resolved':saved['resolved'],'negativeStore':str(root/'concurrent-cancel-negative.partial')};entered=threading.Event();resume=threading.Event();cancel=threading.Event();faults=[]
READ=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_float));WRITE=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(ctypes.c_uint16));POLL=ctypes.CFUNCTYPE(ctypes.c_int,ctypes.c_void_p)
@READ
def read(_,y,rows,ptr):
 try:entered.set();resume.wait(5);np.copyto(np.ctypeslib.as_array(ptr,shape=(rows*321*3,)).reshape(rows,321,3),image[y:y+rows]);return 0
 except BaseException as e:faults.append(repr(e));return 1
@WRITE
def write(*args):return 1
@POLL
def poll(_):return int(cancel.is_set())
fn=lib.spk_stream_research_v1;fn.argtypes=[ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_uint64,ctypes.c_uint64,ctypes.c_uint64,READ,WRITE,POLL,ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_int;lib.spk_string_free.argtypes=[ctypes.c_void_p];result=[];out1=ctypes.c_void_p();out2=ctypes.c_void_p()
def call():result.append(fn(str(engine/'resources-patch').encode(),json.dumps(request).encode(),321,215,17,512*1048576,256*1048576,64*1048576,read,write,poll,None,ctypes.byref(out1)))
t=threading.Thread(target=call);t.start()
try:
 if not entered.wait(5):raise ValueError('First native call did not reach bounded reader')
 status2=fn(str(engine/'resources-patch').encode(),json.dumps(request).encode(),321,215,17,512*1048576,256*1048576,64*1048576,read,write,poll,None,ctypes.byref(out2));cancel.set()
finally:resume.set();t.join(10)
if t.is_alive():raise ValueError('Cancel/join timeout')
reply=json.loads(ctypes.string_at(out1).decode());lib.spk_string_free(out1)
if out2.value:lib.spk_string_free(out2)
if not(status2==-30 and result==[-7] and not faults and reply['deviceLiveAfter']==reply['stagingLiveAfter']==0):raise ValueError('Native concurrency/cancel failed')
j={'version':'NativeConcurrentRejectionV1','secondCallStatus':status2,'secondReplyNull':not bool(out2.value),'firstCallCancelStatus':result[0],'firstJoined':True,'callbackFaults':faults,'native':reply,'negativeStoreAbsent':not Path(request['negativeStore']).exists(),'noFinalTiffCreated':True};(root/'evidence/concurrency-cancel.json').write_text(json.dumps(j,indent=2));image._mmap.close();print('Native busy -30, cancelled worker -7, join+live0 PASS')
