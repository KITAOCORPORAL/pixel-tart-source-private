"""Fresh home passive observer build using sealed home Release tool paths. No GPU."""
from pathlib import Path
import sys,os,json,subprocess,hashlib,shutil
root,source,build,repo=map(Path,sys.argv[1:5]);old=root/'round10-build-diagnostic-v2';e=repo/'docs/research/color-engine-20261029/home-scale-localization-20261102'
if build.exists():raise ValueError('Build must be new')
cache={}
for line in (old/'CMakeCache.txt').read_text().splitlines():
 if line and not line.startswith(('#','//')) and '=' in line and ':' in line.split('=',1)[0]:left,value=line.split('=',1);cache[left.split(':')[0]]=value
cmake=cache['CMAKE_COMMAND'];keys=['CMAKE_CXX_COMPILER','CMAKE_C_COMPILER','CMAKE_MAKE_PROGRAM','SPEKTRALAB_VULKAN_HEADERS','SPEKTRALAB_GLSLANG'];cmd=[cmake,'-S',str(source),'-B',str(build),'-G','Ninja','-DCMAKE_BUILD_TYPE=Release','-DBUILD_TESTING=ON','-DSPEKTRALAB_REQUIRE_VULKAN=ON','-DSPEKTRALAB_BUILD_TEST_DLL=ON','-DSPEKTRALAB_BUILD_NATIVE_RAW=OFF','-DSPEKTRALAB_BUILD_DESKTOP=OFF']+['-D'+key+'='+cache[key] for key in keys];env=os.environ.copy();env['PATH']=str(Path(cache['CMAKE_CXX_COMPILER']).parent)+os.pathsep+env['PATH'];commands=[cmd];r=subprocess.run(cmd,env=env,capture_output=True,text=True);(e/'configure.txt').write_text(r.stdout+r.stderr,encoding='utf-8');print('CONFIGURE',r.returncode,flush=True)
if r.returncode:sys.exit(r.returncode)
cmd=[cmake,'--build',str(build),'--target','spektralab_engine_shared','--parallel','4'];commands.append(cmd);r=subprocess.run(cmd,env=env,capture_output=True,text=True);(e/'build.txt').write_text(r.stdout+r.stderr,encoding='utf-8');print('BUILD',r.returncode,(r.stdout+r.stderr)[-3500:],flush=True)
if r.returncode:sys.exit(r.returncode)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
oldDlls={p.name:sha(p) for p in (old/'engine').glob('*.dll') if p.name!='spektrafilm_engine.dll'};newDlls={}
for name,expected in oldDlls.items():
 p=Path(cache['CMAKE_CXX_COMPILER']).parent/name
 if not p.is_file() or sha(p)!=expected:raise ValueError('Sealed runtime unavailable '+name)
 shutil.copy2(p,build/'engine'/name);newDlls[name]=sha(build/'engine'/name)
newcache={}
for line in (build/'CMakeCache.txt').read_text().splitlines():
 if line and not line.startswith(('#','//')) and '=' in line and ':' in line.split('=',1)[0]:left,value=line.split('=',1);newcache[left.split(':')[0]]=value
flags={k:{'old':v,'new':newcache.get(k),'equal':v==newcache.get(k)} for k,v in cache.items() if k.startswith(('CMAKE_CXX_FLAGS','CMAKE_C_FLAGS')) or k=='CMAKE_BUILD_TYPE'}
if not all(x['equal'] for x in flags.values()):raise ValueError('Build flags changed')
j={'version':'HomeScalePassiveBuildV1','gpuCalls':0,'commands':commands,'sealedOldBuild':str(old),'newBuild':str(build),'flags':flags,'oldRuntimeDlls':oldDlls,'newRuntimeDlls':newDlls,'librarySha256':sha(build/'engine/spektrafilm_engine.dll'),'tools':{k:{'path':cache[k],'sha256':sha(cache[k])} for k in ['CMAKE_COMMAND','CMAKE_CXX_COMPILER','CMAKE_C_COMPILER','CMAKE_MAKE_PROGRAM','SPEKTRALAB_GLSLANG']}}
with (e/'build-identity.json').open('x',encoding='utf-8') as f:json.dump(j,f,indent=2)
print('PRE_GPU_BUILD_COMPLETE')
