"""Build the exact pinned scalar harness sources with the available MSVC toolchain.
GPL sources/binaries stay only in the external research artifact root.
"""
from pathlib import Path
import argparse,importlib.util,json,os,subprocess,sys,hashlib
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('original_builder',type=Path);p.add_argument('compiler',type=Path);p.add_argument('devcmd',type=Path);a=p.parse_args()
root=a.root.resolve();assert len(list((root/'references').glob('*')))==7
command='call "'+str(a.devcmd.resolve())+'" -arch=x64 -host_arch=x64 >nul && set'
r=subprocess.run(command,shell=True,capture_output=True,text=True,encoding='utf-8',errors='replace')
if r.returncode:raise RuntimeError('VS environment preparation failed: '+r.stderr[:1500])
env=os.environ.copy()
for line in r.stdout.splitlines():
 if '=' in line:
  key,value=line.split('=',1)
  if key.upper() in ['PATH','INCLUDE','LIB','LIBPATH','VCTOOLSINSTALLDIR','WINDOWSSDKDIR']:env[key]=value
spec=importlib.util.spec_from_file_location('frozen_reference_builder',a.original_builder);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
original_run=subprocess.run;commands=[]
def adapted(cmd,**kwargs):
 if isinstance(cmd,list) and cmd and str(cmd[0])==str(a.compiler.resolve()) and '-std=c++17' in cmd:
  source=Path(cmd[5]);exe=Path(cmd[-1]);actual=[str(a.compiler.resolve()),'/nologo','/std:c++17','/O2','/fp:strict','/U__SSE2__','/EHsc','/Zc:nrvo','/FIstring',str(source),'/Fe:'+str(exe),'/Fo:'+str(source.with_suffix('.obj'))]
  commands.append(dict(requested=cmd,actual=actual));kwargs['env']=env;kwargs['encoding']='utf-8';kwargs['errors']='replace'
  return original_run(actual,**kwargs)
 return original_run(cmd,**kwargs)
subprocess.run=adapted;sys.argv=[str(a.original_builder),'--local-root',str(root),'--compiler',str(a.compiler.resolve())]
try:module.main()
finally:subprocess.run=original_run
manifest=json.loads((root/'reference-manifest.json').read_text('utf-8'))
for build,record in zip(manifest['build'],commands):build['historicalRequestedCommand']=build['command'];build['command']=record['actual']
manifest['compilerChangedFromFrozenBuild']=True;manifest['compiler']='MSVC 14.44.35207, /fp:strict, /O2, scalar';manifest['binaryIdentityScope']='New local reference binary; not the historical LLVM-MinGW EXE identity'
expected=json.loads((a.original_builder.resolve().parents[2]/'docs/research/portrait-highlight-20261111/REFERENCES.json').read_text('utf-8'))
assert manifest['extracted']==expected['extracted']
for build,frozen in zip(manifest['build'],expected['build']):assert build['sourceSha256']==frozen['sourceSha256'],build['name']
(root/'reference-manifest.json').write_bytes((json.dumps(manifest,indent=2)+'\n').encode())
print('PASS: source harness hashes and extracted functions match; compiler/EXE identities explicitly differ')
