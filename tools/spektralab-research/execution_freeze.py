"""Pre-invocation byte freeze and child-side verification. Explicit paths, no secrets."""
from pathlib import Path
import hashlib,json,sys,os,subprocess,platform,importlib
from natural_admission import sha,scan,npy_info,resources,load_json,atomic_json,need
from host_environment import environment
LOCAL=['stream_case.py','stream_full_once.py','host_budget.py','native_wait.py','natural_admission.py','host_environment.py','execution_freeze.py','stream_saved_reopen.py']
PACKAGES=['numpy','psutil','PIL','tifffile']
def file_item(path):
 p=Path(path).resolve();return {'path':str(p),'bytes':p.stat().st_size,'sha256':sha(p)}
def packages():
 result=[]
 for name in PACKAGES:
  mod=importlib.import_module(name);root=Path(mod.__file__).parent;files=[]
  candidates=list(root.rglob('*'))
  for sibling in root.parent.glob(root.name+'*.libs'):
   if sibling.is_dir():candidates+=list(sibling.rglob('*'))
  for p in sorted(candidates):
   if p.is_file() and '__pycache__' not in p.parts and p.suffix not in ['.pyc','.pyo']:files.append(file_item(p))
  result.append({'name':name,'version':getattr(mod,'__version__',None),'root':str(root),'files':files})
 return result
def create_snapshot(a,path):
 need(not path.exists(),'Execution snapshot exists');host=environment();m=load_json(a.manifest);info=npy_info(a.input);st=scan(a.input,info,16);entry=next(x for x in m['files'] if x['path']==a.input.resolve().relative_to(Path(m['inputRoot']).resolve()).as_posix());need(st['pixelSha256']==entry['pixelSha256'],'Prepared pixels differ');need(sha(Path(host['sourceFile'])).upper()==host['sourceSha256'],'Natural source changed');root=Path(__file__).parent;repo=root.parents[1];resolved=next(c for c in load_json(repo/'docs/research/color-engine-20261029/company-round6/natural-cases.json')['cases'] if c['localDirectory']=='portra-A-proxy')['resolved'].copy();resolved.update(striped=False,strip_rows=0,strip_budget_bytes=0)
 pkg=packages();runtime=[]
 for mod in list(sys.modules.values()):
  f=getattr(mod,'__file__',None)
  if f and Path(f).is_file() and Path(f).suffix not in ['.pyc','.pyo']:runtime.append(file_item(f))
 obj={'version':'HomeFullExecutionFreezeV1','createdUtc':__import__('datetime').datetime.now(__import__('datetime').timezone.utc).isoformat(),'budgetBoundary':'SUCCESS_GROUP_COMMIT','fullCallsRequested':1,'localSources':[file_item(root/n) for n in LOCAL]+[file_item(repo/'docs/research/color-engine-20261029/company-round6/natural-cases.json')],'runtimeModules':list({x['path']:x for x in runtime}.values()),'pythonExecutable':file_item(sys.executable),'pythonRuntimeDlls':[file_item(p) for p in sorted([*Path(sys.base_prefix).glob('python*.dll'),*(Path(sys.base_prefix)/'DLLs').glob('*.dll'),*(Path(sys.base_prefix)/'DLLs').glob('*.pyd')])], 'standardLibraryFiles':[file_item(p) for p in sorted((Path(sys.base_prefix)/'Lib').rglob('*.py')) if 'site-packages' not in p.parts and '__pycache__' not in p.parts],'pythonVersion':sys.version,'pythonCacheTag':sys.implementation.cache_tag,'packages':pkg,'nativeDlls':[file_item(p) for p in sorted(a.library.parent.glob('*.dll'))],'streamLibrary':file_item(a.library),'resourcesRoot':str(a.resources.resolve()),'resources':resources(a.resources),'environmentConfig':file_item(Path(os.environ['COLOR_RESEARCH_ENV'])),'host':host,'inputManifest':file_item(a.manifest),'inputFile':file_item(a.input),'inputPixelSha256':st['pixelSha256'],'shape':info['shape'],'sourceFile':file_item(Path(host['sourceFile'])),'resolved':resolved,'rows':127,'consumerRows':7,'caps':host['caps'],'environment':{k:os.environ.get(k) for k in ['PYTHONPATH','PYTHONDONTWRITEBYTECODE','COLOR_RESEARCH_ENV','SPEKTRAFILM_NODE_TIMINGS','SPEKTRAFILM_TRANSFER_TIMINGS','VK_DRIVER_FILES','VK_ICD_FILENAMES']},'gpu':subprocess.check_output(['nvidia-smi','--query-gpu=name,driver_version,memory.total,memory.free','--format=csv,noheader'],text=True),'os':platform.platform(),'status':'FROZEN_BEFORE_CHILD_INVOCATION'};atomic_json(path,obj);return obj
def verify_snapshot(path,a):
 o=load_json(path);need(o['version']=='HomeFullExecutionFreezeV1' and o['budgetBoundary']=='SUCCESS_GROUP_COMMIT','Wrong freeze contract')
 items=o['localSources']+o['nativeDlls']+o['runtimeModules']+o['pythonRuntimeDlls']+o['standardLibraryFiles']+[o[k] for k in ['pythonExecutable','environmentConfig','inputManifest','inputFile','sourceFile']]
 for pkg in o['packages']:items+=pkg['files']
 for f in items:need(Path(f['path']).stat().st_size==f['bytes'] and sha(Path(f['path']))==f['sha256'],'Frozen execution byte changed: '+f['path'])
 need(str(a.library.resolve())==o['streamLibrary']['path'] and str(a.input.resolve())==o['inputFile']['path'] and str(a.manifest.resolve())==o['inputManifest']['path'],'Freeze invocation paths changed');need(resources(a.resources)==o['resources'],'Frozen resource set changed');need(sys.version==o['pythonVersion'] and Path(sys.executable).resolve()==Path(o['pythonExecutable']['path']),'Frozen Python changed');need(a.rows==o['rows'] and a.consumer_rows==o['consumerRows'] and a.device_mib==256 and a.staging_mib==64 and a.host_mib==512,'Frozen full parameters/caps changed')
 return o


