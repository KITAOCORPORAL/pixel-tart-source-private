"""FilmScaleSubstageTapV1: one frozen gray whole call, never retries/other cases."""
from pathlib import Path
import argparse,json,hashlib,subprocess,sys,os,datetime,socket,platform,traceback
import numpy as np
p=argparse.ArgumentParser();p.add_argument('--checkout',type=Path,required=True);p.add_argument('--library',type=Path,required=True);p.add_argument('--resources',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--role',choices=['company','home'],required=True);a=p.parse_args();repo=Path(__file__).resolve().parents[3]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def need(ok,m):
 if not ok:raise ValueError(m)
need(not a.output.exists(),'Output must be new, no retry');need(a.output.parent.is_dir(),'Parent missing');expectedResources=json.loads((Path(__file__).parent/'expected-resources.json').read_text());actualResources={p.relative_to(a.resources).as_posix():sha(p) for p in sorted(a.resources.rglob('*')) if p.is_file()};need(actualResources==expectedResources,'Frozen resources changed');fixture=Path(__file__).parent/'fixtures/gray-boundaries.npy';image=np.load(fixture,allow_pickle=False);manifest=json.loads((Path(__file__).parent/'fixtures/fixtures.json').read_text());f=manifest['fixtures'][0];need(sha(fixture)==f['fileSha256'] and hashlib.sha256(image.tobytes()).hexdigest()==f['pixelSha256'],'Fixed gray input mismatch');resolved=json.loads((Path(__file__).parent/'resolved-portraA.json').read_text());need(image.shape==(17,33,3),'Only existing gray-boundaries admitted');a.output.mkdir();(a.output/'taps').mkdir();(a.output/'input.npy').write_bytes(fixture.read_bytes())
cache={}
for line in (a.library.parent.parent/'CMakeCache.txt').read_text().splitlines():
 if line and not line.startswith(('#','//')) and '=' in line and ':' in line.split('=',1)[0]:left,value=line.split('=',1);cache[left.split(':',1)[0]]=value
sources=['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/pipeline/blur.cpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/src/core/hanatos.cpp','engine/src/core/setup_cache.cpp','engine/CMakeLists.txt','engine/windows_exports.def'];gpu=subprocess.check_output(['nvidia-smi','--query-gpu=name,driver_version','--format=csv,noheader'],text=True).strip();receipt={'version':'GrayScalePassiveRunV1','state':'RECORDED_BEFORE_GPU','role':a.role,'machine':socket.gethostname(),'system':platform.platform(),'python':sys.version,'atAsiaShanghai':datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),'gpu':gpu,'librarySha256':sha(a.library),'runtimeDlls':{p.name:sha(p) for p in sorted(a.library.parent.glob('*.dll'))},'resources':actualResources,'inputIdentity':f,'resolved':resolved,'checkoutCommit':subprocess.check_output(['git','-C',str(a.checkout),'rev-parse','HEAD'],text=True).strip(),'checkoutStatus':subprocess.check_output(['git','-C',str(a.checkout),'status','--porcelain'],text=True),'sourceHashes':{x:sha(a.checkout/x) for x in sources},'CMakeCacheSha256':sha(a.library.parent.parent/'CMakeCache.txt'),'buildNinjaSha256':sha(a.library.parent.parent/'build.ninja'),'buildSettings':cache,'perObjectCommands':subprocess.check_output([cache['CMAKE_MAKE_PROGRAM'],'-C',str(a.library.parent.parent),'-t','commands'],text=True).splitlines(),'tools':{k:{'path':cache[k],'sha256':sha(cache[k])} for k in ['CMAKE_CXX_COMPILER','CMAKE_C_COMPILER','CMAKE_MAKE_PROGRAM','SPEKTRALAB_GLSLANG','CMAKE_COMMAND']},'runnerSha256':sha(Path(__file__)),'gpuApiCounts':{'engineCreate':0,'open':0,'wholeRender':0,'streamRender':0,'btProbe':0},'automaticRetry':False};(a.output/'execution-before.json').write_text(json.dumps(receipt,indent=2));engine=session=result=None;report={'version':'GrayScalePassiveExecutionV1','success':False,'receiptSha256':sha(a.output/'execution-before.json'),'addedCases':['gray-boundaries'],'observations':[]}
try:
 os.environ.pop('SPK_SYNTHETIC_STAGE_DIR',None);os.environ['SPK_SCALE_SUBSTAGE_DIR']=str((a.output/'taps').resolve());sys.path[:0]=[str(a.checkout/'engine/tests'),str(a.checkout/'engine/tools')];from spk_ctypes import Engine;from windows_lifecycle import OwnedResult
 receipt['gpuApiCounts']['engineCreate']+=1;engine=Engine(library=a.library,resources=a.resources);report['capabilities']=engine.capabilities();receipt['gpuApiCounts']['open']+=1;session=engine.open(image,resolved);report['actualResolved']=session.get_params();need(report['actualResolved']==resolved,'Resolved changed');receipt['gpuApiCounts']['wholeRender']+=1;result=OwnedResult(session,'full');data=result.pixels().copy();need(data.shape==(17,33,4),'Final dimensions changed');data.astype('<u2').tofile(a.output/'whole-observed.rgba16');report['outputHash']=sha(a.output/'whole-observed.rgba16')
 if a.role=='company':
  sealed=repo/'docs/research/color-engine-20261029/company-diagnostic-20261102/company-package/gray-boundaries/whole.rgba16';old=np.fromfile(sealed,dtype='<u2').reshape(data.shape);delta=data.astype(np.int32)-old.astype(np.int32);report['sealedCompanyFinalComparison']={'changedChannels':int((delta!=0).sum()),'maxAbsU16':int(np.abs(delta).max()),'sealedSha256':sha(sealed)};need(not (delta!=0).any(),'Observed final differs from sealed company; stop')
 report['success']=True
except BaseException as ex:report['primaryError']=traceback.format_exc()
finally:
 cleanup=[]
 for label,obj in [('result',result),('session',session),('engine',engine)]:
  if obj:
   try:obj.close()
   except BaseException as ex:cleanup.append({'resource':label,'error':repr(ex)})
 os.environ.pop('SPK_SCALE_SUBSTAGE_DIR',None);report['cleanupErrors']=cleanup
 if cleanup:report['success']=False
 report['gpuApiCounts']=receipt['gpuApiCounts'];report['initializationGpuDispatches']='engine check_math_mode/startup probes not instrumented; do not count as one render or claim total vkdispatch';report['addedPassiveGpuReadbacks']=7;report['shaderArithmeticDispatchesAddedByTaps']=0
 for file in sorted((a.output/'taps').iterdir()):
  record={'file':'taps/'+file.name,'bytes':file.stat().st_size,'sha256':sha(file)}
  if file.suffix in ['.f32','.f64']:
   dtype='<f4' if file.suffix=='.f32' else '<f8';v=np.fromfile(file,dtype=dtype);need(np.isfinite(v).all(),'Observed nonfinite tap');record.update(dtype=dtype,values=v.size,min=float(v.min()),max=float(v.max()))
  report['observations'].append(record)
 (a.output/'run.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'success':report['success'],'gpuApiCounts':report['gpuApiCounts'],'sealedFinal':report.get('sealedCompanyFinalComparison'),'error':report.get('primaryError'),'output':str(a.output)}));sys.exit(0 if report['success'] else 3)
