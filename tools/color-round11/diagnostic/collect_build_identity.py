"""Pre-run build/resource inventory, explicit paths and no cross-machine status templates."""
import argparse,json,hashlib,subprocess,sys,platform,socket
from pathlib import Path

def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for b in iter(lambda:f.read(4194304),b''):h.update(b)
 return h.hexdigest()
p=argparse.ArgumentParser();p.add_argument('--checkout',type=Path,required=True);p.add_argument('--library',type=Path,required=True);p.add_argument('--trace-library',type=Path,required=True);p.add_argument('--resources',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise ValueError('Provenance output exists')
obj={'version':'Round11BuildIdentityV1','phase':'RECORDED_BEFORE_RUN','checkoutCommit':subprocess.check_output(['git','-C',str(a.checkout),'rev-parse','HEAD'],text=True).strip(),'checkoutStatus':subprocess.check_output(['git','-C',str(a.checkout),'status','--porcelain'],text=True),'libraries':{},'resources':{},'machine':socket.gethostname(),'system':platform.platform(),'python':sys.version,'builds':{},'sourceFiles':{}}
for role,path in [('main',a.library),('trace',a.trace_library)]:
 path=path.resolve();obj['libraries'][role]={'path':str(path),'sha256':sha(path),'runtimeDlls':{p.name:sha(p) for p in sorted(path.parent.glob('*.dll'))}};build=path.parent.parent;cache={}
 for line in (build/'CMakeCache.txt').read_text().splitlines():
  if line and not line.startswith(('#','//')) and '=' in line and ':' in line.split('=',1)[0]:left,value=line.split('=',1);cache[left.split(':',1)[0]]=value
 tools={k:{'path':cache[k],'sha256':sha(Path(cache[k]))} for k in ['CMAKE_CXX_COMPILER','CMAKE_C_COMPILER','CMAKE_MAKE_PROGRAM','SPEKTRALAB_GLSLANG','CMAKE_COMMAND'] if k in cache and Path(cache[k]).is_file()};commands=subprocess.check_output([cache['CMAKE_MAKE_PROGRAM'],'-C',str(build),'-t','commands'],text=True);obj['builds'][role]={'settings':{k:v for k,v in cache.items() if k.startswith(('CMAKE_CXX_','CMAKE_C_','CMAKE_BUILD_TYPE','SPEKTRALAB_'))},'toolFiles':tools,'perObjectCommands':commands.splitlines(),'perObjectCommandSha256':hashlib.sha256(commands.encode()).hexdigest(),'buildNinjaSha256':sha(build/'build.ninja'),'sourceDirectory':cache.get('CMAKE_HOME_DIRECTORY'),'sourceHashes':{rel:sha(Path(cache['CMAKE_HOME_DIRECTORY'])/rel) for rel in ['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/CMakeLists.txt','engine/windows_exports.def']}}
for p in sorted(a.resources.rglob('*')):
 if p.is_file():obj['resources'][p.relative_to(a.resources).as_posix()]=sha(p)
for rel in ['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/CMakeLists.txt','engine/windows_exports.def']:obj['sourceFiles'][rel]=sha(a.checkout/rel)
with a.output.open('x',encoding='utf-8') as f:json.dump(obj,f,indent=2)
print(json.dumps({'version':obj['version'],'output':str(a.output),'library':obj['libraries']['main']['sha256']}))
