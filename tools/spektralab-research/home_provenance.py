"""Home evidence check and frozen source scope. Not another image/GPU run."""
import json,hashlib,sys,os,platform,subprocess
from pathlib import Path
from host_environment import environment
from natural_admission import sha,resources,load_json
import numpy,psutil,PIL,tifffile
host=environment();root=Path(host['researchRoot']);repo=Path(__file__).resolve().parents[2];out=repo/'docs/research/color-engine-20261029/home-round9'
report={'version':'HomeResearchEnvironmentV1','date':'2026-10-30','timezone':'Europe/Berlin','config':host,'python':sys.version,'packages':{'numpy':numpy.__version__,'Pillow':PIL.__version__,'psutil':psutil.__version__,'tifffile':tifffile.__version__},'hostMemory':psutil.virtual_memory()._asdict(),'gpuSnapshot':subprocess.check_output(['nvidia-smi','--query-gpu=name,driver_version,memory.total,memory.free','--format=csv,noheader'],text=True),'vulkanSnapshot':subprocess.check_output(['vulkaninfo','--summary'],text=True,stderr=subprocess.STDOUT),'sourceSha256':sha(Path(host['sourceFile'])),'builtLibraries':[],'toolHashes':[]}
for key in ['streamLibrary','wholeLibrary']:
 p=Path(host[key]);report['builtLibraries'].append({'role':key,'path':str(p),'sha256':sha(p),'bytes':p.stat().st_size})
for rel in ['deps/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin/clang++.exe','deps/glslang/bin/glslang.exe','deps/pytools/cmake/data/bin/cmake.exe','deps/pytools/bin/ninja.exe']:
 p=root/rel;report['toolHashes'].append({'path':rel,'sha256':sha(p)})
report['headers']={'commit':subprocess.check_output(['git','-C',str(root/'deps/Vulkan-Headers'),'rev-parse','HEAD'],text=True).strip(),'tree':subprocess.check_output(['git','-C',str(root/'deps/Vulkan-Headers'),'rev-parse','HEAD^{tree}'],text=True).strip()}
resource=resources(Path(host['resources']));report['resourceSetSha256']=hashlib.sha256(json.dumps({x['path']:x['sha256'] for x in resource},sort_keys=True,separators=(',',':')).encode()).hexdigest();report['resourceFiles']=resource
paths=['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/windows_exports.def','engine/CMakeLists.txt','API-SPEC-callable-render-service.md'];report['streamSourceManifest']=[{'path':x,'sha256':sha(root/'source-stream-v1'/x)} for x in paths]
company=load_json(repo/'docs/research/color-engine-20261029/company-round8/provenance.json');old={x['path']:x['sha256'] for x in company['sourceManifest']};report['changedFromCompanyGeneratedSource']=[x['path'] for x in report['streamSourceManifest'] if x['sha256']!=old[x['path']]];report['changePurpose']='engine.cpp compiled authorized negativeStore root only; color/default/other7source hashes unchanged';report['nativeNegativeStoreRoot']=host['authorizedOutputRoot'];(out/'environment-and-provenance.json').write_text(json.dumps(report,indent=2));print(json.dumps({'resourceExact':report['resourceSetSha256']==company['compiledResourceManifestSha256'],'changedFiles':report['changedFromCompanyGeneratedSource'],'libraries':report['builtLibraries']},indent=2))
