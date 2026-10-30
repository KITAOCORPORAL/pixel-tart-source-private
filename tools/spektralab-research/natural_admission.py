"""Versioned pre-engine admission. No full-frame boolean/hash copies. Tool-only."""
from pathlib import Path
import hashlib,json,os,struct,sys,ctypes
import numpy as np
from host_environment import environment
_HOST=environment()
AUTHORIZED_OUTPUT=Path(_HOST['authorizedOutputRoot']).resolve()
SOURCE_ROOT=Path(_HOST['sourceRoot']).resolve()
DOMAINS={'A':'standard ProPhoto RGB D50 linear float32; negatives/>1 retained','B':'production display sRGB D65 encoded float32 creative input'}
def need(ok,text):
 if not ok:raise ValueError(text)
def pairs(items):
 d={}
 for k,v in items:
  need(k not in d,'Duplicate JSON key '+k);d[k]=v
 return d
def load_json(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'),object_pairs_hook=pairs)
def atomic_json(p,obj):
 p=Path(p);tmp=p.with_name(p.name+'.tmp-'+str(os.getpid()));
 with tmp.open('x',encoding='utf-8') as f:json.dump(obj,f,ensure_ascii=True,indent=2);f.flush();os.fsync(f.fileno())
 os.replace(tmp,p)
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def output_path(p,root=AUTHORIZED_OUTPUT):
 p=Path(p).resolve();root=Path(root).resolve();need(p!=root and p.is_relative_to(root),'Output outside authorized research root');need(not p.is_relative_to(SOURCE_ROOT) and not SOURCE_ROOT.is_relative_to(p),'Output/source overlap');need(not p.exists(),'Output already exists');
 for parent in [p.parent,*p.parent.parents]:need(not parent.is_symlink() and not (getattr(parent.stat(),'st_file_attributes',0)&1024),'Output includes symlink/reparse point')
 need(p.parent.is_dir(),'Output parent missing');return p

class LockedInput:
 """Windows read-sharing only: prevent a writer/replacement during check+upload."""
 def __init__(self,path):
  self.kernel=ctypes.WinDLL('kernel32',use_last_error=True);f=self.kernel.CreateFileW;f.argtypes=[ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p];f.restype=ctypes.c_void_p
  self.handle=f(str(Path(path).resolve()),0x80000000,1,None,3,0x80,None)
  if self.handle==ctypes.c_void_p(-1).value:raise OSError(ctypes.get_last_error(),'Cannot lock input against write/replacement')
 def close(self):
  if self.handle is not None:
   self.kernel.CloseHandle.argtypes=[ctypes.c_void_p];need(bool(self.kernel.CloseHandle(self.handle)),'Input lock close failed');self.handle=None
def npy_info(path):
 path=Path(path);size=path.stat().st_size
 with path.open('rb') as f:
  version=np.lib.format.read_magic(f);need(version in [(1,0),(2,0)],'Unsupported npy version')
  shape,fortran,dtype=(np.lib.format.read_array_header_1_0(f) if version==(1,0) else np.lib.format.read_array_header_2_0(f));offset=f.tell()
 need(len(shape)==3 and all(type(n) is int and 0<n<=2**31-1 for n in shape) and shape[2]==3,'Invalid RGB dimensions/channels')
 need(not fortran and dtype==np.dtype('<f4') and sys.byteorder=='little','Expected C-order little-endian float32')
 count=1
 for n in shape:need(count<=(2**63-1)//n,'Dimension multiplication overflow');count*=n
 need(count<=2**31-1 and count*4<=512*1048576,'Input exceeds explicit CPU/input storage cap')
 need(size==offset+count*4,'NPY file length mismatch');return {'shape':list(shape),'offset':offset,'dataBytes':count*4,'fileBytes':size,'dtype':'<f4'}
def scan(path,info,rows=32):
 arr=np.load(path,mmap_mode='r',allow_pickle=False);h=hashlib.sha256();minimum=np.full(3,np.inf);maximum=-minimum;neg=np.zeros(3,np.int64);above=neg.copy();maxbool=0
 try:
  for y in range(0,info['shape'][0],rows):
   block=arr[y:y+rows];finite=np.isfinite(block);maxbool=max(maxbool,finite.nbytes);need(bool(finite.all()),'Nonfinite input');del finite;h.update(memoryview(block).cast('B'));minimum=np.minimum(minimum,block.min(axis=(0,1)));maximum=np.maximum(maximum,block.max(axis=(0,1)));neg+=(block<0).sum(axis=(0,1));above+=(block>1).sum(axis=(0,1))
 finally:arr._mmap.close()
 return {'pixelSha256':h.hexdigest(),'min':minimum.tolist(),'max':maximum.tolist(),'negative':neg.tolist(),'aboveOne':above.tolist(),'blockRows':rows,'maximumFiniteBooleanBytes':maxbool}
def resources(root):
 root=Path(root).resolve();return [{'path':p.relative_to(root).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file()]
def validate(manifest_path,manifest_sha,inputpath,route,library,resource_root,cpu_available,gpu_budget):
 need(route in DOMAINS,'Unknown route');need(not (Path(manifest_path).parent/'failure.json').exists(),'Preparation has failure marker');need(sha(manifest_path)==manifest_sha.lower(),'Preparation manifest hash mismatch');m=load_json(manifest_path)
 required={'version','sourceSha256','productionReportSha256','inputRoot','orientation','bridge','backend','resources','files','preparationIdentity'};need(required<=set(m),'Missing preparation manifest field');need(m['version']=='NaturalAdmissionManifestV2' and m['orientation']==1,'Unsupported preparation definition');need(len(m['sourceSha256'])==64 and len(m['productionReportSha256'])==64,'Missing source identity');need(m['bridge']['version']=='ExistingResearchD65ToD50V1' and not m['bridge']['extraWB'] and not m['bridge']['displayMappingOnA'],'Bridge/domain mismatch');need(m['preparationIdentity']['sourceSha256']==m['sourceSha256'],'Preparation source mismatch');need({'P','sRGBtoXYZD65','Bradford','sourceWhiteXY','destinationWhiteXY','engineProPhotoD50toXYZ','combinedMatrix'}<=set(m['bridge']),'Missing bridge parameters');
 for key in ['P','sRGBtoXYZD65','Bradford','engineProPhotoD50toXYZ','combinedMatrix']:
  matrix=np.asarray(m['bridge'][key]);need(matrix.shape==(3,3) and bool(np.isfinite(matrix).all()),'Invalid bridge matrix')
 need(m['bridge']['sourceWhiteXY']==[.3127,.329] and m['bridge']['destinationWhiteXY']==[.3457,.3585],'Unsupported bridge whitepoint');
 resource_names=[r['path'] for r in m['resources']];need(len(resource_names)==len(set(resource_names)),'Duplicate resource manifest item')
 entries=m['files'];names=[x['path'] for x in entries];need(len(set(names))==len(names),'Duplicate input manifest entry');need(all({'path','fileSha256','shape','route','orientation','domain','bridgeVersion','pixelSha256','bytes'}<=set(x) for x in entries),'Incomplete input entry');need(all(x['route'] in DOMAINS for x in entries),'Unknown manifest route')
 path=Path(inputpath).resolve();relative=path.relative_to(Path(m['inputRoot']).resolve()).as_posix();matches=[x for x in entries if x['path']==relative];need(len(matches)==1,'Input not in preparation manifest');e=matches[0];need(e['route']==route and e['domain']==DOMAINS[route] and e['orientation']==1 and e['bridgeVersion']==m['bridge']['version'],'Route/domain/orientation swap')
 info=npy_info(path);need(info['shape']==e['shape'] and info['fileBytes']==e['bytes'],'Prepared dimensions/length mismatch')
 # CPU policy before hash/finite mapping, independently from GPU admission.
 cpu_estimate=info['dataBytes']+info['shape'][0]*info['shape'][1]*8+256*1048576
 need(cpu_estimate<=min(2*1024**3,max(0,cpu_available-1024**3)),'CPU processing budget refused')
 need(sha(path)==e['fileSha256'],'Prepared input file modified');stats=scan(path,info);need(stats['pixelSha256']==e['pixelSha256'],'Prepared input pixels modified');need(sha(library)==m['backend']['sha256'],'Backend differs from preparation manifest');need(resources(resource_root)==m['resources'],'Resource collection changed');need(gpu_budget['admitted'],'GPU budget refused')
 return m,e,info,stats,{'estimatedCpuBytes':cpu_estimate,'availableCpuBytes':cpu_available,'independentOfGpuBudget':True}
